using SharpSheets.Utilities;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Forms;
using SharpEditor.DataManagers;
using System.IO;

namespace SharpEditor.SharpSettingsWindow {

	/// <summary>
	/// Interaction logic for SettingsWindow.xaml
	/// </summary>
	public partial class SettingsWindow : Window {

		public SettingsWindow() {
			InitializeComponent();

			TemplateDirectoryTextBox.Text = SharpEditorPathInfo.TemplateDirectory;
		}

		public bool IsClosed { get; private set; } = false;
		protected override void OnClosed(EventArgs e) {
			base.OnClosed(e);

			IsClosed = true;
		}

		private void ExitClick(object? sender, RoutedEventArgs e) {
			this.Close();
			SharpEditorWindow.Instance?.Focus();
		}

		#region Template Directory

		private void OnTemplateDirectoryTextChanged(object? sender, TextChangedEventArgs e) {
			string currentPath = TemplateDirectoryTextBox.Text;
			if (!string.IsNullOrWhiteSpace(currentPath)) {
				try {
					string fullPath = System.IO.Path.GetFullPath(TemplateDirectoryTextBox.Text);
					if(System.IO.Directory.Exists(fullPath) || (System.IO.Directory.GetParent(fullPath)?.Exists ?? false)) {
						TemplatePathErrorTextBlock.Visibility = Visibility.Collapsed;
						ApplyButton.IsEnabled = fullPath != SharpEditorPathInfo.TemplateDirectory;
						return;
					}
				}
				catch (Exception) {
					// TODO Be more specific
				}
			}

			TemplatePathErrorTextBlock.Text = "Invalid path!";
			TemplatePathErrorTextBlock.Visibility = Visibility.Visible;
			ApplyButton.IsEnabled = false;
		}

		private void TemplateDirectoryApplyClick(object? sender, RoutedEventArgs e) {
			string currentTemplateDirectory = SharpEditorPathInfo.TemplateDirectory;
			string newTemplateDirectory = System.IO.Path.GetFullPath(TemplateDirectoryTextBox.Text);

			bool copyExisting = false;
			if (System.IO.Directory.Exists(currentTemplateDirectory) && System.IO.Directory.GetFileSystemEntries(currentTemplateDirectory).Length > 0) {
				MessageBoxResult result = System.Windows.MessageBox.Show("There are templates in the current directory. Do you wish to copy them to the new location?", "Existing Templates", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

				if(result == MessageBoxResult.Cancel) {
					e.Handled = true;
					return;
				}
				else {
					copyExisting = result == MessageBoxResult.Yes;
				}
			}

			if (System.IO.Directory.Exists(newTemplateDirectory) && System.IO.Directory.GetFileSystemEntries(newTemplateDirectory).Length > 0) {
				MessageBoxResult result = System.Windows.MessageBox.Show("There are existing files in the specified directory. Do you wish to continue? The existing files will not be deleted.", "Existing Files", MessageBoxButton.YesNo, MessageBoxImage.Question);

				if(result != MessageBoxResult.Yes) {
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
							MessageBoxResult result = System.Windows.MessageBox.Show("Some files in the new directory will be overwritten. Do you wish to continue?", "Existing Files", MessageBoxButton.YesNo, MessageBoxImage.Warning);

							if (result != MessageBoxResult.Yes) {
								e.Handled = true;
								return;
							}

							break;
						}
					}
				}

				Console.WriteLine($"Copy all files from {currentTemplateDirectory} to {newTemplateDirectory}");

				foreach(string filepath in FileUtils.GetAllFiles(currentTemplateDirectory)) {
					try {
						string destination = GetDestination(filepath);
						CopyFile(filepath, destination);
					}
					catch(IOException) {
						MessageBoxResult result = System.Windows.MessageBox.Show($"An error was encountered copying {filepath}. Do you wish to continue?", "Error Copying File", MessageBoxButton.YesNo, MessageBoxImage.Warning);

						if (result != MessageBoxResult.No) {
							System.Windows.MessageBox.Show("Change of template directory has been aborted. Some files may have been copied.", "Change Aborted", MessageBoxButton.OK, MessageBoxImage.Information);

							e.Handled = true;
							return;
						}
					}
				}
			}

			Console.WriteLine($"Set template directory to: {newTemplateDirectory}");
			SharpEditorPathInfo.TemplateDirectory = newTemplateDirectory;

			TemplateDirectoryTextBox.Text = newTemplateDirectory;

			System.Windows.MessageBox.Show($"Template directory successfully changed to {newTemplateDirectory}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

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

		private void BrowseTemplateDirectoryClick(object? sender, RoutedEventArgs e) {
			FolderBrowserDialog dlg = new FolderBrowserDialog {
				Description = "Select template directory. This change will not be implemented until Apply is selected.",
				SelectedPath = SharpEditorPathInfo.TemplateDirectory,
				ShowNewFolderButton = true
			};

			if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
				if (!string.IsNullOrWhiteSpace(dlg.SelectedPath)) {
					TemplateDirectoryTextBox.Text = dlg.SelectedPath;
				}
			}
		}

		private void ResetTemplateDirectoryClick(object? sender, RoutedEventArgs e) {
			TemplateDirectoryTextBox.Text = SharpEditorPathInfo.TemplateDirectory;
		}

		#endregion

		private void TextColorPreviewKeyDown(object? sender, System.Windows.Input.KeyEventArgs e) {
			if(e.Key == System.Windows.Input.Key.Enter) {
				//Console.WriteLine(e.Key);
				//(sender as FrameworkElement)?.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));

				BindingExpression? binding = (sender as System.Windows.Controls.TextBox)?.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty);
				if(binding is BindingExpression) {
					binding.UpdateSource();
				}
			
				e.Handled = true;
			}
		}
	}

	[ValueConversion(typeof(System.Windows.Media.Color), typeof(String))]
	public class ColorToStringConverter : IValueConverter {
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
			System.Windows.Media.Color color = (System.Windows.Media.Color)value;
			return "#" + color.R.ToString("X2") + color.G.ToString("X2") + color.B.ToString("X2");
			//return color.ToString();
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
			if (value is string strValue) {
				try {
					SharpSheets.Colors.Color color = SharpSheets.Utilities.ColorUtils.Parse(strValue);
					return System.Windows.Media.Color.FromRgb(color.R, color.G, color.B);
				}
				catch (FormatException) {
					return DependencyProperty.UnsetValue;
				}
			}
			else {
				return DependencyProperty.UnsetValue;
			}
		}
	}

	[ValueConversion(typeof(System.Windows.Media.Color), typeof(System.Windows.Media.Brush))]
	class ColorToBrushConverter : IValueConverter {
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
			System.Windows.Media.Color color = (System.Windows.Media.Color)value;
			return new System.Windows.Media.SolidColorBrush(color);
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
			System.Windows.Media.SolidColorBrush brush = (System.Windows.Media.SolidColorBrush)value;
			return brush.Color;
		}
	}

}
