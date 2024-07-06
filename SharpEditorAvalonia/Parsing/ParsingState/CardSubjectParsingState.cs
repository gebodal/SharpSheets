using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using SharpSheets.Utilities;
using SharpSheets.Parsing;
using SharpSheets.Cards.Definitions;
using SharpSheets.Canvas;
using SharpSheets.Evaluations.Nodes;
using SharpSheets.Cards.Card;
using SharpSheets.Cards.CardSubjects;
using SharpSheets.Cards.CardConfigs;
using SharpSheets.Exceptions;
using SharpEditorAvalonia.DataManagers;
using System.Diagnostics.CodeAnalysis;
using SharpSheets.Evaluations;

namespace SharpEditorAvalonia {

	public class CardSubjectParsingState : ParsingState<CardSubjectSpan> {

		public static readonly float TitleHeaderSizeFactor = 1.3f;
		public static readonly float SegmentHeaderSizeFactor = 1.1f;
		public static readonly float FeatureHeaderSizeFactor = 1.05f;

		public CardSubjectParsingState(TextDocument document) : base(document) { }

		protected CardCollection? Cards { get; set; }
		protected SubjectOrigins? Origins { get; set; }
		protected List<FilePath>? CardDependencies { get; set; }

		public override IParser Parser { get; } = new CardCollectionParser(
			new CardSubjectParser(SharpEditorRegistries.CardSetConfigRegistryInstance),
			SharpEditorRegistries.WidgetFactoryInstance,
			SharpEditorRegistries.ShapeFactoryInstance
			);
		public override string Extension { get { return SharpEditorFileInfo.CardSubjectExtension; } }
		public override string FileFilter { get { return SharpEditorFileInfo.CardSubjectFileFilter1; } }
		public override string DefaultFilename { get { return "cardsubjects"; } }

		public override bool HasDesignerContent { get; } = true;
		public override bool HasGeneratedContent { get; } = true;

		public override IDocumentContent? DrawableContent { get { return Cards; } }
		public override IDrawingMapper? DrawingMapper { get { return Origins; } }

		protected List<SharpParsingException>? NoLocationErrors { get; set; }

		public override IEnumerable<FilePath>? Dependencies => CardDependencies;

		public override IEnumerable<IVisualLineTransformer> GetLineTransformers() {
			return new CardHeaderFontSizeTransformer().Yield(); // TODO Implement
		}

		public override bool ColorOwners { get { return true; } }

		protected override void ResetData() {
			Origins = null;
			NoLocationErrors = null;
		}

		protected override IEnumerable<SharpSheetsException> GetAdditionalErrors() {
			return NoLocationErrors ?? Enumerable.Empty<SharpSheetsException>();
		}

		#region Drawing Errors
		public override void LoadDrawingErrors(SharpDrawingException[]? newErrors) {
			//Console.WriteLine("Update drawing errors.");

			List<CardSubjectSpan> newSpans = new List<CardSubjectSpan>();
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
						CardSubjectSpan span = Create(startOffset, errorLocation.Length);
						span.Type = CardSubjectSpanType.DRAWING_ERROR;
						span.DrawingExceptions = errors;
						//span.MarkerTypes = TextMarkerTypes.SquigglyUnderline;
						//span.MarkerColor = Colors.Orange;
						newSpans.Add(span);
					}
					catch (ArgumentOutOfRangeException) { }
				}
			}

			// Separate this out here to there is as little downtime as possible between deletion and creation
			RemoveAll(s => s.Type == CardSubjectSpanType.DRAWING_ERROR);
			foreach (CardSubjectSpan span in newSpans) {
				Add(span);
			}

			LoadingFinished();
		}

		public override IEnumerable<SharpDrawingException> GetDrawingErrors() {
			return ConfigSpans.Select(s=>s.DrawingExceptions).WhereNotNull().SelectMany(e => e);
		}
		#endregion

		#region Span Maintainer

		public CardSubjectSpan Create(int startOffset, int length) {
			ValidateSpan(startOffset, length);
			return new CardSubjectSpan(this, startOffset, length);
		}

		private CardSubjectSpan? MakeSpan(DocumentSpan location) {
			try {
				int startOffset = Document.GetOffset(location.Line + 1, location.Column + 1);
				CardSubjectSpan span = Create(startOffset, location.Length);
				Add(span);
				return span;
			}
			catch (ArgumentOutOfRangeException) {
				return null;
			}
		}

		/*
		private static readonly Regex cardDefinitionRegex = new Regex(@"^(\#\=\s*(?<path>.+)|\#\!.*)$");
		private static readonly Regex macroRegex = new Regex(@"^(?:\=+|\s+\=\=+)");
		private static readonly Regex titleRegex = new Regex(@"
			^ # Must be at start of string
			(?<titlestyle>\#\>|\#+)
			\s*
			(?<title> # Captures the whole title string
				(?<titletext>[^\(\)\[\]\#]*) # Title content (can be empty)
				(\s*\((?<titlenote>[^\(\)\[\]]*)\))? # Optional note in ()-brackets
				(\s*\[(?<titledetails>[^\[\]]*)\])? # Optional details in []-brackets
			)
			$ # Must be end of string
			", RegexOptions.IgnorePatternWhitespace);
		private static readonly Regex entryRegex = new Regex(@"^(?<entryname>[^\#\=]([^\(\)\{:]|(?<=\\)\{)+)(\s*\((?<entrynote>([^\(\)\{:]|(?<=\\)\{)*)\))?:\s*(?<entrytext>.+)$");
		*/

		private IEnumerable<KeyValuePair<Definition, ContextProperty<object>>> GetAllProperties(DefinitionEnvironment environment) {
			foreach (KeyValuePair<Definition, ContextProperty<object>> i in environment.GetPropertyValues()) {
				yield return i;
			}
			foreach(KeyValuePair<Definition, ContextProperty<EvaluationNode>> i in environment.GetPropertyNodes()) {
				yield return new KeyValuePair<Definition, ContextProperty<object>>(i.Key, i.Value.Apply<object>(n => n));
			}
		}

		private void AddEnvironmentSpans(CardSubjectSpan? parentSpan, DefinitionEnvironment environment) {
			foreach (KeyValuePair<Definition, ContextProperty<object>> definedProperty in GetAllProperties(environment)) {
				DocumentSpan propertyNameLocation = definedProperty.Value.Location;
				DocumentSpan propertyValueLocation = definedProperty.Value.ValueLocation;
				//DocumentSpan propertyLocation = new DocumentSpan(propertyNameLocation.Line, propertyNameLocation.Column, propertyValueLocation.Column + propertyValueLocation.Length - propertyNameLocation.Column);

				CardSubjectSpan? propertySpan = MakeSpan(propertyNameLocation);
				if (propertySpan != null) {
					propertySpan.Type = CardSubjectSpanType.PROPERTY;
					propertySpan.Name = definedProperty.Value.Name;
					propertySpan.Value = definedProperty.Value.Value;
					propertySpan.Definition = definedProperty.Key;

					if (parentSpan != null) {
						parentSpan.Children.Add(propertySpan);
						propertySpan.Owners.Add(parentSpan);
					}
				}
			}
		}

		public override void LoadResultEntry(ResultEntry result) {
			lock (LoadingLock) {

				//Console.WriteLine("Loading new result.");

				RemoveAll(s => s.Type != CardSubjectSpanType.DRAWING_ERROR); // Remove anything which is not a drawing error (they are handled separately)

				// Save config origins
				Origins = new SubjectOrigins(this, result.results.origins);
				// Save parse output
				Cards = result.content as CardCollection;

				CardDependencies = result.results.dependencies;

				IDocumentEntity root = result.results.rootEntity;
				Dictionary<IDocumentEntity, CardSubjectSpan> entitySpans = new Dictionary<IDocumentEntity, CardSubjectSpan>(DocumentEntity.EntityComparer);
				foreach (IDocumentEntity entity in root.Yield().Concat(root.TraverseChildren())) {
					if(entity is CardSubjectDocument cardSubjectDocument) {
						foreach (ContextValue<CardSetConfig> definition in cardSubjectDocument.GetConfigurations()) {
							DocumentSpan definitionLocation = definition.Location;

							CardSubjectSpan? span = MakeSpan(definitionLocation);
							if (span != null) {
								span.Type = CardSubjectSpanType.MACRO;
								span.Value = definition.Value;
							}
						}
					}
					else if (entity is CardSubjectSet cardSubjectSet) {
						DocumentSpan setLocation = cardSubjectSet.Location;

						if (setLocation.Line >= 0) {
							CardSubjectSpan? span = MakeSpan(setLocation);
							if (span != null) {
								span.Type = CardSubjectSpanType.MACRO;

								entitySpans.Add(cardSubjectSet, span); // Is this a good idea...?
							}
						}
					}
					else if (entity is CardSubject cardSubject) {
						DocumentSpan subjectLocation = cardSubject.Location;

						CardSubjectSpan? span = MakeSpan(subjectLocation);
						if (span != null) {
							span.Type = CardSubjectSpanType.SUBJECT;
							span.Name = cardSubject.Name.Value;
							span.Entity = cardSubject;

							entitySpans.Add(cardSubject, span);
						}

						AddEnvironmentSpans(span, cardSubject.Properties);
					}
					else if(entity is CardSegment cardSegment) {
						DocumentSpan segmentLocation = cardSegment.Location;

						CardSubjectSpan? span = MakeSpan(segmentLocation);
						if (span != null) {
							span.Type = CardSubjectSpanType.SEGMENT;
							span.Name = cardSegment.Heading.Value;
							span.Entity = cardSegment;

							entitySpans.Add(cardSegment, span);

							if (cardSegment.Subject != null && entitySpans.TryGetValue(cardSegment.Subject, out CardSubjectSpan? parent)) {
								parent.Children.Add(span);
								span.Owners.Add(parent);
							}
						}

						AddEnvironmentSpans(span, cardSegment.Details);
					}
					else if(entity is CardFeature cardFeature) {
						DocumentSpan featureLocation = cardFeature.Location;

						// TODO What about titleless features? What is their location?
						CardSubjectSpan? span = MakeSpan(featureLocation);
						if (span != null) {
							span.Type = CardSubjectSpanType.FEATURE;
							span.Name = cardFeature.Title.Value;
							span.Entity = cardFeature;

							entitySpans.Add(cardFeature, span);

							if (cardFeature.Segment != null && entitySpans.TryGetValue(cardFeature.Segment, out CardSubjectSpan? parent)) {
								parent.Children.Add(span);
								span.Owners.Add(parent);
							}
						}

						AddEnvironmentSpans(span, cardFeature.Details);
					}
				}

				foreach (DocumentLine line in Document.Lines) {
					int lineIndex = line.LineNumber - 1;
					if (line.Length > 0 && !(result.results.usedLines?.Contains(lineIndex) ?? true)) {
						CardSubjectSpan span = Create(line.Offset, line.Length);
						span.IsUnused = true;
						Add(span);
					}
				}

				// Create parsing errors spans
				foreach (IGrouping<DocumentSpan, SharpParsingException> errorGroup in result.results.errors.Where(e => e.Location != null).GroupBy(e => e.Location!.Value)) {
					//Console.WriteLine($"Errors for line: {errorGroup.Key.Line + 1}");
					DocumentSpan errorLocation = errorGroup.Key;

					try {
						int startOffset = Document.GetOffset(errorLocation.Line + 1, errorLocation.Column + 1);
						SharpParsingException[] errors = errorGroup.Distinct(SharpParsingExceptionComparer.Instance).ToArray();
						CardSubjectSpan span = Create(startOffset, errorLocation.Length);
						span.ParsingExceptions = errors;
						//span.MarkerTypes = TextMarkerTypes.SquigglyUnderline;
						//span.MarkerColor = Colors.Red; // TODO This shouldn't be hard-coded here
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
		#endregion

		#region Accessors

		private static readonly IComparer<CardSubjectSpan> cardSubjectOwnerComparer = new CardSubjectSpanOrderer();
		private class CardSubjectSpanOrderer : IComparer<CardSubjectSpan> {
			public int Compare(CardSubjectSpan? x, CardSubjectSpan? y) {
				if(x is null || y is null) { return (x is null && y is null) ? 0 : 1; }
				else if (x.StartOffset != y.StartOffset) { return x.StartOffset.CompareTo(y.StartOffset); }
				else if (x.IsParent && !y.IsParent) { return 1; }
				else if (!x.IsParent && y.IsParent) { return -1; }
				else if(x.IsParent && y.IsParent) { return x.Entity.Depth.CompareTo(y.Entity.Depth); }
				else { return x.Length.CompareTo(y.Length); }
			}
		}

		public CardSubjectSpan? GetOwner(int line) {
			CardSubjectSpan? span = this.ConfigSpans?.Where(c => {
				if (c.StartOffset >= Document.TextLength || c.EndOffset > Document.TextLength) { return false; }

				DocumentLine spanLine = Document.GetLineByOffset(c.StartOffset);
				int spanLineNumber = spanLine.LineNumber;

				if (c.Type == CardSubjectSpanType.MACRO) {
					return spanLineNumber <= line;
				}
				else if (c.IsParent) {
					if (c.Entity is CardFeature feature && !feature.IsMultiLine) {
						return spanLineNumber == line;
					}
					else {
						return spanLineNumber <= line;
					}
				}
				else {
					return false;
				}
			}).OrderByDescending(c => c, cardSubjectOwnerComparer).FirstOrDefault();
			if (span != null && span.Type == CardSubjectSpanType.MACRO) {
				return null; // Macros always break up cards
			}
			else {
				return span;
			}
		}

		public CardSubjectSpan? GetOwner(DocumentLine line) {
			return GetOwner(line.LineNumber);
		}

		public IDocumentEntity? GetEntity(DocumentLine line) {
			return GetOwner(line)?.Entity;
		}

		public IDocumentEntity? GetEntity(int offset) {
			return GetOwner(Document.GetLineByOffset(offset))?.Entity;
		}

		public ICardDocumentEntity? GetCardEntity(int offset) {
			return GetEntity(Document.GetLineByOffset(offset)) as ICardDocumentEntity;
		}

		public CardSetConfig? GetCurrentCardSetDefinition(int offset) {
			return ConfigSpans?
				.Where(s => s.StartOffset <= offset && s.Type == CardSubjectSpanType.MACRO)
				.OrderByDescending(s => s.StartOffset)
				.Select(s => s.Value)
				.OfType<CardSetConfig>()
				.FirstOrDefault();
		}

		#endregion
	}



	public class SubjectOrigins : IDrawingMapper {
		private readonly CardSubjectParsingState parsingState;

		private readonly Dictionary<IDocumentEntity, object> resulting; // Map context to drawn objects
		private readonly Dictionary<object, IDocumentEntity> entities; // Map drawn objects to contexts
		private readonly Dictionary<string, IDocumentEntity> names;

		public object this[IDocumentEntity entity] { get { return resulting[entity]; } }
		public IDocumentEntity this[object result] { get { return entities[result]; } }

		private static readonly Dictionary<Type, int> precedence = new Dictionary<Type, int> {
			{ typeof(CardSubject), 3 },
			{ typeof(CardSegment), 2 },
			{ typeof(CardFeature), 1 }
		};

		public SubjectOrigins(CardSubjectParsingState parsingState, Dictionary<object, IDocumentEntity>? origins) {
			this.parsingState = parsingState;
			this.entities = origins ?? new Dictionary<object, IDocumentEntity>();
			//this.resulting = origins.ToDictionary(kv => kv.Value, kv => kv.Key, new IdentityEqualityComparer<IDocumentEntity>());

			this.resulting = new Dictionary<IDocumentEntity, object>(new IdentityEqualityComparer<IDocumentEntity>());
			this.names = new Dictionary<string, IDocumentEntity>();
			foreach (KeyValuePair<object, IDocumentEntity> entry in this.entities) {
				if(!(resulting.TryGetValue(entry.Value, out object? existing) && existing.GetType() == typeof(DynamicCard))) {
					this.resulting[entry.Value] = entry.Key;
				}

				if (names.TryGetValue(entry.Value.FullName, out IDocumentEntity? existingEntity)) {
					if(precedence.GetValueOrDefault(entry.Value.GetType(), 0) > precedence.GetValueOrDefault(existingEntity.GetType(), 0)) {
						names[entry.Value.FullName] = entry.Value;
					}
				}
				else {
					names.Add(entry.Value.FullName, entry.Value);
				}
			}
		}

		public bool ContainsResulting(object result) {
			if (result == null) { return false; }
			return entities.ContainsKey(result);
		}

		public bool ContainsEntity(IDocumentEntity entity) {
			if (entity == null) { return false; }
			return resulting.ContainsKey(entity);
		}

		public object? GetResulting(IDocumentEntity? entity) {
			if (entity == null) {
				return null;
			}

			do {
				if (resulting.TryGetValue(entity, out object? result)) {
					return result;
				}
				else if (names.TryGetValue(entity.FullName, out IDocumentEntity? localContext) && resulting.TryGetValue(localContext, out object? localResult)) {
					return localResult;
				}
			} while ((entity = entity.Parent) != null);
			
			return null;
		}

		public IDocumentEntity? GetEntity(object result) {
			return entities.GetValueOrFallback(result, null);
		}

		public IEnumerable<object> GetDrawnObjects(int offset) {
			IDocumentEntity? context = parsingState.GetEntity(offset);
			//if (context is CardFeature feature) { context = feature.section; }
			object? resulting = GetResulting(context);
			if (resulting != null) {
				yield return resulting;
			}
		}

		public DocumentSpan? GetDrawnObjectLocation(object drawnObject) {
			IDocumentEntity? origin = GetEntity(drawnObject);
			if (origin != null) {
				/*
				if (origin.Line < 0) { return 1; }
				else { return origin.Line + 1; }
				*/
				return origin.Location;
			}
			else {
				return null;
			}
		}

		public int? GetDrawnObjectDepth(object drawnObject) {
			IDocumentEntity? origin = GetEntity(drawnObject);
			if (origin != null) {
				return origin.Depth;
			}
			else {
				return null;
			}
		}
	}

	public enum CardSubjectSpanType : int { NONE = 0, SUBJECT = 1, SEGMENT = 2, FEATURE = 3, PROPERTY = 4, MACRO = 5, EXPRESSION = 6, DRAWING_ERROR = 7 }

	public class CardSubjectSpan : EditorParseSpan {
		[MemberNotNullWhen(true, nameof(Entity))]
		public bool IsParent { get { return (Type == CardSubjectSpanType.SUBJECT || Type == CardSubjectSpanType.SEGMENT || Type == CardSubjectSpanType.FEATURE) && Entity != null; } } // { get; set; }
		public string? Name { get; set; }
		public object? Value { get; set; }
		public CardSubjectSpanType Type { get; set; } = CardSubjectSpanType.NONE;
		public IDocumentEntity? Entity { get; set; }
		public Definition? Definition { get; set; }

		public IEnvironment? Environment {
			get {
				if (Entity != null) {
					if (Entity is CardSubject subject) {
						return subject.Environment;
					}
					else if(Entity is CardSegment segment) {
						return segment.Environment;
					}
					else if(Entity is CardFeature feature) {
						return feature.Environment;
					}
				}
				return null;
			}
		}

		public CardSubjectSpan(IParsingState service, int startOffset, int length) : base(service, startOffset, length) { }
	}

	public class CardHeaderFontSizeTransformer : DocumentColorizingTransformer {
		private static readonly Regex titleStartRegex = new Regex(@"^(\#\>|\#{1,3})(?!\=|\!)");

		protected override void ColorizeLine(DocumentLine line) {
			string chars = CurrentContext.GetText(line.Offset, Math.Min(3, line.Length)).Text;
			if (chars.StartsWith("#")) {
				float multiplier = 1f;
				Match styleMatch = titleStartRegex.Match(chars);
				string titleStyle = styleMatch.Groups[1].Value;
				if(titleStyle == "#>" || titleStyle.Length == 1) {
					multiplier = CardSubjectParsingState.TitleHeaderSizeFactor;
				}
				else if (titleStyle.Length == 2) {
					multiplier = CardSubjectParsingState.SegmentHeaderSizeFactor;
				}
				else if (titleStyle.Length > 2) {
					multiplier = CardSubjectParsingState.FeatureHeaderSizeFactor;
				}

				foreach (VisualLineElement elem in CurrentContext.VisualLine.Elements) {
					ChangeVisualElements(
						elem.VisualColumn, // startOffset
						elem.VisualColumn + elem.VisualLength, // endOffset
						(VisualLineElement element) => {
							double origRend = element.TextRunProperties.FontRenderingEmSize;
							element.TextRunProperties.SetFontRenderingEmSize(origRend * multiplier);
						});
				}
			}
		}
	}
}
