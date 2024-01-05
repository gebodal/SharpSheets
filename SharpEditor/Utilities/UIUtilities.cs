using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace SharpEditor.Utilities {
	public static class UIUtilities {

		public static void AddRange(this UIElementCollection uiElementCollection, IEnumerable<UIElement> elements) {
			foreach(UIElement element in elements) {
				uiElementCollection.Add(element);
			}
		}

	}
}
