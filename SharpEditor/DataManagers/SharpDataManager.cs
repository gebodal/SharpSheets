using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace SharpEditor.DataManagers {

	public partial class SharpDataManager : ObservableObject {

		private static ConfigName ConfigName => new ConfigName(SharpEditorData.GetEditorName(), ".conf");

		public static void Initialise() { } // Dummy method to force static initialisation

		public static SharpDataManager Instance { get; }

		static SharpDataManager() {

			// Does the settings file exist?
			// If yes, load it
			// If not, check for an earlier version, load that if it exists and save a new copy with the new version
			// If no earlier version exists, create an empty object
			// Save the object in Instance

			Console.WriteLine("Calculated current config path: " + SharpConfigManager.GetCurrentConfigPath(ConfigName));

			SharpDataManagerContent? loaded = SharpConfigManager.Load<SharpDataManagerContent>(ConfigName, out bool latest);

			if(loaded is not null) {
				Instance = new SharpDataManager(loaded);
			}
			else {
				Console.WriteLine("Create new config.");
				Instance = new SharpDataManager(new SharpDataManagerContent());
			}

			if(latest && loaded is null) {
				SharpConfigManager.SaveBackup(ConfigName);
			}

			if (!latest) {
				SharpConfigManager.Save(Instance.GetContent(), ConfigName);
			}
		}

		protected override void OnPropertyChanged(PropertyChangedEventArgs e) {
			base.OnPropertyChanged(e);
			
			Save();
		}

		private void Save() {
			SharpConfigManager.Save(GetContent(), ConfigName);
		}

		public class SharpDataManagerContent {

			// Designer properties
			public bool DesignerDisplayFields { get; set; } = true;
			public bool DesignerViewerOpenDefault { get; set; } = false;
			public int ScreenDPI { get; set; } = 96;

			// Generator settings
			public bool OpenOnGenerate { get; set; } = false;

			// Editor settings
			public bool ShowLineNumbers { get; set; } = true;
			public bool ShowEndOfLine { get; set; } = false;
			public bool ShowWhitespace { get; set; } = false;
			public bool WrapLines { get; set; } = false;
			public double TextZoom { get; set; } = 1.0;
			public int TabWidth { get; set; } = 4;

			// Window settings
			public bool WindowMaximized { get; set; } = true;

			// Warning and error settings
			public bool WarnFontLicensing { get; set; } = true;

			// File system settings
			public string LastFileDirectory { get; set; } = "";
			public string TemplateDirectory { get; set; } = "";

			public SharpDataManagerContent() { }

		}

		private SharpDataManager(SharpDataManagerContent content) {
			// Designer properties
			designerDisplayFields = content.DesignerDisplayFields;
			designerViewerOpenDefault = content.DesignerViewerOpenDefault;
			screenDPI = content.ScreenDPI;

			// Generator settings
			openOnGenerate = content.OpenOnGenerate;

			// Editor settings
			showLineNumbers = content.ShowLineNumbers;
			showEndOfLine = content.ShowEndOfLine;
			showWhitespace = content.ShowWhitespace;
			wrapLines = content.WrapLines;
			textZoom = content.TextZoom;
			tabWidth = content.TabWidth;

			// Window settings
			windowMaximized = content.WindowMaximized;

			// Warning and error settings
			warnFontLicensing = content.WarnFontLicensing;

			// File system settings
			lastFileDirectory = content.LastFileDirectory;
			templateDirectory = content.TemplateDirectory;
		}

		private SharpDataManagerContent GetContent() {
			return new SharpDataManagerContent() {
				// Designer properties
				DesignerDisplayFields = DesignerDisplayFields,
				DesignerViewerOpenDefault = DesignerViewerOpenDefault,
				ScreenDPI = ScreenDPI,

				// Generator settings
				OpenOnGenerate = OpenOnGenerate,

				// Editor settings
				ShowLineNumbers = ShowLineNumbers,
				ShowEndOfLine = ShowEndOfLine,
				ShowWhitespace = ShowWhitespace,
				WrapLines = WrapLines,
				TextZoom = TextZoom,
				TabWidth = TabWidth,

				// Window settings
				WindowMaximized = WindowMaximized,

				// Warning and error settings
				WarnFontLicensing = WarnFontLicensing,

				// File system settings
				LastFileDirectory = LastFileDirectory,
				TemplateDirectory = TemplateDirectory,
			};
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
		private bool showWhitespace = false;
		public event EventHandler? ShowWhitespaceChanged;
		partial void OnShowWhitespaceChanged(bool value) {
			ShowWhitespaceChanged?.Invoke(this, new EventArgs());
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

		[ObservableProperty]
		private int tabWidth = 4;
		public event EventHandler? TabWidthChanged;
		partial void OnTabWidthChanged(int value) {
			TabWidthChanged?.Invoke(this, new EventArgs());
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

	}

}
