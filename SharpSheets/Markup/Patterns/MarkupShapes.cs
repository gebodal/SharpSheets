using SharpSheets.Evaluations;
using SharpSheets.Shapes;
using SharpSheets.Layouts;
using System;
using System.Collections.Generic;
using System.Linq;
using SharpSheets.Parsing;
using SharpSheets.Canvas;
using SharpSheets.Documentation;
using SharpSheets.Widgets;
using SharpSheets.Markup.Elements;
using SharpSheets.Markup.Canvas;
using SharpSheets.Utilities;
using SharpSheets.Exceptions;
using SharpSheets.Markup.Parsing;

namespace SharpSheets.Markup.Patterns {

	public abstract class MarkupShapePattern : MarkupPattern {

		protected abstract Type InstanceType { get; }

		public MarkupShapePattern(
			string? library,
			string name,
			string? description,
			IMarkupArgument[] arguments,
			MarkupValidation[] validations,
			Rectangle? exampleSize,
			Size? exampleCanvas,
			DivElement rootElement,
			Utilities.FilePath source
			) : base(library, name, description, arguments, validations, exampleSize, exampleCanvas, rootElement, source) { }

	}

	public abstract class MarkupShapePattern<T> : MarkupShapePattern where T : IShape {

		public MarkupShapePattern(
			string? library,
			string name,
			string? description,
			IMarkupArgument[] arguments,
			MarkupValidation[] validations,
			Rectangle? exampleSize,
			Size? exampleCanvas,
			DivElement rootElement,
			Utilities.FilePath source
			) : base(library, name, description, arguments, validations, exampleSize, exampleCanvas, rootElement, source) { }

		public sealed override MarkupBuilderDetails GetBuilderDetails() {
			return new MarkupBuilderDetails(this, DisplayType.FromSystem<T>(), DisplayType.Create(InstanceType), GetArgumentDetails().ToArray(), Description is not null ? new DocumentationString(Description) : null);
		}

		protected virtual IEnumerable<(EvaluationValue value, EnvironmentVariableInfo info)> GetAdditionalArguments(IContext context, ShapeFactory.ShapeParams? shapeParams, DirectoryPath source, WidgetFactory widgetFactory, ShapeFactory? shapeFactory) {
			return Enumerable.Empty<(EvaluationValue value, EnvironmentVariableInfo info)>();
		}

		protected static EvaluationValue MakeArgumentValue(ArgumentDetails arg, EvaluationType type, IContext context, DirectoryPath source, WidgetFactory widgetFactory, ShapeFactory? shapeFactory) {
			if (type is BoolEvaluationType boolType) {
				bool value = context.HasFlag(arg.Name, arg.UseLocal, context) ? context.GetFlag(arg.Name, arg.UseLocal, context) : ((bool?)arg.DefaultValue ?? false);
				return boolType.MakeValue(value);
			}

			string? valueStr = context.GetProperty(arg.Name, arg.UseLocal, context, null);

			if(valueStr != null) {
				try {
					return type.ParseValue(valueStr, source);
					// TODO Needs improving/making more robust?
				}
				catch (FormatException) { }
			}

			// TODO This needs better error handling!

			return type.MakeValue(arg.DefaultValue);
		}

		protected abstract T ConstructInstance(IEnvironment argumentEnvironment, ShapeFactory.ShapeParams? shapeParams, ShapeFactory? shapeFactory, bool constructionLines);

		public T MakeShape(IContext? context, ShapeFactory.ShapeParams? shapeParams, DirectoryPath source, ShapeFactory? shapeFactory, bool constructionLines, out SharpParsingException[] buildErrors) {
			WidgetFactory dummyWidgetFactory = new WidgetFactory(MarkupRegistry.Empty, shapeFactory);

			IEnvironment argumentEnvironment = ParseArguments(context ?? SharpSheets.Parsing.Context.Empty, source, null, shapeFactory, context == null, out buildErrors)
				.AppendEnvironment(GetAdditionalArguments(context ?? SharpSheets.Parsing.Context.Empty, shapeParams, source, dummyWidgetFactory, shapeFactory));

			return ConstructInstance(argumentEnvironment, shapeParams, shapeFactory, constructionLines);
		}

		public override object MakeExample(WidgetFactory? widgetFactory, ShapeFactory? shapeFactory, bool diagnostic, out SharpParsingException[] buildErrors) {
			return MakeShape(null, null, sourceDirectory, shapeFactory, diagnostic, out buildErrors);
		}

		protected abstract ArgumentDetails[] GetAdditionalArgumentDetails();

		protected sealed override IEnumerable<ArgumentDetails> GetArgumentDetails() {
			return GetAdditionalArgumentDetails().Concat(base.GetArgumentDetails());
		}

	}

	public abstract class MarkupAreaShapePattern<T> : MarkupShapePattern<T> where T : IAreaShape {

		public MarkupAreaShapePattern(
			string? library,
			string name,
			string? description,
			IMarkupArgument[] arguments,
			MarkupValidation[] validations,
			Rectangle? exampleSize,
			Size? exampleCanvas,
			DivElement rootElement,
			Utilities.FilePath source
			) : base(library, name, description, arguments, validations, exampleSize, exampleCanvas, rootElement, source) { }

		protected override IEnumerable<(EvaluationValue value, EnvironmentVariableInfo info)> GetAdditionalArguments(IContext context, ShapeFactory.ShapeParams? shapeParams, DirectoryPath source, WidgetFactory widgetFactory, ShapeFactory? shapeFactory) {
			ShapeFactory.AreaShapeParams areaShapeParams = shapeParams?.As<ShapeFactory.AreaShapeParams>() ?? new ShapeFactory.AreaShapeParams(-1f);
			return base.GetAdditionalArguments(context, shapeParams, source, widgetFactory, shapeFactory)
				.Append(PatternData.AreaShapeAspectVariable(Context, areaShapeParams.Aspect));
		}

		protected override ArgumentDetails[] GetAdditionalArgumentDetails() {
			return PatternData.AreaShapeVariables;
		}

	}

	public abstract class MarkupShape : IShape, IMarkupObject {

		public MarkupPattern Pattern { get; }
		protected readonly ShapeFactory? shapeFactory;
		protected readonly IEnvironment arguments;
		protected readonly bool diagnostic;

		public string DisplayName => Pattern.Name;

		public MarkupShape(MarkupPattern pattern, ShapeFactory? shapeFactory, IEnvironment arguments, bool diagnostic) {
			this.Pattern = pattern;
			this.shapeFactory = shapeFactory;
			this.arguments = arguments;
			this.diagnostic = diagnostic;
		}

		protected virtual IEnvironment GetDrawableEnvironment() {
			return arguments;
		}

		protected DrawableDivElement? GetDrawableRoot(ISharpGraphicsState graphicsState) {
			return Pattern.rootElement.GetDrawable(graphicsState.GetMarkupData(), GetDrawableEnvironment(), shapeFactory, diagnostic);
		}

		public virtual void Draw(ISharpCanvas canvas, Rectangle rect) {
			GetDrawableRoot(canvas)?.Draw(canvas, rect, default);
		}

	}

	public abstract class MarkupAreaShape : MarkupShape, IAreaShape {

		public float Aspect { get; }

		public MarkupAreaShape(MarkupPattern pattern, ShapeFactory? shapeFactory, IEnvironment arguments, bool constructionLines, float aspect) : base(pattern, shapeFactory, arguments, constructionLines) {
			this.Aspect = aspect;
		}

		public Rectangle AspectRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			return rect.Aspect(Aspect);
		}

		public sealed override void Draw(ISharpCanvas canvas, Rectangle rect) {
			base.Draw(canvas, AspectRect(canvas, rect));
		}
	}

	#region IBox

	public class MarkupBoxPattern : MarkupAreaShapePattern<IBox> {

		protected override Type InstanceType { get; } = typeof(MarkupBox);

		public MarkupBoxPattern(
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
			) : base(library, name, description, arguments, validations, exampleSize, exampleCanvas, rootElement, source) { }

		protected override IBox ConstructInstance(IEnvironment argumentEnvironment, ShapeFactory.ShapeParams? shapeParams, ShapeFactory? shapeFactory, bool constructionLines) {
			ShapeFactory.AreaShapeParams areaShapeParams = shapeParams?.As<ShapeFactory.AreaShapeParams>() ?? new ShapeFactory.AreaShapeParams(-1f);
			return new MarkupBox(this, shapeFactory, argumentEnvironment, constructionLines, areaShapeParams.Aspect);
		}

	}

	public class MarkupBox : MarkupAreaShape, IBox {

		public MarkupBox(MarkupBoxPattern pattern, ShapeFactory? shapeFactory, IEnvironment arguments, bool constructionLines, float aspect) : base(pattern, shapeFactory, arguments, constructionLines, aspect) { }

		public Rectangle RemainingRect(ISharpGraphicsState graphicsState, Rectangle fullRect) {
			return GetDrawableRoot(graphicsState)?.GetNamedArea("remaining", graphicsState, AspectRect(graphicsState, fullRect)) ?? throw new MissingAreaException("Could not get area \"remaining\"");
		}

		public Rectangle FullRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			return GetDrawableRoot(graphicsState)?.GetFullFromNamedArea("remaining", graphicsState, rect) ?? throw new MissingAreaException("Could not get area \"remaining\"");
		}

	}

	#endregion

	#region ILabelledBox

	public class MarkupLabelledBoxPattern : MarkupAreaShapePattern<ILabelledBox> {

		protected override Type InstanceType { get; } = typeof(MarkupLabelledBox);

		public MarkupLabelledBoxPattern(
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
			) : base(library, name, description, arguments, validations, exampleSize, exampleCanvas, rootElement, source) { }

		protected override ILabelledBox ConstructInstance(IEnvironment argumentEnvironment, ShapeFactory.ShapeParams? shapeParams, ShapeFactory? shapeFactory, bool constructionLines) {
			ShapeFactory.AreaShapeParams areaShapeParams = shapeParams?.As<ShapeFactory.AreaShapeParams>() ?? new ShapeFactory.AreaShapeParams(-1f);
			return new MarkupLabelledBox(this, shapeFactory, argumentEnvironment, constructionLines, areaShapeParams.Aspect);
		}

	}

	public class MarkupLabelledBox : MarkupAreaShape, ILabelledBox {

		public MarkupLabelledBox(MarkupLabelledBoxPattern style, ShapeFactory? shapeFactory, IEnvironment arguments, bool diagnostic, float aspect) : base(style, shapeFactory, arguments, diagnostic, aspect) { }

		public Rectangle LabelRect(ISharpGraphicsState graphicsState, Rectangle fullRect) {
			return GetDrawableRoot(graphicsState)?.GetNamedArea("label", graphicsState, AspectRect(graphicsState, fullRect)) ?? throw new MissingAreaException("Could not get area \"label\"");
		}

		public Rectangle RemainingRect(ISharpGraphicsState graphicsState, Rectangle fullRect) {
			return GetDrawableRoot(graphicsState)?.GetNamedArea("remaining", graphicsState, AspectRect(graphicsState, fullRect)) ?? throw new MissingAreaException("Could not get area \"remaining\"");
		}

		public Rectangle FullRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			return GetDrawableRoot(graphicsState)?.GetFullFromNamedArea("remaining", graphicsState, rect) ?? throw new MissingAreaException("Could not get area \"remaining\"");
		}

	}

	#endregion

	#region ITitleStyledBox

	public class MarkupTitleStyledBoxPattern : MarkupShapePattern<ITitleStyledBox> {

		protected override Type InstanceType { get; } = typeof(MarkupTitleStyledBox);

		public MarkupTitleStyledBoxPattern(
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
			) : base(library, name, description, arguments, validations, exampleSize, exampleCanvas, rootElement, source) { }

		private ShapeFactory.TitleStyleParams ResolveParams(ShapeFactory.ShapeParams? shapeParams) {
			return shapeParams?.As<ShapeFactory.TitleStyleParams>() ?? new ShapeFactory.TitleStyleParams(new NoOutline(-1f), Name);
		}

		protected override ITitleStyledBox ConstructInstance(IEnvironment argumentEnvironment, ShapeFactory.ShapeParams? shapeParams, ShapeFactory? shapeFactory, bool constructionLines) {
			return new MarkupTitleStyledBox(this, shapeFactory, argumentEnvironment, constructionLines);
		}

		protected override IEnumerable<(EvaluationValue value, EnvironmentVariableInfo info)> GetAdditionalArguments(IContext context, ShapeFactory.ShapeParams? shapeParams, DirectoryPath source, WidgetFactory widgetFactory, ShapeFactory? shapeFactory) {
			IEnumerable<(EvaluationValue, EnvironmentVariableInfo info)> baseArgs = base.GetAdditionalArguments(context, shapeParams, source, widgetFactory, shapeFactory);
			foreach ((EvaluationValue, EnvironmentVariableInfo info) baseArg in baseArgs) {
				yield return baseArg;
			}

			ShapeFactory.TitleStyleParams titleStyleParams = ResolveParams(shapeParams);

			yield return PatternData.ShapeNameVariable(Context, titleStyleParams.Name);
			yield return PatternData.ShapePartsVariable(Context, titleStyleParams.Name.SplitAndTrim('\n'));

			yield return PatternData.TitleStyledBoxVariable(Context, titleStyleParams.Box);

			foreach ((ArgumentDetails arg, EnvironmentVariableInfo info) in PatternData.TitledShapeArgs(Context)) {
				EvaluationValue value = MakeArgumentValue(arg, info.EvaluationType, context, source, widgetFactory, shapeFactory);
				yield return (value, info);
			}
		}

		protected override ArgumentDetails[] GetAdditionalArgumentDetails() {
			return PatternData.TitleStyleBuilderArgs;
		}

	}

	public class MarkupTitleStyledBox : MarkupAreaShape, ITitleStyledBox {

		public MarkupTitleStyledBox(MarkupTitleStyledBoxPattern style, ShapeFactory? shapeFactory, IEnvironment arguments, bool diagnostic) : base(style, shapeFactory, arguments, diagnostic, -1f) { }

		public Rectangle RemainingRect(ISharpGraphicsState graphicsState, Rectangle fullRect) {
			return GetDrawableRoot(graphicsState)?.GetNamedArea("remaining", graphicsState, AspectRect(graphicsState, fullRect)) ?? throw new MissingAreaException("Could not get area \"remaining\"");
		}

		public Rectangle FullRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			return GetDrawableRoot(graphicsState)?.GetFullFromNamedArea("remaining", graphicsState, rect) ?? throw new MissingAreaException("Could not get area \"remaining\"");
		}

	}

	#endregion

	#region ITitledBox

	public class MarkupTitledBoxPattern : MarkupAreaShapePattern<ITitledBox> {

		protected override Type InstanceType { get; } = typeof(MarkupTitledBox);

		public MarkupTitledBoxPattern(
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
			) : base(library, name, description, arguments, validations, exampleSize, exampleCanvas, rootElement, source) { }

		private ShapeFactory.TitledBoxParams ResolveParams(ShapeFactory.ShapeParams? shapeParams) {
			return shapeParams?.As<ShapeFactory.TitledBoxParams>() ?? new ShapeFactory.TitledBoxParams(-1f, Name);
		}

		protected override ITitledBox ConstructInstance(IEnvironment argumentEnvironment, ShapeFactory.ShapeParams? shapeParams, ShapeFactory? shapeFactory, bool constructionLines) {
			ShapeFactory.TitledBoxParams titledBoxParams = ResolveParams(shapeParams);
			return new MarkupTitledBox(this, shapeFactory, argumentEnvironment, constructionLines, titledBoxParams.Aspect);
		}

		protected override IEnumerable<(EvaluationValue value, EnvironmentVariableInfo info)> GetAdditionalArguments(IContext context, ShapeFactory.ShapeParams? shapeParams, DirectoryPath source, WidgetFactory widgetFactory, ShapeFactory? shapeFactory) {
			IEnumerable<(EvaluationValue value, EnvironmentVariableInfo info)> baseArgs = base.GetAdditionalArguments(context, shapeParams, source, widgetFactory, shapeFactory);
			foreach ((EvaluationValue value, EnvironmentVariableInfo info) baseArg in baseArgs) {
				yield return baseArg;
			}

			ShapeFactory.TitledBoxParams titledBoxParams = ResolveParams(shapeParams);

			yield return PatternData.ShapeNameVariable(Context, titledBoxParams.Name);
			yield return PatternData.ShapePartsVariable(Context, titledBoxParams.Name.SplitAndTrim('\n'));

			foreach ((ArgumentDetails arg, EnvironmentVariableInfo info) in PatternData.TitledShapeArgs(Context)) {
				EvaluationValue value = MakeArgumentValue(arg, info.EvaluationType, context, source, widgetFactory, shapeFactory);
				yield return (value, info);
			}
		}

		protected override ArgumentDetails[] GetAdditionalArgumentDetails() {
			return PatternData.TitledShapeBuilderArgs;
		}

	}

	public class MarkupTitledBox : MarkupAreaShape, ITitledBox {

		public MarkupTitledBox(MarkupTitledBoxPattern style, ShapeFactory? shapeFactory, IEnvironment arguments, bool diagnostic, float aspect) : base(style, shapeFactory, arguments, diagnostic, aspect) { }

		public Rectangle RemainingRect(ISharpGraphicsState graphicsState, Rectangle fullRect) {
			return GetDrawableRoot(graphicsState)?.GetNamedArea("remaining", graphicsState, AspectRect(graphicsState, fullRect)) ?? throw new MissingAreaException("Could not get area \"remaining\"");
		}

		public Rectangle FullRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			return GetDrawableRoot(graphicsState)?.GetFullFromNamedArea("remaining", graphicsState, rect) ?? throw new MissingAreaException("Could not get area \"remaining\"");
		}

	}

	#endregion

	#region IEntriedShape

	public class MarkupEntriedShapePattern : MarkupAreaShapePattern<IEntriedShape> {

		protected override Type InstanceType { get; } = typeof(MarkupEntriedShape);

		public MarkupEntriedShapePattern(
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
			) : base(library, name, description, arguments, validations, exampleSize, exampleCanvas, rootElement, source) { }

		protected override IEntriedShape ConstructInstance(IEnvironment argumentEnvironment, ShapeFactory.ShapeParams? shapeParams, ShapeFactory? shapeFactory, bool constructionLines) {
			ShapeFactory.AreaShapeParams areaShapeParams = shapeParams?.As<ShapeFactory.AreaShapeParams>() ?? new ShapeFactory.AreaShapeParams(-1f);
			return new MarkupEntriedShape(this, shapeFactory, argumentEnvironment, constructionLines, areaShapeParams.Aspect);
		}

	}

	public class MarkupEntriedShape : MarkupAreaShape, IEntriedShape {

		public MarkupEntriedShape(MarkupEntriedShapePattern style, ShapeFactory? shapeFactory, IEnvironment arguments, bool diagnostic, float aspect) : base(style, shapeFactory, arguments, diagnostic, aspect) { }

		public int EntryCount(ISharpGraphicsState graphicsState, Rectangle fullRect) {
			DrawableDivElement? drawable = GetDrawableRoot(graphicsState);

			if (drawable is null) { return 0; }

			int maxEntry = 0;
			foreach (string areaName in drawable.GetAreas()) {
				if (areaName.StartsWith("entry")) {
					if (int.TryParse(areaName[5..], out int areaKey) && areaKey > 0) {
						maxEntry = areaKey;
					}
				}
			}

			return maxEntry;
		}

		public Rectangle EntryRect(ISharpGraphicsState graphicsState, int entryIndex, Rectangle fullRect) {
			string areaName = $"entry{entryIndex + 1}";
			return GetDrawableRoot(graphicsState)?.GetNamedArea(areaName, graphicsState, AspectRect(graphicsState, fullRect)) ?? throw new MissingAreaException($"Could not get area \"{areaName}\".");
		}

	}

	#endregion

	#region IBar

	public class MarkupBarPattern : MarkupAreaShapePattern<IBar> {

		protected override Type InstanceType { get; } = typeof(MarkupBar);

		public MarkupBarPattern(
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
			) : base(library, name, description, arguments, validations, exampleSize, exampleCanvas, rootElement, source) { }

		protected override IBar ConstructInstance(IEnvironment argumentEnvironment, ShapeFactory.ShapeParams? shapeParams, ShapeFactory? shapeFactory, bool constructionLines) {
			ShapeFactory.AreaShapeParams areaShapeParams = shapeParams?.As<ShapeFactory.AreaShapeParams>() ?? new ShapeFactory.AreaShapeParams(-1f);
			return new MarkupBar(this, shapeFactory, argumentEnvironment, constructionLines, areaShapeParams.Aspect);
		}

	}

	public class MarkupBar : MarkupAreaShape, IBar {

		public MarkupBar(MarkupBarPattern style, ShapeFactory? shapeFactory, IEnvironment arguments, bool diagnostic, float aspect) : base(style, shapeFactory, arguments, diagnostic, aspect) { }

		public Rectangle LabelRect(ISharpGraphicsState graphicsState, Rectangle fullRect) {
			return GetDrawableRoot(graphicsState)?.GetNamedArea("label", graphicsState, AspectRect(graphicsState, fullRect)) ?? throw new MissingAreaException("Could not get area \"label\"");
		}

		public Rectangle RemainingRect(ISharpGraphicsState graphicsState, Rectangle fullRect) {
			return GetDrawableRoot(graphicsState)?.GetNamedArea("remaining", graphicsState, AspectRect(graphicsState, fullRect)) ?? throw new MissingAreaException("Could not get area \"remaining\"");
		}
	}

	#endregion

	#region IUsageBar

	public class MarkupUsageBarPattern : MarkupAreaShapePattern<IUsageBar> {

		protected override Type InstanceType { get; } = typeof(MarkupUsageBar);

		public MarkupUsageBarPattern(
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
			) : base(library, name, description, arguments, validations, exampleSize, exampleCanvas, rootElement, source) { }

		protected override IUsageBar ConstructInstance(IEnvironment argumentEnvironment, ShapeFactory.ShapeParams? shapeParams, ShapeFactory? shapeFactory, bool constructionLines) {
			ShapeFactory.AreaShapeParams areaShapeParams = shapeParams?.As<ShapeFactory.AreaShapeParams>() ?? new ShapeFactory.AreaShapeParams(-1f);
			return new MarkupUsageBar(this, shapeFactory, argumentEnvironment, constructionLines, areaShapeParams.Aspect);
		}

	}

	public class MarkupUsageBar : MarkupAreaShape, IUsageBar {

		public MarkupUsageBar(MarkupUsageBarPattern style, ShapeFactory? shapeFactory, IEnvironment arguments, bool diagnostic, float aspect) : base(style, shapeFactory, arguments, diagnostic, aspect) { }

		public int EntryCount(ISharpGraphicsState graphicsState, Rectangle rect) => 2;

		public Rectangle EntryRect(ISharpGraphicsState graphicsState, int entryIndex, Rectangle rect) {
			if (entryIndex == 0) {
				return FirstEntryRect(graphicsState, rect);
			}
			else if (entryIndex == 1) {
				return SecondEntryRect(graphicsState, rect);
			}
			else {
				throw new ArgumentOutOfRangeException(nameof(entryIndex), "UsageBar shapes only provide two entries.");
			}
		}

		public Rectangle FirstEntryRect(ISharpGraphicsState graphicsState, Rectangle fullRect) {
			return GetDrawableRoot(graphicsState)?.GetNamedArea("entry1", graphicsState, AspectRect(graphicsState, fullRect)) ?? throw new MissingAreaException("Could not get area \"entry1\"");
		}

		public Rectangle SecondEntryRect(ISharpGraphicsState graphicsState, Rectangle fullRect) {
			return GetDrawableRoot(graphicsState)?.GetNamedArea("entry2", graphicsState, AspectRect(graphicsState, fullRect)) ?? throw new MissingAreaException("Could not get area \"entry2\"");
		}

		public Rectangle LabelRect(ISharpGraphicsState graphicsState, Rectangle fullRect) {
			return GetDrawableRoot(graphicsState)?.GetNamedArea("label", graphicsState, AspectRect(graphicsState, fullRect)) ?? throw new MissingAreaException("Could not get area \"label\"");
		}
	}

	#endregion

	#region IDetail

	public class MarkupDetailPattern : MarkupShapePattern<IDetail> {

		protected override Type InstanceType { get; } = typeof(MarkupDetail);

		public MarkupDetailPattern(
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
			) : base(library, name, description, arguments, validations, exampleSize, exampleCanvas, rootElement, source) { }

		protected override IDetail ConstructInstance(IEnvironment argumentEnvironment, ShapeFactory.ShapeParams? shapeParams, ShapeFactory? shapeFactory, bool constructionLines) {
			return new MarkupDetail(this, shapeFactory, argumentEnvironment, constructionLines);
		}

		protected override ArgumentDetails[] GetAdditionalArgumentDetails() {
			//return PatternData.DetailVariables;
			return Array.Empty<ArgumentDetails>();
		}

	}

	public class MarkupDetail : MarkupShape, IDetail {

		public LayoutDirection Layout { protected get; set; }

		public MarkupDetail(MarkupDetailPattern pattern, ShapeFactory? shapeFactory, IEnvironment arguments, bool constructionLines) : base(pattern, shapeFactory, arguments, constructionLines) { }

		protected override IEnvironment GetDrawableEnvironment() {
			return base.GetDrawableEnvironment().AppendEnvironment(new List<(object?, EnvironmentVariableInfo)>() {
				(Layout, PatternData.DetailLayoutVariable(Pattern.Context))
			});
		}

	}

	#endregion

	public class MissingAreaException : SharpSheetsException {
		public MissingAreaException(string message) : base(message) { }
		public MissingAreaException(string message, Exception innerException) : base(message, innerException) { }
	}

}