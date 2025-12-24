using SharpEditor.ContentBuilders;
using SharpSheets.Canvas;
using SharpSheets.Documentation;
using SharpSheets.Markup.Patterns;
using SharpSheets.Parsing;
using SharpSheets.Shapes;
using SharpSheets.Utilities;
using System;
using System.Collections.Generic;
using SharpSheets.Layouts;
using SharpSheets.Widgets;
using SharpEditor.DataManagers;
using System.Diagnostics.CodeAnalysis;
using static SharpEditor.ContentBuilders.BaseContentBuilder;
using static SharpEditor.Documentation.DocumentationBuilders.BaseDocumentationBuilder;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using SharpEditor.Designer;
using SharpEditor.Utilities;
using SharpEditor.Windows;
using SharpEditor.Designer.DrawingCanvas;

namespace SharpEditor.Documentation.DocumentationBuilders {

	// UIElement -> Control
	// FrameworkElement -> Control

	public static class BuilderPageBuilder {

		public static DocumentationPage GetBuilderPage(BuilderDetails builder, DocumentationWindow window, Func<BuilderDetails?>? refreshAction) {
			if (builder == null) {
				return MakeErrorPage("Invalid builder.");
			}

			return MakePage(GetBuilderPageContent(builder, window), builder.Name, () => GetBuilderPageContent(refreshAction?.Invoke(), window));
		}

		private static Control GetBuilderPageContent(BuilderDetails? builder, DocumentationWindow window) {
			if (builder == null) {
				return MakeErrorContent("Invalid builder.");
			}

			StackPanel stack = new StackPanel() { Orientation = Orientation.Vertical };

			TextBlock headerBlock = GetContentTextBlock(BuilderContentBuilder.MakeBuilderHeaderBlock(builder), TextBlockMargin);
			headerBlock.MakeFontSizeRelative(TextBlockClass.H3);

			if (builder is MarkupBuilderDetails markupBuilder) {
				Avalonia.Controls.Grid headerGrid = MakeExternalLinkHeader(headerBlock, "Open Pattern File...", out Button patternSourceButton, window);
				patternSourceButton.Click += delegate { SharpEditorWindow.Instance?.OpenEditorDocument(markupBuilder.Pattern.source.Path, true); };
				stack.Children.Add(headerGrid);
			}
			else {
				stack.Children.Add(headerBlock);
			}

			if (MakeDescriptionTextBlock(builder.Description, window) is TextBlock descriptionBlock) {
				//TextBlock descriptionBlock = BaseContentBuilder.GetContentTextBlock(constructor.Description, IndentedMargin);
				stack.Children.Add(descriptionBlock);
			}

			if (builder.DeclaringType.IsAssignableTo(typeof(IShape)) || builder.DeclaringType.IsAssignableTo(typeof(IWidget))) {
				Control? graphicElement = MakeExampleGraphic(builder);
				if (graphicElement != null) {
					stack.Children.Add(graphicElement);
				}
			}

			if (builder.Arguments.Length > 0) {
				stack.Children.Add(MakeSeparator());

				foreach (BuilderArgumentDetails arg in builder.BuilderArguments) {
					stack.Children.Add(MakeSingleArgumentElement(arg, window).SetMargin(ParagraphSpacingMargin));
				}
			}

			return stack;
		}

		public static Control MakeSingleArgumentElement(BuilderArgumentDetails argument, DocumentationWindow window) {
			StackPanel argPanel = new StackPanel() { Orientation = Orientation.Vertical };

			argPanel.Children.Add(MakeArgumentHeaderBlock(argument, window));

			if (MakeDescriptionTextBlock(argument.ArgumentDescription, window) is TextBlock desctionBlock) {
				//argPanel.Children.Add(BaseContentBuilder.GetContentTextBlock(argument.ArgumentDescription, ArgumentDetailsMargin));
				argPanel.Children.Add(desctionBlock);
			}

			if (argument.ArgumentType.DisplayType.IsEnum && SharpDocumentation.GetEnumDoc(argument.ArgumentType.DisplayType) is EnumDoc enumDoc) {
				argPanel.Children.Add(EnumContentBuilder.MakeEnumOptionsBlock(enumDoc, ArgumentDetailsMargin));
			}

			if (!argument.IsOptional || argument.UseLocal) {
				List<string> notes = new List<string>();
				if (!argument.IsOptional) { notes.Add("required"); }
				if (argument.UseLocal) { notes.Add("local"); }
				argPanel.Children.Add(GetContentTextBlock("(" + string.Join(", ", notes).ToTitleCase() + ")", ArgumentDetailsMargin));
			}

			return argPanel;
		}

		public static TextBlock MakeArgumentHeaderBlock(BuilderArgumentDetails argument, DocumentationWindow window) {
			TextBlock argumentBlock = GetContentTextBlock(TextBlockMargin);

			argumentBlock.Inlines?.Add(GetArgumentTypeInline(argument, window));

			argumentBlock.Inlines?.Add(new Run(SharpValueHandler.NO_BREAK_SPACE + argument.BuilderName) { Foreground = SharpEditorPalette.GetTypeBrush(argument.DeclaringType) });

			ArgumentDetails arg = argument.Argument;
			//while (arg != null && arg is PrefixedArgumentDetails prefixed) { arg = prefixed.Basis; } // What was this supposed to be doing?

			argumentBlock.Inlines?.Add(new Run("." + arg.Name) { });

			if (argument.Implied != null) {
				argumentBlock.Inlines?.Add(new Run("." + argument.Implied));
			}

			argumentBlock.Inlines?.AddRange(BuilderContentBuilder.GetArgumentDefaultInlines(argument.Argument, null));

			return argumentBlock;
		}

		public static Inline GetArgumentTypeInline(BuilderArgumentDetails argument, DocumentationWindow window) {
			if (EnumContentBuilder.IsEnum(argument.ArgumentType, out EnumDoc? enumDoc)) {
				ClickableRun enumClickable = new ClickableRun(SharpValueHandler.GetTypeName(argument.ArgumentType)) { Foreground = SharpEditorPalette.TypeBrush };
				enumClickable.MouseLeftButtonDown += window.MakeNavigationDelegate(enumDoc, null);
				return enumClickable;
			}
			else {
				return new Run(SharpValueHandler.GetTypeName(argument.ArgumentType)) { Foreground = SharpEditorPalette.TypeBrush };
			}
		}

		private static readonly float ExampleGraphicDefaultMargin = 0.1f;
		public static Control? MakeExampleGraphic(BuilderDetails builder) {
			if (!builder.DeclaringType.IsAssignableTo(typeof(IShape)) && !builder.DeclaringType.IsAssignableTo(typeof(IWidget))) {
				return null;
			}

			if (builder.Rect != null && (builder.Rect.Width <= 0 || builder.Rect.Height <= 0)) {
				// It has been indicated that no example should be drawn for this object
				return null;
			}

			try {
				SharpGeometryDrawingDocument exampleDocument = new SharpGeometryDrawingDocument();
				ISharpCanvas canvas;
				List<Rectangle> displayRects = new List<Rectangle>();

				SharpSheets.Layouts.Size GetPage(Rectangle shapeArea) {
					float margin = Math.Max(shapeArea.Width, shapeArea.Height) * ExampleGraphicDefaultMargin;
					return (SharpSheets.Layouts.Size)shapeArea.Margins(margin, true);
				}
				Rectangle GetShape(SharpSheets.Layouts.Size pageArea) {
					float margin = Math.Max(pageArea.Width, pageArea.Height) * (ExampleGraphicDefaultMargin / (1 + 2 * ExampleGraphicDefaultMargin));
					return new Rectangle(pageArea.Width, pageArea.Height).Margins(margin, false);
				}

				if (builder.DeclaringType.IsAssignableTo(typeof(IShape))) {
					IContext shapeContext = Context.Simple("example", new Dictionary<string, string>() { { "style", builder.FullName } }, new Dictionary<string, bool>());
					DirectoryPath source = new DirectoryPath(SharpEditorPathInfo.TemplateDirectory);
					string exampleName = builder.Name; // "NAME";

					IShape shape;
					if (builder.DeclaringType.IsSimple<BoxedTitle>()) {
						shape = new BoxedTitle(new Simple(-1), exampleName, new Rounded(-1), trim: new Margins(1f));
					}
					else if (builder.DeclaringType.IsSimple<TabTitle>()) {
						shape = new TabTitle(new Simple(-1), exampleName, new Rounded(-1), trim: new Margins(1f), includeProtrusion: true);
					}
					else if (builder.DeclaringType.IsAssignableTo(typeof(ITitleStyledBox))) {
						shape = SharpEditorRegistries.ShapeFactoryInstance.MakeTitleStyle(shapeContext, new Simple(-1, dashes: new float[] { 3f, 3f }, stroke: SharpSheets.Colors.Color.Black), exampleName, source, out _);
					}
					else if (builder.DisplayType.GetSingle() is Type builderSystemType) {
						shape = SharpEditorRegistries.ShapeFactoryInstance.MakeExample(builderSystemType, builder.FullName, source, out _); // .MakeShape(constructor.DisplayType, shapeContext, exampleName, source);
					}
					else {
						shape = ShapeFactory.MakeDefault_IBox();
					}

					SharpSheets.Layouts.Size pageSize;
					Rectangle shapeRect;

					if (shape is IMarkupObject markupObject && (markupObject.Pattern.exampleCanvas != null || markupObject.Pattern.exampleRect != null)) {
						if (markupObject.Pattern.exampleCanvas != null && markupObject.Pattern.exampleRect != null) {
							pageSize = markupObject.Pattern.exampleCanvas;
							shapeRect = markupObject.Pattern.exampleRect;
						}
						else if (markupObject.Pattern.exampleCanvas != null) {
							pageSize = markupObject.Pattern.exampleCanvas;
							shapeRect = GetShape(pageSize);
						}
						else { // markupObject.Pattern.exampleRect != null
							pageSize = GetPage(markupObject.Pattern.exampleRect!);
							shapeRect = GetShape(pageSize);
						}
					}
					else if (builder.Canvas != null || builder.Rect != null) {
						if (builder.Canvas != null && builder.Rect != null) {
							pageSize = builder.Canvas;
							shapeRect = builder.Rect;
						}
						else if (builder.Canvas != null) {
							pageSize = builder.Canvas;
							shapeRect = GetShape(pageSize);
						}
						else { // constructor.Rect != null
							pageSize = GetPage(builder.Rect!);
							shapeRect = GetShape(pageSize);
						}
					}
					else if (builder.DeclaringType.IsAssignableTo(typeof(IBar)) || builder.DeclaringType.IsAssignableTo(typeof(IUsageBar))) {
						pageSize = GetPage(new Rectangle(140, 30));
						shapeRect = GetShape(pageSize);
					}
					else if (builder.DeclaringType.IsAssignableTo(typeof(IDetail))) {
						pageSize = GetPage(new Rectangle(90, 20));
						shapeRect = GetShape(pageSize);
					}
					else {
						pageSize = GetPage(new Rectangle(110, 90));
						shapeRect = GetShape(pageSize);
					}

					canvas = exampleDocument.AddNewPage(pageSize);

					if (shape is IDetail detail) {
						// TODO This needs improving so we can see vertical version too
						detail.Draw(canvas, shapeRect, LayoutDirection.ROWS);
					}
					else if (shape is IDrawRectShape drawShape) {
						drawShape.Draw(canvas, shapeRect);
					}

					if (shape is IFramedArea framed) {
						displayRects.Add(framed.RemainingRect(canvas, shapeRect));
					}
					if (shape is ILabelledArea labelled) {
						displayRects.Add(labelled.LabelRect(canvas, shapeRect));
					}
					if (shape is IEntriedArea entried) {
						displayRects.AddRange(entried.EntryRects(canvas, shapeRect));
					}
				}
				else if (builder.DeclaringType.IsAssignableTo(typeof(IWidget))) {
					//IContext context = Context.Simple("example", new Dictionary<string, string>(), new Dictionary<string, bool>());
					DirectoryPath source = new DirectoryPath(SharpEditorPathInfo.TemplateDirectory);

					IWidget widget = SharpEditorRegistries.WidgetFactoryInstance.MakeExample(builder.FullName, source, false, out List<SharpSheets.Exceptions.SharpParsingException> errors);
					//IWidget widget = SharpEditorRegistries.WidgetFactoryInstance.MakeWidget(constructor.Name, context, source, out List<SharpSheets.Exceptions.SharpParsingException> errors);

					SharpSheets.Layouts.Size pageSize;
					Rectangle widgetRect;

					if (widget is IMarkupObject markupObject && (markupObject.Pattern.exampleCanvas != null || markupObject.Pattern.exampleRect != null)) {
						if (markupObject.Pattern.exampleCanvas != null && markupObject.Pattern.exampleRect != null) {
							pageSize = markupObject.Pattern.exampleCanvas;
							widgetRect = markupObject.Pattern.exampleRect;
						}
						else if (markupObject.Pattern.exampleCanvas != null) {
							pageSize = markupObject.Pattern.exampleCanvas;
							widgetRect = GetShape(pageSize);
						}
						else { // markupObject.Pattern.exampleRect != null
							pageSize = GetPage(markupObject.Pattern.exampleRect!);
							widgetRect = GetShape(pageSize);
						}
					}
					else if (builder.Canvas != null || builder.Rect != null) {
						if (builder.Canvas != null && builder.Rect != null) {
							pageSize = builder.Canvas;
							widgetRect = builder.Rect;
						}
						else if (builder.Canvas != null) {
							pageSize = builder.Canvas;
							widgetRect = GetShape(pageSize);
						}
						else { // constructor.Rect != null
							pageSize = GetPage(builder.Rect!);
							widgetRect = GetShape(pageSize);
						}
					}
					else {
						pageSize = GetPage(new Rectangle(110, 90));
						widgetRect = GetShape(pageSize);
					}

					canvas = exampleDocument.AddNewPage(pageSize);

					widget.Draw(canvas, widgetRect, default);

					/*
					if (widget.ContainerArea(canvas, widgetRect) is SharpSheets.Layouts.Rectangle containerArea) {
						displayRects.Add(containerArea);
					}
					*/
					if (widget.RemainingRect(canvas, widgetRect) is Rectangle remainingArea) {
						displayRects.Add(remainingArea);
					}
					// More rects?
				}
				else {
					return null;
				}

				if (displayRects != null && displayRects.Count > 0) {
					canvas.SetStrokeDash(new StrokeDash(new float[] { 5.0f, 10.0f }, 0f))
						.SetStrokeColor(SharpSheets.Colors.Color.Gray.WithOpacity(0.6f))
						.SetLineWidth(canvas.GetLineWidth() / 2f);
					foreach (Rectangle displayRect in displayRects) {
						if (displayRect != null) {
							canvas.Rectangle(displayRect).Stroke();
						}
					}
				}

				SharpGeometryDrawingCanvas page = exampleDocument.Pages[0];

				DrawingElement element = new DrawingElement(page.drawingGroup.BuildGroup()) {
					//LayoutTransform = TestBlock.LayoutTransform,
					Width = page.CanvasRect.Width,
					Height = page.CanvasRect.Height
				};

				LayoutTransformControl layoutTransformControl = new LayoutTransformControl() {
					Child = element,
					LayoutTransform = GetLayoutTransform(40, 40, 145, 145, element.Width, element.Height)
				};

				return new Border() { Child = layoutTransformControl, Margin = CanvasMargin, HorizontalAlignment = HorizontalAlignment.Center };
			}
			catch (Exception e) {
				Console.WriteLine(e.Message);
				Console.WriteLine(e.StackTrace);
				return null;
			}
		}

		private static ScaleTransform GetLayoutTransform(double minX, double minY, double maxX, double maxY, double width, double height) {
			if (width >= minX && width <= maxX && height >= minY && height <= maxY) {
				return new ScaleTransform(3, 3);
			}

			double scale = 1;

			if (width > maxX || height > maxY) {
				scale = Math.Min(maxX / width, maxY / height);
			}
			else if (scale <= 1 && (width < minX || height < minY)) {
				scale = Math.Max(minX / width, minY / height);
			}

			return new ScaleTransform(3 * scale, 3 * scale);
		}

	}

}
