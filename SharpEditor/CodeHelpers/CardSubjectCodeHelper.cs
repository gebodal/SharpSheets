using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using SharpSheets.Cards.Definitions;
using SharpSheets.Parsing;
using SharpSheets.Utilities;
using SharpSheets.Cards.CardSubjects;
using SharpSheets.Cards.CardConfigs;
using SharpEditor.DataManagers;
using SharpSheets.Evaluations;
using Avalonia.Input;
using Avalonia.Controls;
using SharpEditor.Windows;
using Avalonia.Threading;
using SharpEditor.Completion;
using SharpEditor.Dialogues;
using SharpEditor.Parsing.ParsingState;

namespace SharpEditor.CodeHelpers {

	public class CardSubjectCodeHelper : ICodeHelper {

		protected readonly TextEditor textEditor;
		protected readonly CardSubjectParsingState parsingState;

		protected TextDocument Document { get { return textEditor.Document; } }

		private CardSubjectCodeHelper(TextEditor textEditor, CardSubjectParsingState parsingState) {
			this.textEditor = textEditor;
			this.parsingState = parsingState;
		}

		public static ICodeHelper GetCodeHelper(TextEditor textEditor, CardSubjectParsingState parsingState) {
			return new CardSubjectCodeHelper(textEditor, parsingState);
		}

		#region Completion Window
		private readonly Regex archiveStartRegex = new Regex(@"^\#\>\s*");
		private readonly Regex definitionStartRegex = new Regex(@"^\#\=\s*");

		private string GetCurrentLineText(bool upToCarat) {
			ISegment currentLine = textEditor.Document.GetLineByOffset(textEditor.CaretOffset);

			if (currentLine == null) {
				return "";
			}

			string lineText = textEditor.Document.GetText(currentLine);

			if (lineText == null) {
				return "";
			}
			else if (upToCarat) {
				return lineText.Substring(0, textEditor.CaretOffset - currentLine.Offset);
			}
			else {
				return lineText;
			}
		}

		public int GetCompletionStartOffset() {

			string beforeCarat = GetCurrentLineText(true);

			if (archiveStartRegex.Match(beforeCarat) is Match archiveMatch && archiveMatch.Success) {
				int archiveEnd = archiveMatch.Index + archiveMatch.Length;
				int delta = beforeCarat.Length - archiveEnd;

				//Console.WriteLine($"Caret: {textEditor.CaretOffset}, beforeCaret: {beforeCarat.Length}, archive index: {archiveMatch.Index}, archive length: {archiveMatch.Length}, archive end: {archiveEnd}, delta: {delta}");

				return textEditor.CaretOffset - delta;
			}

			return textEditor.CaretOffset;
		}

		public IList<ICompletionData> GetCompletionData() {
			List<ICompletionData> data = new List<ICompletionData>();

			string lineText = GetCurrentLineText(false);

			if (!string.IsNullOrEmpty(lineText) && archiveStartRegex.IsMatch(lineText)) {
				if (parsingState.GetCurrentCardSetDefinition(textEditor.CaretOffset)?.Archive is IReadOnlyDictionary<string, CardSubject> archive) {
					foreach (CardSubject archiveEntry in archive.Values) {
						data.Add(new CompletionEntry(archiveEntry.Name.Value));
					}
				}
			}
			else if (!string.IsNullOrEmpty(lineText) && definitionStartRegex.IsMatch(lineText)) {
				foreach (string definitionName in SharpEditorRegistries.CardSetConfigRegistryInstance.Select(d => d.name)) {
					data.Add(new CompletionEntry(definitionName));
				}
			}
			else if (parsingState?.GetCardEntity(textEditor.CaretOffset) is ICardDocumentEntity cardEntity) {
				bool escaped = textEditor.CaretOffset > 0 && textEditor.Document.Text[textEditor.CaretOffset - 1] == '$';
				foreach (Definition definition in cardEntity.Definitions) {
					string escapedName = (escaped ? "" : "$") + definition.name.ToString();
					data.Add(new CompletionEntry(escapedName) { DescriptionElements = TooltipBuilder.MakeDefinitionEntries(definition, cardEntity.Environment).MakeArray() });
				}
			}

			return data;
		}

		public bool TextEnteredTriggerCompletion(TextInputEventArgs e) {
			if (e.Text == "$") {
				return true;
			}
			else if (e.Text == " ") {
				string lineText = GetCurrentLineText(true);
				if (!string.IsNullOrEmpty(lineText) && (archiveStartRegex.IsMatch(lineText) || definitionStartRegex.IsMatch(lineText))) {
					return true;
				}
			}
			return false;
		}

		#endregion

		#region Tooltip
		public IList<Control> GetToolTipContent(int offset, string word) {
			List<Control> contents = new List<Control>();

			//IDocumentEntity entity = parsingState.GetEntity(Document.GetLineByOffset(offset));
			IDocumentEntity? entity = parsingState.GetSpansAtOffset(offset)
				.Where(s => s.Entity != null)
				.OrderByDescending(s => s.Length)
				.Select(s => s.Entity)
				.FirstOrDefault();

			if (entity is ICardDocumentEntity cardEntity) {
				contents.AddRange(MakeCardEntityEntries(cardEntity));
			}
			else if (parsingState.GetSpansAtOffset(offset).FirstOrDefault(s => s.Type == CardSubjectSpanType.PROPERTY) is CardSubjectSpan property && property.Definition is not null) {
				IEnvironment? environment = parsingState.GetCardEntity(property.StartOffset)?.Environment;
				contents.Add(TooltipBuilder.MakeCardDefinitionBlocks(property.Definition, environment));
			}
			return contents;
		}

		public IList<Control> GetFallbackToolTipContent(int offset) {
			return Array.Empty<Control>();
		}

		public static IEnumerable<TextBlock> MakeCardEntityEntries(ICardDocumentEntity cardEntity) {
			IEnvironment evaluationEnvironment = cardEntity.Environment;
			DefinitionGroup? definitionList = null;
			if (cardEntity is CardSubject subject) {
				yield return TooltipBuilder.GetToolTipTextBlock($"Subject: {subject.Name.Value}");

				definitionList = subject.SubjectDefinitions;
			}
			else if (cardEntity is CardSegment segment) {
				yield return TooltipBuilder.GetToolTipTextBlock($"Segment: {segment.Heading.Value}");

				definitionList = segment.SegmentDefinitions;
			}
			else if (cardEntity is CardFeature feature) {
				string featureText = feature.Text.Value.ToString();
				string exampleText = string.IsNullOrWhiteSpace(feature.Title.Value) ? ("(titleless) " + featureText.Substring(0, Math.Min(20, featureText.Length))) : feature.Title.Value;

				yield return TooltipBuilder.GetToolTipTextBlock("Feature: " + exampleText);

				definitionList = feature.FeatureDefinitions; // feature.TextEnvironment;
			}

			if (definitionList != null && evaluationEnvironment != null) {
				foreach (TextBlock frameworkElement in TooltipBuilder.MakeDefinitionEntries(definitionList, evaluationEnvironment)) {
					yield return frameworkElement;
				}
			}
		}
		#endregion

		#region Context Menu

		public IList<Control> GetContextMenuItems(int offset, string? word) {

			List<Control> menuItems = new List<Control>();

			if (parsingState?.GetSpansAtOffset(offset)?.Where(s => s.Entity is CardSubject).Select(s => s.Entity).FirstOrDefault() is CardSubject subject
				&& textEditor.Document.GetLineByOffset(offset) is ISegment line
				&& Document.GetText(line) is string lineText && lineText.TrimStart().StartsWith("#>")) {

				MenuItem insertFullTextMenuItem = new MenuItem {
					Header = "Insert Full Text"
				};

				//insertFullTextMenuItem.Click += (s,e) => textEditor.Dispatcher.Invoke(() => {
				insertFullTextMenuItem.Click += (s, e) => Dispatcher.UIThread.InvokeAsync(() => { // Yes async?
					Console.WriteLine("Replacing title with full text");
					textEditor.Document.Replace(line, subject.ToText() + "\n");
				});

				/*
				insertFullTextMenuItem.Click += delegate {
					Console.WriteLine("Replacing title with full text");
					textEditor.Document.Replace(line, subject.ToText() + "\n");
				};
				*/

				menuItems.Add(insertFullTextMenuItem);
			}
			else if (word is null && (!InsideCard(offset) ?? false) && parsingState?.GetCurrentCardSetDefinition(offset) is CardSetConfig cardSetConfig) {

				CardConfig[] cardConfigs = cardSetConfig.cardConfigs.Select(c => c.Value).ToArray();

				List<(string? name, CardConfig config)> listedConfigs = new List<(string? name, CardConfig config)>();

				if (cardConfigs.Length == 1) {
					if (cardConfigs[0].name is not null) {
						listedConfigs.Add((cardConfigs[0].name, cardConfigs[0]));
					}
					else {
						listedConfigs.Add((null, cardConfigs[0]));
					}
				}
				else if (cardConfigs.Length > 1) {
					foreach (CardConfig cardConfig in cardSetConfig.cardConfigs.Select(c => c.Value)) {
						if (cardConfig.name is not null) {
							listedConfigs.Add((cardConfig.name, cardConfig));
						}
					}
				}

				foreach ((string? configName, CardConfig cardConfig) in listedConfigs) {
					string header = configName is not null ? $"Add new {configName} card" : "Add new card";

					MenuItem insertExampleTextMenuItem = new MenuItem {
						Header = header
					};

					insertExampleTextMenuItem.Click += (s, e) => Dispatcher.UIThread.InvokeAsync(() => { // Yes async?
						Console.WriteLine("Inserting example text");
						textEditor.Document.Insert(offset, cardConfig.GetExampleText() + '\n');
					});

					menuItems.Add(insertExampleTextMenuItem);
				}
			}

			if (parsingState?.GetCurrentCardSetDefinition(offset) is CardSetConfig cardSetDefinition) {
				if (menuItems.Count > 0) { menuItems.Add(new Separator()); }
				menuItems.AddRange(MakeDefinitionMenuItem(cardSetDefinition));
			}

			return menuItems;
		}

		protected IEnumerable<MenuItem> MakeDefinitionMenuItem(CardSetConfig cardSetDefinition) {
			MenuItem definitionFileMenuItem = new MenuItem {
				Header = "Open Definition File..."
			};
			definitionFileMenuItem.Click += async delegate {
				if (cardSetDefinition.origin.IsFile) {
					SharpEditorWindow.Instance?.OpenEditorDocument(cardSetDefinition.origin.Path, true);
				}
				else {
					await MessageBoxes.Show(textEditor, "Could not find file.", "Could Not Find File", MessageBoxButton.OK, MessageBoxImage.Error);
				}
			};
			yield return definitionFileMenuItem;

			MenuItem documentationMenuItem = new MenuItem {
				Header = cardSetDefinition.name + " Documentation..."
			};
			documentationMenuItem.Click += delegate {
				SharpEditorWindow.Instance?.controller?.ActivateDocumentationWindow().NavigateTo(cardSetDefinition, null); // TODO Any way to refresh here?
			};
			yield return documentationMenuItem;
		}

		protected bool? InsideCard(int offset) {
			if(parsingState is null) { return null; }

			CardSubjectSpan? nextOrCurrentSubject = parsingState.ConfigSpans
				.Where(s => s.StartOffset > offset || (s.StartOffset <= offset && s.EndOffset >= offset))
				.OrderBy(s => s.StartOffset)
				.FirstOrDefault();

			if(nextOrCurrentSubject is null) { return false; }

			if(nextOrCurrentSubject.StartOffset <= offset) { return true; }

			return nextOrCurrentSubject.Entity is not CardSubject;
		}

		#endregion

		#region Text Entered
		public void TextEntered(TextInputEventArgs e) {
			int carat = textEditor.CaretOffset;
			if (e.Text == "{") {
				textEditor.Document.Insert(carat, "}", AnchorMovementType.BeforeInsertion);
				e.Handled = true;
			}
			else if (e.Text == "[") {
				textEditor.Document.Insert(carat, "]", AnchorMovementType.BeforeInsertion);
				e.Handled = true;
			}
		}
		#endregion

		#region Commenting

		public bool SupportsIncrementComment { get; } = true;

		public void IncrementComment(int offset, int length) {
			DocumentLine startLine = Document.GetLineByOffset(offset);
			DocumentLine endLine = Document.GetLineByOffset(offset + length);

			Document.BeginUpdate();

			for (int i = startLine.LineNumber; i <= endLine.LineNumber; i++) {
				DocumentLine line = Document.GetLineByNumber(i);
				Document.Insert(line.Offset, "%");
			}

			Document.EndUpdate();
		}

		public void DecrementComment(int offset, int length) {
			DocumentLine startLine = Document.GetLineByOffset(offset);
			DocumentLine endLine = Document.GetLineByOffset(offset + length);

			Document.BeginUpdate();

			for (int i = startLine.LineNumber; i <= endLine.LineNumber; i++) {
				DocumentLine line = Document.GetLineByNumber(i);
				string lineText = Document.GetText(line);
				for (int c = 0; c < lineText.Length; c++) {
					if (!char.IsWhiteSpace(lineText[c])) {
						if (lineText[c] == '%') {
							Document.Remove(line.Offset + c, 1);
						}
						break;
					}
				}
			}

			Document.EndUpdate();
		}

		#endregion

		#region Pasting

		public void TextPasted(EventArgs args, int offset) {

		}

		#endregion
	}

}
