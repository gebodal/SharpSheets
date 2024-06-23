using Avalonia.Controls;
using SharpEditorAvalonia.DataManagers;
using System;

namespace SharpEditorAvalonia.Windows {

	public partial class LoadingWindow : Window {

		public LoadingWindow() {
			InitializeComponent();
			
			VersionTextBlock.Text = $"Version {SharpEditorData.GetDisplayVersionString()}";
		}


		public bool IsClosed { get; private set; } = false;
		protected override void OnClosed(EventArgs e) {
			IsClosed = true;
			base.OnClosed(e);
		}

	}

}
