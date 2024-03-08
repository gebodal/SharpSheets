using System;
using System.Windows;
using SharpEditor.DataManagers;

namespace SharpEditor {
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application {

		public App() {
			InitializeComponent();

			this.Dispatcher.UnhandledException += OnDispatcherUnhandledException;
		}

		protected override void OnStartup(StartupEventArgs e) {

			/*
			// Does this work as expected?
			// https://stackoverflow.com/a/10203030/11002708
			AppDomain currentDomain = AppDomain.CurrentDomain;
			// Handler for unhandled exceptions.
			currentDomain.UnhandledException += GlobalUnhandledExceptionHandler;
			// Handler for exceptions in threads behind forms.
			System.Windows.Forms.Application.ThreadException += GlobalThreadExceptionHandler;
			*/

			base.OnStartup(e);

			// Create and show loading window while we're awaiting the main window
			LoadingWindow loadingWindow = new LoadingWindow();
			loadingWindow.Show();

			AppController controller = new AppController(this, e.Args);
			controller.Run();

			// Close loading window here after main window is fully loaded
			loadingWindow.Close();
		}

		/*
		private static void GlobalUnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e) {
			Exception? exObj = e.ExceptionObject as Exception;
			ILog log = LogManager.GetLogger(typeof(App)); //Log4NET
			if (exObj is Exception ex) {
				log.Error(ex.Message + "\n" + ex.StackTrace);
			}
			else {
				log.Error("Unknown exception thrown");
			}
		}

		private static void GlobalThreadExceptionHandler(object sender, System.Threading.ThreadExceptionEventArgs e) {
			Exception ex = e.Exception;
			ILog log = LogManager.GetLogger(typeof(App)); //Log4NET
			log.Error(ex.Message + "\n" + ex.StackTrace);
		}
		*/

		void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e) {
			MessageBox.Show("Unhandled exception occurred: \n" + e.Exception.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
		}

	}
}