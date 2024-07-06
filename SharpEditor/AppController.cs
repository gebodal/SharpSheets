using SharpEditor.Documentation;
using SharpEditor.DataManagers;
using SharpEditor.Windows;
using System;
using System.Linq;
using System.Windows;
using System.IO;
using Avalonia.Controls;
using Avalonia.Threading;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia;

namespace SharpEditor {

	public class AppController {

		private readonly App appInstance;
		public readonly SharpEditorWindow window;

		public AppController(App app) {
			this.appInstance = app;

			this.window = new SharpEditorWindow(this);
		}

		public static async Task<AppController> Create(App app, Visual currentVisual, string[]? args) {
			// Initialise static variables in data handlers
			SharpDataManager.Initialise();

			await AppInitialization(currentVisual);

			AppController controller = new AppController(app);

			// Initialise static variables in data handlers
			SharpSheetsStateManager.Initialise();
			SharpEditorPalette.Initialise();
			SharpEditorRegistries.Initialise();

			controller.window.GetObservable(Window.WindowStateProperty).Subscribe(controller.Window_StateChanged);
			controller.window.Resized += controller.Window_Resized;

			SharpEditorRegistries.OnRegistryErrorsChanged += controller.OnRegistryErrorsChanged;
			controller.window.TemplateAlertEnabled = SharpEditorRegistries.HasRegistryErrors;

			// If we were passed any arguments, check if they are files, and if so open them
			if (args is not null && args.Length > 0) {
				foreach (string arg in args) {
					if (File.Exists(arg)) {
						controller.window.OpenEditorDocument(arg, true);
					}
					// TODO What to do about invalid arguments?
				}
			}
			else {
				controller.window.OpenEmptyDocument(DocumentType.SHARPCONFIG, false, true);
			}

			return controller;
		}

		private void Window_Resized(object? sender, WindowResizedEventArgs e) {
			UpdateWindowState();
		}

		private void Window_StateChanged(WindowState state) {
			UpdateWindowState();
		}

		private void UpdateWindowState() {
			if (window.IsLoaded) {
				_ = Dispatcher.UIThread.InvokeAsync(() => {
					if (window.WindowState == WindowState.Normal) {
						SharpDataManager.Instance.WindowMaximized = false;
					}
					else if (window.WindowState == WindowState.Maximized) {
						SharpDataManager.Instance.WindowMaximized = true;
					}
				});
				// Otherwise leave unchanged
			}
		}

		public void Run() {
			// Retrieve previous maximized state
			if (SharpDataManager.Instance.WindowMaximized) {
				window.WindowState = WindowState.Maximized;
			}
			else {
				window.WindowState = WindowState.Normal;
			}

			//window.Show();
		}

		public void Exit(bool closeMainWindow) {
			//Console.WriteLine("AppController.Exit");

			documentationWindow?.Close();
			templateErrorWindow?.Close();
			settingsWindow?.Close();

			if (closeMainWindow) {
				window.Close();
			}

			if (window.IsClosed) {
				// And finally, exit application
				//Console.WriteLine("Shutdown");
				(appInstance.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown(0);
			}
		}

		#region App Initialization

		private static async Task AppInitialization(Visual visual) {
			Console.WriteLine("Checking template directory...");
			if (!SharpEditorPathInfo.IsTemplateDirectorySet) {
				Console.WriteLine("Template directory not set.");
				MessageBoxResult result = await MessageBoxes.Show($"You must select a template directory.\n\nDo you wish to select a custom directory? (The default is {SharpEditorPathInfo.GetDefaultApplicationTemplateDirectory()})\n\nThis can be changed later in Settings.", "Template Directory", MessageBoxButton.YesNo, MessageBoxImage.Information);

				if (result == MessageBoxResult.Yes) {
					string? selectedPath = (await visual.OpenDirectoryPicker(
						title: "Select template directory.",
						allowMultiple: false,
						startingLocation: Environment.GetFolderPath(Environment.SpecialFolder.Personal)
						)).FirstOrDefault();

					if (!string.IsNullOrWhiteSpace(selectedPath)) {
						SharpEditorPathInfo.TemplateDirectory = selectedPath;
					}
				}
				else {
					SharpEditorPathInfo.TemplateDirectory = SharpEditorPathInfo.GetDefaultApplicationTemplateDirectory();
				}

				await MessageBoxes.Show($"The template directory has been set to {SharpEditorPathInfo.TemplateDirectory} (this can be changed in Settings). This directory will now be created if it does not exist.", "Template Directory", MessageBoxButton.OK, MessageBoxImage.Information);
			}
			else if (!Directory.Exists(SharpEditorPathInfo.TemplateDirectory)) {
				Console.WriteLine("Template directory does not exist.");
				SharpEditorPathInfo.TemplateDirectory = SharpEditorPathInfo.GetDefaultApplicationTemplateDirectory();
				await MessageBoxes.Show($"Stored template directory path does not exist. Setting to {SharpEditorPathInfo.TemplateDirectory} (this can be changed in Settings).", "Template Directory", MessageBoxButton.OK, MessageBoxImage.Error);
			}

			if (!Directory.Exists(SharpEditorPathInfo.TemplateDirectory)) {
				Console.WriteLine("Creating template directory...");
				CreateTemplateDirectory();
			}

			Console.WriteLine("Finished checking template directory.");
		}

		private static void CreateTemplateDirectory() {
			string templateDirectory = SharpEditorPathInfo.TemplateDirectory;
			if (templateDirectory != null && !Directory.Exists(templateDirectory)) {
				Directory.CreateDirectory(templateDirectory);
				Console.WriteLine("Create directory: " + templateDirectory);
			}
		}

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

			//templateErrorWindow.Dispatcher.Invoke(() => {
			Dispatcher.UIThread.Invoke(() => {
				templateErrorWindow.SetErrors(SharpEditorRegistries.RegistryErrors);
			});

			return templateErrorWindow;
		}

		private void OnRegistryErrorsChanged() {
			bool registryErrorsAvailable = SharpEditorRegistries.HasRegistryErrors;

			Dispatcher.UIThread.Invoke(() => {
				this.window.TemplateAlertEnabled = registryErrorsAvailable;
			});

			if (templateErrorWindow != null) {
				//templateErrorWindow.Dispatcher.Invoke(() => {
				Dispatcher.UIThread.Invoke(() => {
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
				//settingsWindow.ShowDialog(this.window);
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
