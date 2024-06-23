using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SharpEditorAvalonia.DataManagers;
using SharpEditorAvalonia.Program;
using SharpEditorAvalonia.Registries;
using SharpSheets.Parsing;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace SharpEditorAvalonia.Windows {

	public partial class SharpEditorWindow : Window {

		public readonly AppController controller;
		private readonly ObservableCollection<EditorTabItem> editorTabs;

		// TODO Progress tracker for Generator?
		private readonly Generator generator;

		public static SharpEditorWindow? Instance { get; private set; } = null;

		public SharpDataManager DataManager { get; }

		/// <summary>
		/// Called whenever any changes occur to the open list of files, including changing the currently
		/// selected file tab.
		/// </summary>
		public event EventHandler? EditorStateChanged;
		/// <summary>
		/// Called whenever the cursor changes position, selection changes, or the editor focus changes.
		/// </summary>
		public event EventHandler? EditorCursorChanged;

		public SharpEditorWindow(AppController controller) {
			this.controller = controller;
			// Create list of tab items
			editorTabs = new ObservableCollection<EditorTabItem>();
			DataManager = SharpDataManager.Instance;
			InitialiseCommands();
			
			InitializeComponent();
			this.DataContext = this;

			InitialiseTemplateFileList();
			InitialiseEditorInterfaces();
			
			GenerateEnabled = false;
			//SavePossible = false;
			IncrementCommentEnabled = false;

			generator = new Generator(Dispatcher.UIThread) { OpenOnGenerate = SharpDataManager.Instance.OpenOnGenerate };
			SharpDataManager.Instance.OpenOnGenerateChanged += delegate { generator.OpenOnGenerate = SharpDataManager.Instance.OpenOnGenerate; };
			generator.Start();

			// Assign tab items collection
			EditorTabControl.ItemsSource = editorTabs;

			EditorTabControl.SelectionChanged += TabSelectionChanged;

			EditorStateChanged += (o, e) => { EditorCursorChanged?.Invoke(o, e); };

			// TODO Should this be in AppController?
			OpenEmptyDocument(DocumentType.SHARPCONFIG, false, true);
			
			if (Instance == null) { Instance = this; } // This seems a little suspect...
		}

		public SharpEditorWindow() {
			// Dummy constructor to resolve this issue:
			// Avalonia warning AVLN:0005: XAML resource "avares://SystemTrayMenu/UserInterface/Menu.axaml" won't be reachable via runtime loader, as no public constructor was found
			// See: https://github.com/AvaloniaUI/Avalonia/issues/11312
			throw new NotImplementedException();
		}

		#region Window Activation Handlers

		public void ShowDocumentationMenuClick() {
			controller.ActivateDocumentationWindow();
		}

		public void ShowTemplateErrorsMenuClick() {
			controller.ActivateTemplateErrorWindow();
		}

		public void ShowSettingsWindowClick() {
			controller.ActivateSettingsWindow();
		}

		#endregion Window Activation Handlers

		#region Tab Handling

		public bool HasTabOpen { get { return EditorTabControl?.SelectedIndex is int index && index != -1; } }
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
					EditorStateChanged?.Invoke(this, new EventArgs());
				}
			}
		}

		private void FocusEvent(object? sender, DocumentEditorEventArgs e) {
			FocusOnEditor(e.Editor);
		}

		public async Task<bool> CloseTab(EditorTabItem item) {
			if (await CloseDocument(item.Content)) {
				editorTabs.Remove(item);
				EditorStateChanged?.Invoke(this, new EventArgs());
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
				currentlyVisible.textEditor.TextArea.SelectionChanged -= OnTextEditorCursorChanged;
				currentlyVisible.textEditor.TextArea.Caret.PositionChanged -= OnTextEditorCursorChanged;
				currentlyVisible.textEditor.DocumentChanged -= OnTextEditorCursorChanged;
				currentlyVisible = null;
				UpdateTitle(null);
			}

			if (EditorTabControl.SelectedIndex != -1) {
				currentlyVisible = GetCurrentEditor();
				if (currentlyVisible != null) {
					currentlyVisible.UpdateHeader += UpdateTitle;
					currentlyVisible.DocumentTypeChanged += OnDocumentTypeChanged;
					currentlyVisible.textEditor.TextArea.SelectionChanged += OnTextEditorCursorChanged;
					currentlyVisible.textEditor.TextArea.Caret.PositionChanged += OnTextEditorCursorChanged;
					currentlyVisible.textEditor.DocumentChanged += OnTextEditorCursorChanged;
					UpdateTitle(currentlyVisible.Header);

					DesignerVisible = currentlyVisible.IsDesignerVisible;
				}
			}

			//SavePossible = EditorTabControl.SelectedIndex != -1;
			// Set GenerateEnabled here
			GenerateEnabled = currentlyVisible?.parsingManager?.HasGeneratedContent ?? false;
			IncrementCommentEnabled = currentlyVisible?.SupportsIncrementComment ?? false;
			EditorStateChanged?.Invoke(this, new EventArgs());
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

		private void OnDocumentTypeChanged(object? sender, DocumentEditorEventArgs e) {
			if (currentlyVisible != null) {
				GenerateEnabled = currentlyVisible.parsingManager?.HasGeneratedContent ?? false;
				IncrementCommentEnabled = currentlyVisible.SupportsIncrementComment;
				EditorStateChanged?.Invoke(this, new EventArgs());
			}
		}

		private void OnTextEditorCursorChanged(object? sender, EventArgs e) {
			EditorCursorChanged?.Invoke(sender, e);
		}

		// TODO Implement draggable tab items
		/*
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
		*/
		#endregion Tab Handling

		#region Editor Interfaces
		private void InitialiseEditorInterfaces() {
			DesignerVisibleChanged += UpdateCurrentEditorDesignerVisible;
		}

		private void UpdateCurrentEditorDesignerVisible(object? sender, EventArgs e) {
			SharpDocumentEditor? current = GetCurrentEditor();
			if (current != null) {
				current.IsDesignerVisible = DesignerVisible;
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
		#endregion Editor Interfaces

		#region Application
		public bool IsClosed { get; private set; } = false;

		private void OnWindowClosing(object? sender, WindowClosingEventArgs e) {
			Console.WriteLine("OnWindowClosing");
			controller.Exit(false);
		}

		private void ExitClick(object? sender, RoutedEventArgs e) {
			Console.WriteLine("ExitClick");
			controller.Exit(true);
		}

		protected override async void OnClosing(WindowClosingEventArgs e) {
			Console.WriteLine("OnClosing");
			foreach (EditorTabItem item in editorTabs.ToArray()) {
				if (item.Content.HasUnsavedProgress) {
					FocusOnEditor(item.Content);
				}
				if (!(await CloseTab(item))) {
					// Something has happened to prevent a tab from closing, so return, just in case
					e.Cancel = true;
					return;
				}
			}

			generator.Stop();

			base.OnClosing(e);

			IsClosed = !e.Cancel;
		}
		#endregion Application

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

			EditorStateChanged?.Invoke(this, new EventArgs());

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

			EditorStateChanged?.Invoke(this, new EventArgs());

			return editor;
		}

		public async Task<bool> CloseDocument(SharpDocumentEditor editor) {
			if (editor.HasUnsavedProgress) {

				string message;
				if (editor.CurrentFilePath != null) {
					message = $"There are unsaved changes for {Path.GetFileName(editor.CurrentFilePath)}";
				}
				else {
					message = "You have unsaved changes.";
				}

				MessageBoxResult result = await MessageBoxes.Show(message + " Do you wish to save before closing?", "Unsaved Changes", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

				if (result == MessageBoxResult.Yes) {
					await SaveEditorDocument(editor, false, false);
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

			EditorStateChanged?.Invoke(this, new EventArgs());

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

		private async Task<string?> OpenFiles(string? initialDirectory, string? fileFilter = null) {

			string[] filenames = await this.OpenFilePicker(
				title: "Open",
				allowMultiple: true,
				startingLocation: initialDirectory,
				filter: fileFilter ?? SharpEditorFileInfo.AllSharpSheetsFileFilters);

			// TODO Check files exist?

			if (filenames.Length > 0) {
				foreach (string filename in filenames) {
					OpenEditorDocument(filename, true);
				}

				// Return final directory
				if (!string.IsNullOrWhiteSpace(filenames[0])) {
					return Path.GetDirectoryName(filenames[0]);
				}
				else {
					return initialDirectory;
				}
			}

			return null; // No directory information to return
		}

		private async void OpenFileClick(object? sender, RoutedEventArgs e) {
			string? fileDirectory = await OpenFiles(SharpEditorPathInfo.LastFileDirectory);
			if (fileDirectory != null) {
				SharpEditorPathInfo.LastFileDirectory = fileDirectory;
			}
		}

		private async void OpenTemplateFileClick(object? sender, RoutedEventArgs e) {
			string? fileDirectory = await OpenFiles(SharpEditorPathInfo.LastTemplateDirectory, SharpEditorFileInfo.AllTemplateFileFilters);
			if (fileDirectory != null) {
				SharpEditorPathInfo.LastTemplateDirectory = fileDirectory;
			}
		}

		private async Task<string?> SaveEditorDocument(SharpDocumentEditor editor, bool forceChooseFilename, bool template, string? initialDirectory = null) {
			string? filePath = editor.CurrentFilePath;
			string? chosenDirectory = null;
			if (forceChooseFilename || filePath == null) {

				string startingFilename = string.IsNullOrWhiteSpace(filePath) ? editor.parsingManager.GetCurrentDefaultFilename() : Path.GetFileName(filePath); // TODO Check if this requires extension?
				string? baseFileFilter = editor.parsingManager.GetCurrentFileFilter();
				string fullFileFilter = baseFileFilter != null ? SharpEditorFileInfo.GetFullFileFilters(baseFileFilter, true) : (template ? SharpEditorFileInfo.AllTemplateFileFilters : SharpEditorFileInfo.AllSharpSheetsFileFilters);

				string? filename = await this.SaveFilePicker(
					title: "Save As",
					suggestedName: startingFilename,
					defaultExtension: editor.parsingManager.GetCurrentExtension(),
					startingLocation: initialDirectory ?? SharpEditorPathInfo.LastFileDirectory,
					filter: fullFileFilter,
					showOverwritePrompt: true
					// AddExtension = true
					// ValidateNames = true
					);

				if (!string.IsNullOrWhiteSpace(filename)) {
					filePath = filename;
					chosenDirectory = Path.GetDirectoryName(filename);
				}
				else {
					return null;
				}
			}

			editor.Save(filePath);

			EditorStateChanged?.Invoke(this, new EventArgs());

			return chosenDirectory;
		}

		private async void SaveFileClick(object? sender, RoutedEventArgs e) {
			if (GetCurrentEditor() is SharpDocumentEditor currentEditor) {
				string? fileDirectory = await SaveEditorDocument(currentEditor, false, false);
				if (fileDirectory != null) {
					SharpEditorPathInfo.LastFileDirectory = fileDirectory;
				}
			}
		}

		private async void SaveAsFileClick(object? sender, RoutedEventArgs e) {
			if (GetCurrentEditor() is SharpDocumentEditor currentEditor) {
				string? fileDirectory = await SaveEditorDocument(currentEditor, true, false);
				if (fileDirectory != null) {
					SharpEditorPathInfo.LastFileDirectory = fileDirectory;
				}
			}
		}

		private async void SaveAsTemplateClick(object? sender, RoutedEventArgs e) {
			//SaveEditorDocument(GetCurrentEditor(), true, true, SharpEditorFileInfo.GetTemplateDirectory());
			if (GetCurrentEditor() is SharpDocumentEditor currentEditor) {
				string? fileDirectory = await SaveEditorDocument(currentEditor, true, true, SharpEditorPathInfo.LastTemplateDirectory);
				if (fileDirectory != null) {
					SharpEditorPathInfo.LastTemplateDirectory = fileDirectory;
				}
			}
		}

		private async void SaveAllFileClick(object? sender, RoutedEventArgs e) {
			foreach (SharpDocumentEditor editor in GetOpenEditors().Where(d => d.HasUnsavedProgress)) {
				Console.WriteLine($"SaveEditorDocument for {editor.CurrentFilePath}");
				string? fileDirectory = await SaveEditorDocument(editor, false, false);
				if (fileDirectory != null) {
					SharpEditorPathInfo.LastFileDirectory = fileDirectory;
				}
			}
		}

		// TODO Implement window drag and drop
		/*
		private void WindowDrop(object? sender, DragEventArgs e) {
			if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
				if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0) {
					foreach (string filepath in files) {
						if (SharpEditorFileInfo.GetDocumentType(filepath) != DocumentType.UNKNOWN) {
							OpenEditorDocument(filepath, true);
						}
						else if (TemplateImports.IsKnownTemplateFileType(filepath)) {
							MessageBoxResult result = MessageBox.Show($"Import {System.IO.Path.GetFileName(filepath)} into templates?", "Import Templates", MessageBoxButton.YesNo, MessageBoxImage.Question);
							if (result == MessageBoxResult.Yes) {
								ImportTemplateFile(filepath);
							}
						}
					}
				}
			}
		}
		*/

		private async void ImportTemplateFiles(string? initialDirectory) {
			string[] filenames = await this.OpenFilePicker(
				title: "Import",
				allowMultiple: true,
				startingLocation: initialDirectory,
				filter: SharpEditorFileInfo.AllTemplateImportFileFilters
				// TODO CheckFileExists = true ????
				);

			if (filenames.Length > 0) {
				foreach (string filename in filenames) {
					ImportTemplateFile(filename);
				}
			}
		}

		private void ImportTemplateFilesClick(object? sender, RoutedEventArgs e) {
			ImportTemplateFiles(SharpEditorPathInfo.LastFileDirectory);
		}

		private void ImportTemplateFile(string filepath) {
			try {
				TemplateImports.ImportFile(filepath);
			}
			catch (IOException e) {
				_ = MessageBoxes.Show($"There was an error importing {filepath}: {e.Message ?? "Unknown error"}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
			}
			catch (NotImplementedException e) {
				_ = MessageBoxes.Show($"Cannot import {filepath}: {e.Message ?? "Unknown error"}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}

		#endregion Files

		#region Generate

		private void Generate(IParser parser, string filename, string configuration, string source, string? fieldsSourcePath) {
			generator.Enqueue(parser, filename, configuration, source, fieldsSourcePath);
		}

		private async void GenerateButtonClick(object? sender, RoutedEventArgs e) {
			SharpDocumentEditor? editor = GetCurrentEditor();

			if (editor is null) { return; }

			if (editor.CurrentFilePath == null) {
				MessageBoxResult result = await MessageBoxes.Show("You must save this file before generating.\n\nDo you wish to save now?", "Unsaved Changes", MessageBoxButton.YesNo, MessageBoxImage.Question);
				if (result == MessageBoxResult.No) {
					return;
				}
				SaveAsFileClick(sender, e);
				if (editor.CurrentFilePath == null) {
					return;
				}
			}

			IParser? parser = editor.parsingManager.GetParsingState()?.Parser;
			if (parser == null) {
				await MessageBoxes.Show("No parser set for this document.", "Cannot Generate", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}

			string source = SharpEditorPathInfo.GetDirectoryPathOrFallback(editor.CurrentFilePath);
			string initialGenerateFile = editor.CurrentGeneratePath != null ? Path.GetFileName(editor.CurrentGeneratePath) : Path.ChangeExtension(Path.GetFileName(editor.CurrentFilePath), ".pdf");
			string initialGenerateFileDir = editor.CurrentGeneratePath != null ? SharpEditorPathInfo.GetDirectoryPathOrFallback(editor.CurrentGeneratePath) : source;

			string? filename = await this.SaveFilePicker(
				title: "Save Generated File As",
				suggestedName: initialGenerateFile,
				defaultExtension: ".pdf",
				startingLocation: initialGenerateFileDir,
				filter: "PDF Documents|*.pdf|All Files|*.*",
				showOverwritePrompt: true
				);

			if (filename is not null) {
				editor.CurrentGeneratePath = filename;
				Generate(parser, filename, editor.Text, source, null);
			}
			else {
				return;
			}
		}

		private async void GenerateAndPopulate(bool forceChooseSource) {
			SharpDocumentEditor? editor = GetCurrentEditor();

			if (editor is null) { return; }

			if (editor.CurrentFilePath == null) {
				SaveAsFileClick(this, new RoutedEventArgs());
				if (editor.CurrentFilePath == null) {
					return;
				}
			}

			IParser? parser = editor.parsingManager.GetParsingState()?.Parser;
			if (parser == null) {
				await MessageBoxes.Show("No parser set for this document.", "Cannot Generate", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}

			string source = SharpEditorPathInfo.GetDirectoryPathOrFallback(editor.CurrentFilePath);
			string initialGenerateFile = editor.CurrentGeneratePath != null ? Path.GetFileName(editor.CurrentGeneratePath) : Path.ChangeExtension(Path.GetFileName(editor.CurrentFilePath), ".pdf");
			string initialGenerateFileDir = editor.CurrentGeneratePath != null ? SharpEditorPathInfo.GetDirectoryPathOrFallback(editor.CurrentGeneratePath) : source;
			string initialFieldFileDir = editor.CurrentFieldSourcePath != null ? SharpEditorPathInfo.GetDirectoryPathOrFallback(editor.CurrentFieldSourcePath) : source;

			if (forceChooseSource || editor.CurrentFieldSourcePath == null) {
				string[] filenames = await this.OpenFilePicker(
					title: "Choose Field Source File",
					allowMultiple: false,
					startingLocation: initialFieldFileDir,
					filter: "PDF Document|*.pdf|All Files|*.*"
					// FileName = initialFieldFile ???
					// CheckFileExists = true
					);

				if (filenames.Length > 0 && filenames[0] is string fieldsSourceFilename) {
					editor.CurrentFieldSourcePath = string.IsNullOrEmpty(fieldsSourceFilename) ? null : fieldsSourceFilename;
				}
				else {
					return;
				}
			}

			string? filename = await this.SaveFilePicker(
				title: "Save Generated File As",
				suggestedName: initialGenerateFile,
				defaultExtension: ".pdf",
				startingLocation: initialGenerateFileDir,
				filter: "PDF Documents|*.pdf|All Files|*.*",
				showOverwritePrompt: true
				);

			if (!string.IsNullOrEmpty(filename)) {
				editor.CurrentGeneratePath = filename;
				Generate(parser, filename, editor.Text, source, editor.CurrentFieldSourcePath);
			}
			else {
				return;
			}
		}

		private void GenerateAndPopulateFromButtonClick(object? sender, RoutedEventArgs e) {
			GenerateAndPopulate(true);
		}

		private void GenerateAndPopulateButtonClick(object? sender, RoutedEventArgs e) {
			GenerateAndPopulate(false);
		}

		#endregion Generate

		#region Template Files List

		private void InitialiseTemplateFileList() {
			TemplatesMenuItem.ItemsSource = GetTemplateMenuItems();

			SharpEditorRegistries.TemplateRegistry.RegistryChanged += TemplateFileListChanged;
		}

		private void TemplateFileListChanged() {
			Dispatcher.UIThread.Invoke(delegate {
				TemplatesMenuItem.ItemsSource = GetTemplateMenuItems();
			});
		}

		private void TemplateMenuItemExecute(TemplateMenuItem templateData) {
			if (templateData.Path is not null) {
				Console.WriteLine(templateData.Name + " -> " + templateData.Path);
				try {
					DocumentType templateType = SharpEditorFileInfo.GetDocumentType(templateData.Path);
					SharpDocumentEditor newEditor = OpenEmptyDocument(templateType, true, true);
					newEditor.textEditor.Text = TemplateRegistry.GetTemplateContent(templateData.Path);
				}
				catch (Exception e) {
					_ = MessageBoxes.Show($"There was an error opening template {templateData.Name}: {e.Message ?? "Unknown error"}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				}
			}
		}

		private ObservableCollection<TemplateMenuItem> GetTemplateMenuItems() {
			ObservableCollection<TemplateMenuItem> items = new ObservableCollection<TemplateMenuItem>();

			foreach ((string path, string[] parts) in SharpEditorRegistries.TemplateRegistry.Registered) {
				TemplateMenuItem? parent = null;
				ObservableCollection<TemplateMenuItem> levelItems = items;
				for (int i = 0; i < parts.Length; i++) {
					TemplateMenuItem menuItem;
					if (levelItems.FirstOrDefault(m => m.Name == parts[i]) is TemplateMenuItem existing) {
						menuItem = existing;
					}
					else {
						if (parent is not null && parent.Path is not null) {
							TemplateMenuItem defaultItem = new TemplateMenuItem("(Default)", TemplateMenuItemExecute) { Path = parent.Path };
							parent.Path = null;
							levelItems.Add(defaultItem);
						}
						menuItem = new TemplateMenuItem(parts[i], TemplateMenuItemExecute);
						levelItems.Add(menuItem);
					}
					parent = menuItem;
					levelItems = menuItem.MenuItems;
					if (i == parts.Length - 1) { // If we're at the last one
						if (menuItem.MenuItems.Count > 0) {
							TemplateMenuItem defaultItem = new TemplateMenuItem("(Default)", TemplateMenuItemExecute) { Path = path };
							menuItem.MenuItems.Add(defaultItem);
						}
						else {
							menuItem.Path = path;
						}
					}
				}
			}

			SortTemplateMenuItems(items);

			return items;
		}

		private static void SortTemplateMenuItems(ObservableCollection<TemplateMenuItem> templateMenuItems) {
			templateMenuItems.Sort();
			foreach (TemplateMenuItem item in templateMenuItems) {
				SortTemplateMenuItems(item.MenuItems);
			}
		}

		#endregion

		#region Commands

		public ICommand OpenCommand { get; private set; }
		public ICommand CloseCommand { get; private set; }
		public ICommand SaveCommand { get; private set; }
		public ICommand SaveAsCommand { get; private set; }
		public ICommand SaveAllCommand { get; private set; }

		public ICommand GenerateCommand { get; private set; }
		public ICommand GeneratePopulateCommand { get; private set; }
		public ICommand GeneratePopulateFromCommand { get; private set; }
		
		public ICommand IncrementCommentCommand { get; private set; }
		public ICommand DecrementCommentCommand { get; private set; }

		public ICommand HelpCommand { get; private set; }

		public ICommand CutCommand { get; private set; }
		public ICommand CopyCommand { get; private set; }
		public ICommand PasteCommand { get; private set; }
		public ICommand DeleteCommand { get; private set; }
		public ICommand UndoCommand { get; private set; }
		public ICommand RedoCommand { get; private set; }

		[MemberNotNull(nameof(OpenCommand), nameof(CloseCommand),
			nameof(SaveCommand), nameof(SaveAsCommand), nameof(SaveAllCommand),
			nameof(GenerateCommand), nameof(GeneratePopulateCommand), nameof(GeneratePopulateFromCommand),
			nameof(IncrementCommentCommand), nameof(DecrementCommentCommand),
			nameof(HelpCommand),
			nameof(CutCommand), nameof(CopyCommand), nameof(PasteCommand), nameof(DeleteCommand),
			nameof(UndoCommand), nameof(RedoCommand))]
		private void InitialiseCommands() {
			OpenCommand = new RelayCommand(OpenExecuted);
			CloseCommand = new RelayCommand(CloseCurrentExecuted, CanCloseCurrent);
			SaveCommand = new RelayCommand(SaveExecuted, CanSaveCurrent);
			SaveAsCommand = new RelayCommand(SaveAsExecuted, CanSaveCurrent);
			SaveAllCommand = new RelayCommand(SaveAllExecuted, CanSaveAny);

			GenerateCommand = new RelayCommand(GenerateExecuted, CanGenerate);
			GeneratePopulateCommand = new RelayCommand(GeneratePopulateExecuted, CanGenerate);
			GeneratePopulateFromCommand = new RelayCommand(GeneratePopulateFromExecuted, CanGenerate);

			IncrementCommentCommand = new RelayCommand(IncrementCommentExecuted, CanIncrementComment);
			DecrementCommentCommand = new RelayCommand(DecrementCommentExecuted, CanIncrementComment);

			HelpCommand = new RelayCommand(HelpExecuted);

			CutCommand = new RelayCommand(CutExecuted, CanCutExecute);
			CopyCommand = new RelayCommand(CopyExecuted, CanCopyExecute);
			PasteCommand = new RelayCommand(PasteExecuted, CanPasteExecute);
			DeleteCommand = new RelayCommand(DeleteExecuted, CanDeleteExecute);
			UndoCommand = new RelayCommand(UndoExecuted, CanUndoExecute);
			RedoCommand = new RelayCommand(RedoExecuted, CanRedoExecute);

			//editorTabs.CollectionChanged += OnFileStateChanged;
			EditorStateChanged += OnFileStateChanged;
			EditorCursorChanged += OnEditorCursorChanged;
			GenerateEnabledChanged += OnGenerateEnabledChanged;
			IncrementCommentEnabledChanged += OnIncrementCommentEnabledChanged;
		}

		private void OnFileStateChanged(object? sender, EventArgs e) {
			(OpenCommand as RelayCommand)?.NotifyCanExecuteChanged();
			(CloseCommand as RelayCommand)?.NotifyCanExecuteChanged();
			(SaveCommand as RelayCommand)?.NotifyCanExecuteChanged();
			(SaveAsCommand as RelayCommand)?.NotifyCanExecuteChanged();
			(SaveAllCommand as RelayCommand)?.NotifyCanExecuteChanged();
		}

		private void OnEditorCursorChanged(object? sender, EventArgs e) {
			(CutCommand as RelayCommand)?.NotifyCanExecuteChanged();
			(CopyCommand as RelayCommand)?.NotifyCanExecuteChanged();
			(PasteCommand as RelayCommand)?.NotifyCanExecuteChanged();
			(DeleteCommand as RelayCommand)?.NotifyCanExecuteChanged();
			(UndoCommand as RelayCommand)?.NotifyCanExecuteChanged();
			(RedoCommand as RelayCommand)?.NotifyCanExecuteChanged();
		}

		private void OnGenerateEnabledChanged(object? sender, EventArgs e) {
			(GenerateCommand as RelayCommand)?.NotifyCanExecuteChanged();
			(GeneratePopulateCommand as RelayCommand)?.NotifyCanExecuteChanged();
			(GeneratePopulateFromCommand as RelayCommand)?.NotifyCanExecuteChanged();
		}

		private void OnIncrementCommentEnabledChanged(object? sender, EventArgs e) {
			(IncrementCommentCommand as RelayCommand)?.NotifyCanExecuteChanged();
			(DecrementCommentCommand as RelayCommand)?.NotifyCanExecuteChanged();
		}

		private void OpenExecuted() {
			OpenFileClick(this, new RoutedEventArgs());
		}

		private void CloseCurrentExecuted() {
			if (editorTabs.Count > 0) {
				_ = CloseTab(editorTabs[EditorTabControl.SelectedIndex]);
			}
		}

		private bool CanCloseCurrent() {
			return HasTabOpen;
		}

		private void SaveExecuted() {
			SaveFileClick(this, new RoutedEventArgs());
		}

		private void SaveAsExecuted() {
			SaveAsFileClick(this, new RoutedEventArgs());
		}

		private bool CanSaveCurrent() {
			return HasTabOpen;
		}

		private void HelpExecuted() {
			controller.ActivateDocumentationWindow();
		}

		private void SaveAllExecuted() {
			SaveAllFileClick(this, new RoutedEventArgs());
		}

		private bool CanSaveAny() {
			return NumTabs > 0;
		}

		private void GenerateExecuted() {
			GenerateButtonClick(this, new RoutedEventArgs());
		}

		private void GeneratePopulateExecuted() {
			GenerateAndPopulateButtonClick(this, new RoutedEventArgs());
		}

		private void GeneratePopulateFromExecuted() {
			GenerateAndPopulateFromButtonClick(this, new RoutedEventArgs());
		}

		private bool CanGenerate() {
			return GenerateEnabled;
		}

		private void IncrementCommentExecuted() {
			GetCurrentEditor()?.IncrementComment();
		}

		private void DecrementCommentExecuted() {
			GetCurrentEditor()?.DecrementComment();
		}

		private bool CanIncrementComment() {
			return IncrementCommentEnabled;
		}

		private void CutExecuted() {
			GetCurrentEditor()?.textEditor.Cut();
		}
		private bool CanCutExecute() {
			return GetCurrentEditor()?.textEditor.CanCut ?? false;
		}

		private void CopyExecuted() {
			GetCurrentEditor()?.textEditor.Copy();
		}
		private bool CanCopyExecute() {
			return GetCurrentEditor()?.textEditor.CanCopy ?? false;
		}

		private void PasteExecuted() {
			GetCurrentEditor()?.textEditor.Paste();
		}
		private bool CanPasteExecute() {
			return GetCurrentEditor()?.textEditor.CanPaste ?? false;
		}

		private void DeleteExecuted() {
			GetCurrentEditor()?.textEditor.Delete();
		}
		private bool CanDeleteExecute() {
			return GetCurrentEditor()?.textEditor.CanDelete ?? false;
		}

		private void UndoExecuted() {
			GetCurrentEditor()?.textEditor.Undo();
		}
		private bool CanUndoExecute() {
			return GetCurrentEditor()?.textEditor.CanUndo ?? false;
		}

		private void RedoExecuted() {
			GetCurrentEditor()?.textEditor.Redo();
		}
		private bool CanRedoExecute() {
			return GetCurrentEditor()?.textEditor.CanRedo ?? false;
		}


		#endregion Commands

		#region Checkable MenuItems

		private void ToggleMenuItemCheckBox(object sender, RoutedEventArgs e) {
			if(sender is MenuItem menuItem && menuItem.Icon is CheckBox checkBox) {
				checkBox.IsChecked = !checkBox.IsChecked;
			}
		}

		#endregion

		#region GenerateEnabled

		public static readonly DirectProperty<SharpEditorWindow, bool> GenerateEnabledProperty =
			AvaloniaProperty.RegisterDirect<SharpEditorWindow, bool>(
				nameof(GenerateEnabled),
				o => o.GenerateEnabled);

		public event EventHandler? GenerateEnabledChanged;

		private bool generateEnabled;

		public bool GenerateEnabled {
			get { return generateEnabled; }
			private set {
				SetAndRaise(GenerateEnabledProperty, ref generateEnabled, value);
				GenerateEnabledChanged?.Invoke(this, new EventArgs());
			}
		}

		#endregion

		#region IncrementCommentEnabled

		public static readonly DirectProperty<SharpEditorWindow, bool> IncrementCommentEnabledProperty =
			AvaloniaProperty.RegisterDirect<SharpEditorWindow, bool>(
				nameof(IncrementCommentEnabled),
				o => o.IncrementCommentEnabled);

		public event EventHandler? IncrementCommentEnabledChanged;

		private bool incrementCommentEnabled;

		public bool IncrementCommentEnabled {
			get { return incrementCommentEnabled; }
			private set {
				SetAndRaise(IncrementCommentEnabledProperty, ref incrementCommentEnabled, value);
				IncrementCommentEnabledChanged?.Invoke(this, new EventArgs());
			}
		}

		#endregion

		#region DesignerVisible

		public static readonly DirectProperty<SharpEditorWindow, bool> DesignerVisibleProperty =
			AvaloniaProperty.RegisterDirect<SharpEditorWindow, bool>(
				nameof(DesignerVisible),
				o => o.DesignerVisible,
				(o, v) => o.DesignerVisible = v);

		public event EventHandler? DesignerVisibleChanged;

		private bool designerVisible;

		public bool DesignerVisible {
			get { return designerVisible; }
			set {
				SetAndRaise(DesignerVisibleProperty, ref designerVisible, value);
				DesignerVisibleChanged?.Invoke(this, new EventArgs());
			}
		}

		#endregion

		#region TemplateAlertEnabled

		public static readonly DirectProperty<SharpEditorWindow, bool> TemplateAlertEnabledProperty =
			AvaloniaProperty.RegisterDirect<SharpEditorWindow, bool>(
				nameof(TemplateAlertEnabled),
				o => o.TemplateAlertEnabled,
				(o, v) => o.TemplateAlertEnabled = v);

		public event EventHandler? TemplateAlertEnabledChanged;

		private bool templateAlertEnabled;

		public bool TemplateAlertEnabled {
			get { return templateAlertEnabled; }
			set {
				SetAndRaise(TemplateAlertEnabledProperty, ref templateAlertEnabled, value);
				TemplateAlertEnabledChanged?.Invoke(this, new EventArgs());
			}
		}

		#endregion

	}

	public class TemplateMenuItem : IComparable<TemplateMenuItem> {

		public string Name { get; set; }
		public string? Path { get; set; }

		public bool HasPath { get { return Path is not null; } }

		public ObservableCollection<TemplateMenuItem> MenuItems { get; set; }

		public ICommand ItemCommand { get; }

		public TemplateMenuItem(string name, Action<TemplateMenuItem> command) {
			Name = name;
			Path = null;
			MenuItems = new ObservableCollection<TemplateMenuItem>();

			ItemCommand = new RelayCommand(() => { command(this); });
		}

		public int CompareTo(TemplateMenuItem? other) {
			if (other == null) { return 1; }

			if (MenuItems.Count == 0 ^ other.MenuItems.Count == 0) {
				return other.MenuItems.Count.CompareTo(MenuItems.Count);
			}
			else {
				return Name.CompareTo(other.Name);
			}
		}
	}

	public static class ObservableCollectionUtils {
		public static void Sort<T>(this ObservableCollection<T> collection) where T : IComparable<T> {
			List<T> sorted = collection.OrderBy(x => x).ToList();
			for (int i = 0; i < sorted.Count; i++) {
				collection.Move(collection.IndexOf(sorted[i]), i);
			}
		}
	}

}
