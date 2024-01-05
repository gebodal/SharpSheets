// Copyright (c) 2009 Daniel Grunwald
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of this
// software and associated documentation files (the "Software"), to deal in the Software
// without restriction, including without limitation the rights to use, copy, modify, merge,
// publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons
// to whom the Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all copies or
// substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
// PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
// FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
// OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using SharpSheets.Utilities;
using SharpSheets.Parsing;
using SharpEditor.Program;
using SharpEditor.DataManagers;
using System.Windows.Controls.Primitives;
using SharpEditor.Registries;

namespace SharpEditor {

	/// <summary>
	/// Interaction logic for Window1.xaml
	/// </summary>
	public partial class SharpEditorWindow : Window {

		public readonly AppController controller;
		private readonly ObservableCollection<EditorTabItem> editorTabs;

		// TODO Progress tracket for Generator?
		private readonly Generator generator;

		public static SharpEditorWindow? Instance { get; private set; } = null;

		public SharpEditorWindow(AppController controller) {
			this.controller = controller;
			
			InitializeComponent();
			SetValue(TextOptions.TextFormattingModeProperty, TextFormattingMode.Display);

			InitialiseTemplateFileList();

			//Console.WriteLine("Previous file location: " + SharpEditorFileInfo.LastFileDirectory);
			//Console.WriteLine("Previous template location: " + SharpEditorFileInfo.LastTemplateDirectory);

			//ShowDesignerDefaultButton.IsChecked = SharpEditorDetails.DesignerViewerOpenDefault;

			GenerateEnabled = false;
			SavePossible = false;
			IncrementCommentEnabled = false;

			generator = new Generator(this.Dispatcher) { OpenOnGenerate = SharpDataManager.Instance.OpenOnGenerate };
			SharpDataManager.Instance.OpenOnGenerateChanged += delegate { generator.OpenOnGenerate = SharpDataManager.Instance.OpenOnGenerate; };
			generator.Start();

			// Create list of tab items
			editorTabs = new ObservableCollection<EditorTabItem>();
			EditorTabControl.ItemsSource = editorTabs;

			EditorTabControl.SelectionChanged += TabSelectionChanged;

			//OpenEditorDocument(null, true);
			OpenEmptyDocument(DocumentType.SHARPCONFIG, false, true);

			if (Instance == null) { Instance = this; } // This seems a little suspect...
		}

		#region View Handlers

		private void ShowDocumentationMenuClick(object? sender, RoutedEventArgs e) {
			controller.ActivateDocumentationWindow();
		}

		private void ShowTemplateErrorsMenuClick(object? sender, RoutedEventArgs e) {
			controller.ActivateTemplateErrorWindow();
		}

		#endregion

		#region Tab Handling

		public bool HasTabOpen { get { return EditorTabControl?.SelectedIndex is int index ? index != -1 : false; } }
		public int NumTabs { get { return editorTabs?.Count ?? 0; } }

		private SharpDocumentEditor? currentlyVisible;

		public SharpDocumentEditor? GetCurrentEditor() {
			if (editorTabs.Count > 0) {
				return editorTabs[EditorTabControl.SelectedIndex].Content;
			}
			else {
				return null;
			}
		}

		public IEnumerable<SharpDocumentEditor> GetOpenEditors() {
			return editorTabs.Select(t => t.Content);
		}

		public void FocusOnEditor(SharpDocumentEditor editor) {
			EditorTabItem? item = editorTabs.FirstOrDefault(t => t.Content == editor);
			if (item != null) {
				int index = editorTabs.IndexOf(item);
				if (index >= 0) {
					EditorTabControl.SelectedIndex = index;
					editor.FocusEditor();
					GenerateEnabled = editor.parsingManager.HasGeneratedContent;
					IncrementCommentEnabled = editor.SupportsIncrementComment;
				}
			}
		}

		private void FocusEvent(object? sender, SharpDocumentEditor editor) {
			FocusOnEditor(editor);
		}

		public bool CloseTab(EditorTabItem item) {
			if (CloseDocument(item.Content)) {
				editorTabs.Remove(item);
				return true;
			}
			else {
				return false;
			}
		}

		private void TabSelectionChanged(object? sender, SelectionChangedEventArgs e) {
			if (currentlyVisible != null) {
				currentlyVisible.UpdateHeader -= UpdateTitle;
				currentlyVisible.DocumentTypeChanged -= OnDocumentTypeChanged;
				currentlyVisible = null;
				UpdateTitle(null);
			}

			if (EditorTabControl.SelectedIndex != -1) {
				currentlyVisible = GetCurrentEditor();
				if (currentlyVisible != null) {
					currentlyVisible.UpdateHeader += UpdateTitle;
					currentlyVisible.DocumentTypeChanged += OnDocumentTypeChanged;
					UpdateTitle(currentlyVisible.Header);

					ShowDesignerButton.IsChecked = currentlyVisible.IsDesignerVisible;
					ToolbarShowDesignerButton.IsChecked = currentlyVisible.IsDesignerVisible;

					ShowLineNumbersButton.IsChecked = currentlyVisible.textEditor.ShowLineNumbers;
					ShowEndOfLineButton.IsChecked = currentlyVisible.textEditor.Options.ShowEndOfLine;
					WrapLinesButton.IsChecked = currentlyVisible.textEditor.WordWrap;
				}
			}

			SavePossible = EditorTabControl.SelectedIndex != -1;
			// Set GenerateEnabled here
			GenerateEnabled = currentlyVisible?.parsingManager?.HasGeneratedContent ?? false;
			IncrementCommentEnabled = currentlyVisible?.SupportsIncrementComment ?? false;
		}

		private void UpdateTitle(string? header) {
			if (header == null) {
				Title = "SharpEditor";
			}
			else {
				Title = header + " - SharpEditor";
			}
		}

		private void UpdateTitle(object? sender, UpdateHeaderEventArgs e) {
			UpdateTitle(e.Header);
		}

		private void OnDocumentTypeChanged(object? sender, SharpDocumentEditor e) {
			if (currentlyVisible != null) {
				GenerateEnabled = currentlyVisible.parsingManager?.HasGeneratedContent ?? false;
				IncrementCommentEnabled = currentlyVisible.SupportsIncrementComment;
			}
		}

		/*
		private void ToggleShowLineNumbersClicked(object? sender, RoutedEventArgs e) {
			if (currentlyVisible != null) {
				currentlyVisible.textEditor.ShowLineNumbers = ShowLineNumbersButton.IsChecked;
			}
		}
		private void ToggleShowEndOfLineClicked(object? sender, RoutedEventArgs e) {
			if (currentlyVisible != null) {
				currentlyVisible.textEditor.Options.ShowEndOfLine = ShowEndOfLineButton.IsChecked;
			}
		}
		private void ToggleWrapLinesClicked(object? sender, RoutedEventArgs e) {
			if (currentlyVisible != null) {
				currentlyVisible.textEditor.WordWrap = WrapLinesButton.IsChecked;
			}
		}
		*/

		private void TabItem_MouseMove(object? sender, MouseEventArgs e) {
			if (e.Source is not TabItem tabItem) {
				return;
			}

			if (Mouse.PrimaryDevice.LeftButton == MouseButtonState.Pressed) {
				DragDrop.DoDragDrop(EditorTabControl, tabItem, DragDropEffects.All);

				e.Handled = true; // TODO Is this all we need?
			}
		}

		private void TabItem_Drop(object? sender, DragEventArgs e) {
			if (e.Source is TabItem tabItemTarget &&
				e.Data.GetData(typeof(TabItem)) is TabItem tabItemSource &&
				tabItemTarget.Content is EditorTabItem editorTabTarget &&
				tabItemSource.Content is EditorTabItem editorTabSource) {

				if (!tabItemTarget.Equals(tabItemSource) &&
					editorTabs.IndexOf(editorTabTarget) is int tabItemTargetIdx && tabItemTargetIdx != -1 &&
					editorTabs.IndexOf(editorTabSource) is int tabItemSourceIdx && tabItemSourceIdx != -1
					) {

					if (editorTabs.Remove(editorTabSource)) {
						editorTabs.Insert(tabItemTargetIdx, editorTabSource);
						EditorTabControl.SelectedIndex = tabItemTargetIdx;
					}

				}

				e.Handled = true;
			}
		}

		private void EditorTabPanel_Drop(object? sender, DragEventArgs e) {
			if (e.Source is TabPanel &&
				e.Data.GetData(typeof(TabItem)) is TabItem tabItemSource &&
				tabItemSource.Content is EditorTabItem editorTabSource &&
				editorTabs.IndexOf(editorTabSource) is int tabItemSourceIdx && tabItemSourceIdx != -1
				) {

				if (editorTabs.Remove(editorTabSource)) {
					editorTabs.Add(editorTabSource);
					EditorTabControl.SelectedIndex = editorTabs.Count - 1;
				}

				e.Handled = true;
			}
		}
		#endregion

		#region Editor Interfaces
		private void ShowDesignerButtonClick(object? sender, RoutedEventArgs e) {
			SharpDocumentEditor? current = GetCurrentEditor();
			if (current != null) {
				current.IsDesignerVisible = ShowDesignerButton.IsChecked;
				ToolbarShowDesignerButton.IsChecked = ShowDesignerButton.IsChecked;
			}
		}

		/*
		private void ShowDesignerDefaultButtonClick(object? sender, RoutedEventArgs e) {
			SharpEditorDetails.DesignerViewerOpenDefault = ShowDesignerDefaultButton.IsChecked;
		}
		*/

		private void ToolbarShowDesignerButtonClick(object? sender, RoutedEventArgs e) {
			SharpDocumentEditor? current = GetCurrentEditor();
			if (current != null) {
				current.IsDesignerVisible = ToolbarShowDesignerButton.IsChecked ?? false;
				ShowDesignerButton.IsChecked = ToolbarShowDesignerButton.IsChecked ?? false;
			}
		}

		private void SwitchDesignerLayoutClick(object? sender, RoutedEventArgs e) {
			SharpDocumentEditor? current = GetCurrentEditor();
			if (current != null) {
				current.DesignerPanesOrientation = current.DesignerPanesOrientation == Orientation.Horizontal ? Orientation.Vertical : Orientation.Horizontal;
			}
		}

		private void FloatDesignerClick(object? sender, RoutedEventArgs e) {
			SharpDocumentEditor? current = GetCurrentEditor();
			if (current != null) {
				current.FloatDesigner();
			}
		}

		private void ErrorStatusClick(object? sender, MouseButtonEventArgs e) {
			GetCurrentEditor()?.ErrorStatusClick(sender, e);
		}
		#endregion

		#region Application
		public bool IsClosed { get; private set; } = false;

		private void OnWindowClosing(object? sender, CancelEventArgs e) {
			Console.WriteLine("OnWindowClosing");
			controller.Exit(false);
		}

		private void ExitClick(object? sender, RoutedEventArgs e) {
			Console.WriteLine("ExitClick");
			controller.Exit(true);
		}

		protected override void OnClosing(CancelEventArgs e) {
			Console.WriteLine("OnClosing");
			foreach (EditorTabItem item in editorTabs.ToArray()) {
				if (item.Content.HasUnsavedProgress) {
					FocusOnEditor(item.Content);
				}
				if (!CloseTab(item)) {
					// Something has happened to prevent a tab from closing, so return, just in case
					e.Cancel = true;
					return;
				}
			}

			generator.Stop();

			base.OnClosing(e);

			IsClosed = !e.Cancel;
		}
		#endregion

		#region Files

		public SharpDocumentEditor OpenEditorDocument(string? filename, bool openTab) {
			SharpDocumentEditor editor;
			if (filename != null && GetOpenEditors().FirstOrDefault(e => e.CurrentFilePath != null && e.CurrentFilePath == filename) is SharpDocumentEditor alreadyOpen) {
				editor = alreadyOpen;
			}
			else if (filename != null && GetOpenEditors().Count() == 1 && GetOpenEditors().FirstOrDefault() is SharpDocumentEditor onlyOpen && onlyOpen.IsDisposable) {
				editor = onlyOpen;
				editor.Open(filename);
			}
			else {
				editor = new SharpDocumentEditor(filename) {
					IsDesignerVisible = GetCurrentEditor()?.IsDesignerVisible ?? SharpDataManager.Instance.DesignerViewerOpenDefault,
					DesignerPanesOrientation = GetCurrentEditor()?.DesignerPanesOrientation ?? Orientation.Horizontal
				};
				editor.RequestFocus += FocusEvent;

				editorTabs.Add(new EditorTabItem(this, editor));
			}

			if (openTab) {
				Activate();
				FocusOnEditor(editor);
			}

			return editor;
		}

		public SharpDocumentEditor OpenEmptyDocument(DocumentType documentType, bool intentional, bool openTab) {
			SharpDocumentEditor editor;
			if (GetOpenEditors().Count() == 1 && GetOpenEditors().FirstOrDefault() is SharpDocumentEditor onlyOpen && onlyOpen.IsDisposable) {
				editor = onlyOpen;
				editor.New(documentType, intentional);
			}
			else {
				editor = new SharpDocumentEditor(documentType, intentional) {
					IsDesignerVisible = GetCurrentEditor()?.IsDesignerVisible ?? SharpDataManager.Instance.DesignerViewerOpenDefault,
					DesignerPanesOrientation = GetCurrentEditor()?.DesignerPanesOrientation ?? Orientation.Horizontal
				};
				editor.RequestFocus += FocusEvent;
				
				editorTabs.Add(new EditorTabItem(this, editor));
			}

			if (openTab) {
				Activate();
				FocusOnEditor(editor);
			}

			return editor;
		}

		public bool CloseDocument(SharpDocumentEditor editor) {
			if (editor.HasUnsavedProgress) {

				string message;
				if (editor.CurrentFilePath != null) {
					message = $"There are unsaved changes for {Path.GetFileName(editor.CurrentFilePath)}";
				}
				else {
					message = "You have unsaved changes.";
				}

				MessageBoxResult result = MessageBox.Show(message + " Do you wish to save before closing?", "Unsaved Changes", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

				if (result == MessageBoxResult.Yes) {
					SaveEditorDocument(editor, false, false);
					if (editor.HasUnsavedProgress) { return false; } // If still unsaved somehow, return, just in case
				}
				else if (result == MessageBoxResult.Cancel) {
					return false;
				}
				// Else if result == No, then just continue to exit
			}

			editor.RequestFocus -= FocusEvent;

			// Safely close document stuff (e.g. managers, release document file, etc.)
			editor.Uninstall();

			return true;
		}

		private void NewFileClick(object? sender, RoutedEventArgs e) {
			/*
			if (unsavedProgress) {
				MessageBoxResult result = MessageBox.Show("You have unsaved changes. Do you wish to save?", "Unsaved Changes", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

				if (result == MessageBoxResult.Yes) {
					saveFileClick(this, null);
					if (unsavedProgress) { return; } // If still unsaved somehow, return, just in case
				}
				else if (result == MessageBoxResult.Cancel) {
					e.Handled = true;
					return;
				}
				// Else if result == No, then just continue
			}
			*/

			OpenEditorDocument(null, true);
		}

		private void NewBlankDocumentClick(object? sender, RoutedEventArgs e) {
			OpenEmptyDocument(DocumentType.UNKNOWN, true, true);
		}

		private void NewSharpConfigurationClick(object? sender, RoutedEventArgs e) {
			OpenEmptyDocument(DocumentType.SHARPCONFIG, true, true);
		}

		private void NewCardSubjectsClick(object? sender, RoutedEventArgs e) {
			OpenEmptyDocument(DocumentType.CARDSUBJECT, true, true);
		}

		private void NewCardConfigurationClick(object? sender, RoutedEventArgs e) {
			OpenEmptyDocument(DocumentType.CARDCONFIG, true, true);
		}

		private void NewMarkupClick(object? sender, RoutedEventArgs e) {
			OpenEmptyDocument(DocumentType.MARKUP, true, true);
		}

		private string? OpenFiles(string? initialDirectory, string? fileFilter = null) {
			OpenFileDialog dlg = new OpenFileDialog {
				CheckFileExists = true,
				Multiselect = true,
				Filter = fileFilter ?? SharpEditorFileInfo.AllSharpSheetsFileFilters,
				InitialDirectory = initialDirectory
			};
			if (dlg.ShowDialog() ?? false) {
				foreach (string filename in dlg.FileNames) {
					OpenEditorDocument(filename, true);
				}

				// Return final directory
				if (!string.IsNullOrWhiteSpace(dlg.FileName)) {
					return Path.GetDirectoryName(dlg.FileName);
				}
				else {
					return initialDirectory;
				}
			}

			return null; // No directory information to return
		}

		private void OpenFileClick(object? sender, RoutedEventArgs e) {
			string? fileDirectory = OpenFiles(SharpEditorPathInfo.LastFileDirectory);
			if (fileDirectory != null) {
				SharpEditorPathInfo.LastFileDirectory = fileDirectory;
			}
		}

		private void OpenTemplateFileClick(object? sender, RoutedEventArgs e) {
			string? fileDirectory = OpenFiles(SharpEditorPathInfo.LastTemplateDirectory, SharpEditorFileInfo.AllTemplateFileFilters);
			if (fileDirectory != null) {
				SharpEditorPathInfo.LastTemplateDirectory = fileDirectory;
			}
		}

		private string? SaveEditorDocument(SharpDocumentEditor editor, bool forceChooseFilename, bool template, string? initialDirectory = null) {
			string? filePath = editor.CurrentFilePath;
			string? chosenDirectory = null;
			if (forceChooseFilename || filePath == null) {

				string startingFilename = string.IsNullOrWhiteSpace(filePath) ? editor.parsingManager.GetCurrentDefaultFilename() : Path.GetFileName(filePath); // TODO Check if this requires extension?
				string? baseFileFilter = editor.parsingManager.GetCurrentFileFilter();
				string fullFileFilter = baseFileFilter != null ? SharpEditorFileInfo.GetFullFileFilters(baseFileFilter, true) : (template ? SharpEditorFileInfo.AllTemplateFileFilters : SharpEditorFileInfo.AllSharpSheetsFileFilters);

				SaveFileDialog dlg = new SaveFileDialog {
					AddExtension = true,
					ValidateNames = true,
					DefaultExt = editor.parsingManager.GetCurrentExtension(),
					Filter = fullFileFilter,
					FileName = startingFilename,
					InitialDirectory = initialDirectory ?? SharpEditorPathInfo.LastFileDirectory
				};

				if (dlg.ShowDialog() ?? false) {
					filePath = dlg.FileName;
					chosenDirectory = Path.GetDirectoryName(dlg.FileName);
				}
				else {
					return null;
				}
			}

			editor.Save(filePath);
			return chosenDirectory;
		}

		private void SaveFileClick(object? sender, RoutedEventArgs e) {
			if (GetCurrentEditor() is SharpDocumentEditor currentEditor) {
				string? fileDirectory = SaveEditorDocument(currentEditor, false, false);
				if (fileDirectory != null) {
					SharpEditorPathInfo.LastFileDirectory = fileDirectory;
				}
			}
		}

		private void SaveAsFileClick(object? sender, RoutedEventArgs e) {
			if (GetCurrentEditor() is SharpDocumentEditor currentEditor) {
				string? fileDirectory = SaveEditorDocument(currentEditor, true, false);
				if (fileDirectory != null) {
					SharpEditorPathInfo.LastFileDirectory = fileDirectory;
				}
			}
		}

		private void SaveAsTemplateClick(object? sender, RoutedEventArgs e) {
			//SaveEditorDocument(GetCurrentEditor(), true, true, SharpEditorFileInfo.GetTemplateDirectory());
			if (GetCurrentEditor() is SharpDocumentEditor currentEditor) {
				string? fileDirectory = SaveEditorDocument(currentEditor, true, true, SharpEditorPathInfo.LastTemplateDirectory);
				if (fileDirectory != null) {
					SharpEditorPathInfo.LastTemplateDirectory = fileDirectory;
				}
			}
		}

		private void SaveAllFileClick(object? sender, RoutedEventArgs e) {
			foreach (SharpDocumentEditor editor in GetOpenEditors().Where(d => d.HasUnsavedProgress)) {
				Console.WriteLine($"SaveEditorDocument for {editor.CurrentFilePath}");
				string? fileDirectory = SaveEditorDocument(editor, false, false);
				if (fileDirectory != null) {
					SharpEditorPathInfo.LastFileDirectory = fileDirectory;
				}
			}
		}

		/*
		private void WindowPreviewKeyDown(object? sender, KeyEventArgs e) {
			if (e.Key == Key.S && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control) {
				if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift) {
					SaveAllFileClick(sender, e);
				}
				else {
					SaveFileClick(sender, e);
				}
				e.Handled = true; // Don't actually enter this character
			}
		}
		*/

		private void WindowDrop(object? sender, DragEventArgs e) {
			if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
				if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0) {
					foreach (string filepath in files) {
						if (SharpEditorFileInfo.GetDocumentType(filepath) != DocumentType.UNKNOWN) {
							OpenEditorDocument(filepath, true);
						}
						else if (TemplateImports.IsKnownTemplateFileType(filepath)) {
							MessageBoxResult result = MessageBox.Show($"Import {System.IO.Path.GetFileName(filepath)} into templates?", "Import Templates", MessageBoxButton.YesNo, MessageBoxImage.Question);
							if(result == MessageBoxResult.Yes) {
								TemplateImports.ImportFile(filepath);
							}
						}
					}
				}
			}
		}

		private void ImportTemplateFiles(string? initialDirectory) {
			OpenFileDialog dlg = new OpenFileDialog {
				CheckFileExists = true,
				Multiselect = true,
				Filter = SharpEditorFileInfo.AllTemplateImportFileFilters,
				InitialDirectory = initialDirectory,
				Title = "Import"
			};
			if (dlg.ShowDialog() ?? false) {
				foreach (string filename in dlg.FileNames) {
					try {
						TemplateImports.ImportFile(filename);
					}
					catch(IOException e) {
						MessageBox.Show($"There was an error importing {filename}: {e.Message ?? "Unknown error"}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
					}
					catch (NotImplementedException e) {
						MessageBox.Show($"Cannot import {filename}: {e.Message ?? "Unknown error"}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
					}
				}
			}
		}

		private void ImportTemplateFilesClick(object? sender, RoutedEventArgs e) {
			ImportTemplateFiles(SharpEditorPathInfo.LastFileDirectory);
		}

		#endregion

		#region Generate

		private void Generate(IParser parser, string filename, string configuration, string source, string? fieldsSourcePath) {
			generator.Enqueue(parser, filename, configuration, source, fieldsSourcePath);
		}

		private void GenerateButtonClick(object? sender, RoutedEventArgs e) {
			SharpDocumentEditor? editor = GetCurrentEditor();

			if(editor is null) { return; }

			if (editor.CurrentFilePath == null) {
				MessageBoxResult result = MessageBox.Show("You must save this file before generating.\n\nDo you wish to save now?", "Unsaved Changes", MessageBoxButton.YesNo, MessageBoxImage.Question);
				if(result == MessageBoxResult.No) {
					return;
				}
				SaveAsFileClick(sender, e);
				if (editor.CurrentFilePath == null) {
					return;
				}
			}

			IParser? parser = editor.parsingManager.GetParsingState()?.Parser;
			if (parser == null) {
				MessageBox.Show("No parser set for this document.", "Cannot Generate", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}

			string source = SharpEditorPathInfo.GetDirectoryPathOrFallback(editor.CurrentFilePath);
			string initialGenerateFile = editor.CurrentGeneratePath != null ? Path.GetFileName(editor.CurrentGeneratePath) : Path.ChangeExtension(Path.GetFileName(editor.CurrentFilePath), ".pdf");
			string initialGenerateFileDir = editor.CurrentGeneratePath != null ? SharpEditorPathInfo.GetDirectoryPathOrFallback(editor.CurrentGeneratePath) : source;

			SaveFileDialog dlg = new SaveFileDialog {
				InitialDirectory = initialGenerateFileDir,
				FileName = initialGenerateFile,
				DefaultExt = ".pdf",
				Filter = "PDF Documents|*.pdf|All Files|*.*",
				Title = "Save Generated File As"
			};
			if (dlg.ShowDialog() ?? false) {
				editor.CurrentGeneratePath = dlg.FileName;
				Generate(parser, dlg.FileName, editor.Text, source, null);
			}
			else {
				return;
			}
		}

		private void GenerateAndPopulateFromButtonClick(object? sender, RoutedEventArgs e) {
			SharpDocumentEditor? editor = GetCurrentEditor();

			if(editor is null) { return; }

			if (editor.CurrentFilePath == null) {
				SaveAsFileClick(sender, e);
				if (editor.CurrentFilePath == null) {
					return;
				}
			}

			IParser? parser = editor.parsingManager.GetParsingState()?.Parser;
			if (parser == null) {
				MessageBox.Show("No parser set for this document.", "Cannot Generate", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}

			string source = SharpEditorPathInfo.GetDirectoryPathOrFallback(editor.CurrentFilePath);
			string initialGenerateFile = editor.CurrentGeneratePath != null ? Path.GetFileName(editor.CurrentGeneratePath) : Path.ChangeExtension(Path.GetFileName(editor.CurrentFilePath), ".pdf");
			string initialGenerateFileDir = editor.CurrentGeneratePath != null ? SharpEditorPathInfo.GetDirectoryPathOrFallback(editor.CurrentGeneratePath) : source;
			string initialFieldFile = editor.CurrentFieldSourcePath != null ? Path.GetFileName(editor.CurrentFieldSourcePath) : "";
			string initialFieldFileDir = editor.CurrentFieldSourcePath != null ? SharpEditorPathInfo.GetDirectoryPathOrFallback(editor.CurrentFieldSourcePath) : source;

			OpenFileDialog fieldsSourceDlg = new OpenFileDialog {
				InitialDirectory = initialFieldFileDir,
				FileName = initialFieldFile,
				CheckFileExists = true,
				Filter = "PDF Document|*.pdf|All Files|*.*",
				Multiselect = false,
				Title = "Choose Field Source File"
			};
			if (fieldsSourceDlg.ShowDialog() ?? false) {
				editor.CurrentFieldSourcePath = string.IsNullOrEmpty(fieldsSourceDlg.FileName) ? null : fieldsSourceDlg.FileName;
			}
			else {
				return;
			}

			SaveFileDialog dlg = new SaveFileDialog {
				InitialDirectory = initialGenerateFileDir,
				FileName = initialGenerateFile,
				DefaultExt = ".pdf",
				Filter = "PDF Documents|*.pdf|All Files|*.*",
				Title = "Save Generated File As"
			};
			if (dlg.ShowDialog() ?? false) {
				editor.CurrentGeneratePath = dlg.FileName;
				Generate(parser, dlg.FileName, editor.Text, source, editor.CurrentFieldSourcePath);
			}
			else {
				return;
			}
		}

		private void GenerateAndPopulateButtonClick(object? sender, RoutedEventArgs e) {
			SharpDocumentEditor? editor = GetCurrentEditor();

			if (editor is null) { return; }

			if (editor.CurrentFilePath == null) {
				SaveAsFileClick(sender, e);
				if (editor.CurrentFilePath == null) {
					return;
				}
			}

			IParser? parser = editor.parsingManager.GetParsingState()?.Parser;
			if (parser == null) {
				MessageBox.Show("No parser set for this document.", "Cannot Generate", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}

			string source = SharpEditorPathInfo.GetDirectoryPathOrFallback(editor.CurrentFilePath);
			string initialGenerateFile = editor.CurrentGeneratePath != null ? Path.GetFileName(editor.CurrentGeneratePath) : Path.ChangeExtension(Path.GetFileName(editor.CurrentFilePath), ".pdf");
			string initialGenerateFileDir = editor.CurrentGeneratePath != null ? SharpEditorPathInfo.GetDirectoryPathOrFallback(editor.CurrentGeneratePath) : source;
			string initialFieldFileDir = editor.CurrentFieldSourcePath != null ? SharpEditorPathInfo.GetDirectoryPathOrFallback(editor.CurrentFieldSourcePath) : source;

			if (editor.CurrentFieldSourcePath == null) {
				OpenFileDialog fieldsSourceDlg = new OpenFileDialog {
					InitialDirectory = initialFieldFileDir,
					CheckFileExists = true,
					Filter = "PDF Document|*.pdf|All Files|*.*",
					Multiselect = false,
					Title = "Choose Field Source File"
				};
				if (fieldsSourceDlg.ShowDialog() ?? false) {
					editor.CurrentFieldSourcePath = string.IsNullOrEmpty(fieldsSourceDlg.FileName) ? null : fieldsSourceDlg.FileName;
				}
				else {
					return;
				}
			}

			SaveFileDialog dlg = new SaveFileDialog {
				InitialDirectory = initialGenerateFileDir,
				FileName = initialGenerateFile,
				DefaultExt = ".pdf",
				Filter = "PDF Documents|*.pdf|All Files|*.*",
				Title = "Save Generated File As"
			};
			if (dlg.ShowDialog() ?? false) {
				editor.CurrentGeneratePath = dlg.FileName;
				Generate(parser, dlg.FileName, editor.Text, source, editor.CurrentFieldSourcePath);
			}
			else {
				return;
			}
		}

		#endregion

		#region Template Notification

		// TODO This should be moved to SharpDataManager
		private bool _templateAlertEnabled = false;
		public bool TemplateAlertEnabled {
			get {
				return _templateAlertEnabled;
			}
			set {
				_templateAlertEnabled = value;
				TemplateErrorHeaderIcon.Dispatcher.Invoke(() => {
					TemplateErrorHeaderIcon.IsEnabled = _templateAlertEnabled;
					TemplateErrorHeaderIcon.Visibility = _templateAlertEnabled ? Visibility.Visible : Visibility.Collapsed;
				});
				TemplateErrorMenuIcon.Dispatcher.Invoke(() => {
					TemplateErrorMenuIcon.IsEnabled = _templateAlertEnabled;
					TemplateErrorMenuIcon.Visibility = _templateAlertEnabled ? Visibility.Visible : Visibility.Collapsed;
				});
			}
		}

		#endregion

		#region Template Files List

		private void InitialiseTemplateFileList() {
			TemplatesMenuItem.ItemsSource = GetTemplateMenuItems();

			SharpEditorRegistries.TemplateRegistry.RegistryChanged += TemplateFileListChanged;
		}

		private void TemplateFileListChanged() {
			TemplatesMenuItem.Dispatcher.Invoke(delegate {
				TemplatesMenuItem.ItemsSource = GetTemplateMenuItems();
			});
		}

		private List<TemplateMenuItem> GetTemplateMenuItems() {
			return SharpEditorRegistries.TemplateRegistry.Registered
				.Select(r => new TemplateMenuItem(string.Join(".", r.Value), r.Key))
				.OrderBy(t => t.Name)
				.ToList();
		}

		private void TemplateMenuItemClick(object? sender, RoutedEventArgs args) {
			if (sender is MenuItem menuItem && menuItem.DataContext is TemplateMenuItem templateData) {
				Console.WriteLine(templateData.Name + " -> " + templateData.Path);
				try {
					DocumentType templateType = SharpEditorFileInfo.GetDocumentType(templateData.Path);
					SharpDocumentEditor newEditor = OpenEmptyDocument(templateType, true, true);
					newEditor.textEditor.Text = TemplateRegistry.GetTemplateContent(templateData.Path);
				}
				catch(Exception e) {
					MessageBox.Show($"There was an error opening template {templateData.Name}: {e.Message ?? "Unknown error"}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				}
			}
		}

		#endregion

		#region GenerateEnabled

		public static readonly DependencyProperty GenerateEnabledProperty =
			DependencyProperty.Register("GenerateEnabled", typeof(bool),
			typeof(SharpEditorWindow), new UIPropertyMetadata(true, OnGenerateEnabledChanged));
		public event EventHandler? GenerateEnabledChanged;

		public bool GenerateEnabled {
			get {
				return (bool)GetValue(GenerateEnabledProperty);
			}
			set {
				SetValue(GenerateEnabledProperty, value);
			}
		}

		private static void OnGenerateEnabledChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e) {
			if (sender is SharpEditorWindow window) {
				window.GenerateEnabledChanged?.Invoke(sender, EventArgs.Empty);
			}
		}

		#endregion

		#region IncrementCommentEnabled

		public static readonly DependencyProperty IncrementCommentEnabledProperty =
			DependencyProperty.Register("IncrementCommentEnabled", typeof(bool),
			typeof(SharpEditorWindow), new UIPropertyMetadata(true, OnIncrementCommentEnabledChanged));
		public event EventHandler? IncrementCommentEnabledChanged;

		public bool IncrementCommentEnabled {
			get {
				return (bool)GetValue(IncrementCommentEnabledProperty);
			}
			set {
				SetValue(IncrementCommentEnabledProperty, value);
			}
		}

		private static void OnIncrementCommentEnabledChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e) {
			if (sender is SharpEditorWindow window) {
				window.IncrementCommentEnabledChanged?.Invoke(sender, EventArgs.Empty);
			}
		}

		#endregion

		#region SavePossible

		public static readonly DependencyProperty SavePossibleProperty =
			DependencyProperty.Register("SavePossible", typeof(bool),
			typeof(SharpEditorWindow), new UIPropertyMetadata(true, OnSavePossibleChanged));
		public event EventHandler? SavePossibleChanged;

		public bool SavePossible {
			get {
				return (bool)GetValue(SavePossibleProperty);
			}
			set {
				SetValue(SavePossibleProperty, value);
			}
		}

		private static void OnSavePossibleChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e) {
			if (sender is SharpEditorWindow window) {
				window.SavePossibleChanged?.Invoke(sender, EventArgs.Empty);
			}
		}

		#endregion

		#region Commands

		private void OpenExecuted(object? sender, ExecutedRoutedEventArgs e) {
			OpenFileClick(sender, e);
		}

		private void OpenCanExecute(object? sender, CanExecuteRoutedEventArgs e) {
			e.CanExecute = true;
		}

		private void CloseExecuted(object? sender, ExecutedRoutedEventArgs e) {
			if (editorTabs.Count > 0) {
				CloseTab(editorTabs[EditorTabControl.SelectedIndex]);
			}
		}

		private void CloseCanExecute(object? sender, CanExecuteRoutedEventArgs e) {
			e.CanExecute = HasTabOpen;
		}

		private void SaveExecuted(object? sender, ExecutedRoutedEventArgs e) {
			SaveFileClick(sender, e);
		}

		private void SaveCanExecute(object? sender, CanExecuteRoutedEventArgs e) {
			e.CanExecute = HasTabOpen;
		}

		private void SaveAsExecuted(object? sender, ExecutedRoutedEventArgs e) {
			SaveAsFileClick(sender, e);
		}

		private void SaveAsCanExecute(object? sender, CanExecuteRoutedEventArgs e) {
			e.CanExecute = HasTabOpen;
		}

		private void HelpExecuted(object? sender, ExecutedRoutedEventArgs e) {
			controller.ActivateDocumentationWindow();
		}

		private void HelpCanExecute(object? sender, CanExecuteRoutedEventArgs e) {
			e.CanExecute = true;
		}

		public static RoutedUICommand SaveAllCommand = new RoutedUICommand(
			"Save All",
			"SaveAll",
			typeof(SharpEditorWindow),
			new InputGestureCollection() {
				new KeyGesture(Key.S, ModifierKeys.Control | ModifierKeys.Shift)
			});

		private void SaveAllCommandExecuted(object? sender, ExecutedRoutedEventArgs e) {
			SaveAllFileClick(sender, e);
		}

		private void SaveAllCommandCanExecute(object? sender, CanExecuteRoutedEventArgs e) {
			e.CanExecute = NumTabs > 0;
		}

		public static RoutedUICommand GenerateCommand = new RoutedUICommand(
			"Generate",
			"Generate",
			typeof(SharpEditorWindow),
			new InputGestureCollection() {
				new KeyGesture(Key.G, ModifierKeys.Control)
			});

		private void GenerateCommandExecuted(object? sender, ExecutedRoutedEventArgs e) {
			GenerateButtonClick(sender, e);
		}

		private void GenerateCommandCanExecute(object? sender, CanExecuteRoutedEventArgs e) {
			e.CanExecute = GenerateEnabled;
		}

		public static RoutedUICommand GeneratePopulateCommand = new RoutedUICommand(
			"Generate and Populate",
			"GeneratePopulate",
			typeof(SharpEditorWindow),
			new InputGestureCollection() {
				new KeyGesture(Key.G, ModifierKeys.Control | ModifierKeys.Shift)
			});

		private void GeneratePopulateCommandExecuted(object? sender, ExecutedRoutedEventArgs e) {
			GenerateAndPopulateButtonClick(sender, e);
		}

		private void GeneratePopulateCommandCanExecute(object? sender, CanExecuteRoutedEventArgs e) {
			e.CanExecute = GenerateEnabled;
		}

		public static RoutedUICommand GeneratePopulateFromCommand = new RoutedUICommand(
			"Generate and Populate From",
			"GeneratePopulateFrom",
			typeof(SharpEditorWindow)
			);

		private void GeneratePopulateFromCommandExecuted(object? sender, ExecutedRoutedEventArgs e) {
			GenerateAndPopulateFromButtonClick(sender, e);
		}

		private void GeneratePopulateFromCommandCanExecute(object? sender, CanExecuteRoutedEventArgs e) {
			e.CanExecute = GenerateEnabled;
		}

		public static RoutedUICommand AddCommentCommand = new RoutedUICommand(
			"Add Comment",
			"AddComment",
			typeof(SharpEditorWindow)
			);

		private void AddCommentCommandExecuted(object? sender, ExecutedRoutedEventArgs e) {
			GetCurrentEditor()?.IncrementComment();
		}

		private void AddCommentCommandCanExecute(object? sender, CanExecuteRoutedEventArgs e) {
			e.CanExecute = IncrementCommentEnabled;
		}

		public static RoutedUICommand RemoveCommentCommand = new RoutedUICommand(
			"Remove Comment",
			"RemoveComment",
			typeof(SharpEditorWindow)
			);

		private void RemoveCommentCommandExecuted(object? sender, ExecutedRoutedEventArgs e) {
			GetCurrentEditor()?.DecrementComment();
		}

		private void RemoveCommentCommandCanExecute(object? sender, CanExecuteRoutedEventArgs e) {
			e.CanExecute = IncrementCommentEnabled;
		}

		#endregion

		#region Settings

		private void ShowSettingsWindowClick(object? sender, RoutedEventArgs e) {
			controller.ActivateSettingsWindow();
		}

		#endregion

		/*
		private void AddPresetButton_Click(object? sender, RoutedEventArgs e) {
			var addButton = sender as FrameworkElement;
			if (addButton != null) {
				addButton.ContextMenu.IsOpen = true;
			}
		}
		*/

	}

	public class TemplateMenuItem {
		
		public string Name { get; }
		public string Path { get; }

		public TemplateMenuItem(string name, string path) {
			Name = name;
			Path = path;
		}

	}

}