using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeboPdf.Utilities {

	public static class HexWriter {

		private static readonly char[] hexAlphabet = new char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F' };
		
		public static string ToString(byte[] bytes) {
			StringBuilder result = new StringBuilder(bytes.Length * 2);

			foreach (byte b in bytes) {
				result.Append(hexAlphabet[(int)(b >> 4)]);
				result.Append(hexAlphabet[(int)(b & 0xF)]);
			}

			return result.ToString();
		}

		public static string ToString(byte b) {
			StringBuilder result = new StringBuilder(2);

			result.Append(hexAlphabet[(int)(b >> 4)]);
			result.Append(hexAlphabet[(int)(b & 0xF)]);

			return result.ToString();
		}

		public static byte ToByte(char c1, char c2) {
			return (byte)((ToByte(c1) << 4) | ToByte(c2));
		}

		private static byte ToByte(char c) {
			if(c >= 'a' && c <= 'f') {
				return (byte)(c - 'a' + 10);
			}
			else if (c >= 'A' && c <= 'F') {
				return (byte)(c - 'A' + 10);
			}
			else if (c >= '0' && c <= '9') {
				return (byte)(c - '0');
			}
			else {
				throw new FormatException($"Invalid hex character \'{c}\'.");
			}
		}

		public static float ConvertF2Dot14(ushort bytes) {
			// https://stackoverflow.com/q/53915557/11002708
			//Console.WriteLine($"Converting {bytes:X} {Convert.ToString(bytes, 2).PadLeft(16, '0')}");

			float intPart = (float)(bytes >> 14);
			if (intPart > 1f) {
				intPart -= 4f;
			}

			float frac = (float)(bytes & 0b11_1111_1111_1111) / 16384f; // 2^14 = 16384
			
			return intPart + frac;

			/*
			short signed = unchecked((short)bytes);
			return signed / 16384.0f;
			*/
		}

		public static ushort ConvertF2Dot14(float value) {
			if (float.IsNaN(value) || float.IsInfinity(value))
				throw new ArgumentOutOfRangeException(nameof(value));

			// Valid F2.14 range
			const float Min = -2.0f;
			const float Max = 1.99993896484375f; // (2^15 - 1) / 2^14

			if (value < Min || value > Max)
				throw new ArgumentOutOfRangeException(nameof(value), "Value out of F2.14 range.");

			// Scale in double for precision, round to nearest
			double scaled = Math.Round((double)value * 16384.0, MidpointRounding.ToEven);

			// Cast through short to preserve sign, then reinterpret as ushort
			short signed = (short)scaled;
			return unchecked((ushort)signed);
		}

	}

}
