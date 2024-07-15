using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;
using SharpEditor.TextMarker;
using SharpSheets.Documentation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using SharpEditor.DataManagers;
using Avalonia.Media;
using Avalonia;
using SharpEditor.CodeHelpers;
using SharpSheets.Utilities;

namespace SharpEditor {

	public class ParsingStateColorizingTransformer : DocumentColorizingTransformer {

		readonly SolidColorBrush ownerBrush = new SolidColorBrush(Colors.White) { Opacity = 0.15 };
		readonly SolidColorBrush childBrush = new SolidColorBrush(Colors.Orange) { Opacity = 0.15 };
		readonly SolidColorBrush unusedBrush = new SolidColorBrush(new Color(0xff, 0x80, 0x80, 0x80)); // "#808080"

		private bool ColorOwners { get { return parsingManager.ColorOwners; } }

		private readonly ParsingManager parsingManager;
		private readonly TextArea textArea;
		public ParsingStateColorizingTransformer(ParsingManager parsingManager, TextArea textArea) {
			this.parsingManager = parsingManager;
			this.textArea = textArea;
		}

		protected override void ColorizeLine(DocumentLine line) {
			IParsingState? parsingState = parsingManager.GetParsingState();
			if (parsingState == null) { return; }
			int lineStart = line.Offset;
			int lineEnd = lineStart + line.Length;

			HashSet<EditorParseSpan>? caretChildren = null, caretOwners = null;
			if (ColorOwners) {
				caretChildren = new HashSet<EditorParseSpan>();
				caretOwners = new HashSet<EditorParseSpan>();
				foreach (EditorParseSpan caretSpan in parsingState.FindSpansContaining(textArea.Caret.Offset)) {
					caretChildren.UnionWith(caretSpan.Children);
					caretOwners.UnionWith(caretSpan.Owners);
				}
			}

			foreach (EditorParseSpan span in parsingState.FindOverlappingSpans(lineStart, line.Length)) {
				Brush? foregroundBrush = null;
				Brush? backgroundBrush = null;
				FontStyle? fontStyle = span.FontStyle;
				float? fontSizeFactor = span.FontSizeFactor;

				if (span.IsUnused) {
					foregroundBrush = unusedBrush;
					fontStyle = FontStyle.Italic;
					fontSizeFactor = null;
				}
				else if (span.ForegroundColor != null) {
					foregroundBrush = new SolidColorBrush(span.ForegroundColor.Value);
					foregroundBrush.ToImmutable();
				}

				if (span.BackgroundColor != null) {
					backgroundBrush = new SolidColorBrush(span.BackgroundColor.Value);
				}
				else if (ColorOwners && !textArea.Selection.IsMultiline) { //  && line.LineNumber != textArea.Caret.Line
					if (caretChildren!.Contains(span)) {
						// Caret span owns this span
						backgroundBrush = childBrush;
					}
					else if (caretOwners!.Contains(span)) {
						// This span owns caret span
						backgroundBrush = ownerBrush;
					}
				}

				ChangeLinePart(
					Math.Max(span.StartOffset, lineStart),
					Math.Min(span.EndOffset, lineEnd),
					element => {
						if (foregroundBrush != null) {
							element.TextRunProperties.SetForegroundBrush(foregroundBrush);
						}
						if (backgroundBrush != null) {
							element.TextRunProperties.SetBackgroundBrush(backgroundBrush);
						}
						if (fontSizeFactor != null) {
							element.TextRunProperties.SetFontRenderingEmSize(element.TextRunProperties.FontRenderingEmSize * fontSizeFactor.Value);
							//element.TextRunProperties.SetFontHintingEmSize(element.TextRunProperties.FontHintingEmSize * fontSizeFactor.Value);
						}
						//element.TextRunProperties.SetTextDecorations(new TextDecorationCollection() { TextDecorations.Strikethrough });
						Typeface tf = element.TextRunProperties.Typeface;
						element.TextRunProperties.SetTypeface(new Typeface(
							tf.FontFamily,
							fontStyle ?? tf.Style,
							span.FontWeight ?? tf.Weight,
							tf.Stretch
						));
					}
				);
			}
		}
	}

	public class SharpConfigColorizingTransformer<TSpan> : DocumentColorizingTransformer where TSpan : SharpConfigSpan {

		public Brush WidgetBrush { get; set; }
		public Brush ShapeBrush { get; set; }
		public Brush SpecialArgBrush { get; set; }

		private readonly SharpConfigParsingState<TSpan> parsingState;
		private readonly ITypeDetailsCollection headerTypes;
		private readonly ITypeDetailsCollection styleTypes;
		public SharpConfigColorizingTransformer(SharpConfigParsingState<TSpan> parsingState, ITypeDetailsCollection headerTypes, ITypeDetailsCollection styleTypes, IEnumerable<ArgumentDetails>? specialArgs) {
			this.parsingState = parsingState ?? throw new ArgumentException("parsingState cannot be null.");
			this.headerTypes = headerTypes;
			this.styleTypes = styleTypes;

			WidgetBrush = SharpEditorPalette.RectBrush;
			ShapeBrush = SharpEditorPalette.StyleBrush;
			SpecialArgBrush = SharpEditorPalette.ArgBrush;

			specialPropertyRegex = MakeSpecialPropertyRegex(specialArgs);
		}

		private static readonly Regex widgetLineRegex = new Regex(@"^\s*([a-z][a-z0-9]*(?:\.[a-z][a-z0-9]*)*)\s*(?=\:?\s*(#|$))", RegexOptions.IgnoreCase);
		private static readonly Regex shapeRegex = new Regex(@"^\s*[a-z][a-z0-9]*\s*\:\s*([a-z][a-z0-9]*)(?=\s*(#|$))", RegexOptions.IgnoreCase);
		private readonly Regex? specialPropertyRegex;

		private static Regex? MakeSpecialPropertyRegex(IEnumerable<ArgumentDetails>? args) {
			if(args == null) { return null; }
			string allNames = string.Join(@"|", args.Select(a => Regex.Escape(a.Name)));
			if (allNames.Length > 0) {
				return new Regex(string.Format(@"^\s*({0})\s*(?=\:?\s*[^\s])", allNames), RegexOptions.IgnoreCase);
			}
			else {
				return null;
			}
		}

		protected override void ColorizeLine(DocumentLine line) {
			//int lineStart = line.Offset;
			//int lineEnd = lineStart + line.Length;

			if(!parsingState.IsLineUsed(line.LineNumber)) {
				return; // Is this going to work properly?
			}

			string lineText = parsingState.Document.GetText(line);
			Match widgetMatch = widgetLineRegex.Match(lineText);
			if (widgetMatch.Success) {
				Group widgetName = widgetMatch.Groups[1];
				if (headerTypes.ContainsKey(widgetName.Value)) {
					ColorSpan(line.Offset + widgetName.Index, line.Offset + widgetName.Index + widgetName.Length, WidgetBrush, FontWeight.Normal);
				}
			}

			Match shapeMatch = shapeRegex.Match(lineText);
			if (shapeMatch.Success) {
				Group shapeName = shapeMatch.Groups[1];
				if (styleTypes.ContainsKey(shapeName.Value)) {
					ColorSpan(line.Offset + shapeName.Index, line.Offset + shapeName.Index + shapeName.Length, ShapeBrush, null);
				}
			}

			if (specialPropertyRegex != null) {
				Match argMatch = specialPropertyRegex.Match(lineText);
				if (argMatch.Success) {
					Group argName = argMatch.Groups[1];
					ColorSpan(line.Offset + argName.Index, line.Offset + argName.Index + argName.Length, SpecialArgBrush, FontWeight.Bold);
				}
			}
		}

		private void ColorSpan(int startOffset, int endOffset, Brush foregroundBrush, FontWeight? fontWeight) {
			if (foregroundBrush != null) {
				ChangeLinePart(startOffset, endOffset, element => {
					element.TextRunProperties.SetForegroundBrush(foregroundBrush);
					if (fontWeight.HasValue) {
						Typeface oldtf = element.TextRunProperties.Typeface;
						Typeface newtf = new Typeface(oldtf.FontFamily, oldtf.Style, fontWeight.Value, oldtf.Stretch);
						element.TextRunProperties.SetTypeface(newtf);
					}
				});
			}
		}
	}

	public class ParsingStateUnderlineRenderer : IBackgroundRenderer {

		private readonly ParsingManager parsingManager;
		public ParsingStateUnderlineRenderer(ParsingManager parsingManager) {
			this.parsingManager = parsingManager;
		}

		public KnownLayer Layer {
			get {
				// draw behind selection
				return KnownLayer.Selection;
			}
		}

		static readonly TextMarkerTypes underlineMarkerTypes = TextMarkerTypes.SquigglyUnderline | TextMarkerTypes.NormalUnderline | TextMarkerTypes.DottedUnderline;

		public void Draw(TextView textView, DrawingContext drawingContext) {
			if (textView == null)
				throw new ArgumentNullException(nameof(textView));
			if (drawingContext == null)
				throw new ArgumentNullException(nameof(drawingContext));

			IParsingState? parsingState = parsingManager.GetParsingState();
			if (parsingState == null || !textView.VisualLinesValid)
				return;
			ReadOnlyCollection<VisualLine> visualLines = textView.VisualLines;
			if (visualLines.Count == 0)
				return;
			int viewStart = visualLines.First().FirstDocumentLine.Offset;
			int viewEnd = visualLines.Last().LastDocumentLine.EndOffset;
			foreach (EditorParseSpan span in parsingState.FindOverlappingSpans(viewStart, viewEnd - viewStart)) {

				/*
				if (span.BackgroundColor != null) {
					BackgroundGeometryBuilder geoBuilder = new BackgroundGeometryBuilder {
						AlignToWholePixels = true,
						CornerRadius = 3
					};
					geoBuilder.AddSegment(textView, span);
					Geometry geometry = geoBuilder.CreateGeometry();
					if (geometry != null) {
						Color color = span.BackgroundColor.Value;
						SolidColorBrush brush = new SolidColorBrush(color);
						brush.Freeze();
						drawingContext.DrawGeometry(brush, null, geometry);
					}
				}
				*/

				if ((span.MarkerTypes & underlineMarkerTypes) != 0) {
					foreach (Rect r in BackgroundGeometryBuilder.GetRectsForSegment(textView, span)) {
						Point startPoint = r.BottomLeft;
						Point endPoint = r.BottomRight;

						Brush usedBrush = new SolidColorBrush(span.MarkerColor);
						usedBrush.ToImmutable();
						if ((span.MarkerTypes & TextMarkerTypes.SquigglyUnderline) != 0) {
							StreamGeometry geometry = new StreamGeometry();
							using (StreamGeometryContext ctx = geometry.Open()) {
								ctx.BeginFigure(startPoint, false);
								//ctx.PolyLineTo(CreatePoints(startPoint, endPoint, 2.5), true, false);
								foreach(Point p in CreatePoints(startPoint, endPoint, 2.5)) {
									ctx.LineTo(p);
								}
							}
							//geometry.Freeze();

							Pen usedPen = new Pen(usedBrush, 1);
							usedPen.ToImmutable();
							drawingContext.DrawGeometry(Brushes.Transparent, usedPen, geometry);
						}
						else if ((span.MarkerTypes & TextMarkerTypes.NormalUnderline) != 0) {
							Pen usedPen = new Pen(usedBrush, 1);
							usedPen.ToImmutable();
							drawingContext.DrawLine(usedPen, startPoint, endPoint);
						}
						else if ((span.MarkerTypes & TextMarkerTypes.DottedUnderline) != 0) {
							Pen usedPen = new Pen(usedBrush, 1) {
								DashStyle = DashStyle.Dot
							};
							usedPen.ToImmutable();
							drawingContext.DrawLine(usedPen, startPoint, endPoint);
						}
					}
				}
			}
		}

		private static Point[] CreatePoints(Point start, Point end, double offset) {
			int count = Math.Max((int)((end.X - start.X) / offset) + 1, 4);
			Point[] points = new Point[count];
			for (int i = 0; i < count; i++) {
				points[i] = new Point(start.X + i * offset, start.Y - ((i + 1) % 2 == 0 ? offset : 0));
			}
			return points;
		}

	}

	public class ParsingStateSameTokenRenderer : IBackgroundRenderer {

		private readonly ParsingManager parsingManager;
		private readonly TextArea textArea;
		public ParsingStateSameTokenRenderer(ParsingManager parsingManager, TextArea textArea) {
			this.parsingManager = parsingManager;
			this.textArea = textArea;
		}

		public KnownLayer Layer {
			get {
				// draw behind selection
				return KnownLayer.Selection;
			}
		}

		private readonly Regex wordRegex = new Regex(@"[a-z][a-z\-_]*[a-z]|[a-z][a-z0-9_]*|[0-9]+\.[0-9]+|[0-9]+", RegexOptions.IgnoreCase);
		private (string?,int) GetWord() {
			int caretOffset = textArea.Caret.Offset;
			ISegment? caretSegment = textArea.Document.GetSharpTermSegmentFromOffset(caretOffset);
			
			if (caretSegment is null) { return (null, -1); }

			string caretWord = textArea.Document.GetText(caretSegment);

			if (wordRegex.Match(caretWord) is Match wordMatch && wordMatch.Success) {
				caretWord = wordMatch.Value;
				caretOffset = caretOffset.Clamp(caretSegment.Offset, caretSegment.Offset + caretSegment.Length);
				return (caretWord, caretOffset);
			}
			else {
				return (null, -1);
			}
		}

		public void Draw(TextView textView, DrawingContext drawingContext) {
			if (textView == null) { throw new ArgumentNullException(nameof(textView)); }
			if (drawingContext == null) { throw new ArgumentNullException(nameof(drawingContext)); }

			if (!textView.VisualLinesValid) { return; }

			IParsingState? parsingState = parsingManager.GetParsingState();
			if (parsingState == null || !parsingState.HighlightSameToken) { return; }

			int highlightPosition;
			string? highlightText;
			if (textArea.Selection.IsEmpty) {
				(highlightText, highlightPosition) = GetWord();
			}
			else {
				string selectedText = textArea.Selection.GetText();
				if (!string.IsNullOrWhiteSpace(selectedText) && !selectedText.Contains('\n')) {
					highlightText = selectedText;
					highlightPosition = textArea.Selection.SurroundingSegment.Offset;
				}
				else {
					highlightText = null;
					highlightPosition = -1;
				}
			}

			if (string.IsNullOrWhiteSpace(highlightText)) { return; }

			ReadOnlyCollection<VisualLine> visualLines = textView.VisualLines;
			if (visualLines.Count == 0) { return; }

			IBrush? brush = new SolidColorBrush(Colors.LightGray) { Opacity = 0.2 }.ToImmutable();
			IPen? pen = null;
			Regex highlightRegex = new Regex(Regex.Escape(highlightText), RegexOptions.Multiline);

			int viewStart = visualLines.First().FirstDocumentLine.LineNumber - 1; // -1 to adjust from LineNumber to line index
			int viewEnd = visualLines.Last().LastDocumentLine.LineNumber - 1;

			for (int i = viewStart; i <= viewEnd; i++) {
				DocumentLine line = textView.Document.Lines[i];
				string lineText = textView.Document.GetText(line);

				foreach (Match m in (IEnumerable<Match>)highlightRegex.Matches(lineText)) {
					int wordIndex = m.Index;
					if (wordIndex < 0) { continue; }

					TextSegment highlightSegment = new TextSegment() { StartOffset = line.Offset + wordIndex, Length = highlightText.Length };

					if (highlightSegment.Contains(highlightPosition, 0)) { continue; }

					foreach (Rect r in BackgroundGeometryBuilder.GetRectsForSegment(textView, highlightSegment)) {
						drawingContext.DrawRectangle(brush, pen, r);
					}
				}
			}
		}

	}

	public class ParsingErrorBackgroundRenderer : IBackgroundRenderer {

		private readonly ParsingManager parsingManager;
		public ParsingErrorBackgroundRenderer(ParsingManager parsingManager) {
			this.parsingManager = parsingManager;
		}

		public KnownLayer Layer {
			get {
				// draw behind selection
				return KnownLayer.Selection;
			}
		}

		public DashStyle Dash { get; set; } = new DashStyle(new double[] { 2.0, 2.0 }, 0.0);
		public double Thickness { get; set; } = 1.6;

		public void Draw(TextView textView, DrawingContext drawingContext) {
			if (textView == null)
				throw new ArgumentNullException(nameof(textView));
			if (drawingContext == null)
				throw new ArgumentNullException(nameof(drawingContext));

			IParsingState? parsingState = parsingManager.GetParsingState();
			if (parsingState == null || !textView.VisualLinesValid)
				return;
			ReadOnlyCollection<VisualLine> visualLines = textView.VisualLines;
			if (visualLines.Count == 0)
				return;
			int viewStart = visualLines.First().FirstDocumentLine.Offset;
			int viewEnd = visualLines.Last().LastDocumentLine.EndOffset;
			foreach (EditorParseSpan span in parsingState.FindOverlappingSpans(viewStart, viewEnd - viewStart)) {

				if (span.IsParsingError || span.IsDrawingError) {
					Color errorColor = span.IsParsingError ? Colors.Red : Colors.DarkOrange; // TODO Better set these colors in SharpEditorDetails?
					foreach (Rect r in BackgroundGeometryBuilder.GetRectsForSegment(textView, span)) {
						Point startPoint = r.BottomLeft;
						Point endPoint = r.BottomRight;

						////startPoint.Offset(-Offset, 0);
						//endPoint.Offset(Dash.Dashes[^1] * 0.75, 0);
						endPoint += new Avalonia.Vector(Dash.Dashes?[^1] ?? 0.0, 0.0);

						Brush errorBrush = new SolidColorBrush(errorColor);
						errorBrush.ToImmutable();

						Pen errorPen = new Pen(errorBrush, Thickness) {
							DashStyle = Dash,
						};
						errorPen.ToImmutable();
						drawingContext.DrawLine(errorPen, startPoint, endPoint);
					}
				}
			}
		}

	}


}
