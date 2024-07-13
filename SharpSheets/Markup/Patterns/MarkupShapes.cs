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

		public sealed override MarkupConstructorDetails GetConstructorDetails() {
			return new MarkupConstructorDetails(this, typeof(T), InstanceType, GetArgumentDetails().ToArray(), Description is not null ? new DocumentationString(Description) : null);
		}

		protected virtual IEnumerable<(object? value, EnvironmentVariableInfo info)> GetAdditionalArguments(IContext context, string name, float aspect, DirectoryPath source, WidgetFactory widgetFactory, ShapeFactory? shapeFactory) {
			return Enumerable.Empty<(object? value, EnvironmentVariableInfo info)>();
		}

		protected static object? MakeArgumentValue(string name, Type type, bool useLocal, bool isOptional, object? defaultValue, IContext context, DirectoryPath source, WidgetFactory widgetFactory, ShapeFactory? shapeFactory) {
			object? value = SharpFactory.CreateParameter(name, type, useLocal, isOptional, defaultValue, context, source, widgetFactory, shapeFactory, out _, out _); // TODO Should we pass these arguments up the chain somehow?
			/*
			if (type.IsEnum) {
				value = value.ToString();
			}
			*/
			return value;
		}

		protected abstract T ConstructInstance(IEnvironment argumentEnvironment, float aspect, ShapeFactory? shapeFactory, bool constructionLines);

		public T MakeShape(IContext? context, string name, float aspect, DirectoryPath source, ShapeFactory? shapeFactory, bool constructionLines, out SharpParsingException[] buildErrors) {
			WidgetFactory dummyWidgetFactory = new WidgetFactory(MarkupRegistry.Empty, shapeFactory);

			IEnvironment argumentEnvironment = ParseArguments(context ?? Context.Empty, source, null, shapeFactory, context == null, out buildErrors)
				.AppendEnvironment(SimpleEnvironments.Create(GetAdditionalArguments(context ?? Context.Empty, name ?? "NAME", aspect, source, dummyWidgetFactory, shapeFactory)));

			return ConstructInstance(argumentEnvironment, aspect, shapeFactory, constructionLines);
		}

		public override object MakeExample(WidgetFactory? widgetFactory, ShapeFactory? shapeFactory, bool diagnostic, out SharpParsingException[] buildErrors) {
			return MakeShape(null, Name ?? "NAME", -1f, sourceDirectory, shapeFactory, diagnostic, out buildErrors);
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

		protected override IEnumerable<(object? value, EnvironmentVariableInfo info)> GetAdditionalArguments(IContext context, string name, float aspect, DirectoryPath source, WidgetFactory widgetFactory, ShapeFactory? shapeFactory) {
			return base.GetAdditionalArguments(context, name, aspect, source, widgetFactory, shapeFactory)
				.Append((aspect, PatternData.AreaShapeAspectVariable));
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

		protected override IBox ConstructInstance(IEnvironment argumentEnvironment, float aspect, ShapeFactory? shapeFactory, bool constructionLines) {
			return new MarkupBox(this, shapeFactory, argumentEnvironment, constructionLines, aspect);
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

		protected override ILabelledBox ConstructInstance(IEnvironment argumentEnvironment, float aspect, ShapeFactory? shapeFactory, bool constructionLines) {
			return new MarkupLabelledBox(this, shapeFactory, argumentEnvironment, constructionLines, aspect);
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

		protected override ITitledBox ConstructInstance(IEnvironment argumentEnvironment, float aspect, ShapeFactory? shapeFactory, bool constructionLines) {
			return new MarkupTitledBox(this, shapeFactory, argumentEnvironment, constructionLines, aspect);
		}

		protected override IEnumerable<(object? value, EnvironmentVariableInfo info)> GetAdditionalArguments(IContext context, string name, float aspect, DirectoryPath source, WidgetFactory widgetFactory, ShapeFactory? shapeFactory) {
			IEnumerable<(object? value, EnvironmentVariableInfo info)> baseArgs = base.GetAdditionalArguments(context, name, aspect, source, widgetFactory, shapeFactory);
			foreach ((object? value, EnvironmentVariableInfo info) baseArg in baseArgs) {
				yield return baseArg;
			}

			yield return (name, PatternData.ShapeNameVariable);
			yield return (name.SplitAndTrim('\n'), PatternData.ShapePartsVariable);

			foreach ((ArgumentDetails arg, EnvironmentVariableInfo info) in PatternData.TitledShapeArgs) {
				object? value = MakeArgumentValue(arg.Name, arg.Type.DataType, arg.UseLocal, arg.IsOptional, arg.DefaultValue, context, source, widgetFactory, shapeFactory);
				yield return (value, info);
			}
		}

		protected override ArgumentDetails[] GetAdditionalArgumentDetails() {
			return PatternData.TitledShapeConstructorArgs;
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

		protected override IEntriedShape ConstructInstance(IEnvironment argumentEnvironment, float aspect, ShapeFactory? shapeFactory, bool constructionLines) {
			return new MarkupEntriedShape(this, shapeFactory, argumentEnvironment, constructionLines, aspect);
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

		protected override IBar ConstructInstance(IEnvironment argumentEnvironment, float aspect, ShapeFactory? shapeFactory, bool constructionLines) {
			return new MarkupBar(this, shapeFactory, argumentEnvironment, constructionLines, aspect);
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

		protected override IUsageBar ConstructInstance(IEnvironment argumentEnvironment, float aspect, ShapeFactory? shapeFactory, bool constructionLines) {
			return new MarkupUsageBar(this, shapeFactory, argumentEnvironment, constructionLines, aspect);
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

		protected override IDetail ConstructInstance(IEnvironment argumentEnvironment, float aspect, ShapeFactory? shapeFactory, bool constructionLines) {
			return new MarkupDetail(this, shapeFactory, argumentEnvironment, constructionLines);
		}

		protected override ArgumentDetails[] GetAdditionalArgumentDetails() {
			//return PatternData.DetailVariables;
			return Array.Empty<ArgumentDetails>();
		}

	}

	public class MarkupDetail : MarkupShape, IDetail {

		public Layout Layout { protected get; set; }

		public MarkupDetail(MarkupDetailPattern pattern, ShapeFactory? shapeFactory, IEnvironment arguments, bool constructionLines) : base(pattern, shapeFactory, arguments, constructionLines) { }

		protected override IEnvironment GetDrawableEnvironment() {
			return base.GetDrawableEnvironment().AppendEnvironment(SimpleEnvironments.Create(new List<(object?, EnvironmentVariableInfo)>() {
				(Layout, PatternData.DetailLayoutVariable)
			}));
		}

	}

	#endregion

	public class MissingAreaException : SharpSheetsException {
		public MissingAreaException(string message) : base(message) { }
		public MissingAreaException(string message, Exception innerException) : base(message, innerException) { }
	}

}