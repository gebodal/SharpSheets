using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using SharpEditorAvalonia.Documentation;
using SharpEditorAvalonia.ViewModels;
using SharpEditorAvalonia.Views;
using SharpEditorAvalonia.Windows;
using System;

namespace SharpEditorAvalonia;

public partial class App : Application {

	public override void Initialize() {
		AvaloniaXamlLoader.Load(this);
	}

	public override async void OnFrameworkInitializationCompleted() {
		// Line below is needed to remove Avalonia data validation.
		// Without this line you will get duplicate validations from both Avalonia and CT
		BindingPlugins.DataValidators.RemoveAt(0);
		
		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {

			// Create and show loading window while we're awaiting the main window
			LoadingWindow loadingWindow = new LoadingWindow();
			desktop.MainWindow = loadingWindow;
			loadingWindow.Show();

			// Build controller
			AppController controller = await AppController.Create(this, desktop.Args);
			
			// Run controller
			controller.Run();

			// Close loading window here after main window is fully loaded
			loadingWindow.Close();

			// Assign application main window
			desktop.MainWindow = controller.window;
		}
		else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform) {
			singleViewPlatform.MainView = new MainView {
				DataContext = new MainViewModel()
			};
			//throw new InvalidOperationException("Application only valid for a classic desktop style application lifetime.");
		}

		base.OnFrameworkInitializationCompleted();
	}

	// TODO How to implement this?
	/*
	void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e) {
		MessageBox.Show("Unhandled exception occurred: \n" + e.Exception.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
	}
	*/

}
