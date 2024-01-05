using System;
using System.Linq;

namespace SharpSheets.Utilities {
	public static class EnumUtils {

		/// <summary></summary>
		/// <exception cref="FormatException"></exception>
		public static object ParseEnum(Type type, string value) {
			try {
				object parsed = Enum.Parse(type, value, true);
				if (Enum.IsDefined(type, parsed)) {
					return parsed;
				}
				else {
					throw new FormatException($"{type.Name} must be one of the following: " + string.Join(", ", Enum.GetValues(type).Cast<object>()));
				}
			}
			catch (ArgumentException) {
				throw new FormatException($"{type.Name} must be one of the following: " + string.Join(", ", Enum.GetValues(type).Cast<object>()));
			}
		}

		/// <summary></summary>
		/// <exception cref="FormatException"></exception>
		public static T ParseEnum<T>(string value) where T : Enum {
			return (T)ParseEnum(typeof(T), value);
		}

		public static bool IsDefined(Type type, string value) {
			return Enum.GetNames(type).Any(x => string.Equals(x, value, StringComparison.InvariantCultureIgnoreCase));
			//return Enum.TryParse(value, true, out T _);
		}

		public static bool IsDefined<T>(string value) where T : Enum {
			return IsDefined(typeof(T), value);
		}

	}
}
