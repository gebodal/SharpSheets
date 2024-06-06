using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SharpEditorAvalonia.DataManagers;
using SharpSheets.Utilities;
using System;
using System.Collections.Generic;
using System.IO;

namespace SharpEditorAvalonia.Windows {

	public partial class SettingsWindow : Window {

		public SettingsWindow() {
			InitializeComponent();

			this.DataContext = SharpDataManager.Instance;
			
			TemplateDirectoryTextBox.Text = SharpEditorPathInfo.TemplateDirectory;
		}

		public bool IsClosed { get; private set; } = false;
		protected override void OnClosed(EventArgs e) {
			base.OnClosed(e);
			
			IsClosed = true;
		}

		private void ExitClick(object? sender, RoutedEventArgs e) {
			this.Close();
			// SharpEditorWindow.Instance?.Focus(); // TODO Implement again
		}

		#region Template Directory

		private void OnTemplateDirectoryTextChanged(object? sender, TextChangedEventArgs e) {
			string? currentPath = TemplateDirectoryTextBox.Text;
			if (!string.IsNullOrWhiteSpace(currentPath)) {
				try {
					string fullPath = System.IO.Path.GetFullPath(currentPath);
					if (System.IO.Directory.Exists(fullPath) || (System.IO.Directory.GetParent(fullPath)?.Exists ?? false)) {
						TemplatePathErrorTextBlock.IsVisible = false;
						ApplyButton.IsEnabled = fullPath != SharpEditorPathInfo.TemplateDirectory;
						return;
					}
				}
				catch (Exception) {
					// TODO Be more specific
				}
			}

			TemplatePathErrorTextBlock.Text = "Invalid path!";
			TemplatePathErrorTextBlock.IsVisible = true;
			ApplyButton.IsEnabled = false;
		}

		private async void TemplateDirectoryApplyClick(object? sender, RoutedEventArgs e) {
			string currentTemplateDirectory = SharpEditorPathInfo.TemplateDirectory;
			string newTemplateDirectory = System.IO.Path.GetFullPath(TemplateDirectoryTextBox.Text ?? ""); // Is this fallback OK?

			bool copyExisting = false;
			if (System.IO.Directory.Exists(currentTemplateDirectory) && System.IO.Directory.GetFileSystemEntries(currentTemplateDirectory).Length > 0) {
				MessageBoxResult result = await MessageBoxes.Show("There are templates in the current directory. Do you wish to copy them to the new location?", "Existing Templates", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

				if (result == MessageBoxResult.Cancel) {
					e.Handled = true;
					return;
				}
				else {
					copyExisting = result == MessageBoxResult.Yes;
				}
			}

			if (System.IO.Directory.Exists(newTemplateDirectory) && System.IO.Directory.GetFileSystemEntries(newTemplateDirectory).Length > 0) {
				MessageBoxResult result = await MessageBoxes.Show("There are existing files in the specified directory. Do you wish to continue? The existing files will not be deleted.", "Existing Files", MessageBoxButton.YesNo, MessageBoxImage.Question);

				if (result != MessageBoxResult.Yes) {
					e.Handled = true;
					return;
				}
			}

			bool newDirectoryAlreadyExists = System.IO.Directory.Exists(newTemplateDirectory);

			if (!newDirectoryAlreadyExists) {
				Console.WriteLine($"Create {newTemplateDirectory}");
				System.IO.Directory.CreateDirectory(newTemplateDirectory);
			}

			string GetDestination(string filepath) {
				return System.IO.Path.Combine(newTemplateDirectory, FileUtils.GetRelativePath(currentTemplateDirectory, filepath));
			}

			if (copyExisting) {
				if (newDirectoryAlreadyExists) {
					foreach (string filepath in FileUtils.GetAllFiles(currentTemplateDirectory)) {
						string destination = GetDestination(filepath);
						if (System.IO.File.Exists(destination)) {
							MessageBoxResult result = await MessageBoxes.Show("Some files in the new directory will be overwritten. Do you wish to continue?", "Existing Files", MessageBoxButton.YesNo, MessageBoxImage.Warning);

							if (result != MessageBoxResult.Yes) {
								e.Handled = true;
								return;
							}

							break;
						}
					}
				}

				Console.WriteLine($"Copy all files from {currentTemplateDirectory} to {newTemplateDirectory}");

				foreach (string filepath in FileUtils.GetAllFiles(currentTemplateDirectory)) {
					try {
						string destination = GetDestination(filepath);
						CopyFile(filepath, destination);
					}
					catch (IOException) {
						MessageBoxResult result = await MessageBoxes.Show($"An error was encountered copying {filepath}. Do you wish to continue?", "Error Copying File", MessageBoxButton.YesNo, MessageBoxImage.Warning);

						if (result != MessageBoxResult.No) {
							await MessageBoxes.Show("Change of template directory has been aborted. Some files may have been copied.", "Change Aborted", MessageBoxButton.OK, MessageBoxImage.Information);

							e.Handled = true;
							return;
						}
					}
				}
			}

			Console.WriteLine($"Set template directory to: {newTemplateDirectory}");
			SharpEditorPathInfo.TemplateDirectory = newTemplateDirectory;

			TemplateDirectoryTextBox.Text = newTemplateDirectory;

			await MessageBoxes.Show($"Template directory successfully changed to {newTemplateDirectory}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

			e.Handled = true;
		}

		private static void CopyFile(string sourcePath, string destinationPath) {
			Console.WriteLine($"Copy {sourcePath} to {destinationPath}");

			string destDir = Path.GetDirectoryName(destinationPath) ?? throw new DirectoryNotFoundException($"Could not find directory for {destinationPath}");
			if (!Directory.Exists(destDir)) {
				Console.WriteLine($"Create directory {destDir}");
				Directory.CreateDirectory(destDir);
			}

			using (Stream source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read)) {
				using (FileStream destination = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write)) {
					source.Seek(0, SeekOrigin.Begin);
					source.CopyTo(destination);
				}
			}
		}

		private void RestoreTemplateDirectoryApplicationDefaultClick(object? sender, RoutedEventArgs e) {
			TemplateDirectoryTextBox.Text = SharpEditorPathInfo.GetDefaultApplicationTemplateDirectory();
		}

		private async void BrowseTemplateDirectoryClick(object? sender, RoutedEventArgs e) {

			IStorageFolder? templateFolder = await this.StorageProvider.TryGetFolderFromPathAsync(SharpEditorPathInfo.TemplateDirectory);

			IReadOnlyList<IStorageFolder> values = await this.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions() {
				AllowMultiple = false,
				SuggestedStartLocation = templateFolder,
				Title = "Template Directory"
				//Description = "Select template directory. This change will not be implemented until Apply is selected.",
				//ShowNewFolderButton = true
			});

			if (values.Count > 0 && values[0] is IStorageFolder selectedFolder && selectedFolder.TryGetLocalPath() is string selectedPath) {
				if (!string.IsNullOrWhiteSpace(selectedPath)) {
					TemplateDirectoryTextBox.Text = selectedPath;
				}
			}
		}

		private void ResetTemplateDirectoryClick(object? sender, RoutedEventArgs e) {
			TemplateDirectoryTextBox.Text = SharpEditorPathInfo.TemplateDirectory;
		}

		#endregion

	}

}
