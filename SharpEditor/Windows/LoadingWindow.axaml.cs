using Avalonia.Controls;
using Avalonia.Threading;
using SharpEditor.DataManagers;
using System;
using System.Threading.Tasks;

namespace SharpEditor.Windows {

	public partial class LoadingWindow : Window {

		public LoadingWindow() {
			InitializeComponent();
			
			VersionTextBlock.Text = $"Version {SharpEditorData.GetDisplayVersionString()}";
			MessageTextBlock.Text = "Loading...";
		}

		public async Task SetMessageText(string text) {
			// Seems like overkill, but otherwise it doesn't render in time
			Dispatcher.UIThread.Invoke(() => {
				MessageTextBlock.Text = text;
				InvalidateVisual();
			}, DispatcherPriority.Render);
			await Task.Delay(1);
		}

		public bool IsClosed { get; private set; } = false;
		protected override void OnClosed(EventArgs e) {
			IsClosed = true;
			base.OnClosed(e);
		}

	}

}
