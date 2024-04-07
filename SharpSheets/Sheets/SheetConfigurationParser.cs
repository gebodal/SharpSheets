using System.Collections.Generic;
using System;
using System.Linq;
using SharpSheets.Widgets;
using SharpSheets.Utilities;
using SharpSheets.Parsing;
using SharpSheets.Exceptions;

namespace SharpSheets.Sheets {
	public class SheetConfigurationParser : IParser<SharpPageList> {

		private readonly WidgetFactory widgetFactory;

		public SheetConfigurationParser(WidgetFactory widgetFactory) {
			this.widgetFactory = widgetFactory;
		}

		private class SheetEntryStack : EntryStack {

			protected readonly WidgetFactory widgetFactory;

			public readonly List<ConfigEntry> pages;

			public SheetEntryStack(DirectoryPath source, WidgetFactory widgetFactory) : base(source, SheetConfigurationConstants.SheetsName) {
				this.widgetFactory = widgetFactory;
				pages = new List<ConfigEntry>();
			}

			public override void Push(DocumentSpan location, int indentLevel, string type, bool isNamed, bool localOnly) {
				if (isNamed) {
					base.Push(location, indentLevel, type, isNamed, localOnly);
				}
				else {
					if (stack.Count == 0 && pages.Count == 0) {
						NewPage(DocumentSpan.Imaginary, -1);
					}

					if (stack.Count == 0 && pages.Count > 0) {
						parsingExceptions.Add(new SharpParsingException(location, "Invalid entry, does not have an associated page."));
					}
					else {
						base.Push(location, indentLevel, type, isNamed, localOnly);
					}
				}
			}

			public void NewPage(DocumentSpan location, int indentLevel) {
				stack.Clear();
				ConfigEntry page = new ConfigEntry(rootEntry, location, SheetConfigurationConstants.PageName);
				stack.Push(new EntryStackItem(indentLevel, page));
				pages.Add(page);
				rootEntry.AddChild(page);
			}

			public SharpPageList GenerateSheets(out Dictionary<object, IDocumentEntity> origins, out List<SharpParsingException> errors) {
				rootEntry.RefreshVisited();
				SharpPageList sharpPages = new SharpPageList();

				origins = new Dictionary<object, IDocumentEntity>(new IdentityEqualityComparer<object>());
				errors = new List<SharpParsingException>();

				WidgetFactory originTrackingFactory = widgetFactory.TrackOrigins(origins);

				foreach (ConfigEntry pageEntry in pages) {
					Page pageWidget = originTrackingFactory.MakePage(pageEntry, source, out SharpParsingException[] pageErrors);
					sharpPages.AddPage(pageWidget);
					errors.AddRange(pageErrors);
				}

				return sharpPages;
			}

			public void CollectConfigurationResults(out SharpPageList sheets, out CompilationResult results) {
				sheets = GenerateSheets(out Dictionary<object, IDocumentEntity> origins, out List<SharpParsingException> buildErrors);
				results = CompilationResult.CompileResult(rootEntry, origins, parsingExceptions, buildErrors, new List<FilePath>(), Enumerable.Empty<int>());
			}
		}

		/// <summary></summary>
		/// <exception cref="DirectoryNotFoundException"></exception>
		private SheetEntryStack ParseConfiguration(DirectoryPath source, string config) {

			if (!source.Exists) {
				throw new DirectoryNotFoundException($"Source directory does not exist: \"{source.Path}\"");
			}

			SheetEntryStack entryStack = new SheetEntryStack(source, widgetFactory);
			int? invalidState = null; // Indent level of current invalid state, if it exists

			foreach(SharpDocumentLine line in SharpDocumentLineParsing.SplitLines(config)) {

				if (line.IndentLevel < 0) {
					entryStack.parsingExceptions.Add(new SharpParsingException(line.Location, "Invalid indentation."));
					continue;
				}

				if(invalidState.HasValue) {
					if (line.IndentLevel > invalidState.Value) {
						// Invalid line
						continue;
					}
					else {
						invalidState = null;
					}
				}

				while (entryStack.Count > 0 &&  line.IndentLevel <= entryStack.CurrentIndentLevel) {
					entryStack.Pop();
				}

				if (line.LineType == LineType.DIV) {
					if (SharpDocuments.StringEquals(line.Content, SheetConfigurationConstants.PageName)) {
						if (line.IndentLevel == 0) {
							entryStack.NewPage(line.Location, line.IndentLevel);
						}
						else {
							entryStack.parsingExceptions.Add(new SharpParsingException(line.Location, $"\"{SheetConfigurationConstants.PageName.ToLowerInvariant()}\" must always be at root (indent 0)."));
						}
					}
					else if (widgetFactory.ContainsKey(line.Content)) {
						entryStack.Push(line.Location, line.IndentLevel, line.Content, false, false);
					}
					else {
						entryStack.SetProperty(line.Location, line.Content, line.PropertyLocation, "", line.LocalOnly);
						invalidState = line.IndentLevel;
					}
				}
				else if(line.LineType == LineType.NAMEDCHILD) {
					entryStack.Push(line.Location, line.IndentLevel, line.Content, true, line.LocalOnly);
				}
				else if (line.LineType == LineType.PROPERTY) {
					entryStack.SetProperty(line.Location, line.Content, line.PropertyLocation, line.Property ?? "", line.LocalOnly); // Property should never be null is LineType is PROPERTY
				}
				else if (line.LineType == LineType.ENTRY) {
					entryStack.AddEntry(line.Location, line.Content);
				}
				else if (line.LineType == LineType.FLAG) {
					if (widgetFactory.IsWidget(line.Content)) { // flag is actually an empty SharpRect
						entryStack.Push(line.Location, line.IndentLevel, line.Content, false, false);
						entryStack.Pop();
					}
					else { // This really is just a flag
						entryStack.AddFlag(line.Location, line.Content, line.LocalOnly);
					}
				}
				else { // line.LineType == LineType.ERROR || line.LineType == LineType.DEFINITION
					entryStack.parsingExceptions.Add(new SharpParsingException(line.Location, "Could not parse line."));
					continue;
				}

			}

			return entryStack;
		}

		public SharpPageList ParseContent(FilePath origin, DirectoryPath source, string config, out CompilationResult results) {
			SheetEntryStack parsed = ParseConfiguration(source, config);
			parsed.CollectConfigurationResults(out SharpPageList sheets, out results);
			return sheets;
		}

		object IParser.Parse(FilePath origin, DirectoryPath source, string config, out CompilationResult results) {
			return ParseContent(origin, source, config, out results);
		}
	}
}
