using GeboPdf.Objects;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeboPdf.Text {

	public enum PdfEncodingValue { PDFDocEncoding, StandardEncoding, MacRomanEncoding, WinAnsiEncoding }

	public abstract class PdfEncoding : Encoding {

		public abstract PdfName? Name { get; }

		private static readonly Dictionary<string, char> CharacterNames;

		public static readonly PdfEncoding PDFDocEncoding;
		public static readonly PdfEncoding StandardEncoding;
		public static readonly PdfEncoding MacRomanEncoding;
		public static readonly PdfEncoding WinAnsiEncoding;

		public static readonly PdfEncoding UnicodeEncoding;

		static PdfEncoding() {
			CharacterNames = new Dictionary<string, char>();
			Dictionary<ushort, byte> pdfDocEncoding = new Dictionary<ushort, byte>();
			Dictionary<ushort, byte> standardEncoding = new Dictionary<ushort, byte>();
			Dictionary<ushort, byte> macRomanEncoding = new Dictionary<ushort, byte>();
			Dictionary<ushort, byte> winAnsiEncoding = new Dictionary<ushort, byte>();

			string encodingsResource = typeof(PdfEncoding).Assembly.GetManifestResourceNames().Where(r => r.EndsWith("PdfEncodings.csv")).First();

			using (Stream afmStream = typeof(PdfEncoding).Assembly.GetManifestResourceStream(encodingsResource)!) // We know this resource exists here
			using (StreamReader reader = new StreamReader(afmStream, Encoding.ASCII)) {

				reader.ReadLine(); // Skip header

				string? line;
				while ((line = reader.ReadLine()) != null) {
					line = line.Trim();
					if(line.Length > 0) {
						string[] parts = line.Split(',');

						// Maximum Unicode value in the PDF encoding listings is below U+FFFF (U+FB02)
						ushort unicode = System.Convert.ToUInt16(parts[0].Substring(2), 16);
						string name = parts[1];

						CharacterNames.Add(name, (char)unicode);

						byte pdfDoc = System.Convert.ToByte(parts[2], 8);
						pdfDocEncoding.Add(unicode, pdfDoc);

						if(parts[3].Length > 0) {
							byte standard = System.Convert.ToByte(parts[3], 8);
							standardEncoding.Add(unicode, standard);
						}
						if (parts[4].Length > 0) {
							byte maxRoman = System.Convert.ToByte(parts[4], 8);
							macRomanEncoding.Add(unicode, maxRoman);
						}
						if (parts[5].Length > 0) {
							byte winAnsi = System.Convert.ToByte(parts[5], 8);
							winAnsiEncoding.Add(unicode, winAnsi);
						}
					}
				}

			}

			PDFDocEncoding = new PdfCharMapEncoding(pdfDocEncoding, new PdfName("PDFDocEncoding"));
			StandardEncoding = new PdfCharMapEncoding(standardEncoding, new PdfName("StandardEncoding"));
			MacRomanEncoding = new PdfCharMapEncoding(macRomanEncoding, new PdfName("MacRomanEncoding"));
			WinAnsiEncoding = new PdfCharMapEncoding(winAnsiEncoding, new PdfName("WinAnsiEncoding"));

			UnicodeEncoding = new PdfUnicodeEncoding();
		}

		public static char GetCharacter(string name) {
			return CharacterNames.TryGetValue(name, out char c) ? c : (char)0;
		}

		private class PdfCharMapEncoding : PdfEncoding {

			public override PdfName? Name { get; }

			private readonly IReadOnlyDictionary<ushort, byte> encoding;
			private readonly IReadOnlyDictionary<byte, ushort> toUnicode;

			public PdfCharMapEncoding(IReadOnlyDictionary<ushort, byte> encoding, PdfName name) {
				this.encoding = encoding;
				this.toUnicode = encoding.ToDictionary(kv => kv.Value, kv => kv.Key);

				Name = name;
			}

			// Single byte encodings
			public override int GetByteCount(char[] chars, int index, int count) => count;
			public override int GetCharCount(byte[] bytes, int index, int count) => count;
			public override int GetMaxByteCount(int charCount) => charCount;
			public override int GetMaxCharCount(int byteCount) => byteCount;

			public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex) {
				for (int i = 0; i < charCount; i++) {
					char c = chars[charIndex + i];
					byte b = encoding.TryGetValue((ushort)c, out byte enc) ? enc : encoding[(ushort)'?'];
					bytes[byteIndex + i] = b;
				}
				return charCount;
			}

			public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex) {
				for (int i = 0; i < byteCount; i++) {
					byte b = bytes[byteIndex + i];
					char c = toUnicode.TryGetValue(b, out ushort unicode) ? (char)unicode : '?';
					chars[charIndex + i] = c;
				}
				return byteCount;
			}
		}

		private class PdfUnicodeEncoding : PdfEncoding {

			public override PdfName? Name => null;

			private readonly Encoding BaseEncoding = Encoding.BigEndianUnicode;
			private readonly byte[] StartBytes = new byte[] { 254, 255 };

			public PdfUnicodeEncoding() { }

			public override int GetByteCount(char[] chars, int index, int count) {
				return StartBytes.Length + BaseEncoding.GetByteCount(chars, index, count);
			}

			public override int GetCharCount(byte[] bytes, int index, int count) {
				if (!(index + 1 < bytes.Length && bytes[index] == StartBytes[0] && bytes[index + 1] == StartBytes[1]) || count < 2) {
					throw new FormatException("Invalid start bytes for PDF Unicode text string.");
				}
				return BaseEncoding.GetCharCount(bytes, index + 2, count);
			}

			public override int GetMaxByteCount(int charCount) {
				return StartBytes.Length + BaseEncoding.GetMaxByteCount(charCount);
			}

			public override int GetMaxCharCount(int byteCount) {
				return BaseEncoding.GetMaxCharCount(byteCount - StartBytes.Length);
			}

			public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex) {
				bytes[byteIndex] = StartBytes[0];
				bytes[byteIndex + 1] = StartBytes[1];
				return 2 + BaseEncoding.GetBytes(chars, charIndex, charCount, bytes, byteIndex + 2);
			}

			public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex) {
				if (!(byteIndex + 1 < bytes.Length && bytes[byteIndex] == StartBytes[0] && bytes[byteIndex + 1] == StartBytes[1]) || byteCount < 2) {
					throw new FormatException("Invalid start bytes for PDF Unicode text string.");
				}

				return BaseEncoding.GetChars(bytes, byteIndex + 2, byteCount - 2, chars, charIndex);
			}
		}
	}

}
