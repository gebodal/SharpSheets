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
using SharpSheets.Cards.Definitions;
using SharpSheets.Cards.CardConfigs;
using SharpSheets.Layouts;
using SharpSheets.Widgets;
using System.Windows.Media.Imaging;
using SharpEditor.DataManagers;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Media.TextFormatting;
using static SharpEditor.ContentBuilders.BaseContentBuilder;

namespace SharpEditor.Documentation {

	public static class DocumentationBuilder {

		public static Thickness TextBlockMargin { get; } = new Thickness(7, 4, 7, 4);
		public static Thickness IndentedMargin { get; } = new Thickness(7 + 15, 4, 7, 4);
		public static Thickness ArgumentDetailsMargin { get; } = new Thickness(7 + 15 + 10, 4, 7, 4);

		public static Thickness TitleMargin { get; } = new Thickness(7, 4, 7, 10);
		public static Thickness SectionMargin { get; } = new Thickness(0, 0, 0, 30);
		public static Thickness ParagraphMargin { get; } = new Thickness(7, 4, 7, 10);
		public static Thickness ParagraphSpacingMargin { get; } = new Thickness(0, 0, 0, 10);

		public static Thickness ContentsBlockMargin { get; } = new Thickness(30, 0, 0, 0);
		public static Thickness CanvasMargin { get; } = new Thickness(7, 20, 7, 20);

		public static DocumentationPage1 MakePage(UIElement content, string name, DocumentationRefresh? refreshAction) {
			DocumentationPage1 page = new DocumentationPage1() {
				RefreshAction = refreshAction,
				Title = name,
				Foreground = Brushes.White,
				FontSize = 14 };
			page.SetPageContent(content);
			return page;
		}

		public static DocumentationPage1 MakeErrorPage(string message) {
			return MakePage(MakeErrorContent(message), "Error", null);
		}

		private static UIElement MakeErrorContent(string message) {
			return new TextBlock() { Text = message };
		}

		public static DocumentationPage1 GetConstructorPage(ConstructorDetails constructor, DocumentationWindow window, Func<ConstructorDetails?>? refreshAction) {
			if(constructor == null) {
				return MakeErrorPage("Invalid constructor.");
			}

			return MakePage(GetConstructorPageContent(constructor, window), constructor.Name, () => GetConstructorPageContent(refreshAction?.Invoke(), window));
		}

		private static UIElement GetConstructorPageContent(ConstructorDetails? constructor, DocumentationWindow window) {
			if (constructor == null) {
				return MakeErrorContent("Invalid constructor.");
			}

			StackPanel stack = new StackPanel() { Orientation = Orientation.Vertical };

			TextBlock headerBlock = BaseContentBuilder.GetContentTextBlock(ConstructorContentBuilder.MakeConstructorHeaderBlock(constructor), TextBlockMargin);
			BaseContentBuilder.MakeFontSizeRelative(headerBlock, 2.0);

			if(constructor is MarkupConstructorDetails markupConstructor) {
				Grid headerGrid = MakeExternalLinkHeader(headerBlock, "Open Pattern File...", out Button patternSourceButton, window);
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

		public static DocumentationPage1 GetEnumPage(EnumDoc enumDoc, DocumentationWindow window, Func<EnumDoc?>? refreshAction) {
			if (enumDoc == null) {
				return MakeErrorPage("Invalid enum documentation.");
			}

			return MakePage(GetEnumPageContent(enumDoc, window), enumDoc.type, () => GetEnumPageContent(refreshAction?.Invoke(), window));
		}

		private static UIElement GetEnumPageContent(EnumDoc? enumDoc, DocumentationWindow window) {
			if (enumDoc == null) {
				return MakeErrorContent("Invalid enum documentation.");
			}

			StackPanel stack = new StackPanel() { Orientation = Orientation.Vertical };

			TextBlock headerBlock = BaseContentBuilder.GetContentTextBlock(TextBlockMargin);
			headerBlock.Text = enumDoc.type;
			headerBlock.Foreground = SharpEditorPalette.TypeBrush;
			BaseContentBuilder.MakeFontSizeRelative(headerBlock, 2.0);
			stack.Children.Add(headerBlock);

			if (MakeDescriptionTextBlock(enumDoc.description, window) is TextBlock descriptionBlock) {
				//TextBlock descriptionBlock = BaseContentBuilder.GetContentTextBlock(enumDoc.description, IndentedMargin);
				stack.Children.Add(descriptionBlock);
			}

			stack.Children.Add(MakeSeparator());

			foreach (EnumValDoc enumVal in enumDoc.values) {
				StackPanel argPanel = new StackPanel() { Margin = ParagraphSpacingMargin, Orientation = Orientation.Vertical };

				TextBlock nameBlock = BaseContentBuilder.GetContentTextBlock(TextBlockMargin);
				nameBlock.Inlines.Add(new Run(enumVal.type ?? enumDoc.type) { Foreground = SharpEditorPalette.TypeBrush });
				nameBlock.Inlines.Add(new Run("." + enumVal.name));
				argPanel.Children.Add(nameBlock);

				if (MakeDescriptionTextBlock(enumVal.description, window) is TextBlock valDescriptionBlock) {
					//argPanel.Children.Add(BaseContentBuilder.GetContentTextBlock(enumVal.description, ArgumentDetailsMargin));
					argPanel.Children.Add(valDescriptionBlock);
				}

				stack.Children.Add(argPanel);
			}

			return stack;
		}

		public static DocumentationPage1 GetMarkupElementPage(ConstructorDetails constructor, DocumentationWindow window, Func<ConstructorDetails?>? refreshAction) {
			if (constructor == null) {
				return MakeErrorPage("Invalid constructor.");
			}

			return MakePage(GetMarkupElementPageContent(constructor, window), constructor.Name, () => GetMarkupElementPageContent(refreshAction?.Invoke(), window));
		}

		private static UIElement GetMarkupElementPageContent(ConstructorDetails? constructor, DocumentationWindow window) {
			if (constructor == null) {
				return MakeErrorContent("Invalid constructor.");
			}

			StackPanel stack = new StackPanel() { Orientation = Orientation.Vertical };

			TextBlock headerBlock = BaseContentBuilder.GetContentTextBlock(XMLContentBuilder.MakeMarkupTypeHeader(constructor), TextBlockMargin);
			BaseContentBuilder.MakeFontSizeRelative(headerBlock, 2.0);
			stack.Children.Add(headerBlock);

			if (MakeDescriptionTextBlock(constructor.Description, window) is TextBlock descriptionBlock) {
				//TextBlock descriptionBlock = BaseContentBuilder.GetContentTextBlock(constructor.Description, IndentedMargin);
				stack.Children.Add(descriptionBlock);
			}

			if (constructor.Arguments.Length > 0) {
				stack.Children.Add(MakeSeparator());

				foreach (ConstructorArgumentDetails arg in constructor.ConstructorArguments) {
					stack.Children.Add(MakeSingleMarkupArgumentBlocks(arg, window).SetMargin(ParagraphSpacingMargin));
				}
			}

			return stack;
		}

		public static DocumentationPage1 GetCardSetConfigPage(CardSetConfig cardSetConfig, DocumentationWindow window, Func<CardSetConfig?>? refreshAction) {
			if (cardSetConfig == null) {
				return MakeErrorPage("Invalid card configuration.");
			}

			return MakePage(GetCardSetConfigPageContent(cardSetConfig, window), cardSetConfig.name, () => GetCardSetConfigPageContent(refreshAction?.Invoke(), window));
		}

		private static UIElement GetCardSetConfigPageContent(CardSetConfig? cardSetConfig, DocumentationWindow window) {
			if (cardSetConfig == null) {
				return MakeErrorContent("Invalid card configuration.");
			}

			StackPanel stack = new StackPanel() { Orientation = Orientation.Vertical };

			TextBlock headerBlock = BaseContentBuilder.GetContentTextBlock(MakeCardConfigHeader(cardSetConfig), TextBlockMargin);
			headerBlock.TextWrapping = TextWrapping.NoWrap;
			BaseContentBuilder.MakeFontSizeRelative(headerBlock, 2.0);

			Grid headerGrid = MakeExternalLinkHeader(headerBlock, "Open Configuration File...", out Button configSourceButton, window);
			configSourceButton.Click += delegate { SharpEditorWindow.Instance?.OpenEditorDocument(cardSetConfig.origin.Path, true); };

			stack.Children.Add(headerGrid);

			/*
			if (!string.IsNullOrWhiteSpace(constructor.Description)) {
				TextBlock descriptionBlock = BaseContentBuilder.GetContentTextBlock(constructor.Description, IndentedMargin);
				yield return descriptionBlock;
			}
			*/

			stack.Children.Add(MakeSeparator());

			if (cardSetConfig.definitions.Count > 0) {
				TextBlock attributesHeaderBlock = BaseContentBuilder.GetContentTextBlock("Card Attributes", TitleMargin);
				BaseContentBuilder.MakeFontSizeRelative(attributesHeaderBlock, 1.6);
				stack.Children.Add(attributesHeaderBlock);

				foreach (Definition definition in cardSetConfig.definitions) {
					stack.Children.Add(MakeDefinitionElement(definition));
				}
			}

			if (cardSetConfig.cardSections.Count > 0) {
				TextBlock attributesHeaderBlock = BaseContentBuilder.GetContentTextBlock("Card Sections", TitleMargin);
				BaseContentBuilder.MakeFontSizeRelative(attributesHeaderBlock, 1.6);
				stack.Children.Add(attributesHeaderBlock);

				foreach (Conditional<AbstractCardSectionConfig> section in cardSetConfig.cardSections) {
					if (!(section.Value is DynamicCardSectionConfig dynamic && dynamic.AlwaysInclude)) {
						stack.Children.Add(MakeCardSectionElement(section, window).SetMargin(ParagraphMargin));
					}
				}
			}

			return stack;
		}

		public static Grid MakeExternalLinkHeader(TextBlock headerBlock, string tooltip, out Button linkButton, DocumentationWindow window) {
			Grid headerGrid = new Grid() {
				VerticalAlignment = VerticalAlignment.Center,
				HorizontalAlignment = HorizontalAlignment.Stretch
			};
			headerGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
			headerGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
			headerGrid.RowDefinitions.Add(new RowDefinition());

			Grid.SetColumn(headerBlock, 0);
			Grid.SetRow(headerBlock, 0);
			headerGrid.Children.Add(headerBlock);

			linkButton = new Button {
				Width = 32,
				Height = 32,
				//HorizontalAlignment = HorizontalAlignment.Stretch,
				VerticalAlignment = VerticalAlignment.Stretch,
				Content = new System.Windows.Controls.Image {
					Source = new BitmapImage(new Uri("pack://application:,,,/Images/ExternalLink.png")),
					VerticalAlignment = VerticalAlignment.Bottom,
					HorizontalAlignment = HorizontalAlignment.Stretch,
					Margin = new Thickness(3)
				},
				//Background = Brushes.Transparent,
				Style = window.Resources["SubtleButton"] as Style,
				Opacity = 0.5,
				ToolTip = tooltip
			};
			Grid.SetColumn(linkButton, 1);
			Grid.SetRow(linkButton, 0);
			headerGrid.Children.Add(linkButton);

			return headerGrid;
		}

		private class DocumentationPageInlineProcessor : DocumentationInlineProcessor {
			private readonly DocumentationWindow window; // TODO Use this to make clickable links to types and enums

			public DocumentationPageInlineProcessor(DocumentationWindow window) : base() {
				this.window = window;
			}

			public override Inline Visit(TypeSpan span) {
				return base.Visit(span);
			}
			protected override Inline EnumTypeInline(EnumValueSpan span) {
				return base.EnumTypeInline(span);
			}
		}

		public static TextBlock? MakeDescriptionTextBlock(DocumentationString? descriptionString, DocumentationWindow window) {
			return BaseContentBuilder.MakeDescriptionTextBlock(descriptionString, IndentedMargin, new DocumentationPageInlineProcessor(window));
		}

		public static Separator MakeSeparator(double horizontalPadding = 15.0, double verticalPadding = 15.0) {
			return new Separator() {
				Foreground = Brushes.LightGray,
				Margin = new Thickness(horizontalPadding, verticalPadding, horizontalPadding, verticalPadding)
			};
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
				if(!argument.IsOptional) { notes.Add("required"); }
				if(argument.UseLocal) { notes.Add("local"); }
				argPanel.Children.Add(BaseContentBuilder.GetContentTextBlock("(" + string.Join(", ", notes).ToTitleCase() + ")", ArgumentDetailsMargin));
			}

			return argPanel;
		}

		private static bool IsEnum(ArgumentType argumentType, [MaybeNullWhen(false)] out EnumDoc enumDoc) {
			Type? enumType = null;
			if (argumentType.DisplayType.IsEnum) {
				enumType = argumentType.DisplayType;
			}
			else if(Nullable.GetUnderlyingType(argumentType.DisplayType) is Type nulledType && nulledType.IsEnum) {
				enumType = nulledType;
			}

			if(enumType is Type foundType && SharpDocumentation.GetEnumDoc(foundType) is EnumDoc foundDoc) {
				enumDoc = foundDoc;
				return true;
			}
			else {
				enumDoc = null;
				return false;
			}
		}

		public static Inline GetArgumentTypeInline(ConstructorArgumentDetails argument, DocumentationWindow window) {
			if (IsEnum(argument.ArgumentType, out EnumDoc? enumDoc)) {
				ClickableRun enumClickable = new ClickableRun(SharpValueHandler.GetTypeName(argument.ArgumentType)) { Foreground = SharpEditorPalette.TypeBrush };
				enumClickable.MouseLeftButtonDown += window.MakeNavigationDelegate(enumDoc, null);
				return enumClickable;
			}
			else {
				return new Run(SharpValueHandler.GetTypeName(argument.ArgumentType)) { Foreground = SharpEditorPalette.TypeBrush };
			}
		}

		public static TextBlock MakeArgumentHeaderBlock(ConstructorArgumentDetails argument, DocumentationWindow window) {
			TextBlock argumentBlock = BaseContentBuilder.GetContentTextBlock(TextBlockMargin);

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
				List<SharpSheets.Layouts.Rectangle> displayRects = new List<SharpSheets.Layouts.Rectangle>();

				SharpSheets.Layouts.Size GetPage(SharpSheets.Layouts.Rectangle shapeArea) {
					float margin = Math.Max(shapeArea.Width, shapeArea.Height) * ExampleGraphicDefaultMargin;
					return (SharpSheets.Layouts.Size)shapeArea.Margins(margin, true);
				}
				SharpSheets.Layouts.Rectangle GetShape(SharpSheets.Layouts.Size pageArea) {
					float margin = Math.Max(pageArea.Width, pageArea.Height) * (ExampleGraphicDefaultMargin / (1 + 2 * ExampleGraphicDefaultMargin));
					return new SharpSheets.Layouts.Rectangle(pageArea.Width, pageArea.Height).Margins(margin, false);
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
					SharpSheets.Layouts.Rectangle shapeRect;

					if (shape is IMarkupObject markupObject && (markupObject.Pattern.exampleCanvas != null || markupObject.Pattern.exampleRect != null)) {
						if(markupObject.Pattern.exampleCanvas != null && markupObject.Pattern.exampleRect != null) {
							pageSize = markupObject.Pattern.exampleCanvas;
							shapeRect = markupObject.Pattern.exampleRect;
						}
						else if(markupObject.Pattern.exampleCanvas != null) {
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
						pageSize = GetPage(new SharpSheets.Layouts.Rectangle(140, 30));
						shapeRect = GetShape(pageSize);
					}
					else if (typeof(IDetail).IsAssignableFrom(constructor.DeclaringType)) {
						pageSize = GetPage(new SharpSheets.Layouts.Rectangle(90, 20));
						shapeRect = GetShape(pageSize);
					}
					else {
						pageSize = GetPage(new SharpSheets.Layouts.Rectangle(110, 90));
						shapeRect = GetShape(pageSize);
					}
					
					canvas = wpfDocument.AddNewPage(pageSize);

					if (shape is IDetail detail) {
						// TODO This needs improving so we can see vertical version too
						detail.Layout = SharpSheets.Layouts.Layout.ROWS;
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
				else if (typeof(SharpSheets.Widgets.IWidget).IsAssignableFrom(constructor.DeclaringType)) {
					//IContext context = Context.Simple("example", new Dictionary<string, string>(), new Dictionary<string, bool>());
					DirectoryPath source = new DirectoryPath(SharpEditorPathInfo.TemplateDirectory);

					SharpSheets.Widgets.IWidget widget = SharpEditorRegistries.WidgetFactoryInstance.MakeExample(constructor.FullName, source, false, out List<SharpSheets.Exceptions.SharpParsingException> errors);
					//IWidget widget = SharpEditorRegistries.WidgetFactoryInstance.MakeWidget(constructor.Name, context, source, out List<SharpSheets.Exceptions.SharpParsingException> errors);

					SharpSheets.Layouts.Size pageSize;
					SharpSheets.Layouts.Rectangle widgetRect;

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
						pageSize = GetPage(new SharpSheets.Layouts.Rectangle(110, 90));
						widgetRect = GetShape(pageSize);
					}

					canvas = wpfDocument.AddNewPage(pageSize);

					widget.Draw(canvas, widgetRect, default);

					/*
					if (widget.ContainerArea(canvas, widgetRect) is SharpSheets.Layouts.Rectangle containerArea) {
						displayRects.Add(containerArea);
					}
					*/
					if (widget.RemainingRect(canvas, widgetRect) is SharpSheets.Layouts.Rectangle remainingArea) {
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
					foreach (SharpSheets.Layouts.Rectangle displayRect in displayRects) {
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
			if(width >= minX && width <= maxX && height >= minY && height <= maxY) {
				return new ScaleTransform(3, 3);
			}

			double scale = 1;

			if(width > maxX || height > maxY) {
				scale = Math.Min(maxX / width, maxY / height);
			}
			else if(scale <= 1 && (width < minX || height < minY)) {
				scale = Math.Max(minX / width, minY / height);
			}

			return new ScaleTransform(3 * scale, 3 * scale);
		}

		private static Type GetArgumentType(Type argType, out bool isExpression) {
			if (XMLContentBuilder.ResolveExpressionType(argType) is Type exprType) {
				isExpression = true;
				return exprType;
			}
			else {
				XMLContentBuilder.GetTypeName(argType, out bool concrete);
				isExpression = !concrete;
				return argType;
			}
		}

		private static Type GetArgumentType(ArgumentType argType, out bool isExpression) {
			return GetArgumentType(argType.DisplayType, out isExpression);
		}

		private static FrameworkElement MakeSingleMarkupArgumentBlocks(ConstructorArgumentDetails argument, DocumentationWindow window) {
			StackPanel argPanel = new StackPanel() { Orientation = Orientation.Vertical };

			Type resolvedType = GetArgumentType(argument.ArgumentType, out bool isExpression);

			argPanel.Children.Add(MakeMarkupArgumentHeaderBlock(argument, resolvedType, isExpression, window));

			if (MakeDescriptionTextBlock(argument.ArgumentDescription, window) is TextBlock argDescriptionBlock) {
				//argPanel.Children.Add(BaseContentBuilder.GetContentTextBlock(argument.ArgumentDescription, ArgumentDetailsMargin));
				argPanel.Children.Add(argDescriptionBlock);
			}

			if (resolvedType.IsEnum && SharpDocumentation.GetEnumDoc(resolvedType) is EnumDoc enumDoc) {
				argPanel.Children.Add(EnumContentBuilder.MakeEnumOptionsBlock(enumDoc, ArgumentDetailsMargin));
			}

			if(!isExpression || !argument.IsOptional || argument.UseLocal) {
				List<string> notes = new List<string>();
				if (!isExpression) { notes.Add("Concrete value"); }
				if (!argument.IsOptional) { notes.Add("Required"); }
				if (argument.UseLocal) { notes.Add("Local"); }
				string finalNote = "(" + string.Join(", ", notes) + ")";
				argPanel.Children.Add(BaseContentBuilder.GetContentTextBlock(finalNote, ArgumentDetailsMargin));
			}

			return argPanel;
		}

		private static TextBlock MakeMarkupArgumentHeaderBlock(ConstructorArgumentDetails argument, Type resolvedType, bool isExpression, DocumentationWindow window) {
			TextBlock argumentBlock = BaseContentBuilder.GetContentTextBlock(TextBlockMargin);

			string typeName = XMLContentBuilder.GetTypeName(argument.ArgumentType, out _);

			Run typeRun;
			if (resolvedType.IsEnum && SharpDocumentation.GetEnumDoc(resolvedType) is EnumDoc enumDoc) {
				ClickableRun enumClickable = new ClickableRun(typeName) { Foreground = SharpEditorPalette.TypeBrush };
				enumClickable.MouseLeftButtonDown += window.MakeNavigationDelegate(enumDoc, null); // TODO Passing the type back in won't change anything for refresh
				typeRun = enumClickable;
			}
			else {
				typeRun = new Run(typeName) { Foreground = SharpEditorPalette.TypeBrush };
			}
			argumentBlock.Inlines.Add(typeRun);

			argumentBlock.Inlines.Add(new Run(SharpValueHandler.NO_BREAK_SPACE.ToString()));

			ArgumentDetails arg = argument.Argument;
			//while (arg != null && arg is PrefixedArgumentDetails prefixed) { arg = prefixed.Basis; } // What was this supposed to be doing?

			argumentBlock.Inlines.Add(new Run(arg.Name) { });

			if (argument.Implied != null) {
				argumentBlock.Inlines.Add(new Run("." + argument.Implied));
			}

			argumentBlock.Inlines.AddRange(ConstructorContentBuilder.GetArgumentDefaultInlines(argument.Argument, null));

			return argumentBlock;
		}

		private static FrameworkElement MakeDefinitionElement(Definition definition) {
			FrameworkElement defElem = DefinitionContentBuilder.MakeDefinitionElement(definition, null, TextBlockMargin, IndentedMargin);
			defElem.Margin = ParagraphSpacingMargin;
			return defElem;
		}

		private static IEnumerable<Inline> MakeCardConfigHeader(CardSetConfig cardSetConfig) {
			yield return new Run(cardSetConfig.name) { Foreground = SharpEditorPalette.RectBrush };
		}

		public static FrameworkElement MakeCardSectionElement(Conditional<AbstractCardSectionConfig> section, DocumentationWindow window) {
			StackPanel sectionPanel = new StackPanel() { Orientation = Orientation.Vertical };

			TextBlock sectionHeaderBlock = BaseContentBuilder.GetContentTextBlock("Section", TitleMargin);
			BaseContentBuilder.MakeFontSizeRelative(sectionHeaderBlock, 1.4);
			sectionPanel.Children.Add(sectionHeaderBlock);

			if (!section.Condition.IsConstant) {
				TextBlock conditionBlock = BaseContentBuilder.GetContentTextBlock(TextBlockMargin);
				conditionBlock.Inlines.Add(new Run("Condition: "));
				conditionBlock.Inlines.Add(new Run(section.Condition.ToString()));
				sectionPanel.Children.Add(conditionBlock);
			}

			if (section.Value.definitions.Count > 0) {
				TextBlock attributesHeaderBlock = BaseContentBuilder.GetContentTextBlock("Section Attributes", TitleMargin);
				BaseContentBuilder.MakeFontSizeRelative(attributesHeaderBlock, 1.2);
				sectionPanel.Children.Add(attributesHeaderBlock);

				foreach (Definition definition in section.Value.definitions) {
					sectionPanel.Children.Add(MakeDefinitionElement(definition));
				}
			}

			if(section.Value is DynamicCardSectionConfig dynamicSection) {
				if (dynamicSection.cardFeatures.Count > 0) {
					TextBlock featuresHeaderBlock = BaseContentBuilder.GetContentTextBlock("Section Features", TitleMargin);
					BaseContentBuilder.MakeFontSizeRelative(featuresHeaderBlock, 1.2);
					sectionPanel.Children.Add(featuresHeaderBlock);

					foreach (Conditional<CardFeatureConfig> feature in dynamicSection.cardFeatures) {
						sectionPanel.Children.Add(MakeCardFeatureElements(feature, window).SetMargin(ParagraphMargin));
					}
				}
			}

			return sectionPanel;
		}

		public static FrameworkElement MakeCardFeatureElements(Conditional<CardFeatureConfig> feature, DocumentationWindow window) {
			StackPanel featurePanel = new StackPanel() { Orientation = Orientation.Vertical };

			TextBlock sectionHeaderBlock = BaseContentBuilder.GetContentTextBlock("Feature", TitleMargin);
			BaseContentBuilder.MakeFontSizeRelative(sectionHeaderBlock, 1.2);
			featurePanel.Children.Add(sectionHeaderBlock);

			if (!feature.Condition.IsConstant) {
				TextBlock conditionBlock = BaseContentBuilder.GetContentTextBlock(TextBlockMargin);
				conditionBlock.Inlines.Add(new Run("Condition: "));
				conditionBlock.Inlines.Add(new Run(feature.Condition.ToString()));
				featurePanel.Children.Add(conditionBlock);
			}

			if (feature.Value.definitions.Count > 0) {
				TextBlock attributesHeaderBlock = BaseContentBuilder.GetContentTextBlock("Feature Attributes", TitleMargin);
				BaseContentBuilder.MakeFontSizeRelative(attributesHeaderBlock, 1.2);
				featurePanel.Children.Add(attributesHeaderBlock);

				foreach (Definition definition in feature.Value.definitions) {
					featurePanel.Children.Add(MakeDefinitionElement(definition));
				}
			}

			return featurePanel;
		}

	}
}
