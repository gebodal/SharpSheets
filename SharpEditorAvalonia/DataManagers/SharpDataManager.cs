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
				Instance = LoadSettings(configPath) ?? new SharpDataManager();
			}
			else if(GetOldConfigPath() is string oldConfigPath) {
				Instance = LoadSettings(oldConfigPath) ?? new SharpDataManager();
				Instance.Save();
			}
			else {
				Instance = new SharpDataManager();
			}

		}

		private SharpDataManager() { }

		protected override void OnPropertyChanged(PropertyChangedEventArgs e) {
			base.OnPropertyChanged(e);

			Save();
		}

		private static SharpDataManager? LoadSettings(string filePath) {
			string jsonText = File.ReadAllText(filePath);
			return JsonSerializer.Deserialize<SharpDataManager>(jsonText, jsonSerializeoptions);
		}

		private void Save() {
			string jsonText = JsonSerializer.Serialize(this, jsonSerializeoptions);
			File.WriteAllText(configPath, jsonText);
		}

		private static string GetCurrentConfigPath() {
			string filename = SharpEditorData.GetEditorName() + SharpEditorData.GetVersionString();
			string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
			return Path.Combine(appData, filename);
		}

		private static string? GetOldConfigPath() {
			string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
			string editorName = SharpEditorData.GetEditorName();

			IEnumerable<(string path, Version version)> GetVersions(IEnumerable<string> paths) {
				foreach (string path in paths) {
					string filename = Path.GetFileName(path);
					string versionStr = filename[editorName.Length..^5];
					if (Version.TryParse(versionStr, out Version? parsed)) {
						yield return (path, parsed);
					}
				}
			}

			(string path, Version version)[] existing = GetVersions(Directory.GetFiles(appData, $"{editorName}*.json", SearchOption.TopDirectoryOnly)).ToArray();

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

		[ObservableProperty]
		private bool designerViewerOpenDefault = false;

		#endregion

		#region Generator settings

		[ObservableProperty]
		private bool openOnGenerate = false;

		#endregion

		#region Editor settings

		[ObservableProperty]
		private bool showLineNumbers = true;

		[ObservableProperty]
		private bool showEndOfLine = false;

		[ObservableProperty]
		private bool wrapLines = false;

		[ObservableProperty]
		private double textZoom = 1.0;

		#endregion

		#region Window settings

		[ObservableProperty]
		private bool windowMaximized = true;

		#endregion

		#region Warning and error settings

		[ObservableProperty]
		private bool warnFontLicensing = true;

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
