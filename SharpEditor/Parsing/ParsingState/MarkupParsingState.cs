using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using SharpSheets.Canvas;
using SharpSheets.Evaluations;
using SharpSheets.Exceptions;
using SharpSheets.Markup.Elements;
using SharpSheets.Markup.Helpers;
using SharpSheets.Markup.Parsing;
using SharpSheets.Markup.Patterns;
using SharpSheets.Parsing;
using SharpSheets.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using SharpEditor.DataManagers;
using System.Diagnostics.CodeAnalysis;

namespace SharpEditor.Parsing.ParsingState {
	public class MarkupParsingState : ParsingState<MarkupSpan> {

		public MarkupParsingState(TextDocument document) : base(document) { }

		public override string Extension => SharpEditorFileInfo.MarkupExtension;
		public override string FileFilter => SharpEditorFileInfo.MarkupFileFilter1;
		public override string DefaultFilename => "sharpmarkup";

		public override bool HasDesignerContent { get; } = true;
		public override bool HasGeneratedContent { get; } = false;

		public override bool ColorOwners { get { return true; } }
		public override bool HighlightSameToken { get { return true; } }

		protected IReadOnlyList<FilePath>? MarkupDependencies { get; set; }
		protected MarkupOrigins? Origins { get; set; }

		public override IParser Parser => SharpEditorRegistries.MarkupPatternParserInstance;

		private MarkupExamplesDocument? examplesDocument;
		public override IDocumentContent? DrawableContent { get { return examplesDocument; } }
		public override IDrawingMapper? DrawingMapper { get { return Origins; } }

		protected List<SharpParsingException>? NoLocationErrors { get; set; }

		public override IEnumerable<FilePath>? Dependencies => MarkupDependencies;

		public override IEnumerable<SharpDrawingException> GetDrawingErrors() {
			return Enumerable.Empty<SharpDrawingException>();
		}

		public override IEnumerable<IVisualLineTransformer> GetLineTransformers() { return Enumerable.Empty<IVisualLineTransformer>(); }

		protected override void ResetData() {
			Origins = null;
			NoLocationErrors = null;
		}

		protected override IEnumerable<SharpSheetsException> GetAdditionalErrors() {
			return NoLocationErrors ?? Enumerable.Empty<SharpSheetsException>();
		}

		public override void LoadDrawingErrors(SharpDrawingException[]? newErrors) {
			//Console.WriteLine("Update drawing errors.");

			List<MarkupSpan> newSpans = new List<MarkupSpan>();
			if (newErrors != null && newErrors.Length > 0 && Origins != null) {
				Dictionary<DocumentSpan, SharpDrawingException[]> drawingErrorLocations = newErrors
					.Where(e => Origins.ContainsResulting(e.Origin))
					.GroupBy(e => e.Origin)
					.ToDictionary(g => Origins[g.Key].Location, g => g.ToArray());

				foreach (KeyValuePair<DocumentSpan, SharpDrawingException[]> entry in drawingErrorLocations) {
					DocumentSpan errorLocation = entry.Key;
					SharpDrawingException[] errors = entry.Value;

					try {
						//int startOffset = Document.GetOffset(errorLocation.Line + 1, errorLocation.Column + 1);
						int startOffset = errorLocation.Offset;
						MarkupSpan span = Create(startOffset, errorLocation.Length);
						span.Type = MarkupSpanType.DRAWING_ERROR;
						span.DrawingExceptions = errors;
						//span.MarkerTypes = TextMarkerTypes.SquigglyUnderline;
						//span.MarkerColor = Colors.Orange;
						newSpans.Add(span);
					}
					catch (ArgumentOutOfRangeException) { }
				}
			}

			// Separate this out here to there is as little downtime as possible between deletion and creation
			RemoveAll(s => s.Type == MarkupSpanType.DRAWING_ERROR);
			foreach (MarkupSpan span in newSpans) {
				Add(span);
			}

			LoadingFinished();
		}

		public MarkupSpan Create(int startOffset, int length) {
			ValidateSpan(startOffset, length);
			return new MarkupSpan(this, startOffset, length);
		}

		public override void LoadResultEntry(ResultEntry result) {
			lock (LoadingLock) {

				//Console.WriteLine("Loading new result.");

				RemoveAll(s => s.Type != MarkupSpanType.DRAWING_ERROR); // Remove anything which is not a drawing error (they are handled separately)

				// Save config origins
				Origins = new MarkupOrigins(this, result.results.origins);
				// Save parse output
				List<MarkupPattern>? patterns = result.content as List<MarkupPattern>;
				examplesDocument = patterns is not null ? new MarkupExamplesDocument(patterns, 0.1f, SharpEditorRegistries.WidgetFactoryInstance, SharpEditorRegistries.ShapeFactoryInstance) : null;

				MarkupDependencies = result.results.dependencies;

				MarkupSpan? MakeSpan(DocumentSpan location) {
					try {
						int startOffset = Document.GetOffset(location.Line + 1, location.Column + 1);
						MarkupSpan span = Create(startOffset, location.Length);
						Add(span);
						return span;
					}
					catch (ArgumentOutOfRangeException) {
						return null;
					}
				}
				MarkupSpan? MakeFullSpan(DocumentSpan start, DocumentSpan end) {
					try {
						int startOffset = Document.GetOffset(start.Line + 1, start.Column + 1);
						int endOffset = Document.GetOffset(end.Line + 1, end.Column + 1) + end.Length;
						MarkupSpan span = Create(startOffset, endOffset - startOffset);
						Add(span);
						return span;
					}
					catch (ArgumentOutOfRangeException) {
						return null;
					}
				}

				IDocumentEntity root = result.results.rootEntity;
				Dictionary<IDocumentEntity, MarkupSpan> entitySpans = new Dictionary<IDocumentEntity, MarkupSpan>(DocumentEntity.EntityComparer);
				foreach (IDocumentEntity entity in root.Yield().Concat(root.TraverseChildren())) {
					if (entity is XMLElement element) {
						DocumentSpan elementLocation = element.NameValue.Location;

						MarkupSpan? span = MakeSpan(elementLocation);
						if (span != null) {
							span.Type = MarkupSpanType.ELEMENT;
							span.Name = element.Name;
							span.Entity = element;
							span.Resulting = Origins.GetResulting(element)?.ToList();

							entitySpans.Add(element, span);

							if (element.Parent != null && entitySpans.TryGetValue(element.Parent, out MarkupSpan? parentSpan)) {
								parentSpan.Children.Add(span);
								span.Owners.Add(parentSpan);
							}

							if (element.EndTag != null) {
								DocumentSpan endLocation = element.EndTag.NameLocation;

								MarkupSpan? endSpan = MakeSpan(endLocation);
								if (endSpan != null) {
									endSpan.Type = MarkupSpanType.ENDTAG;
									endSpan.Name = element.Name;

									span.Children.Add(endSpan);
									endSpan.Owners.Add(span);
								}
							}
						}

						// Create full span for whole element, to use for determining which element the carat is currently inside
						if (element.EndTag != null) {
							// We have an end tag, and so the whole span from start to end tag must be considered
							MarkupSpan? fullSpan = MakeFullSpan(element.Location, element.EndTag.Location);
							if (fullSpan != null) {
								fullSpan.Type = MarkupSpanType.WHOLENODE;
								fullSpan.Name = element.Name;
								fullSpan.Entity = element;
								fullSpan.Resulting = span?.Resulting;
							}
						}
						else {
							// We do not have an end tag, and so the first tag is the whole node
							MarkupSpan? fullSpan = MakeSpan(element.Location);
							if (fullSpan != null) {
								fullSpan.Type = MarkupSpanType.WHOLENODE;
								fullSpan.Name = element.Name;
								fullSpan.Entity = element;
								fullSpan.Resulting = span?.Resulting;
							}
						}

						foreach (ContextProperty<string> attribute in element.Attributes) {
							DocumentSpan attributeNameLocation = attribute.Location;

							MarkupSpan? attributeSpan = MakeSpan(attributeNameLocation);
							if (attributeSpan != null) {
								attributeSpan.Type = MarkupSpanType.ATTRIBUTE;
								attributeSpan.Name = attribute.Name;
								//attributeSpan.Value = attribute.Value;
								attributeSpan.Entity = element;

								if (span != null) {
									span.Children.Add(attributeSpan);
									attributeSpan.Owners.Add(span);
								}
							}
						}
					}
					else if (entity is XMLText textNode) {
						DocumentSpan textLocation = textNode.Location;

						MarkupSpan? span = MakeSpan(textLocation);
						if (span != null) {
							span.Type = MarkupSpanType.TEXT;
							span.Entity = textNode;

							entitySpans.Add(textNode, span);

							if (textNode.Parent != null && entitySpans.TryGetValue(textNode.Parent, out MarkupSpan? parent)) {
								parent.Children.Add(span);
								span.Owners.Add(parent);
							}
						}
					}
				}

				// Create parsing errors spans
				foreach (IGrouping<DocumentSpan, SharpParsingException> errorGroup in result.results.errors.Where(e => e.Location.HasValue).GroupBy(e => e.Location!.Value)) {
					//Console.WriteLine($"Errors for line: {errorGroup.Key.Line + 1}");
					DocumentSpan errorLocation = errorGroup.Key;

					try {
						int startOffset = errorLocation.Offset; // Document.GetOffset(errorLocation.Line + 1, errorLocation.Column + 1);
						SharpParsingException[] errors = errorGroup.Distinct(SharpParsingExceptionComparer.Instance).ToArray();
						MarkupSpan span = Create(startOffset, errorLocation.Length);
						span.ParsingExceptions = errors;
						//span.MarkerTypes = TextMarkerTypes.SquigglyUnderline;
						//span.MarkerColor = Colors.Red; // This shouldn't be hard-coded here
						Add(span);
					}
					catch (ArgumentOutOfRangeException) { }
				}

				// Collect errors which have no location information
				NoLocationErrors = result.results.errors.Where(e => e.Location is null).ToList();

				// Update spans to reflect any changes in the document
				UpdateSpansFromSnapshot(result.snapshot.Version);

				//Console.WriteLine("Loaded new result.");
			}

			LoadingFinished();
		}

		public MarkupSpan? GetOwner(int offset) {
			return GetOwners(offset).FirstOrDefault();
			//return GetSpansAtOffset(offset).Where(s => s.Type == MarkupSpanType.WHOLENODE).OrderByDescending(s => s.Entity?.Depth ?? int.MaxValue).FirstOrDefault();
			//return ConfigSpans.Where(s => s.IsXMLNode && s.StartOffset <= offset).OrderByDescending(s => s.StartOffset).FirstOrDefault();
		}

		public IEnumerable<MarkupSpan> GetOwners(int offset) {
			return GetSpansAtOffset(offset).Where(s => s.Type == MarkupSpanType.WHOLENODE).OrderByDescending(s => s.Entity?.Depth ?? int.MaxValue);
		}

		public IDocumentEntity? GetEntity(int offset) {
			return GetOwner(offset)?.Entity;
		}

		public IEnumerable<IDocumentEntity> GetEntities(int offset) {
			return GetOwners(offset).Select(s => s.Entity).WhereNotNull();
		}

		public IVariableBox? GetVariables(int offset) {
			return GetEntities(offset).Select(e => Origins?.GetResulting(e)).WhereNotNull()
				.Select(i => {
					if (i is MarkupPattern pattern) {
						return pattern.Variables;
					}
					else if (i is DivElement div) {
						return div.Variables;
					}
					else {
						return null;
					}
				}).WhereNotNull().FirstOrDefault();
		}
	}

	public enum MarkupSpanType : int { NONE = 0, ELEMENT = 1, ENDTAG = 2, WHOLENODE = 3, TEXT = 4, ATTRIBUTE = 5, DRAWING_ERROR = 6 }
	public class MarkupSpan : EditorParseSpan {
		public string? Name { get; set; } = null;
		public IDocumentEntity? Entity { get; set; } = null;
		public MarkupSpanType Type { get; set; } = MarkupSpanType.NONE;
		public IList<object>? Resulting { get; set; } = null;

		[MemberNotNullWhen(true, nameof(Entity))]
		public bool IsXMLNode { get { return (Type == MarkupSpanType.ELEMENT || Type == MarkupSpanType.TEXT) && Entity != null; } } // { get; set; }

		public MarkupSpan(IParsingState service, int startOffset, int length) : base(service, startOffset, length) { }
	}

	public class MarkupOrigins : IDrawingMapper {

		private readonly MarkupParsingState parsingState;

		private readonly IReadOnlyParseOrigins<IDocumentEntity> entities; // Map drawn objects to document entities
		private readonly Dictionary<IDocumentEntity, List<object>> resulting; // Map document entities to drawn objects

		public IDocumentEntity this[object result] { get { return entities.GetOrigin(result); } }

		public MarkupOrigins(MarkupParsingState parsingState, IReadOnlyParseOrigins<IDocumentEntity>? origins) {
			this.parsingState = parsingState;
			this.entities = origins ?? new ParseOrigins<IDocumentEntity>(); // new Dictionary<object, IDocumentEntity>(origins?.GetData() ?? new Dictionary<object, IDocumentEntity>(), new IdentityEqualityComparer<object>());

			this.resulting = new Dictionary<IDocumentEntity, List<object>>(new IdentityEqualityComparer<IDocumentEntity>());
			foreach (KeyValuePair<object, IDocumentEntity> entry in this.entities.GetData()) {
				if (!resulting.ContainsKey(entry.Value)) {
					resulting[entry.Value] = new List<object>();
				}
				resulting[entry.Value].Add(entry.Key);
			}
		}

		public bool ContainsResulting(object result) {
			if (result == null) { return false; }
			return entities.ContainsProduct(result);
		}

		public IDocumentEntity? GetEntity(object drawnObject) {
			if (entities.TryGetOrigin(drawnObject, out IDocumentEntity? entity)) {
				return entity;
			}
			else {
				return null;
			}
		}

		public IList<object>? GetResulting(IDocumentEntity? entity) {
			if (entity == null) {
				return null;
			}

			if (resulting.TryGetValue(entity, out List<object>? result)) {
				return result;
			}

			return null;
		}

		public IEnumerable<object> GetDrawnObjects(int offset) {
			/*
			IDocumentEntity entity = parsingState.GetEntity(offset);
			if (entity != null && resulting.TryGetValue(entity, out List<object> drawnObjects)) {
				return drawnObjects;
			}
			else {
				return Enumerable.Empty<object>();
			}
			*/

			List<object> allDrawnObjects = new List<object>();

			foreach (IDocumentEntity entity in parsingState.GetEntities(offset).OrderByDescending(e => e.Depth)) {
				if (entity != null && resulting.TryGetValue(entity, out List<object>? drawnObjects)) {
					//return drawnObjects;
					allDrawnObjects.AddRange(drawnObjects);
					break;
				}
			}

			return allDrawnObjects;
		}

		public int? GetDrawnObjectDepth(object drawnObject) {
			return GetEntity(drawnObject)?.Depth;
		}

		public DocumentSpan? GetDrawnObjectLocation(object drawnObject) {
			return GetEntity(drawnObject)?.Location;
		}
	}
}
