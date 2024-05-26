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
			byte intPart = (byte)(bytes >> 14);

			if ((intPart & 0b10) > 0) {
				intPart = (byte)-(((~intPart) & 0b11) + 1); // 2's complement
			}
			
			ushort fracPart = (ushort)(bytes & 0x3FFF);

			// 2^14 = 16384
			return intPart + (fracPart / 16384f);
		}
	}

}
