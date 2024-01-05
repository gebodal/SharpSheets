using System.ComponentModel;
using System.Windows;

namespace SharpEditor {
	/// <summary>
	/// Interaction logic for DesignerFloating.xaml
	/// </summary>
	public partial class DesignerFloating : Window {

		readonly SharpDocumentEditor? editor;

		public DesignerFloating() {
			InitializeComponent();
		}

		private DesignerFloating(SharpDocumentEditor editor) : this() {
			this.editor = editor;
		}

		public static DesignerFloating? Open(SharpDocumentEditor editor) {
			if (editor.DesignerHolder.Children.Contains(editor.DesignerArea)) {
				DesignerFloating floating = new DesignerFloating(editor);
				editor.DesignerHolder.Children.Remove(editor.DesignerArea);
				floating.MainPanel.Children.Add(editor.DesignerArea);
				return floating;
			}
			else {
				return null;
			}
		}

		protected override void OnClosing(CancelEventArgs e) {
			if(editor != null) {
				this.MainPanel.Children.Remove(editor.DesignerArea);
				editor.DesignerHolder.Children.Add(editor.DesignerArea);
				editor.IsDesignerVisible = true;
				editor.DesignerFloating = null;
			}

			base.OnClosing(e);
		}
	}
}
