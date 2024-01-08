using SharpSheets.Evaluations;
using SharpSheets.Widgets;
using SharpSheets.Layouts;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using SharpSheets.Shapes;
using SharpSheets.Parsing;
using SharpSheets.Canvas;
using SharpSheets.Documentation;
using SharpSheets.Markup.Canvas;
using SharpSheets.Markup.Elements;
using SharpSheets.Exceptions;
using SharpSheets.Utilities;
using System.Text.RegularExpressions;
using SharpSheets.Markup.Parsing;

namespace SharpSheets.Markup.Patterns {

	public class MarkupWidgetPattern : MarkupPattern {

		private readonly Regex[] namedChildren;

		public MarkupWidgetPattern(
				string? library,
				string name,
				string? description,
				IMarkupArgument[] arguments,
				MarkupValidation[] validations,
				//MarkupVariable[] variables,
				Rectangle? exampleSize,
				Size? exampleCanvas,
				DivElement rootElement,
				Utilities.FilePath source
			) : base(library, name, description, arguments, validations, exampleSize, exampleCanvas, rootElement, source) {

			namedChildren = FindNamedChildren(arguments).ToArray();
		}

		protected IEnvironment ParseWidgetArguments(IContext context, WidgetSetup setup, Utilities.DirectoryPath source, WidgetFactory? widgetFactory, ShapeFactory? shapeFactory, bool useExamples, out SharpParsingException[] buildErrors) {
			IEnvironment environment = ParseArguments(context ?? Context.Empty, source, widgetFactory, shapeFactory, useExamples, out buildErrors)
				.AppendEnvironment(SimpleEnvironments.Create(new List<(object?, EnvironmentVariableInfo)>() {
					(setup.gutter, new EnvironmentVariableInfo("gutter", EvaluationType.FLOAT, null)),
					(setup.layout, new EnvironmentVariableInfo("layout", MarkupEvaluationTypes.LAYOUT, null))
				}));

			return environment;
		}

		public IWidget MakeWidget(IContext? context, Utilities.DirectoryPath source, WidgetFactory widgetFactory, ShapeFactory? shapeFactory, out SharpParsingException[] buildErrors) {
			try {
				List<SharpParsingException> errors = new List<SharpParsingException>();

				bool useExamples;

				WidgetSetup setup;
				if (context is null) {
					setup = new WidgetSetup(_diagnostic: true);
					useExamples = true;
				}
				else {
					setup = (WidgetSetup)SharpFactory.Construct(WidgetFactory.widgetSetupConstructor, context, source, widgetFactory, shapeFactory, Array.Empty<object>(), out SharpParsingException[] setupErrors);
					errors.AddRange(setupErrors);
					useExamples = false;
				}

				IEnvironment argumentEnvironment = ParseWidgetArguments(context ?? Context.Empty, setup, source, widgetFactory, shapeFactory, useExamples, out SharpParsingException[] argErrors);
				errors.AddRange(argErrors);

				DrawableDivElement? drawable = rootElement.GetDrawable(GetGraphicsData(setup), argumentEnvironment, shapeFactory, useExamples || setup.diagnostic);

				buildErrors = errors.ToArray();
				return new MarkupWidget(this, drawable, setup);
			}
			catch (EvaluationException e) {
				throw new SharpParsingException(context?.Location, $"Evaluation error when constructing instance of {Name} widget: " + e.Message, e);
			}
			/*
			catch (SharpParsingException e) {
				throw new SharpParsingException(context?.Location, $"Parsing error when constructing instance of {Name} widget: " + e.Message, e);
			}
			*/
		}

		public IWidget MakeExample(WidgetFactory? widgetFactory, ShapeFactory? shapeFactory, WidgetSetup? knownSetup, out SharpParsingException[] buildErrors) {
			try {
				WidgetSetup setup = knownSetup ?? new WidgetSetup();

				IEnvironment argumentEnvironment = ParseWidgetArguments(Context.Empty, setup, sourceDirectory, widgetFactory, shapeFactory, true, out buildErrors);

				DrawableDivElement? drawable = rootElement.GetDrawable(GetGraphicsData(setup), argumentEnvironment, shapeFactory, setup.diagnostic);

				return new MarkupWidget(this, drawable, setup);
			}
			catch (EvaluationException e) {
				throw new SharpParsingException(DocumentSpan.Imaginary, $"Evaluation error when constructing example of {Name} widget: " + e.Message, e);
			}
			catch (SharpParsingException e) {
				throw new SharpParsingException(DocumentSpan.Imaginary, $"Parsing error when constructing example of {Name} widget:" + e.Message, e);
			}
		}

		public override object MakeExample(WidgetFactory? widgetFactory, ShapeFactory? shapeFactory, bool diagnostic, out SharpParsingException[] buildErrors) {
			return MakeExample(widgetFactory, shapeFactory, new WidgetSetup(gutter: 8f, _diagnostic: diagnostic), out buildErrors);
		}

		protected override IEnumerable<ArgumentDetails> GetArgumentDetails() {
			return base.GetArgumentDetails().Concat(DocumentationGenerator.GetWidgetSetupArguments());
		}

		public override MarkupConstructorDetails GetConstructorDetails() {
			return new MarkupConstructorDetails(this, typeof(IWidget), typeof(MarkupWidget), GetArgumentDetails().ToArray(), Description is not null ? new DocumentationString(Description) : null);
		}

		private static MarkupCanvasGraphicsData GetGraphicsData(WidgetSetup setup) {
			return new MarkupCanvasGraphicsData(
				setup.linewidth,
				setup.foregroundColor,
				setup.backgroundColor,
				setup.midtoneColor,
				setup.textColor,
				setup.finalFonts
				);
		}

		private static IEnumerable<Regex> FindNamedChildren(IMarkupArgument[] arguments) {
			foreach(IMarkupArgument argument in arguments) {
				if(argument is MarkupSingleArgument singleArg) {
					if (typeof(IWidget).IsAssignableFrom(singleArg.Type.DataType)) {
						yield return new Regex(@"^" + Regex.Escape(singleArg.ArgumentName.ToString()) + @"$", RegexOptions.IgnoreCase);
					}
					else if(singleArg.IsNumbered && typeof(IWidget).IsAssignableFrom(singleArg.Type.ElementType!.DataType)) {
						yield return new Regex(@"^" + Regex.Escape(singleArg.ArgumentName.ToString()) + @"[0-9]+$", RegexOptions.IgnoreCase);
					}
				}
				else if(argument is MarkupGroupArgument groupArg) {
					foreach(Regex namedChildFromGroup in FindNamedChildren(groupArg.Args)) {
						yield return namedChildFromGroup;
					}
				}
				else {
					throw new InvalidOperationException($"Unknown {nameof(IMarkupArgument)} subtype.");
				}
			}
		}

		public bool HasNamedChild(string child) {
			return namedChildren.Any(r => r.IsMatch(child));
		}

		public IEnumerable<Regex> GetNamedChildren() {
			return namedChildren;
		}
	}

	public class MarkupWidget : SharpWidget, IMarkupObject {

		public MarkupPattern Pattern => pattern;

		private readonly MarkupWidgetPattern pattern;
		private readonly DrawableDivElement? drawableDiv;

		//public override bool ProvidesRemaining => drawableDiv.ProvidesRemaining;
		public override bool ProvidesRemaining => drawableDiv?.AreaExists("remaining") ?? false;

		public MarkupWidget(MarkupWidgetPattern pattern, DrawableDivElement? drawableDiv, WidgetSetup setup) : base(setup) {
			this.pattern = pattern;
			this.drawableDiv = drawableDiv;
		}

		protected override Rectangle? GetContainerArea(ISharpGraphicsState graphicsState, Rectangle rect) {
			// TODO Should we go back to using `drawableDiv.GetNamedArea("remaining", canvas, rect)` for this?
			return drawableDiv?.GetNamedArea("remaining", graphicsState, rect);

			//return drawableDiv.GetNamedArea("remaining", canvas, rect);
			//return drawableDiv.RemainingRect(graphicsState, rect);
			//return drawableDiv.ContainerArea(graphicsState, rect);
		}

		protected override void DrawWidget(ISharpCanvas canvas, Rectangle rect, CancellationToken cancellationToken) {
			try {
				drawableDiv?.Draw(canvas, rect, cancellationToken);
			}
			catch (SharpDrawingException e) {
				SharpDrawingException widgetException = new SharpDrawingException(this, e.Message, e);
				//canvas.LogError(widgetException); // The error will be logged higher in the call stack
				throw widgetException;
			}
		}

		protected override Rectangle[] GetDiagnosticRects(ISharpGraphicsState graphicsState, Rectangle available) {
			// TODO Is this right?
			if(drawableDiv is null) { return Array.Empty<Rectangle>(); }
			return drawableDiv.GetAreas().Select(n => drawableDiv.GetNamedArea(n, graphicsState, available)).WhereNotNull()
				.Concat(drawableDiv.DiagnosticRects(graphicsState, available)).ToArray();
		}

		protected override Size? GetMinimumContentSize(ISharpGraphicsState graphicsState, Size availableSpace) {
			return drawableDiv?.MinimumContentSize(graphicsState, availableSpace);
			//return base.GetMinimumContentSize(graphicsState, availableSpace);
		}

	}

	/*
	public class MarkupRect : ISharpRect {

		private readonly RectSetup setup;
		private readonly List<ISharpRect> children;

		public MarkupRect(MarkupPatternInstance patternInstance, RectSetup setup) {
			this.setup = setup;
			this.children = new List<ISharpRect>();
		}

		public bool AddChild(ISharpRect child) {
			children.Add(child);
			return true;
		}

		public IDrawableGridElement GetDrawable(ISharpCanvas canvas) {
			throw new NotImplementedException();
		}

		public Rectangle MinimumContentRect(ISharpCanvas canvas, Rectangle available) {
			return pattern.rootElement.MinimumRect(canvas, Layout.ROWS, available, this);
		}

		public Rectangle ContainerArea(ISharpCanvas canvas, Rectangle rect) {
			return pattern.rootElement.GridElementRemainingRect(canvas, rect, this);
		}

		public void Draw(ISharpCanvas canvas, Rectangle rect, CancellationToken cancellationToken) {
			if (cancellationToken.IsCancellationRequested) { return; }

			canvas.SaveState();

			canvas.ApplySetup(setup);

			Rectangle[] childRects = this.GetElementRects(canvas, rect, out Rectangle availableRect, out _, out Rectangle[] gutters, out _, out _);

			canvas.RegisterArea(this, rect);
			canvas.RegisterArea(this, availableRect);

			//DrawRect(canvas, availableRect);
			pattern.rootElement.Draw(canvas, availableRect, this, this.diagnostic);

			if (children.Count > 0) {

				if (setup.gutterStyle != null && gutters != null) {
					for (int i = 0; i < gutters.Length; i++) {
						setup.gutterStyle.Draw(canvas, gutters[i], Layout);
					}
				}

				for (int i = 0; i < childRects.Length; i++) {
					//Console.WriteLine($"CHILD: {children[i].GetType().Name} => {childRects[i]}");
					try {
						if (childRects[i] != null) {
							try {
								children[i].Draw(canvas, childRects[i], cancellationToken);
							}
							catch (SharpDrawingException e) {
								throw e;
							}
							catch (Exception e) {
								throw new SharpDrawingException(children[i], "Drawing Error: " + e.Message, e); // TODO Return custom error message
							}
						}
						else if (children[i].GetType() != typeof(Empty)) { // It's fine if Empty children have no space available
							throw new SharpDrawingException(children[i], $"No rect available for child {i} ({children[i].GetType().Name})");
						}
					}
					catch (SharpDrawingException e) {
						Rectangle errorRect = childRects[i] ?? rect;
						canvas.SaveState();
						canvas.SetStrokeColor(Color.Red);
						canvas.SetTextColor(Color.Red);
						canvas.Rectangle(errorRect).Stroke();
						canvas.SetTextSize(200f);
						canvas.FitText(errorRect, e.Message, Justification.LEFT, 1.35f);
						canvas.RestoreState();
						canvas.LogError(e);
					}

					if (cancellationToken.IsCancellationRequested) {
						canvas.RestoreState();
						return;
					}
				}
			}

			canvas.RestoreState();
		}

		
	}
	*/
}
