using Avalonia;
using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace SharpEditor.Utilities {

	/// <summary>
	/// This is a converter which will add two numbers
	/// </summary>
	public class MultiplyConverter : IValueConverter {
		public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) {
			// For multiply this is simple. Just return the product of the value and the parameter.
			return (decimal?)value * (decimal?)parameter;
		}

		public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) {
			// If we want to convert back, we need to divide instead of multiply.
			return (decimal?)value / (decimal?)parameter;
		}
	}

	public class TopMarginConverter : IValueConverter {
		public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) {
			// For multiply this is simple. Just return the product of the value and the parameter.
			return new Thickness(0, ((value as double?) ?? 0.0) * ((parameter as double?) ?? 1.0), 0, 0);
		}

		public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) {
			throw new NotImplementedException();
		}
	}

	public class LeftMarginConverter : IValueConverter {
		public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) {
			// For multiply this is simple. Just return the product of the value and the parameter.
			return new Thickness(((value as double?) ?? 0.0) * ((parameter as double?) ?? 1.0), 0, 0, 0);
		}

		public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) {
			throw new NotImplementedException();
		}
	}

	public class HorizontalMarginConverter : IValueConverter {
		public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) {
			// For multiply this is simple. Just return the product of the value and the parameter.
			return new Thickness(((value as double?) ?? 0.0) * ((parameter as double?) ?? 1.0), 0);
		}

		public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) {
			throw new NotImplementedException();
		}
	}

	public class MarginConverter : IValueConverter {
		public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) {
			// For multiply this is simple. Just return the product of the value and the parameter.
			return new Thickness(((value as double?) ?? 0.0) * ((parameter as double?) ?? 1.0));
		}

		public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) {
			throw new NotImplementedException();
		}
	}

}
