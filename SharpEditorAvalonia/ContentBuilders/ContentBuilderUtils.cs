using Avalonia;
using Avalonia.Controls;

namespace SharpEditorAvalonia.ContentBuilders {

	// FrameworkElement -> Control

	public static class ContentBuilderUtils {

		public static Control SetMargin(this Control element, Thickness margin) {
			element.Margin = margin;
			return element;
		}

		public static Control AddMargin(this Control element, Thickness margin) {
			Thickness current = element.Margin;
			//element.Margin = new Thickness(current.Left + margin.Left, current.Top + margin.Top, current.Right + margin.Right, current.Bottom + margin.Bottom);
			element.Margin = current + margin;
			return element;
		}

	}

}
