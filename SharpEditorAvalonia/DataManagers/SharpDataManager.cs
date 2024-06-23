using CommunityToolkit.Mvvm.ComponentModel;
using SharpSheets.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SharpEditorAvalonia.DataManagers {

	public partial class SharpDataManager : ObservableObject {

		private static readonly string configPath;
		private static readonly string configExtension = ".conf";
		private static readonly JsonSerializerOptions jsonSerializeoptions = new JsonSerializerOptions() {
			IncludeFields = true
		};

		public static void Initialise() { } // Dummy method to force static initialisation

		public static SharpDataManager Instance { get; }

		static SharpDataManager() {

			// Does the settings file exist?
			// If yes, load it
			// If not, check for an earlier version, load that if it exists and save a new copy with the new version
			// If no earlier version exists, create an empty object
			// Save the object in Instance

			configPath = GetCurrentConfigPath();

			if (File.Exists(configPath)) {
				Console.WriteLine("Load existing config.");
				Instance = LoadSettings(configPath) ?? new SharpDataManager();
			}
			else if (GetOldConfigPath() is string oldConfigPath) {
				Console.WriteLine("Load old config.");
				Instance = LoadSettings(oldConfigPath) ?? new SharpDataManager();
				Instance.Save();
			}
			else {
				Console.WriteLine("Create new config.");
				Instance = new SharpDataManager();
				Instance.Save();
			}

		}

		public SharpDataManager() { } // I don't like this being public

		protected override void OnPropertyChanged(PropertyChangedEventArgs e) {
			base.OnPropertyChanged(e);
			
			Save();
		}

		private static SharpDataManager? LoadSettings(string filePath) {
			//Console.WriteLine($"Load settings from: {filePath}");
			string jsonText = File.ReadAllText(filePath);
			// TODO Need to deal better with unrecognised properties
			return JsonSerializer.Deserialize<SharpDataManager>(jsonText, jsonSerializeoptions);
		}

		private void Save() {
			string jsonText = JsonSerializer.Serialize(this, jsonSerializeoptions);
			File.WriteAllText(configPath, jsonText);
			//Console.WriteLine($"Save settings to: {configPath}");
		}

		private static string GetCurrentConfigPath() {
			string filename = SharpEditorData.GetEditorName() + SharpEditorData.GetVersionString() + configExtension;
			string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
			return Path.Combine(appData, filename);
		}

		private static string? GetOldConfigPath() {
			string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
			string editorName = SharpEditorData.GetEditorName();

			IEnumerable<(string path, Version version)> GetVersions(IEnumerable<string> paths) {
				foreach (string path in paths) {
					string filename = Path.GetFileName(path);
					string versionStr = filename[editorName.Length..^configExtension.Length];
					if (Version.TryParse(versionStr, out Version? parsed)) {
						yield return (path, parsed);
					}
				}
			}

			(string path, Version version)[] existing = GetVersions(Directory.GetFiles(appData, $"{editorName}*{configExtension}", SearchOption.TopDirectoryOnly)).ToArray();
			
			if (existing.Length > 0) {
				return existing.MaxBy(pv => pv.version).path;
			}
			else {
				return null;
			}
		}

		#region Designer properties

		[ObservableProperty]
		private bool designerDisplayFields = true;
		public event EventHandler? DesignerDisplayFieldsChanged;
		partial void OnDesignerDisplayFieldsChanged(bool value) {
			DesignerDisplayFieldsChanged?.Invoke(this, new EventArgs());
		}

		[ObservableProperty]
		private bool designerViewerOpenDefault = false;

		#endregion

		#region Generator settings

		[ObservableProperty]
		private bool openOnGenerate = false;
		public event EventHandler? OpenOnGenerateChanged;
		partial void OnOpenOnGenerateChanged(bool value) {
			OpenOnGenerateChanged?.Invoke(this, new EventArgs());
		}

		#endregion

		#region Editor settings

		[ObservableProperty]
		private bool showLineNumbers = true;
		public event EventHandler? ShowLineNumbersChanged;
		partial void OnShowLineNumbersChanged(bool value) {
			ShowLineNumbersChanged?.Invoke(this, new EventArgs());
		}

		[ObservableProperty]
		private bool showEndOfLine = false;
		public event EventHandler? ShowEndOfLineChanged;
		partial void OnShowEndOfLineChanged(bool value) {
			ShowEndOfLineChanged?.Invoke(this, new EventArgs());
		}

		[ObservableProperty]
		private bool wrapLines = false;
		public event EventHandler? WrapLinesChanged;
		partial void OnWrapLinesChanged(bool value) {
			WrapLinesChanged?.Invoke(this, new EventArgs());
		}

		[ObservableProperty]
		private double textZoom = 1.0;
		public event EventHandler? TextZoomChanged;
		partial void OnTextZoomChanged(double value) {
			TextZoomChanged?.Invoke(this, new EventArgs());
		}

		#endregion

		#region Window settings

		[ObservableProperty]
		private bool windowMaximized = true;

		#endregion

		#region Warning and error settings

		[ObservableProperty]
		private bool warnFontLicensing = true;
		public event EventHandler? WarnFontLicensingChanged;
		partial void OnWarnFontLicensingChanged(bool value) {
			WarnFontLicensingChanged?.Invoke(this, new EventArgs());
		}

		#endregion

		#region File system settings

		[ObservableProperty]
		private string lastFileDirectory = "";

		[ObservableProperty]
		private string templateDirectory = "";

		#endregion

		/*
		public static readonly DependencyProperty TestColorProperty =
			DependencyProperty.Register("TestColor", typeof(System.Windows.Media.Color),
			typeof(SharpDataManager), new UIPropertyMetadata(System.Windows.Media.Colors.Red, OnTestColorChanged));
		public event EventHandler? TestColorChanged;

		public System.Windows.Media.Color TestColor {
			get {
				return (System.Windows.Media.Color)GetValue(TestColorProperty);
			}
			set {
				SetValue(TestColorProperty, value);
			}
		}

		private static void OnTestColorChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e) {
			if (sender is SharpDataManager manager) {
				manager.TestColorChanged?.Invoke(sender, EventArgs.Empty);
			}
		}
		*/
	}

}
