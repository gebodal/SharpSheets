using System;

namespace SharpSheets.Utilities {
	
	public readonly struct UFloat : IFormattable, IEquatable<UFloat> {

		public float Value { get; }

		/// <summary></summary>
		/// <exception cref="ArgumentException">Thrown when <paramref name="value"/> is negative.</exception>
		public UFloat(float value) {
			if(value < 0) {
				throw new ArgumentException("Value must be positive.", nameof(value));
			}
			this.Value = value;
		}

		public static implicit operator float(UFloat value) {
			return value.Value;
		}

		/// <summary></summary>
		/// <exception cref="FormatException"></exception>
		public static UFloat Parse(string str, IFormatProvider? provider) {
			float value;
			try {
				value = float.Parse(str, provider);
			}
			catch (FormatException) {
				throw new FormatException($"\"{str}\" is not a number.");
			}
			if (value < 0) {
				throw new FormatException($"\"{str}\" is not a positive number.");
			}
			return new UFloat(value);
		}

		/// <summary></summary>
		/// <exception cref="FormatException"></exception>
		public static UFloat Parse(string str) {
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
			return (obj is UFloat ufloat && Equals(ufloat))
				|| (obj is float num && Value == num)
				|| (obj is int inum && Value == inum)
				|| (obj is uint unum && Value == unum);
		}

		public bool Equals(UFloat other) {
			return Value == other.Value;
		}

		public static UFloat operator +(UFloat a, UFloat b) {
			return new UFloat(a.Value + b.Value);
		}
		public static UFloat operator +(UFloat a, uint b) {
			return new UFloat(a.Value + b);
		}
		public static UFloat operator +(uint a, UFloat b) {
			return new UFloat(a + b.Value);
		}

		public static UFloat operator *(UFloat a, UFloat b) {
			return new UFloat(a.Value * b.Value);
		}
		public static UFloat operator *(UFloat a, uint b) {
			return new UFloat(a.Value * b);
		}
		public static UFloat operator *(uint a, UFloat b) {
			return new UFloat(a * b.Value);
		}

		public static UFloat operator /(UFloat a, UFloat b) {
			return new UFloat(a.Value / b.Value);
		}
		public static UFloat operator /(UFloat a, uint b) {
			return new UFloat(a.Value / b);
		}
		public static UFloat operator /(uint a, UFloat b) {
			return new UFloat(a / b.Value);
		}

		public static bool operator ==(UFloat left, UFloat right) {
			return left.Equals(right);
		}

		public static bool operator !=(UFloat left, UFloat right) {
			return !left.Equals(right);
		}

		public override int GetHashCode() {
			return Value.GetHashCode();
		}
	}

}
