using SharpEditor.DataManagers;
using System.Windows;

namespace SharpEditor {
	/// <summary>
	/// Interaction logic for LoadingWindow.xaml
	/// </summary>
	public partial class LoadingWindow : Window {
		public LoadingWindow() {
			InitializeComponent();

			VersionTextBlock.Text = $"Version {SharpEditorData.GetDisplayVersionString()}";
		}
	}
}
