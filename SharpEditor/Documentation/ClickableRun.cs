using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;

namespace SharpEditor.Documentation {

	public class ClickableRun : Run {

		public ClickableRun() : base() { Initialize(); }
		public ClickableRun(string text) : base(text) { Initialize(); }
		public ClickableRun(string text, TextPointer insertionPosition) : base(text, insertionPosition) { Initialize(); }

		private void Initialize() {
			this.Foreground = DocumentationWindow.HyperlinkColor;
		}

		protected override void OnIsMouseDirectlyOverChanged(DependencyPropertyChangedEventArgs e) {
			base.OnIsMouseDirectlyOverChanged(e);

			if (IsMouseDirectlyOver) {
				this.TextDecorations = System.Windows.TextDecorations.Underline;
				this.Cursor = Cursors.Arrow;
			}
			else {
				this.TextDecorations = null;
				this.Cursor = null;
			}
		}

	}

}
