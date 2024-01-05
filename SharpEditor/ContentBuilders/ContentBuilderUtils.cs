using System.Windows;

namespace SharpEditor.ContentBuilders {

	public static class ContentBuilderUtils {

		public static FrameworkElement SetMargin(this FrameworkElement element, Thickness margin) {
			element.Margin = margin;
			return element;
		}

		public static FrameworkElement AddMargin(this FrameworkElement element, Thickness margin) {
			Thickness current = element.Margin;
			element.Margin = new Thickness(current.Left + margin.Left, current.Top + margin.Top, current.Right + margin.Right, current.Bottom + margin.Bottom);
			return element;
		}

	}

}
