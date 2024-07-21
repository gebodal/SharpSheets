using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using SharpEditor.DataManagers;
using SharpSheets.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace SharpEditor.Windows {

	public partial class SettingsWindow : Window {

		public SettingsWindow() {
			InitializeComponent();

			this.DataContext = SharpDataManager.Instance;
			InitialiseHighlightColorSettings();

			this.Focusable = true; // So we can draw focus away from controls (e.g. upon Enter press)

			TemplateDirectoryTextBox.Text = SharpEditorPathInfo.TemplateDirectory;
			DPITextBox.Text = SharpDataManager.Instance.ScreenDPI.ToString();
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

		#region DPI

		private void OnDPITextLostFocus(object? sender, RoutedEventArgs e) {
			if(int.TryParse(DPITextBox.Text, out int newDPI) && newDPI > 0) {
				SharpDataManager.Instance.ScreenDPI = newDPI;
			}
			else {
				DPITextBox.Text = SharpDataManager.Instance.ScreenDPI.ToString();
			}
		}

		private void DPITextBoxKeyDown(object? sender, Avalonia.Input.KeyEventArgs e) {
			if(e.Key == Avalonia.Input.Key.Enter) {
				this.Focus();
			}
		}

		#endregion

		#region Syntax Colors

		private List<HighlightColorSettingGrouping> highlightColorSettings;

		[MemberNotNull(nameof(highlightColorSettings))]
		private void InitialiseHighlightColorSettings() {
			LoadColors(GetPaletteColors());
		}

		private static List<HighlightColorSettingGrouping> ConvertPaletteData(IReadOnlyDictionary<string, HighlightData> paletteData) {
			return paletteData
				.Select(kv => {
					string name = kv.Key;
					Color color = kv.Value.Color;
					bool bold = kv.Value.FontWeight == FontWeight.Bold;
					bool italic = kv.Value.FontStyle == FontStyle.Italic;

					string[] parts = name.Split('_');

					string groupName = parts[0];
					string displayName = parts[1];
					displayName = Regex.Replace(displayName, @"(?<=[a-z])[A-Z0-9]", m => " " + m.Groups[0].Value);

					HighlightColorSetting setting = new HighlightColorSetting(name, displayName, color, bold, italic);

					return (groupName, setting);
				})
				.GroupBy(p => p.groupName)
				.Select(g => new HighlightColorSettingGrouping(g.Key, g.Select(i => i.setting).ToList().OrderBy(c => c.DisplayName).ToList()))
				.OrderBy(c => c.Name)
				.ToList();
		}

		private static List<HighlightColorSettingGrouping> GetPaletteColors() {
			return ConvertPaletteData(SharpEditorPalette.GetColors());
		}

		[MemberNotNull(nameof(highlightColorSettings))]
		private void LoadColors(List<HighlightColorSettingGrouping> colors) {
			highlightColorSettings = colors;
			SyntaxColorGroupsControl.ItemsSource = highlightColorSettings;
		}

		private void ApplyColorsClick(object? sender, RoutedEventArgs e) {
			SharpEditorPalette.SetColors(highlightColorSettings.SelectMany(g=>g.Colors).ToDictionary(
				c => c.Name,
				c => new HighlightData(c.Color, c.Bold ? FontWeight.Bold : FontWeight.Normal, c.Italic ? FontStyle.Italic : FontStyle.Normal)
				));
		}

		private void ResetColorsClick(object? sender, RoutedEventArgs e) {
			LoadColors(GetPaletteColors());
		}

		private void DefaultColorsClick(object? sender, RoutedEventArgs e) {
			LoadColors(ConvertPaletteData(SharpEditorPalette.LoadDefaultHighlightingColors()));
		}

		#endregion

	}

	public partial class HighlightColorSettingGrouping : ObservableObject {

		[ObservableProperty]
		private string name;
		[ObservableProperty]
		private List<HighlightColorSetting> colors;

		public HighlightColorSettingGrouping(string name, List<HighlightColorSetting> colors) {
			this.name = name;
			this.colors = colors;
		}

	}

	public partial class HighlightColorSetting : ObservableObject {

		[ObservableProperty]
		private string name;
		[ObservableProperty]
		private string displayName;
		[ObservableProperty]
		private Color color;
		[ObservableProperty]
		private bool bold;
		[ObservableProperty]
		private bool italic;

		public HighlightColorSetting(string name, string displayName, Color color, bool bold, bool italic) {
			this.name = name;
			this.displayName = displayName;
			this.color = color;
			this.bold = bold;
			this.italic = italic;
		}

	}

	public class ColorToStringConverter : IValueConverter {
		public static readonly ColorToStringConverter Instance = new();

		public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) {
			if (value is Color color) {
				uint rgb = color.ToUInt32();
				if ((rgb & 0xff000000) == 0xff000000) {
					return $"{(rgb & 0xffffff).ToString("x6", CultureInfo.InvariantCulture)}";
				}
				else {
					return $"{rgb.ToString("x8", CultureInfo.InvariantCulture)}";
				}
			}
			else {
				// converter used for the wrong type
				return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);
			}
		}

		public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) {
			if (value is string colorStr) {
				colorStr = colorStr.Trim();
				if (!colorStr.StartsWith('#') && Regex.Match(colorStr, @"^[0-9a-f]+$", RegexOptions.IgnoreCase).Success) {
					colorStr = $"#{colorStr}";
				}
				try {
					return Color.Parse(colorStr);
				}
				catch (Exception) {
					return new BindingNotification(new FormatException("Invalid color string."), BindingErrorType.DataValidationError);
				}
			}
			else {
				// converter used for the wrong type
				return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);
			}
		}
	}

	public class LostFocusUpdateBindingBehavior : Avalonia.Xaml.Interactivity.Behavior<TextBox> {

		static LostFocusUpdateBindingBehavior() {
			TextProperty.Changed.Subscribe(e => {
				((LostFocusUpdateBindingBehavior)e.Sender).OnBindingValueChanged();
			});
		}

		protected override void UpdateDataValidation(AvaloniaProperty property, BindingValueType state, Exception? error) {
			base.UpdateDataValidation(property, state, error);

			if (property == TextProperty && AssociatedObject != null) {
				if (state == BindingValueType.DataValidationError && error is not null)
					DataValidationErrors.SetError(AssociatedObject, error);
				else // if (error is null)
					DataValidationErrors.ClearErrors(AssociatedObject);
			}
		}

		protected override void OnAttached() {
			if (AssociatedObject != null) {
				AssociatedObject.LostFocus += OnLostFocus;
				AssociatedObject.KeyDown += OnKeyDown;
			}

			base.OnAttached();
		}

		protected override void OnDetaching() {
			if (AssociatedObject != null) {
				AssociatedObject.LostFocus -= OnLostFocus;
				AssociatedObject.KeyDown -= OnKeyDown;
			}

			base.OnDetaching();
		}

		private void OnKeyDown(object? sender, KeyEventArgs e) {
			if (AssociatedObject != null && e.Key == Key.Enter)
				Text = AssociatedObject.Text ?? "";
		}

		private void OnLostFocus(object? sender, RoutedEventArgs e) {
			if (AssociatedObject != null)
				Text = AssociatedObject.Text ?? "";
		}

		private void OnBindingValueChanged() {
			if (AssociatedObject != null)
				AssociatedObject.Text = Text;
		}

		public static readonly DirectProperty<LostFocusUpdateBindingBehavior, string> TextProperty
			= AvaloniaProperty.RegisterDirect<LostFocusUpdateBindingBehavior, string>(nameof(Text), o => o.Text,
				(o, v) => o.Text = v, "", BindingMode.TwoWay, true);

		private string _text = "";

		public string Text {
			get { return _text; }
			set { this.SetAndRaise(TextProperty, ref _text, value); }
		}
	}

}
