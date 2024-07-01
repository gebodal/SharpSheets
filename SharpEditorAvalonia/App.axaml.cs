using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using SharpEditorAvalonia.Documentation;
using SharpEditorAvalonia.Windows;
using System;
using System.Threading.Tasks;

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
			loadingWindow.BringIntoView();

			await Task.Delay(100); // How else to get the splash screen to render?!

			// Build controller
			AppController controller = await AppController.Create(this, loadingWindow, desktop.Args);
			
			// Run controller
			controller.Run();

			// Assign application main window
			desktop.MainWindow = controller.window;
			controller.window.Show();
			
			// This does not work.
			/*
			controller.window.Loaded += async delegate {
				if (!loadingWindow.IsClosed) {
					await Task.Delay(2000);
					loadingWindow.Close();
				}
			};
			*/

			// Close loading window here after main window is fully loaded
			loadingWindow.Close();

			//AppDomain.CurrentDomain.UnhandledException += OnDispatcherUnhandledException;
		}
		else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform) {
			singleViewPlatform.MainView = new Grid() {
				Children = {
					new TextBlock() {
						Text = "Application only valid for a classic desktop style application lifetime.",
						VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
						HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
					}
				}
			};
			//throw new InvalidOperationException("Application only valid for a classic desktop style application lifetime.");
		}

		base.OnFrameworkInitializationCompleted();
	}

	// How to implement this?
	/*
	void OnDispatcherUnhandledException(object sender, UnhandledExceptionEventArgs e) {
		Dispatcher.UIThread.Invoke(async () => {
			await MessageBoxes.Show("Unhandled exception occurred: \n" + ((e.ExceptionObject as Exception)?.Message ?? "No message."), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
		});
	}
	*/

}
