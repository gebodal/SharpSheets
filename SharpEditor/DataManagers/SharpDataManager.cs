using System;
using System.Windows;
using System.Configuration;
using System.IO;

namespace SharpEditor.DataManagers {

	public class SharpDataManager : DependencyObject {

		public static void Initialise() { } // Dummy method to force static initialisation

		public static SharpDataManager Instance { get; }

		static SharpDataManager() {
			CheckSettingsFile(); // Upgrade settings file if required
			Instance = new SharpDataManager();
			InitialiseInstance();
		}

		private SharpDataManager() { }

		private static void CheckSettingsFile() {
			// With thanks to: https://stackoverflow.com/a/74227345/11002708
			string configPath = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal).FilePath;
			if (!File.Exists(configPath)) {
				//Existing user config does not exist, so load settings from previous assembly
				SharpEditor.Properties.Settings.Default.Upgrade();
				SharpEditor.Properties.Settings.Default.Reload();
				SharpEditor.Properties.Settings.Default.Save();
			}
		}

		private static void InitialiseInstance() {
			Instance.SetValue(DesignerDisplayFieldsProperty, SharpEditor.Properties.Settings.Default.DesignerDisplayFields);
			Instance.SetValue(DesignerViewerOpenDefaultProperty, SharpEditor.Properties.Settings.Default.DesignerViewerOpenDefault);
			Instance.SetValue(OpenOnGenerateProperty, SharpEditor.Properties.Settings.Default.OpenOnGenerate);
			Instance.SetValue(ShowLineNumbersProperty, SharpEditor.Properties.Settings.Default.ShowLineNumbers);
			Instance.SetValue(ShowEndOfLineProperty, SharpEditor.Properties.Settings.Default.ShowEndOfLine);
			Instance.SetValue(WrapLinesProperty, SharpEditor.Properties.Settings.Default.WrapLines);
			Instance.SetValue(TextZoomProperty, SharpEditor.Properties.Settings.Default.TextZoom);
			Instance.SetValue(WarnFontLicensingProperty, SharpEditor.Properties.Settings.Default.WarnFontLicensing);
		}

		#region DesignerDisplayFields

		public static readonly DependencyProperty DesignerDisplayFieldsProperty =
			DependencyProperty.Register("DesignerDisplayFields", typeof(bool),
			typeof(SharpDataManager), new UIPropertyMetadata(true, OnDesignerDisplayFieldsChanged));
		public event EventHandler? DesignerDisplayFieldsChanged;

		public bool DesignerDisplayFields {
			get {
				return (bool)GetValue(DesignerDisplayFieldsProperty);
			}
			set {
				SetValue(DesignerDisplayFieldsProperty, value);
			}
		}

		private static void OnDesignerDisplayFieldsChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e) {
			if (sender is SharpDataManager manager) {
				SharpEditor.Properties.Settings.Default.DesignerDisplayFields = manager.DesignerDisplayFields;
				SharpEditor.Properties.Settings.Default.Save();
				manager.DesignerDisplayFieldsChanged?.Invoke(sender, EventArgs.Empty);
			}
		}

		#endregion

		#region DesignerViewerOpenDefault

		public static readonly DependencyProperty DesignerViewerOpenDefaultProperty =
			DependencyProperty.Register("DesignerViewerOpenDefault", typeof(bool),
			typeof(SharpDataManager), new UIPropertyMetadata(true, OnDesignerViewerOpenDefaultChanged));
		public event EventHandler? DesignerViewerOpenDefaultChanged;

		public bool DesignerViewerOpenDefault {
			get {
				return (bool)GetValue(DesignerViewerOpenDefaultProperty);
			}
			set {
				SetValue(DesignerViewerOpenDefaultProperty, value);
			}
		}

		private static void OnDesignerViewerOpenDefaultChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e) {
			if (sender is SharpDataManager manager) {
				SharpEditor.Properties.Settings.Default.DesignerViewerOpenDefault = Instance.DesignerViewerOpenDefault;
				SharpEditor.Properties.Settings.Default.Save();
				manager.DesignerViewerOpenDefaultChanged?.Invoke(sender, EventArgs.Empty);
			}
		}

		#endregion

		#region OpenOnGenerate

		public static readonly DependencyProperty OpenOnGenerateProperty =
			DependencyProperty.Register("OpenOnGenerate", typeof(bool),
			typeof(SharpDataManager), new UIPropertyMetadata(false, OnOpenOnGenerateChanged));
		public event EventHandler? OpenOnGenerateChanged;

		public bool OpenOnGenerate {
			get {
				return (bool)GetValue(OpenOnGenerateProperty);
			}
			set {
				SetValue(OpenOnGenerateProperty, value);
			}
		}

		private static void OnOpenOnGenerateChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e) {
			if (sender is SharpDataManager manager) {
				SharpEditor.Properties.Settings.Default.OpenOnGenerate = manager.OpenOnGenerate;
				SharpEditor.Properties.Settings.Default.Save();
				manager.OpenOnGenerateChanged?.Invoke(sender, EventArgs.Empty);
			}
		}

		#endregion

		#region ShowLineNumbers

		public static readonly DependencyProperty ShowLineNumbersProperty =
			DependencyProperty.Register("ShowLineNumbers", typeof(bool),
			typeof(SharpDataManager), new UIPropertyMetadata(true, OnShowLineNumbersChanged));
		public event EventHandler? ShowLineNumbersChanged;

		public bool ShowLineNumbers {
			get {
				return (bool)GetValue(ShowLineNumbersProperty);
			}
			set {
				SetValue(ShowLineNumbersProperty, value);
			}
		}

		private static void OnShowLineNumbersChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e) {
			if (sender is SharpDataManager manager) {
				SharpEditor.Properties.Settings.Default.ShowLineNumbers = Instance.ShowLineNumbers;
				SharpEditor.Properties.Settings.Default.Save();
				manager.ShowLineNumbersChanged?.Invoke(sender, EventArgs.Empty);
			}
		}

		#endregion

		#region ShowLineNumbers

		public static readonly DependencyProperty ShowEndOfLineProperty =
			DependencyProperty.Register("ShowEndOfLine", typeof(bool),
			typeof(SharpDataManager), new UIPropertyMetadata(false, OnShowEndOfLineChanged));
		public event EventHandler? ShowEndOfLineChanged;

		public bool ShowEndOfLine {
			get {
				return (bool)GetValue(ShowEndOfLineProperty);
			}
			set {
				SetValue(ShowEndOfLineProperty, value);
			}
		}

		private static void OnShowEndOfLineChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e) {
			if (sender is SharpDataManager manager) {
				SharpEditor.Properties.Settings.Default.ShowEndOfLine = Instance.ShowEndOfLine;
				SharpEditor.Properties.Settings.Default.Save();
				manager.ShowEndOfLineChanged?.Invoke(sender, EventArgs.Empty);
			}
		}

		#endregion
		
		#region ShowLineNumbers

		public static readonly DependencyProperty WrapLinesProperty =
			DependencyProperty.Register("WrapLines", typeof(bool),
			typeof(SharpDataManager), new UIPropertyMetadata(false, OnWrapLinesChanged));
		public event EventHandler? WrapLinesChanged;

		public bool WrapLines {
			get {
				return (bool)GetValue(WrapLinesProperty);
			}
			set {
				SetValue(WrapLinesProperty, value);
			}
		}

		private static void OnWrapLinesChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e) {
			if (sender is SharpDataManager manager) {
				SharpEditor.Properties.Settings.Default.WrapLines = Instance.WrapLines;
				SharpEditor.Properties.Settings.Default.Save();
				manager.WrapLinesChanged?.Invoke(sender, EventArgs.Empty);
			}
		}

		#endregion

		#region TextZoom

		public static readonly DependencyProperty TextZoomProperty =
			DependencyProperty.Register("TextZoom", typeof(double),
			typeof(SharpDataManager), new UIPropertyMetadata(1.0, OnTextZoomChanged));
		public event EventHandler? TextZoomChanged;

		public double TextZoom {
			get {
				return (double)GetValue(TextZoomProperty);
			}
			set {
				SetValue(TextZoomProperty, value);
			}
		}

		private static void OnTextZoomChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e) {
			if (sender is SharpDataManager manager) {
				SharpEditor.Properties.Settings.Default.TextZoom = Instance.TextZoom;
				SharpEditor.Properties.Settings.Default.Save();
				manager.TextZoomChanged?.Invoke(sender, EventArgs.Empty);
			}
		}

		#endregion

		#region WarnFontLicensing

		public static readonly DependencyProperty WarnFontLicensingProperty =
			DependencyProperty.Register("WarnFontLicensing", typeof(bool),
			typeof(SharpDataManager), new UIPropertyMetadata(true, OnWarnFontLicensingChanged));
		public event EventHandler? WarnFontLicensingChanged;

		public bool WarnFontLicensing {
			get {
				return (bool)GetValue(WarnFontLicensingProperty);
			}
			set {
				SetValue(WarnFontLicensingProperty, value);
			}
		}

		private static void OnWarnFontLicensingChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e) {
			if (sender is SharpDataManager manager) {
				SharpEditor.Properties.Settings.Default.WarnFontLicensing = Instance.WarnFontLicensing;
				SharpEditor.Properties.Settings.Default.Save();
				manager.WarnFontLicensingChanged?.Invoke(sender, EventArgs.Empty);
			}
		}

		#endregion

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
	}

}
