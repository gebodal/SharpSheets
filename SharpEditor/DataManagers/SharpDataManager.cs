using CommunityToolkit.Mvvm.ComponentModel;
using SharpSheets.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SharpEditor.DataManagers {

	public partial class SharpDataManager : ObservableObject {

		private static readonly string configPath;
		private static readonly string configExtension = ".conf";
		private static readonly string configDirBasename = SharpEditorData.GetEditorName();
		private static readonly JsonSerializerOptions jsonSerializeoptions = new JsonSerializerOptions() {
			IncludeFields = true
		};

		public static void Initialise() { } // Dummy method to force static initialisation

		public static SharpDataManager Instance { get; }

		public string ConfigDir => GetCurrentConfigDir();
		public string ConfigPath => configPath;

		static SharpDataManager() {

			// Does the settings file exist?
			// If yes, load it
			// If not, check for an earlier version, load that if it exists and save a new copy with the new version
			// If no earlier version exists, create an empty object
			// Save the object in Instance

			configPath = GetCurrentConfigPath();
			Console.WriteLine("Calculated current config path: " + configPath);

			string configDirPath = GetCurrentConfigDir();
			if (!Directory.Exists(configDirPath)) {
				Console.WriteLine("Create config directory.");
				Directory.CreateDirectory(configDirPath);
			}

			if (File.Exists(configPath)) {
				Console.WriteLine("Load existing config.");
				SharpDataManager? loaded = LoadSettings(configPath);
				if(loaded is null) {
					Console.WriteLine("Could not load existing config.");
				}
				Instance = loaded ?? new SharpDataManager();
			}
			else if (GetOldConfigPath() is string oldConfigPath) {
				Console.WriteLine("Load old config.");
				SharpDataManager? loaded = LoadSettings(oldConfigPath);
				if (loaded is null) {
					Console.WriteLine("Could not load old config.");
				}
				Instance = loaded ?? new SharpDataManager();
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
			try {
				string jsonText = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
				// TODO Need to deal better with unrecognised properties
				return JsonSerializer.Deserialize<SharpDataManager>(jsonText, jsonSerializeoptions);
			}
			catch (IOException) {
				return null;
			}
			catch (SystemException) {
				return null;
			}
			catch (JsonException) {
				return null;
			}
		}

		private void Save() {
			try {
				string jsonText = JsonSerializer.Serialize(this, jsonSerializeoptions);
				File.WriteAllText(configPath, jsonText, System.Text.Encoding.UTF8);
				//Console.WriteLine($"Save settings to: {configPath}");
				return;
			}
			catch (IOException) {
				Console.WriteLine($"Could not save settings to: {configPath}");
				return;
			}
			catch (SystemException) {
				Console.WriteLine($"Could not save settings to: {configPath}");
				return;
			}
		}

		private static string GetCurrentConfigDir() {
			string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
			return Path.Combine(appData, configDirBasename);
		}

		private static string GetCurrentConfigPath() {
			string filename = SharpEditorData.GetEditorName() + SharpEditorData.GetVersionString() + configExtension;
			string dir = GetCurrentConfigDir();
			return Path.Combine(dir, filename);
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

		[ObservableProperty]
		private int screenDPI = 96;
		public event EventHandler? ScreenDPIChanged;
		partial void OnScreenDPIChanged(int value) {
			ScreenDPIChanged?.Invoke(this, new EventArgs());
		}

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
