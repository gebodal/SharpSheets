using CommunityToolkit.Mvvm.ComponentModel;
using System;
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

			SharpDataManager? loaded = SharpConfigManager.Load<SharpDataManager>(ConfigName, out bool latest);

			if(loaded is not null) {
				Instance = loaded;
			}
			else {
				Console.WriteLine("Create new config.");
				Instance = new SharpDataManager();
			}

			if(latest && loaded is null) {
				SharpConfigManager.SaveBackup(ConfigName);
			}

			if (!latest) {
				SharpConfigManager.Save(Instance, ConfigName);
			}
		}

		public SharpDataManager() { } // I don't like this being public

		protected override void OnPropertyChanged(PropertyChangedEventArgs e) {
			base.OnPropertyChanged(e);
			
			Save();
		}

		private void Save() {
			SharpConfigManager.Save(this, ConfigName);
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

	}

}
