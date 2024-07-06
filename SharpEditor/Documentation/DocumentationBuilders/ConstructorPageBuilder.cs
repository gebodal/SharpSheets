using SharpEditor.ContentBuilders;
using SharpSheets.Canvas;
using SharpSheets.Documentation;
using SharpSheets.Markup.Patterns;
using SharpSheets.Parsing;
using SharpSheets.Shapes;
using SharpSheets.Utilities;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using SharpSheets.Layouts;
using SharpSheets.Widgets;
using SharpEditor.DataManagers;
using System.Diagnostics.CodeAnalysis;
using static SharpEditor.ContentBuilders.BaseContentBuilder;
using static SharpEditor.Documentation.DocumentationBuilders.BaseDocumentationBuilder;

namespace SharpEditor.Documentation.DocumentationBuilders {

	public static class ConstructorPageBuilder {

		public static DocumentationPage GetConstructorPage(ConstructorDetails constructor, DocumentationWindow window, Func<ConstructorDetails?>? refreshAction) {
			if (constructor == null) {
				return MakeErrorPage("Invalid constructor.");
			}

			return MakePage(GetConstructorPageContent(constructor, window), constructor.Name, () => GetConstructorPageContent(refreshAction?.Invoke(), window));
		}

		private static UIElement GetConstructorPageContent(ConstructorDetails? constructor, DocumentationWindow window) {
			if (constructor == null) {
				return MakeErrorContent("Invalid constructor.");
			}

			StackPanel stack = new StackPanel() { Orientation = Orientation.Vertical };

			TextBlock headerBlock = GetContentTextBlock(ConstructorContentBuilder.MakeConstructorHeaderBlock(constructor), TextBlockMargin);
			headerBlock.MakeFontSizeRelative(2.0);

			if (constructor is MarkupConstructorDetails markupConstructor) {
				System.Windows.Controls.Grid headerGrid = MakeExternalLinkHeader(headerBlock, "Open Pattern File...", out Button patternSourceButton, window);
				patternSourceButton.Click += delegate { SharpEditorWindow.Instance?.OpenEditorDocument(markupConstructor.Pattern.source.Path, true); };
				stack.Children.Add(headerGrid);
			}
			else {
				stack.Children.Add(headerBlock);
			}

			if (MakeDescriptionTextBlock(constructor.Description, window) is TextBlock descriptionBlock) {
				//TextBlock descriptionBlock = BaseContentBuilder.GetContentTextBlock(constructor.Description, IndentedMargin);
				stack.Children.Add(descriptionBlock);
			}

			if (typeof(IShape).IsAssignableFrom(constructor.DeclaringType) || typeof(IWidget).IsAssignableFrom(constructor.DeclaringType)) {
				FrameworkElement? graphicElement = MakeExampleGraphic(constructor);
				if (graphicElement != null) {
					stack.Children.Add(graphicElement);
				}
			}

			if (constructor.Arguments.Length > 0) {
				stack.Children.Add(MakeSeparator());

				foreach (ConstructorArgumentDetails arg in constructor.ConstructorArguments) {
					stack.Children.Add(MakeSingleArgumentElement(arg, window).SetMargin(ParagraphSpacingMargin));
				}
			}

			return stack;
		}

		public static FrameworkElement MakeSingleArgumentElement(ConstructorArgumentDetails argument, DocumentationWindow window) {
			StackPanel argPanel = new StackPanel() { Orientation = Orientation.Vertical };

			argPanel.Children.Add(MakeArgumentHeaderBlock(argument, window));

			if (MakeDescriptionTextBlock(argument.ArgumentDescription, window) is TextBlock desctionBlock) {
				//argPanel.Children.Add(BaseContentBuilder.GetContentTextBlock(argument.ArgumentDescription, ArgumentDetailsMargin));
				argPanel.Children.Add(desctionBlock);
			}

			if (argument.ArgumentType.IsEnum && SharpDocumentation.GetEnumDoc(argument.ArgumentType.DisplayType) is EnumDoc enumDoc) {
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

		public static TextBlock MakeArgumentHeaderBlock(ConstructorArgumentDetails argument, DocumentationWindow window) {
			TextBlock argumentBlock = GetContentTextBlock(TextBlockMargin);

			argumentBlock.Inlines.Add(GetArgumentTypeInline(argument, window));

			argumentBlock.Inlines.Add(new Run(SharpValueHandler.NO_BREAK_SPACE + argument.ConstructorName) { Foreground = SharpEditorPalette.GetTypeBrush(argument.DeclaringType) });

			ArgumentDetails arg = argument.Argument;
			//while (arg != null && arg is PrefixedArgumentDetails prefixed) { arg = prefixed.Basis; } // What was this supposed to be doing?

			argumentBlock.Inlines.Add(new Run("." + arg.Name) { });

			if (argument.Implied != null) {
				argumentBlock.Inlines.Add(new Run("." + argument.Implied));
			}

			argumentBlock.Inlines.AddRange(ConstructorContentBuilder.GetArgumentDefaultInlines(argument.Argument, null));

			return argumentBlock;
		}

		public static Inline GetArgumentTypeInline(ConstructorArgumentDetails argument, DocumentationWindow window) {
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
		public static FrameworkElement? MakeExampleGraphic(ConstructorDetails constructor) {
			if (!typeof(IShape).IsAssignableFrom(constructor.DeclaringType) && !typeof(IWidget).IsAssignableFrom(constructor.DeclaringType)) {
				return null;
			}

			if (constructor.Rect != null && (constructor.Rect.Width <= 0 || constructor.Rect.Height <= 0)) {
				// It has been indicated that no example should be drawn for this object
				return null;
			}

			try {
				SharpWPFDrawingDocument wpfDocument = new SharpWPFDrawingDocument();
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

				if (typeof(IShape).IsAssignableFrom(constructor.DeclaringType)) {
					IContext shapeContext = Context.Simple("example", new Dictionary<string, string>() { { "style", constructor.FullName } }, new Dictionary<string, bool>());
					DirectoryPath source = new DirectoryPath(SharpEditorPathInfo.TemplateDirectory);
					string exampleName = "NAME";

					/*
					IShape shape;
					if (constructor.DeclaringType == typeof(BoxedTitle)) {
						shape = new BoxedTitle(new Simple(-1), exampleName, new Rounded(-1));
					}
					else if (typeof(ITitleStyledBox).IsAssignableFrom(constructor.DeclaringType)) {
						shape = SharpEditorRegistries.ShapeFactoryInstance.MakeTitleStyle(shapeContext, new Simple(-1, dashes: new float[] { 3f, 3f }, stroke: SharpSheets.Colors.Color.Black), exampleName, source);
					}
					else {
						shape = SharpEditorRegistries.ShapeFactoryInstance.MakeShape(constructor.DisplayType, shapeContext, exampleName, source);
					}
					*/

					IShape shape;
					if (constructor.DeclaringType == typeof(BoxedTitle)) {
						shape = new BoxedTitle(new Simple(-1), exampleName, new Rounded(-1), trim: new Margins(1f));
					}
					else if (constructor.DeclaringType == typeof(TabTitle)) {
						shape = new TabTitle(new Simple(-1), exampleName, new Rounded(-1), trim: new Margins(1f), includeProtrusion: true);
					}
					else if (typeof(ITitleStyledBox).IsAssignableFrom(constructor.DeclaringType)) {
						shape = SharpEditorRegistries.ShapeFactoryInstance.MakeTitleStyle(shapeContext, new Simple(-1, dashes: new float[] { 3f, 3f }, stroke: SharpSheets.Colors.Color.Black), exampleName, source, out _);
					}
					else {
						shape = SharpEditorRegistries.ShapeFactoryInstance.MakeExample(constructor.DisplayType, constructor.FullName, source, out _); // .MakeShape(constructor.DisplayType, shapeContext, exampleName, source);
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
					else if (constructor.Canvas != null || constructor.Rect != null) {
						if (constructor.Canvas != null && constructor.Rect != null) {
							pageSize = constructor.Canvas;
							shapeRect = constructor.Rect;
						}
						else if (constructor.Canvas != null) {
							pageSize = constructor.Canvas;
							shapeRect = GetShape(pageSize);
						}
						else { // constructor.Rect != null
							pageSize = GetPage(constructor.Rect!);
							shapeRect = GetShape(pageSize);
						}
					}
					else if (typeof(IBar).IsAssignableFrom(constructor.DeclaringType) || typeof(IUsageBar).IsAssignableFrom(constructor.DeclaringType)) {
						pageSize = GetPage(new Rectangle(140, 30));
						shapeRect = GetShape(pageSize);
					}
					else if (typeof(IDetail).IsAssignableFrom(constructor.DeclaringType)) {
						pageSize = GetPage(new Rectangle(90, 20));
						shapeRect = GetShape(pageSize);
					}
					else {
						pageSize = GetPage(new Rectangle(110, 90));
						shapeRect = GetShape(pageSize);
					}

					canvas = wpfDocument.AddNewPage(pageSize);

					if (shape is IDetail detail) {
						// TODO This needs improving so we can see vertical version too
						detail.Layout = Layout.ROWS;
						detail.Draw(canvas, shapeRect);
					}
					else {
						shape.Draw(canvas, shapeRect);
					}

					if (shape is IFramedArea framed) {
						displayRects.Add(framed.RemainingRect(canvas, shapeRect));
					}
					if (shape is ILabelledArea labelled) {
						displayRects.Add(labelled.LabelRect(canvas, shapeRect));
					}
					if (shape is IEntriedArea entried) {
						for (int entryIdx = 0; entryIdx < entried.EntryCount; entryIdx++) {
							displayRects.Add(entried.EntryRect(canvas, entryIdx, shapeRect));
						}
					}
				}
				else if (typeof(IWidget).IsAssignableFrom(constructor.DeclaringType)) {
					//IContext context = Context.Simple("example", new Dictionary<string, string>(), new Dictionary<string, bool>());
					DirectoryPath source = new DirectoryPath(SharpEditorPathInfo.TemplateDirectory);

					IWidget widget = SharpEditorRegistries.WidgetFactoryInstance.MakeExample(constructor.FullName, source, false, out List<SharpSheets.Exceptions.SharpParsingException> errors);
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
					else if (constructor.Canvas != null || constructor.Rect != null) {
						if (constructor.Canvas != null && constructor.Rect != null) {
							pageSize = constructor.Canvas;
							widgetRect = constructor.Rect;
						}
						else if (constructor.Canvas != null) {
							pageSize = constructor.Canvas;
							widgetRect = GetShape(pageSize);
						}
						else { // constructor.Rect != null
							pageSize = GetPage(constructor.Rect!);
							widgetRect = GetShape(pageSize);
						}
					}
					else {
						pageSize = GetPage(new Rectangle(110, 90));
						widgetRect = GetShape(pageSize);
					}

					canvas = wpfDocument.AddNewPage(pageSize);

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

				SharpWPFDrawingCanvas page = wpfDocument.Pages[0];

				DrawingElement element = new DrawingElement(page.drawingGroup) {
					//LayoutTransform = TestBlock.LayoutTransform,
					Width = page.CanvasRect.Width,
					Height = page.CanvasRect.Height
				};

				element.LayoutTransform = GetLayoutTransform(40, 40, 145, 145, element.Width, element.Height);

				return new Border() { Child = element, Margin = CanvasMargin };
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
