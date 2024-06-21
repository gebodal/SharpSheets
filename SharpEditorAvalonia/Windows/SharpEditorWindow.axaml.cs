using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using SharpEditorAvalonia.DataManagers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace SharpEditorAvalonia.Windows {

	public partial class SharpEditorWindow : Window {

		public readonly AppController controller;
		private readonly ObservableCollection<EditorTabItem> editorTabs;

		// TODO Progress tracker for Generator?
		private readonly Generator generator;

		public static SharpEditorWindow? Instance { get; private set; } = null;

		public SharpEditorWindow(AppController controller) {
			this.controller = controller;

			InitializeComponent();
			this.DataContext = this;

			// TODO Template file list
			//InitialiseTemplateFileList();

			GenerateEnabled = false;
			SavePossible = false;
			IncrementCommentEnabled = false;

			generator = new Generator(Dispatcher.UIThread) { OpenOnGenerate = SharpDataManager.Instance.OpenOnGenerate };
			SharpDataManager.Instance.OpenOnGenerateChanged += delegate { generator.OpenOnGenerate = SharpDataManager.Instance.OpenOnGenerate; };
			generator.Start();

			// Create list of tab items
			editorTabs = new ObservableCollection<EditorTabItem>();
			EditorTabControl.ItemsSource = editorTabs;

			EditorTabControl.SelectionChanged += TabSelectionChanged;

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

		private void ShowDocumentationMenuClick(object? sender, RoutedEventArgs e) {
			controller.ActivateDocumentationWindow();
		}

		private void ShowTemplateErrorsMenuClick(object? sender, RoutedEventArgs e) {
			controller.ActivateTemplateErrorWindow();
		}

		#endregion

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

					// TODO Implement these properly
					/*
					ShowDesignerButton.IsChecked = currentlyVisible.IsDesignerVisible;
					ToolbarShowDesignerButton.IsChecked = currentlyVisible.IsDesignerVisible;

					ShowLineNumbersButton.IsChecked = currentlyVisible.textEditor.ShowLineNumbers;
					ShowEndOfLineButton.IsChecked = currentlyVisible.textEditor.Options.ShowEndOfLine;
					WrapLinesButton.IsChecked = currentlyVisible.textEditor.WordWrap;
					*/
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
		#endregion












		// TODO Implement update
		public bool IsClosed { get; private set; }

		public bool TemplateAlertEnabled { get; set; } = false;
		public bool GenerateEnabled { get; private set; }
		public bool SavePossible { get; private set; }
		public bool IncrementCommentEnabled { get; private set; }

		public SharpDocumentEditor OpenEditorDocument(string path, bool forced) {
			// TODO Implement
			throw new NotImplementedException();
		}

		public SharpDocumentEditor OpenEmptyDocument(DocumentType documentType, bool intentional, bool openTab) {
			// TODO Implement properly
			SharpDocumentEditor editor = new SharpDocumentEditor(null);
			editorTabs.Add(new EditorTabItem(this, new SharpDocumentEditor(null)));
			return editor;
		}

		public bool CloseDocument(SharpDocumentEditor editor) {
			editor.Uninstall();
			return true;
		}

		public void PerformOpenClick() {
			editorTabs.Add(new EditorTabItem(this, new SharpDocumentEditor(null)));
		}

	}

}
