using Avalonia;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpEditor.Utilities {

	public static class AppResourceUtils {

		public static T? GetResource<T>(this Application app, object key) {
			if (app.Resources.TryGetResource(key, app.ActualThemeVariant, out object? resource) && resource is T t) {
				return t;
			}
			return default;
		}

	}

}
