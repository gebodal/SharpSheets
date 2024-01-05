using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using SharpEditor.DataManagers;
using SharpEditor.TextMarker;
using SharpSheets.Canvas;
using SharpSheets.Exceptions;
using SharpSheets.Parsing;

namespace SharpEditor {

	public delegate void SegmentRedrawHandler(ISegment segment, DispatcherPriority redrawPriority);

	public interface IParsingState {

		event EventHandler StateChanged;

		IParser Parser { get; }
		string Extension { get; }
		string FileFilter { get; }
		string DefaultFilename { get; }

		bool HasDesignerContent { get; }
		bool HasGeneratedContent { get; }

		IDocumentContent? DrawableContent { get; }
		IDrawingMapper? DrawingMapper { get; }

		IEnumerable<SharpSheets.Utilities.FilePath>? Dependencies { get; }

		IEnumerable<IVisualLineTransformer> GetLineTransformers();

		bool ColorOwners { get; }

		void Reset();
		void LoadResultEntry(ResultEntry result);
		void LoadDrawingErrors(SharpDrawingException[]? newErrors);

		IEnumerable<EditorParseSpan> FindOverlappingSpans(int offset, int length);
		IEnumerable<EditorParseSpan> FindSpansContaining(int offset);

		int ErrorCount { get; }
		int[] ErrorOffsets { get; }

		IEnumerable<SharpSheetsException> GetErrors(ISegment segment);
		IEnumerable<SharpSheetsException> GetErrors(int offset);
		IEnumerable<SharpDrawingException> GetDrawingErrors();

		event SegmentRedrawHandler RedrawSegment; // Redraw a segment of text

		void Remove(EditorParseSpan span);

		void Redraw(ISegment segment);
	}

	public abstract class ParsingState<TSpan> : IParsingState where TSpan : EditorParseSpan {

		public TextDocument Document { get; }
		private readonly TextSegmentCollection<TSpan> spans;

		public ParsingState(TextDocument document) {
			this.Document = document;
			this.spans = new TextSegmentCollection<TSpan>(this.Document);
		}

		public event EventHandler? StateChanged;

		protected void LoadingFinished() {
			StateChanged?.Invoke(this, EventArgs.Empty); // Custom delegate??
		}

		public abstract IParser Parser { get; }
		public abstract string Extension { get; }
		public abstract string FileFilter { get; }
		public abstract string DefaultFilename { get; }

		public abstract bool HasDesignerContent { get; }
		public abstract bool HasGeneratedContent { get; }

		public abstract IDocumentContent? DrawableContent { get; }
		public abstract IDrawingMapper? DrawingMapper { get; }

		public abstract IEnumerable<SharpSheets.Utilities.FilePath>? Dependencies { get; }

		public abstract IEnumerable<IVisualLineTransformer> GetLineTransformers();

		public abstract bool ColorOwners { get; } // Flag to indicate that the text view should colour parent and child spans in the document

		protected abstract void ResetData();
		public void Reset() {
			ResetData();
			Clear();
		}

		protected void ValidateSpan(int startOffset, int length) {
			int textLength = Document.TextLength;
			if (startOffset < 0 || startOffset > textLength)
				throw new ArgumentOutOfRangeException(nameof(startOffset), startOffset, "Value must be between 0 and " + textLength);
			if (length < 0 || startOffset + length > textLength)
				throw new ArgumentOutOfRangeException(nameof(length), length, "length must not be negative and startOffset+length must not be after the end of the document");
		}

		protected object LoadingLock { get; } = new object();

		public abstract void LoadResultEntry(ResultEntry result);
		public abstract void LoadDrawingErrors(SharpDrawingException[]? newErrors);

		public void UpdateSpansFromSnapshot(ITextSourceVersion original) {
			// Update spans to reflect any changes in the document
			//foreach(TextChangeEventArgs changeEvent in nextResult.snapshot.Version.GetChangesTo(document.CreateSnapshot().Version)) { }
			ITextSourceVersion current = Document.CreateSnapshot().Version;
			if (original.GetChangesTo(current).Any()) {
				foreach (TSpan span in spans.ToList()) {
					int startOffset = original.MoveOffsetTo(current, span.StartOffset);
					int endOffset = original.MoveOffsetTo(current, span.EndOffset);
					if (startOffset < endOffset) {
						span.StartOffset = startOffset;
						span.EndOffset = endOffset;
					}
					else {
						Remove(span);
					}
				}
			}
		}

		#region Span Maintenance
		/*
		public SharpConfigSpan Create(int startOffset, int length, int indent) {
			//if (spans == null) {
			//	throw new InvalidOperationException("Cannot create a span when not attached to a document.");
			//}

			ValidateSpan(startOffset, length);
			
			//Console.WriteLine($"Create new span: {startOffset}, {length}, {indent}");
			SharpConfigSpan span = new SharpConfigSpan(this, startOffset, length, indent);
			//spans.Add(span);
			// no need to mark span for redraw: the span is invisible until a property is set
			return span;
		}
		*/
		//public abstract TSpan Create(int startOffset, int length, int indent);

		#endregion

		#region Accessors
		public static bool DisplayError(SharpSheetsException e) {
			return SharpDataManager.Instance.WarnFontLicensing || e is not FontLicenseWarningException;
		}

		public int ErrorCount {
			get {
				if(spans is null) {
					return 0;
				}
				else {
					return spans
						.Where(s => s.IsParsingError || s.IsDrawingError)
						.Select(s => (s.ParsingExceptions?.Length ?? 0) + (s.DrawingExceptions?.Length ?? 0))
						.Sum();
				}
			}
		}

		public int[] ErrorOffsets {
			get {
				if (spans is null) {
					return Array.Empty<int>();
				}
				else {
					return spans
						.Where(s => s.IsParsingError || s.IsDrawingError)
						.Select(s => s.StartOffset)
						.Distinct()
						.ToArray();
				}
			}
		}

		public IEnumerable<SharpSheetsException> GetErrors(ISegment segment) {
			if (spans == null) {
				return Enumerable.Empty<SharpSheetsException>();
			}
			else {
				//return spans.FindOverlappingSegments(segment).Where(s => s.Exceptions != null).SelectMany(s => s.Exceptions);
				return GetErrors(spans.FindOverlappingSegments(segment));
			}
		}

		public IEnumerable<SharpSheetsException> GetErrors(int offset) {
			if (spans == null) {
				return Enumerable.Empty<SharpSheetsException>();
			}
			else {
				//return spans.FindSegmentsContaining(offset).Where(s => s.Exceptions != null).SelectMany(s => s.Exceptions);
				return GetErrors(spans.FindSegmentsContaining(offset));
			}
		}

		private IEnumerable<SharpSheetsException> GetErrors(ReadOnlyCollection<TSpan> errorSpans) {
			foreach(TSpan span in errorSpans) {
				if (span.ParsingExceptions != null) {
					foreach (SharpParsingException parsingError in span.ParsingExceptions) {
						if(parsingError is FontLicenseWarningException) {
							if (SharpDataManager.Instance.WarnFontLicensing) {
								yield return parsingError;
							}
						}
						else {
							yield return parsingError;
						}
					}
				}
				if (span.DrawingExceptions != null) {
					foreach (SharpDrawingException drawingError in span.DrawingExceptions) {
						yield return drawingError;
					}
				}
			}
		}

		public IEnumerable<TSpan> ConfigSpans { get { return spans ?? Enumerable.Empty<TSpan>(); } }

		public abstract IEnumerable<SharpDrawingException> GetDrawingErrors();

		public IEnumerable<TSpan> GetSpansAtOffset(int offset) {
			if (spans == null) { return Enumerable.Empty<TSpan>(); }
			else { return spans.FindSegmentsContaining(offset); }
		}

		public IEnumerable<TSpan> FindOverlappingSpans(ISegment segment) {
			return spans.FindOverlappingSegments(segment);
		}
		public IEnumerable<TSpan> FindOverlappingSpans(int offset, int length) {
			return spans.FindOverlappingSegments(offset, length);
		}
		IEnumerable<EditorParseSpan> IParsingState.FindOverlappingSpans(int offset, int length) {
			return FindOverlappingSpans(offset, length);
		}
		
		public IEnumerable<TSpan> FindSpansContaining(int offset) {
			if (spans == null) { yield break; }
			foreach (var i in spans.FindSegmentsContaining(offset)) { yield return i; }
		}
		IEnumerable<EditorParseSpan> IParsingState.FindSpansContaining(int offset) {
			return FindSpansContaining(offset);
		}
		#endregion

		#region Remove and Redraw
		public void Clear() {
			if (spans != null) {
				foreach (TSpan m in spans.ToArray()) {
					Remove(m);
				}
			}
		}

		public void Add(TSpan span) {
			spans.Add(span);
		}

		public void Remove(TSpan span) {
			if (span == null) {
				throw new ArgumentNullException(nameof(span));
			}
			if (spans != null && spans.Remove(span)) {
				Redraw(span);
				span.OnDeleted();
			}
		}
		void IParsingState.Remove(EditorParseSpan span) {
			TSpan tSpan = span as TSpan ?? throw new ArgumentException("Invalid span type.");
			Remove(tSpan);
		}

		public void RemoveAll(Predicate<TSpan> predicate) {
			if (predicate == null) {
				throw new ArgumentNullException(nameof(predicate));
			}
			if (spans != null) {
				foreach (TSpan m in spans.ToArray()) { // ToArray needed so we iterate over a copy
					if (predicate(m)) {
						Remove(m);
					}
				}
			}
		}

		/// <summary>
		/// Inform listeners that the specified text segment needs redrawing.
		/// </summary>
		public void Redraw(ISegment segment) {
			RedrawSegment?.Invoke(segment, DispatcherPriority.Normal); // TODO Is this a slow way of doing this?
		}

		public event SegmentRedrawHandler? RedrawSegment;
		#endregion
	}

	public interface IDrawingMapper {
		IEnumerable<object>? GetDrawnObjects(int offset);
		DocumentSpan? GetDrawnObjectLocation(object drawnObject);
		int? GetDrawnObjectDepth(object drawnObject);
		// TODO Some of this functionality could probably be generalized
	}

	public class EmptyDrawingMapper : IDrawingMapper {
		public IEnumerable<object>? GetDrawnObjects(int offset) {
			return null;
		}

		public DocumentSpan? GetDrawnObjectLocation(object drawnObject) {
			return null;
		}

		public int? GetDrawnObjectDepth(object drawnObject) {
			return null;
		}
	}

	/*
	public class EditorParseSpanEqualityComparer : IEqualityComparer<EditorParseSpan> {
		public static EditorParseSpanEqualityComparer Instance { get; } = new EditorParseSpanEqualityComparer();

		public bool Equals(EditorParseSpan x, EditorParseSpan y) {
			if(object.ReferenceEquals(x, y)) { return true; }
			return x.StartOffset == y.StartOffset
				&& x.Length == y.Length
				&& x.Owners.Count == y.Owners.Count
				&& x.Children.Count == y.Children.Count;
		}

		public int GetHashCode(EditorParseSpan obj) {
			unchecked { // Overflow is fine, just wrap
				int hash = 17;
				// Suitable nullity checks etc, of course :)
				hash = hash * 23 + obj.StartOffset.GetHashCode();
				hash = hash * 23 + obj.Length.GetHashCode();
				hash = hash * 23 + obj.Owners.GetHashCode();
				hash = hash * 23 + obj.Children.GetHashCode();
				return hash;
			}
		}
	}
	*/

	public class EditorParseSpan : TextSegment {
		private readonly IParsingState service;

		public bool IsUnused { get; set; }
		public HashSet<EditorParseSpan> Owners { get; }
		public HashSet<EditorParseSpan> Children { get; }

		public bool IsParsingError => parsingExceptions != null && parsingExceptions.Any(ParsingState<EditorParseSpan>.DisplayError);
		private SharpParsingException[]? parsingExceptions;
		public SharpParsingException[]? ParsingExceptions {
			get {
				return parsingExceptions?.Where(ParsingState<EditorParseSpan>.DisplayError).ToArray();
			}
			set {
				parsingExceptions = value;
			}
		}

		public bool IsDrawingError { get; private set; } = false;
		private SharpDrawingException[]? drawingExceptions;
		public SharpDrawingException[]? DrawingExceptions {
			get {
				return drawingExceptions;
			}
			set {
				drawingExceptions = value;
				IsDrawingError = !(drawingExceptions == null || drawingExceptions.Length == 0);
			}
		}

		public EditorParseSpan(IParsingState service, int startOffset, int length) {
			this.service = service ?? throw new ArgumentNullException(nameof(service));
			this.StartOffset = startOffset;
			this.Length = length;

			Owners = new HashSet<EditorParseSpan>();
			Children = new HashSet<EditorParseSpan>();
		}

		public event EventHandler? Deleted;

		public bool IsDeleted { get { return !this.IsConnectedToCollection; } }
		public void Delete() { service.Remove(this); }
		internal void OnDeleted() { Deleted?.Invoke(this, EventArgs.Empty); }

		private void Redraw() { service.Redraw(this); }

		Color? backgroundColor;
		public Color? BackgroundColor {
			get { return backgroundColor; }
			set {
				if (backgroundColor != value) {
					backgroundColor = value;
					Redraw();
				}
			}
		}

		Color? foregroundColor;
		public Color? ForegroundColor {
			get { return foregroundColor; }
			set {
				if (foregroundColor != value) {
					foregroundColor = value;
					Redraw();
				}
			}
		}

		FontWeight? fontWeight;
		public FontWeight? FontWeight {
			get { return fontWeight; }
			set {
				if (fontWeight != value) {
					fontWeight = value;
					Redraw();
				}
			}
		}

		FontStyle? fontStyle;
		public FontStyle? FontStyle {
			get { return fontStyle; }
			set {
				if (fontStyle != value) {
					fontStyle = value;
					Redraw();
				}
			}
		}

		float? fontSizeFactor;
		public float? FontSizeFactor {
			get { return fontSizeFactor; }
			set {
				if (fontSizeFactor != value) {
					fontSizeFactor = value;
					Redraw();
				}
			}
		}

		public object? Tag { get; set; }

		TextMarkerTypes markerTypes;
		public TextMarkerTypes MarkerTypes {
			get { return markerTypes; }
			set {
				if (markerTypes != value) {
					markerTypes = value;
					Redraw();
				}
			}
		}

		Color markerColor;
		public Color MarkerColor {
			get { return markerColor; }
			set {
				if (markerColor != value) {
					markerColor = value;
					Redraw();
				}
			}
		}
	}

}
