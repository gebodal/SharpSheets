using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpSheets.Utilities {

	public readonly struct UnitInterval : IFormattable, IEquatable<UnitInterval> {

		public float Value { get; }

		/// <summary></summary>
		/// <exception cref="ArgumentException">Thrown when <paramref name="value"/> is outside the range (0,1).</exception>
		public UnitInterval(float value) {
			if (value < 0f || value > 1f) {
				throw new ArgumentException("Value must be between 0 and 1 (inclusive).", nameof(value));
			}
			this.Value = value;
		}

		public static implicit operator float(UnitInterval value) {
			return value.Value;
		}

		/// <summary></summary>
		/// <exception cref="FormatException"></exception>
		public static UnitInterval Parse(string str, IFormatProvider? provider) {
			float value;
			try {
				value = float.Parse(str, provider);
			}
			catch (FormatException) {
				throw new FormatException($"\"{str}\" is not a number.");
			}
			if (value < 0f || value > 1f) {
				throw new FormatException($"\"{str}\" is not a unit interval.");
			}
			return new UnitInterval(value);
		}

		/// <summary></summary>
		/// <exception cref="FormatException"></exception>
		public static UnitInterval Parse(string str) {
			return Parse(str, null);
		}

		public override string ToString() {
			return Value.ToString();
		}

		public string ToString(IFormatProvider? formatProvider) {
			return Value.ToString(formatProvider);
		}

		public string ToString(string? format, IFormatProvider? formatProvider) {
			return Value.ToString(format, formatProvider);
		}

		public override bool Equals(object? obj) {
			return (obj is UnitInterval unitInterval && Equals(unitInterval))
				|| (obj is float num && Value == num)
				|| (obj is int inum && Value == inum)
				|| (obj is uint unum && Value == unum);
		}

		public bool Equals(UnitInterval other) {
			return Value == other.Value;
		}

		public static UnitInterval operator *(UnitInterval a, UnitInterval b) {
			return new UnitInterval(a.Value * b.Value);
		}

		public static UnitInterval operator /(UnitInterval a, UnitInterval b) {
			return new UnitInterval(a.Value / b.Value);
		}
		public static UnitInterval operator /(UnitInterval a, uint b) {
			return new UnitInterval(a.Value / b);
		}

		public static bool operator ==(UnitInterval left, UnitInterval right) {
			return left.Equals(right);
		}

		public static bool operator !=(UnitInterval left, UnitInterval right) {
			return !left.Equals(right);
		}

		public override int GetHashCode() {
			return Value.GetHashCode();
		}

	}
}
