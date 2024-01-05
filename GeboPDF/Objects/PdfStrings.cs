using GeboPdf.Documents;
using GeboPdf.IO;
using System;
using System.Linq;
using System.Text;

namespace GeboPdf.Objects {

	public class PdfRawString : PdfString {

		public override bool HexString { get; }
		public override byte[] Value { get; }

		public PdfRawString(byte[] value, bool hex) : base() {
			this.Value = value;
			this.HexString = hex;
		}

		public override int GetHashCode() => base.GetHashCode();
		public override bool Equals(object? obj) {
			if (ReferenceEquals(this, obj)) { return true; }
			if (obj is PdfRawString other) {
				return HexString == other.HexString && Enumerable.SequenceEqual(Value, other.Value);
			}
			return false;
		}

	}

	public class PdfTextString : PdfString {

		public override bool HexString { get; }
		public override byte[] Value { get; }

		public PdfTextString(string text) : base() {
			this.Value = PdfStringEncoding.GetLiteralStringBytes(text);
			this.HexString = false;
		}

		public override int GetHashCode() => base.GetHashCode();
		public override bool Equals(object? obj) {
			if (ReferenceEquals(this, obj)) { return true; }
			if (obj is PdfTextString other) {
				return Enumerable.SequenceEqual(Value, other.Value);
			}
			return false;
		}

	}

	public class PdfDateString : PdfString {

		public override bool HexString { get; }
		public override byte[] Value { get; }

		public PdfDateString(DateTime date) : base() {
			TimeSpan utcOffset = TimeZoneInfo.Local.GetUtcOffset(date); // TODO Verify that this is correct (changed for .NET 6) (should be fine?)
			string dateString = date.ToString("'D:'yyyyMMddHHmmss") + (utcOffset.TotalHours < 0 ? "-" : "+") + utcOffset.Duration().ToString("hh\\\'mm\\\'");

			this.Value = Encoding.ASCII.GetBytes(dateString);
			this.HexString = false;
		}

		public override int GetHashCode() => base.GetHashCode();
		public override bool Equals(object? obj) {
			if (ReferenceEquals(this, obj)) { return true; }
			if (obj is PdfDateString other) {
				return Enumerable.SequenceEqual(Value, other.Value);
			}
			return false;
		}

	}

	public static class PdfStringEncoding {

		public static byte[] GetLiteralStringBytes(string text) {
			StringBuilder sb = new StringBuilder(); // More efficient way of doing this?

			for (int i = 0; i < text.Length; i++) {
				int c = text[i];
				if (c == 10) { // Line feed
					sb.Append("\\n");
				}
				else if (c == 13) { // Carriage return
					sb.Append("\\r");
				}
				else if (c == 9) { // Horizontal tab
					sb.Append("\\t");
				}
				else if (c == 8) { // Backspace
					sb.Append("\\b");
				}
				else if (c == 12) { // Form feed
					sb.Append("\\f");
				}
				else if (c == 40 || c == 41 || c == 92) { // Escaped characters, '(', ')', '\'
					sb.Append('\\');
					sb.Append((char)c);
				}
				else if (c >= 0 && c < 128) { // ASCII range
					sb.Append((char)c);
				}
				else if (c >= 128 && c < 512) { // Three digit octal range above ASCII
					sb.Append("\\" + Convert.ToString(c, 8).PadLeft(3, '0'));
				}
				else {
					throw new ArgumentException("Invalid character for literal string.");
				}
			}

			return Encoding.ASCII.GetBytes(sb.ToString());
		}

		public static string GetLiteralString(byte[] values) {
			StringBuilder sb = new StringBuilder();

			bool escaped = false;
			int? digits = null;
			int octal = 0;

			for (int i = 0; i < values.Length; i++) {
				byte c = values[i];

				if (escaped) {
					escaped = false;
					if (c == 'n') {
						sb.Append('\n');
					}
					else if (c == 'r') {
						sb.Append('\r');
					}
					else if (c == 't') {
						sb.Append('\t');
					}
					else if (c == 'b') {
						sb.Append('\b');
					}
					else if (c == 'f') {
						sb.Append('\f');
					}
					else if (c == '(' || c == ')' || c == '\\') {
						sb.Append((char)c);
					}
					else if (c >= '0' && c <= '8') {
						digits = 2;
						octal = c - '0';
					}
					else if (c >= 0 && c < 128) { // ASCII range
						sb.Append((char)c);
					}
					else {
						throw new ArgumentException("Invalid escaped byte for literal string.");
					}
				}
				else {
					if (digits.HasValue && c >= '0' && c <= '8') {
						octal *= 8;
						octal += c - '0';

						digits = digits.Value - 1;
						if (digits.Value == 0) {
							digits = null;
							sb.Append((char)octal);
							octal = 0;
							continue;
						}
					}
					else if (digits.HasValue) {
						digits = null;
						sb.Append((char)octal);
						octal = 0;
					}

					if (c == '\\') {
						escaped = true;
					}
					else if (c >= 0 && c < 128) { // ASCII range
						sb.Append((char)c);
					}
					else {
						throw new ArgumentException("Invalid byte for literal string.");
					}
				}
			}

			return sb.ToString();
		}

	}

}
