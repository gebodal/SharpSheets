using System;
using System.Collections.Generic;
using System.Linq;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using SharpSheets.Utilities;
using SharpSheets.Parsing;
using SharpSheets.Cards.Definitions;
using SharpSheets.Exceptions;

namespace SharpEditor {

	public abstract class SharpConfigParsingState<TSpan> : ParsingState<TSpan> where TSpan : SharpConfigSpan {

		//private readonly TextSegmentCollection<T> spans;

		public SharpConfigParsingState(TextDocument document) : base(document) {
			this.configColorizer = MakeColorizer();
		}

		protected ContextOrigins<TSpan>? Origins { get; set; }
		protected IReadOnlyList<FilePath>? ConfigDependencies { get; set; }
		//protected HashSet<int> UnusedLines { get; set; }
		protected HashSet<int>? UsedLines { get; set; }
		protected List<SharpParsingException>? NoLocationErrors { get; set; }

		public override IDrawingMapper? DrawingMapper { get { return Origins; } }

		public override IEnumerable<FilePath> Dependencies => ConfigDependencies ?? Enumerable.Empty<FilePath>();

		private readonly SharpConfigColorizingTransformer<TSpan> configColorizer;
		public override IEnumerable<IVisualLineTransformer> GetLineTransformers() {
			return configColorizer.Yield();
		}
		protected abstract SharpConfigColorizingTransformer<TSpan> MakeColorizer();

		public override bool ColorOwners { get { return true; } }

		protected override void ResetData() {
			Origins = null;
			ConfigDependencies = null;
			//UnusedLines = null;
			UsedLines = null;
			NoLocationErrors = null;
		}

		protected override IEnumerable<SharpSheetsException> GetAdditionalErrors() {
			return NoLocationErrors ?? Enumerable.Empty<SharpSheetsException>();
		}

		#region Drawing Errors
		public override void LoadDrawingErrors(SharpDrawingException[]? newErrors) {
			//Console.WriteLine("Update drawing errors.");

			List<TSpan> newSpans = new List<TSpan>();
			if (newErrors != null && newErrors.Length > 0 && Origins != null) {
				Dictionary<DocumentSpan, SharpDrawingException[]> drawingErrorLocations = newErrors
					.Where(e => Origins.ContainsResulting(e.Origin))
					.GroupBy(e => e.Origin)
					.ToDictionary(g => Origins[g.Key].Location, g => g.ToArray());

				foreach (KeyValuePair<DocumentSpan, SharpDrawingException[]> entry in drawingErrorLocations) {
					DocumentSpan errorLocation = entry.Key;
					SharpDrawingException[] errors = entry.Value;

					try {
						int startOffset = Document.GetOffset(errorLocation.Line + 1, errorLocation.Column + 1);
						TSpan span = Create(startOffset, errorLocation.Length);
						span.Type = SharpConfigSpanType.DRAWING_ERROR;
						span.DrawingExceptions = errors;
						//span.MarkerTypes = TextMarkerTypes.SquigglyUnderline;
						//span.MarkerColor = Colors.Orange;
						newSpans.Add(span);
					}
					catch(ArgumentOutOfRangeException) { }
				}
			}

			// Separate this out here to there is as little downtime as possible between deletion and creation
			RemoveAll(s => s.Type == SharpConfigSpanType.DRAWING_ERROR);
			foreach (TSpan span in newSpans) {
				Add(span);
			}

			LoadingFinished();
		}

		public override IEnumerable<SharpDrawingException> GetDrawingErrors() {
			return ConfigSpans.Where(s => s.DrawingExceptions != null).SelectMany(e => e.DrawingExceptions!);
		}
		#endregion

		#region Span Maintainer

		public abstract TSpan Create(int startOffset, int length);

		//private static readonly Regex commentRegex = new Regex(@"\s*(?<!\\)\#.*$");
		//private static readonly Regex lineRegex = new Regex(@"^((?<property>\@?[a-z][a-z0-9]+(\.[a-z][a-z0-9]+)*:.+)|(?<div>[a-z][a-z0-9]+(\.[a-z][a-z0-9]+)*):|-(?<entry>.+)|(?<flag>\@?\!?[a-z][a-z0-9]+(\.[a-z][a-z0-9]+)*))$", RegexOptions.IgnoreCase);
		//private static readonly Regex prefixRegex = new Regex(@"\@?\!?(?<name>[a-z][a-z0-9]+(\.[a-z][a-z0-9]+)*)", RegexOptions.IgnoreCase);

		protected abstract void LoadContent(ResultEntry result);
		protected abstract void CreateAdditionalSpans();

		public override sealed void LoadResultEntry(ResultEntry result) {
			lock (LoadingLock) {

				//Console.WriteLine("Loading new result.");

				RemoveAll(s => s.Type != SharpConfigSpanType.DRAWING_ERROR); // Remove anything which is not a drawing error (they are handled separately)

				// Save parse output and config origins
				LoadContent(result);

				ConfigDependencies = result.results.dependencies;

				Dictionary<int, TSpan> lineSpans = new Dictionary<int, TSpan>();
				TSpan? MakeSpan(DocumentSpan location, int indentLevel) {
					try {
						if (lineSpans.TryGetValue(location.Line, out TSpan? existing)) { return existing; }
						int startOffset = Document.GetOffset(location.Line + 1, location.Column + 1);
						TSpan span = Create(startOffset, location.Length);
						span.IndentLevel = indentLevel;
						Add(span);
						lineSpans.Add(location.Line, span);
						return span;
					}
					catch (ArgumentOutOfRangeException) {
						return null;
					}
				}

				int documentLineCount = Document.LineCount;

				IContext root = result.results.rootEntity as IContext ?? Context.Empty;
				Dictionary<IContext, TSpan> contextSpans = new Dictionary<IContext, TSpan>(DocumentEntity.EntityComparer);
				foreach(IContext context in root.Yield().Concat(root.TraverseChildren(true))) {
					int indentLevel = context.Depth;
					DocumentSpan contextLocation = context.Location;

					if (contextLocation.Line >= 0) {
						TSpan? span = MakeSpan(contextLocation, indentLevel);
						if (span != null) {
							span.Type = SharpConfigSpanType.DIV;
							span.Name = context.SimpleName;
							span.Context = context;

							if (!contextSpans.ContainsKey(context)) { contextSpans.Add(context, span); }

							if (context.Parent != null && contextSpans.TryGetValue(context.Parent, out TSpan? parent)) {
								parent.Children.Add(span);
								span.Owners.Add(parent);
							}
						}
					}

					foreach(ContextProperty<string> property in context.GetLocalProperties(null)) {
						DocumentSpan propertyNameLocation = property.Location;
						DocumentSpan propertyValueLocation = property.ValueLocation;
						DocumentSpan propertySpan = new DocumentSpan(propertyNameLocation.Offset, propertyNameLocation.Line, propertyNameLocation.Column, propertyValueLocation.Column + propertyValueLocation.Length - propertyNameLocation.Column);

						TSpan? span = MakeSpan(propertySpan, indentLevel + 1);
						if (span != null) {
							span.Type = SharpConfigSpanType.PROPERTY;
							span.Name = property.Name; // TODO Should this have the @! prefixes? 
							span.Value = property.Value;
						}
					}

					foreach(ContextProperty<bool> flag in context.GetLocalFlags(null)) {
						DocumentSpan flagLocation = flag.Location;

						TSpan? span = MakeSpan(flagLocation, indentLevel + 1);
						if (span != null) {
							span.Type = SharpConfigSpanType.FLAG;
							span.Value = flag.Name; // TODO Should this have the @! prefixes?
							span.Name = flag.Name;
						}
					}

					foreach(ContextValue<string> entry in context.GetEntries(null)) {
						DocumentSpan entryLocation = entry.Location;

						TSpan? span = MakeSpan(entryLocation, indentLevel + 1);
						if (span != null) {
							span.Type = SharpConfigSpanType.ENTRY;
							span.Value = entry.Value;
						}
					}

					foreach(ContextValue<string> definition in context.GetDefinitions(null)) {
						DocumentSpan definitionLocation = definition.Location;
						string? definitionName = Definition.GetDefinitionName(definition.Value);

						TSpan? span = MakeSpan(definitionLocation, indentLevel + 1);
						if (span != null) {
							span.Name = definitionName;
							span.Value = definition.Value;
							span.Content = definitionName != null ? GetDefinition(definitionName, context) : null;
							span.Type = SharpConfigSpanType.DEFINITION;
						}
					}
				}

				UsedLines = new HashSet<int>(result.results.usedLines?.Select(l => l + 1) ?? Enumerable.Empty<int>());
				foreach (DocumentLine line in Document.Lines) {
					int lineIndex = line.LineNumber - 1;
					if (line.Length > 0 && !(result.results.usedLines?.Contains(lineIndex) ?? true)) {
						TSpan span = Create(line.Offset, line.Length);
						span.IsUnused = true;
						Add(span);
					}
				}

				// Assign ConfigSpans as children and owners of each other
				// TODO CAN THIS BE IMPROVED ON?
				if (result.results.lineOwners != null) {
					foreach ((int ownedLine, IReadOnlySet<int> owners) in result.results.lineOwners) {
						if (lineSpans.TryGetValue(ownedLine, out TSpan? owned)) { // Line in question
							// owners // All lines which reference this line
							foreach (TSpan owner in owners.Select(i => lineSpans.GetValueOrFallback(i, null)).WhereNotNull()) {
								owned.Owners.Add(owner);
								owner.Children.Add(owned);
							}
						}
					}
				}

				// Create parsing error spans
				foreach (IGrouping<DocumentSpan, SharpParsingException> errorGroup in result.results.errors.Where(e => e.Location != null).GroupBy(e => e.Location!.Value)) {
					//Console.WriteLine($"Errors for line: {errorGroup.Key.Line + 1}");
					DocumentSpan errorLocation = errorGroup.Key;

					try {
						int startOffset = Document.GetOffset(errorLocation.Line + 1, errorLocation.Column + 1);
						SharpParsingException[] errors = errorGroup.Distinct(SharpParsingExceptionComparer.Instance).ToArray();
						TSpan span = Create(startOffset, errorLocation.Length);
						span.ParsingExceptions = errors;
						//span.MarkerTypes = TextMarkerTypes.SquigglyUnderline;
						//span.MarkerColor = Colors.Red; // TODO This shouldn't be hard-coded here
						Add(span);
					}
					catch (ArgumentOutOfRangeException) { }
				}

				// Collect errors which have no location information
				NoLocationErrors = result.results.errors.Where(e => e.Location is null).ToList();

				// Allow subclasses to create any additional spans as they see fit
				CreateAdditionalSpans();

				// Update spans to reflect any changes in the document
				UpdateSpansFromSnapshot(result.snapshot.Version);

				//Console.WriteLine("Loaded new result.");
			}

			LoadingFinished();
		}

		/* Old Version of LoadResultEntry
		public override void LoadResultEntry(ResultEntry result) {
			lock (LoadingLock) {

				//Console.WriteLine("Loading new result.");

				RemoveAll(s => s.Type != SharpConfigSpanType.DRAWING_ERROR); // Remove anything which is not a drawing error (they are handled separately)

				// Save config origins
				Origins = new ContextOrigins<TSpan>(this, result.results.rootEntity as IContext, result.results.origins.ToDictionary(kv => kv.Key, kv => (IContext)kv.Value));
				// Save parse output
				LoadContent(result.content);

				// Create spans representing discrete parsing segments in document
				Dictionary<int, TSpan> lineSpans = new Dictionary<int, TSpan>();
				foreach (DocumentLine line in Document.Lines) {
					if (line.Length == 0) {
						//Console.WriteLine("Empty line.");
						continue;
					}

					int lineNumber = line.LineNumber - 1;
					ISegment whitespace = TextUtilities.GetLeadingWhitespace(Document, line);
					int lineStart = whitespace.EndOffset;
					Match comment = commentRegex.Match(Document.GetText(line));

					if (comment.Success && comment.Index == 0) {
						//Console.WriteLine("Empty line.");
						continue;
					} // Whitespace only from beginning of line (ignoring comments)

					int lineEnd = comment.Success ? line.Offset + comment.Index : line.EndOffset;
					TSpan span = Create(lineStart, lineEnd - lineStart, whitespace.Length);
					Add(span);
					lineSpans.Add(lineNumber, span);

					span.IsUnused = result.results.unusedLines.Contains(lineNumber);

					if (result.results.parents.TryGetValue(lineNumber, out IDocumentEntity entity) && entity is IContext context) {
						span.Context = context;
					}

					if (!ConfigureSpan(span)) {
						// Only get here if subclass has not dealt with this span fully

						string spanText = Document.GetText(span);
						Match spanMatch = lineRegex.Match(spanText);
						if (spanMatch.Groups["property"].Success) {
							span.Type = SharpConfigSpanType.PROPERTY;
							string[] parts = spanMatch.Groups["property"].Value.SplitAndTrim(2, ':').ToArray();
							span.Value = parts[1];
							Match prefixedMatch = prefixRegex.Match(parts[0]);
							span.Name = prefixedMatch.Groups["name"].Value;
						}
						else if (spanMatch.Groups["div"].Success) {
							span.Type = SharpConfigSpanType.DIV;
							span.Name = spanMatch.Groups["div"].Value;
						}
						else if (spanMatch.Groups["entry"].Success) {
							span.Type = SharpConfigSpanType.ENTRY;
							span.Value = spanMatch.Groups["entry"].Value;
						}
						else if (spanMatch.Groups["flag"].Success) {
							if (span.Context != null) {
								// span is actually a div without children
								span.Type = SharpConfigSpanType.DIV;
								span.Name = spanMatch.Groups["flag"].Value;
							}
							else {
								span.Type = SharpConfigSpanType.FLAG;
								span.Value = spanMatch.Groups["flag"].Value;
								Match prefixedMatch = prefixRegex.Match(spanMatch.Groups["flag"].Value);
								span.Name = prefixedMatch.Groups["name"].Value;
							}
						}
					}

					CreateAdditionalSpans(span); // Create any additional spans within this one that may be required
				}

				// Assign ConfigSpans as children and owners of each other
				foreach (KeyValuePair<int, HashSet<int>> lineOwners in result.results.lineOwners) {
					lineSpans.TryGetValue(lineOwners.Key, out TSpan owned); // Line in question
					if (owned != null) {
						HashSet<int> owners = lineOwners.Value; // All lines which reference this line
						foreach (TSpan owner in owners.Select(i => lineSpans.GetValueOrDefault(i, null)).Where(s => s != null)) {
							owned.Owners.Add(owner);
							owner.Children.Add(owned);
						}
					}
				}

				// Assign div spans as children and owners of each other
				foreach (TSpan owner in ConfigSpans.Where(s => s.Type == SharpConfigSpanType.DIV && s.Context != null)) {
					foreach (IContext child in owner.Context.Children) {
						if (lineSpans.TryGetValue(child.Location.Line, out TSpan owned)) {
							owner.Children.Add(owned);
							owned.Owners.Add(owner);
						}
					}
				}

				// Assign parsing errors to lines
				foreach (IGrouping<DocumentSpan, SharpParsingException> errorGroup in result.results.errors.Where(e => e.Location != null).GroupBy(e => e.Location.Value)) {
					TSpan span = FindOverlappingSpans(Document.GetLineByNumber(errorGroup.Key.Line + 1)).OrderByDescending(s => s.Length).FirstOrDefault();
					//Console.WriteLine($"Errors for line: {errorGroup.Key.Value + 1}");
					if (span != null) {
						//Console.WriteLine($"Found span");
						span.Exceptions = errorGroup.Distinct(SharpParsingExceptionComparer.Instance).ToArray<SharpSheetsException>();
						span.MarkerTypes = TextMarkerTypes.SquigglyUnderline;
						span.MarkerColor = Colors.Red;
					}
				}

				// Update spans to reflect any changes in the document
				UpdateSpansFromSnapshot(result.snapshot.Version);

				//Console.WriteLine("Loaded new result.");
			}

			LoadingFinished();
		}
		*/
		#endregion

		#region Accessors

		public TSpan? GetOwner(int line, int whitespace) {
			return this.ConfigSpans?.Where(s => s.IsParent).Where(c => {
				if (c.StartOffset >= Document.TextLength || c.EndOffset > Document.TextLength) { return false; }
				DocumentLine spanLine = Document.GetLineByOffset(c.StartOffset);
				int spanLineNumber = spanLine.LineNumber;
				int spanWhitespace = TextUtilities.GetLeadingWhitespace(Document, spanLine).Length;
				return spanLineNumber == line || (spanLineNumber <= line && spanWhitespace < whitespace);
			}).OrderByDescending(c => c.StartOffset).FirstOrDefault();
		}

		public TSpan? GetOwner(DocumentLine line) {
			int lineWhitespace = TextUtilities.GetLeadingWhitespace(Document, line).Length;
			return GetOwner(line.LineNumber, lineWhitespace);
		}

		public IContext? GetContext(DocumentLine line) {
			return GetOwner(line)?.Context ?? Origins?.Root;
		}

		public IContext? GetContext(int offset) {
			return GetContext(Document.GetLineByOffset(offset));
		}

		/*
		public Type GetOwningType(int lineNumber, int whitespace) {
			TSpan owner = GetOwner(lineNumber, whitespace);

			if (owner != null) {
				return WidgetFactory.GetType(owner.Name);
			}
			else {
				return null;
			}
		}
		*/

		protected abstract Definition? GetDefinition(string name, IContext context);

		public bool IsLineUsed(int line) {
			return UsedLines?.Contains(line) ?? true;
		}

		#endregion
	}



	public class ContextOrigins<T> : IDrawingMapper where T : SharpConfigSpan {
		private readonly SharpConfigParsingState<T> parsingState;

		private readonly IReadOnlyDictionary<IContext, List<object>> resulting; // Map context to drawn objects
		private readonly IReadOnlyDictionary<object, IContext> contexts; // Map drawn objects to contexts
		private readonly IReadOnlyDictionary<string, IContext> names;

		public IContext? Root { get; }
		public object this[IContext context] { get { return resulting[context]; } }
		public IContext this[object result] { get { return contexts[result]; } }

		public ContextOrigins(SharpConfigParsingState<T> parsingState, IContext? root, IReadOnlyDictionary<object, IContext>? origins) {
			this.parsingState = parsingState;
			this.contexts = origins ?? new Dictionary<object, IContext>();
			//this.names = this.contexts.Values.ToDictionary(c => c.FullName);
			this.names = this.contexts.Values.ToDictionaryAllowRepeats(c => c.FullName, false); // TODO Is this overwriting OK?
			this.Root = root;

			//this.resulting = this.contexts.ToDictionary(kv => kv.Value, kv => kv.Key, new IdentityEqualityComparer<IContext>());
			Dictionary<IContext, List<object>> resulting = new Dictionary<IContext, List<object>>(DocumentEntity.EntityComparer);
			foreach (KeyValuePair<object, IContext> entry in this.contexts) {
				if (!resulting.ContainsKey(entry.Value)) {
					resulting[entry.Value] = new List<object>();
				}
				resulting[entry.Value].Add(entry.Key);
			}
			this.resulting = resulting;
		}

		public bool ContainsResulting(object result) {
			if (result == null) { return false; }
			return contexts.ContainsKey(result);
		}

		public bool ContainsContext(IContext context) {
			if (context == null) { return false; }
			return resulting.ContainsKey(context);
		}

		public List<object>? GetResulting(IContext? context) {
			if (context == null) {
				return null;
			}
			else if (resulting.TryGetValue(context, out List<object>? result)) {
				return result;
			}
			else if (names.TryGetValue(context.FullName, out IContext? localContext) && resulting.TryGetValue(localContext, out List<object>? localResult)) {
				return localResult;
			}
			else {
				return null;
			}
		}

		public IContext? GetContext(object result) {
			return contexts.GetValueOrFallback(result, null);
		}

		public IEnumerable<object> GetDrawnObjects(int offset) {
			IContext? context = parsingState.GetContext(offset);
			List<object>? resulting = GetResulting(context);
			if (resulting != null) {
				foreach (object drawnObject in resulting) {
					yield return drawnObject;
				}
			}
		}

		public DocumentSpan? GetDrawnObjectLocation(object drawnObject) {
			IContext? origin = GetContext(drawnObject);
			if (origin != null) {
				return origin.Location;
				//return Math.Max(0, origin.Line) + 1;
				/*
				if (origin.Line < 0) { return 1; }
				else { return origin.Line + 1; }
				*/
			}
			else {
				return null;
			}
		}

		public int? GetDrawnObjectDepth(object drawnObject) {
			IContext? origin = GetContext(drawnObject);
			if (origin != null) {
				return origin.Depth;
			}
			else {
				return null;
			}
		}
	}

	public enum SharpConfigSpanType : int { NONE = 0, PROPERTY = 1, DIV = 2, ENTRY = 3, FLAG = 4, DEFINITION = 5, DRAWING_ERROR = 6, CUSTOM = 7 }

	public class SharpConfigSpan : EditorParseSpan {
		public bool IsParent { get { return Context != null; } } // { get; set; }
		public int? IndentLevel { get; set; }
		public string? Name { get; set; }
		public string? Value { get; set; }
		public SharpConfigSpanType Type { get; set; }
		public IContext? Context { get; set; }
		public object? Content { get; set; }

		public SharpConfigSpan(IParsingState service, int startOffset, int length) : base(service, startOffset, length) { }
	}

}
