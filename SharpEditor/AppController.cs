using SharpEditor.Documentation;
using SharpEditor.SharpSettingsWindow;
using SharpEditor.DataManagers;
using System;
using System.Linq;
using System.Windows;
using System.IO;

namespace SharpEditor {

	public class AppController {

		private readonly App appInstance;
		public readonly SharpEditorWindow window;
		private bool windowMaximized;

		public AppController(App app, string[] args) {
			this.appInstance = app;

			AppInitialization();

			// Initialise static variables in data handlers
			SharpDataManager.Initialise();
			SharpEditorPalette.Initialise();
			SharpEditorRegistries.Initialise();

			this.window = new SharpEditorWindow(this);

			this.window.StateChanged += Window_StateChanged;
			windowMaximized = SharpEditor.Properties.Settings.Default.WindowMaximized;

			SharpEditorRegistries.OnRegistryErrorsChanged += OnRegistryErrorsChanged;
			this.window.TemplateAlertEnabled = SharpEditorRegistries.HasRegistryErrors;

			this.appInstance.Exit += OnApplicationExit;

			// If we were passed any arguments, check if they are files, and if so open them
			foreach (string arg in args) {
				if (File.Exists(arg)) {
					window.OpenEditorDocument(arg, true);
				}
			}
		}

		private void OnApplicationExit(object? sender, ExitEventArgs e) {
			// Save window maximised setting before exiting
			SharpEditor.Properties.Settings.Default.WindowMaximized = windowMaximized;
			SharpEditor.Properties.Settings.Default.Save();
		}

		private void Window_StateChanged(object? sender, EventArgs e) {
			if(window.WindowState == WindowState.Normal) {
				windowMaximized = false;
			}
			else if(window.WindowState == WindowState.Maximized) {
				windowMaximized = true;
			}
			// Otherwise leave unchanged
		}

		public void Run() {
			// Retrieve previous maximized state
			if (windowMaximized) {
				window.WindowState = WindowState.Maximized;
			}

			window.Show();
		}

		public void Exit(bool closeMainWindow) {
			if (documentationWindow != null) {
				documentationWindow.Close();
			}

			if (templateErrorWindow != null) {
				templateErrorWindow.Close();
			}

			if (settingsWindow != null) {
				settingsWindow.Close();
			}

			if (closeMainWindow) {
				window.Close();
			}

			if (window.IsClosed) {
				// And finally, exit application
				System.Windows.Application.Current.Shutdown();
			}
		}

		#region App Initialization

		private void AppInitialization() {
			if (!SharpEditorPathInfo.IsTemplateDirectorySet) {
				MessageBoxResult result = MessageBox.Show($"You must select a template directory.\n\nDo you wish to select a custom directory? (The default is {SharpEditorPathInfo.GetDefaultApplicationTemplateDirectory()})\n\nThis can be changed later in Settings.", "Template Directory", MessageBoxButton.YesNo, MessageBoxImage.Information);

				if (result == MessageBoxResult.Yes) {
					System.Windows.Forms.FolderBrowserDialog dlg = new System.Windows.Forms.FolderBrowserDialog {
						Description = "Select template directory.",
						SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.Personal),
						ShowNewFolderButton = true
					};

					if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
						if (!string.IsNullOrWhiteSpace(dlg.SelectedPath)) {
							SharpEditorPathInfo.TemplateDirectory = dlg.SelectedPath;
						}
					}
				}
				else {
					SharpEditorPathInfo.TemplateDirectory = SharpEditorPathInfo.GetDefaultApplicationTemplateDirectory();
				}

				MessageBox.Show($"The template directory has been set to {SharpEditorPathInfo.TemplateDirectory} (this can be changed in Settings). This directory will now be created if it does not exist.", "Template Directory", MessageBoxButton.OK, MessageBoxImage.Information);
			}
			else if (!Directory.Exists(SharpEditorPathInfo.TemplateDirectory)) {
				SharpEditorPathInfo.TemplateDirectory = SharpEditorPathInfo.GetDefaultApplicationTemplateDirectory();
				MessageBox.Show($"Stored template directory path does not exist. Setting to {SharpEditorPathInfo.TemplateDirectory} (this can be changed in Settings).", "Template Directory", MessageBoxButton.OK, MessageBoxImage.Error);
			}

			if (!Directory.Exists(SharpEditorPathInfo.TemplateDirectory)) {
				CreateTemplateDirectory();
			}
		}

		private void CreateTemplateDirectory() {
			string templateDirectory = SharpEditorPathInfo.TemplateDirectory;
			if (templateDirectory != null && !Directory.Exists(templateDirectory)) {
				Directory.CreateDirectory(templateDirectory);
				Console.WriteLine("Create directory: " + templateDirectory);

				//string cardDefinitionDir = Path.Combine(templateDirectory, "CardDefinitions");
				//Directory.CreateDirectory(cardDefinitionDir);
				//CopyResourceToFile("SharpEditor.ProgramData.SpellCardDefinition.scd", Path.Combine(cardDefinitionDir, "SpellCardDefinition.scd"));
			}
		}

		/*
		private static void CopyResourceToFile(string resourceName, string filepath, bool overwrite) {
			if(File.Exists(filepath) && !overwrite) {
				return;
			}

			using (Stream resource = typeof(SharpEditorWindow).Assembly.GetManifestResourceStream(resourceName)) {
				if (resource == null) { throw new InvalidOperationException("Could not find embedded resource."); }
				using (FileStream fileStream = File.Create(filepath)) {
					resource.Seek(0, SeekOrigin.Begin);
					resource.CopyTo(fileStream);
				}
			}
		}
		*/

		#endregion

		#region Documentation

		private DocumentationWindow? documentationWindow;

		public DocumentationWindow ActivateDocumentationWindow() {
			if (documentationWindow == null || documentationWindow.IsClosed) {
				documentationWindow = new DocumentationWindow();
				documentationWindow.SetPosition(true, 1.0 / 3);
				documentationWindow.Show();
			}
			else {
				if (documentationWindow.WindowState == WindowState.Minimized) {
					documentationWindow.WindowState = WindowState.Normal;
				}
				documentationWindow.Activate();
			}
			return documentationWindow;
		}

		#endregion

		#region Template Errors

		private TemplateErrorWindow? templateErrorWindow;

		public event EventHandler? TemplateErrorsUpdated;

		private TemplateError[]? _templateErrors;
		public TemplateError[] TemplateErrors {
			get {
				return _templateErrors ?? Array.Empty<TemplateError>();
			}
			set {
				_templateErrors = value;
				TemplateErrorsUpdated?.Invoke(this, new EventArgs());
			}
		}

		public bool HasTemplateErrors { get { return _templateErrors != null && _templateErrors.Length > 0; } }

		public TemplateErrorWindow ActivateTemplateErrorWindow() {
			if (templateErrorWindow == null || templateErrorWindow.IsClosed) {
				templateErrorWindow = new TemplateErrorWindow(this);
				templateErrorWindow.Show();
			}
			else {
				if(templateErrorWindow.WindowState == WindowState.Minimized) {
					templateErrorWindow.WindowState = WindowState.Normal;
				}
				templateErrorWindow.Activate();
			}

			templateErrorWindow.Dispatcher.Invoke(() => {
				templateErrorWindow.SetErrors(SharpEditorRegistries.RegistryErrors);
			});

			return templateErrorWindow;
		}

		private void OnRegistryErrorsChanged() {
			bool registryErrorsAvailable = SharpEditorRegistries.HasRegistryErrors;

			this.window.TemplateAlertEnabled = registryErrorsAvailable;

			if (templateErrorWindow != null) {
				templateErrorWindow.Dispatcher.Invoke(() => {
					if (registryErrorsAvailable) {
						templateErrorWindow.SetErrors(SharpEditorRegistries.RegistryErrors);
					}
					else {
						templateErrorWindow.SetErrors(Enumerable.Empty<TemplateError>());
					}
				});
			}
		}

		#endregion

		#region Settings

		private SettingsWindow? settingsWindow;

		public SettingsWindow ActivateSettingsWindow() {
			if (settingsWindow == null || settingsWindow.IsClosed) {
				settingsWindow = new SettingsWindow();
				settingsWindow.Owner = this.window;
				settingsWindow.Show();
			}
			else {
				if (settingsWindow.WindowState == WindowState.Minimized) {
					settingsWindow.WindowState = WindowState.Normal;
				}
				settingsWindow.Activate();
			}
			return settingsWindow;
		}

		#endregion

	}

}
