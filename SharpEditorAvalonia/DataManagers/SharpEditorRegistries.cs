using SharpEditorAvalonia.Program;
using SharpEditorAvalonia.Registries;
using SharpSheets.Cards.CardConfigs;
using SharpSheets.Exceptions;
using SharpSheets.Markup.Parsing;
using SharpSheets.Parsing;
using SharpSheets.Shapes;
using SharpSheets.Widgets;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SharpEditorAvalonia.DataManagers {

	public static class SharpEditorRegistries {

		public static readonly IFileReader FileReader;
		public static readonly MarkupFileRegistry MarkupRegistry;
		public static readonly ShapeFactory ShapeFactoryInstance;
		public static readonly WidgetFactory WidgetFactoryInstance;
		public static readonly MarkupPatternParser MarkupPatternParserInstance;
		public static readonly CardSetConfigFactory CardSetConfigFactoryInstance;
		public static readonly CardSetConfigFileRegistry CardSetConfigRegistryInstance;

		public static TemplateRegistry TemplateRegistry { get; private set; }

		public static void Initialise() { } // Dummy method to force static initialisation

		static SharpEditorRegistries() {
			FileReader = FileReaders.BaseReader();

			MarkupPatternParserInstance = new MarkupPatternParser();

			MarkupRegistry = new MarkupFileRegistry(SharpEditorPathInfo.TemplateDirectory, MarkupPatternParserInstance);

			ShapeFactoryInstance = new ShapeFactory(MarkupRegistry);
			WidgetFactoryInstance = new WidgetFactory(MarkupRegistry, ShapeFactoryInstance);

			// This feels hacky, but oh well
			MarkupPatternParserInstance.ShapeFactory = ShapeFactoryInstance;

			CardSetConfigFactoryInstance = new CardSetConfigFactory(WidgetFactoryInstance, ShapeFactoryInstance, FileReader);

			CardSetConfigRegistryInstance = new CardSetConfigFileRegistry(SharpEditorPathInfo.TemplateDirectory, new CardSetConfigParser(WidgetFactoryInstance, ShapeFactoryInstance, CardSetConfigFactoryInstance));

			TemplateRegistry = new TemplateRegistry(SharpEditorPathInfo.TemplateDirectory);
			
			SharpEditorPathInfo.TemplateDirectoryChanged += OnTemplateDirectoryChanged;

			MarkupRegistry.Start();
			CardSetConfigRegistryInstance.Start();
			TemplateRegistry.Start();
		}

		public delegate void RegistryErrorsChangedHandler();
		public static event RegistryErrorsChangedHandler? OnRegistryErrorsChanged;

		private static readonly Dictionary<string, TemplateError[]> _registryErrors = new Dictionary<string, TemplateError[]>();
		public static TemplateError[] RegistryErrors { get { return _registryErrors.SelectMany(kv => kv.Value).ToArray(); } }

		public static bool HasRegistryErrors { get { return _registryErrors.Any(kv => kv.Value.Length > 0); } }

		public static void LogRegistryErrors(string filepath, IEnumerable<Exception> errors) {
			string fullFilePath = System.IO.Path.GetFullPath(filepath);
			_registryErrors[fullFilePath] = errors.Select(e => new TemplateError(fullFilePath, (e is SharpParsingException pe ? pe.Location : null) ?? DocumentSpan.Imaginary, e)).ToArray();

			foreach (Exception e in errors) {
				Console.WriteLine("Error loading file for registry (" + filepath + "): " + e.Message);
			}

			if (OnRegistryErrorsChanged != null) {
				OnRegistryErrorsChanged.Invoke();
			}
		}

		public static void ClearRegistryErrors(string filepath) {
			string fullFilePath = System.IO.Path.GetFullPath(filepath);
			_registryErrors.Remove(fullFilePath);
		}

		public static void RefreshRegistries() {
			MarkupRegistry.Refresh();
			CardSetConfigRegistryInstance.Refresh();
			TemplateRegistry.Refresh();

			if (OnRegistryErrorsChanged != null) {
				OnRegistryErrorsChanged.Invoke();
			}
		}

		private static void OnTemplateDirectoryChanged() {
			TemplateRegistry.Stop();
			CardSetConfigRegistryInstance.Stop();
			MarkupRegistry.Stop();

			MarkupRegistry.Path = SharpEditorPathInfo.TemplateDirectory;
			CardSetConfigRegistryInstance.Path = SharpEditorPathInfo.TemplateDirectory;
			TemplateRegistry.Path = SharpEditorPathInfo.TemplateDirectory;

			MarkupRegistry.Start();
			CardSetConfigRegistryInstance.Start();
			TemplateRegistry.Start();
		}

	}

}
