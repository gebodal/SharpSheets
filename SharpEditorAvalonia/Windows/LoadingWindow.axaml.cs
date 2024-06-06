using Avalonia.Controls;
using SharpEditorAvalonia.DataManagers;

namespace SharpEditorAvalonia.Windows {

	public partial class LoadingWindow : Window {

		public LoadingWindow() {
			InitializeComponent();
			
			VersionTextBlock.Text = $"Version {SharpEditorData.GetDisplayVersionString()}";
		}

	}

}
