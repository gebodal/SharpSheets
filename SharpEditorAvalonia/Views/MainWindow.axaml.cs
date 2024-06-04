using Avalonia.Controls;
using SharpSheets.Markup.Parsing;
using SharpSheets.Markup.Patterns;
using SharpSheets.Parsing;
using SharpSheets.Shapes;
using SharpSheets.Sheets;
using SharpSheets.Utilities;
using SharpSheets.Widgets;
using System;
using System.Collections.Generic;
using System.IO;

namespace SharpEditorAvalonia.Views;

public partial class MainWindow : Window {

	public MainWindow() {
		InitializeComponent();

		string templatesPath = @"D:\Tom\Documents\SharpSheets\AppData\Templates";
		string configFilePath = @"D:\Tom\Documents\Warhammer\D&D\5th Edition\My Characters\SharpSheets\balthazar.ssc";

		SharpSheets.Markup.Patterns.IMarkupRegistry markupRegistry = GetMarkupRegistry(templatesPath);
		ShapeFactory shapeFactory = new ShapeFactory(markupRegistry);
		WidgetFactory widgetFactory = new WidgetFactory(markupRegistry, shapeFactory);
		SheetConfigurationParser parser = new SheetConfigurationParser(widgetFactory);
		FilePath filePath = new FilePath(Path.GetFullPath(configFilePath));
		DirectoryPath sourcePath = filePath.GetDirectory() ?? throw new ArgumentException("Could not resolve config directory.");
		string configuration = File.ReadAllText(configFilePath);
		//string configName = Path.GetFileNameWithoutExtension(configFilePath);

		SharpPageList sheets = parser.ParseContent(filePath, sourcePath, configuration, out CompilationResult results);

		SharpAvaloniaDrawingDocument document = new SharpAvaloniaDrawingDocument();
		sheets.DrawTo(document, out _, new System.Threading.CancellationToken());

		SharpAvaloniaDrawingCanvas page = document.Pages[0];

		Canvas pageCanvas = new Canvas() {
			Width = page.CanvasRect.Width,
			Height = page.CanvasRect.Height
		};

		DrawingElement element = new DrawingElement(page.drawingGroup) {
			Width = page.CanvasRect.Width,
			Height = page.CanvasRect.Height
		};
		pageCanvas.Children.Add(element);

		Viewer.CanvasContent = pageCanvas;
		Viewer.ZoomCanvasToWholePage();
	}

	public static IMarkupRegistry GetMarkupRegistry(string templatesPath) {
		MarkupPatternParser patternParser = new MarkupPatternParser();
		List<MarkupPattern> patterns = new List<MarkupPattern>();
		foreach (string patternPath in Directory.EnumerateFiles(templatesPath, "*.sbml", SearchOption.AllDirectories)) {
			FilePath patternOrigin = new FilePath(patternPath);
			DirectoryPath patternSrc = patternOrigin.GetDirectory() ?? throw new ArgumentException("Could not resolve pattern directory.");
			string fileText = File.ReadAllText(patternPath);

			List<MarkupPattern> filePatterns = patternParser.ParseContent(patternOrigin, patternSrc, fileText, out CompilationResult result);
			patterns.AddRange(filePatterns);
		}

		IMarkupRegistry markupRegistry = SharpSheets.Markup.Patterns.MarkupRegistry.ReadOnly(patterns);

		return markupRegistry;
	}

}