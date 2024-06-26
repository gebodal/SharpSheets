using Avalonia;
using Avalonia.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpEditorAvalonia.Utilities {

	public static class MouseEventHandlers {

		public static EventHandler<PointerPressedEventArgs> MakeLeftClickHandler(EventHandler<PointerPressedEventArgs> handler) {
			return (s, e) => {
				PointerPoint point = e.GetCurrentPoint(s as Visual);
				if (point.Properties.IsLeftButtonPressed) {
					handler.Invoke(s, e);
				}
			};
		}

	}

}
