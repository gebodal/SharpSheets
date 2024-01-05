using GeboPdf.Objects;
using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace GeboPdf.IO {

	public class PdfStreamWriter {

		private readonly Stream _stream;
		public long bytesWritten { get; private set; }

		public PdfStreamWriter(Stream stream) {
			this._stream = stream;
			this.bytesWritten = 0;
		}

		public void Write(byte b) {
			_stream.WriteByte(b);
			bytesWritten += 1;
		}

		public void Write(byte[] bytes) {
			_stream.Write(bytes, 0, bytes.Length);
			bytesWritten += bytes.Length;
		}

		public void Write(MemoryStream stream) {
			//stream.CopyTo(_stream);
			//bytesWritten += stream.Length;
			//Write(stream.ToArray());
			stream.WriteTo(_stream);
			bytesWritten += stream.Length;
		}

		public void WriteASCII(string s) {
			Write(Encoding.ASCII.GetBytes(s));
		}

		public void WriteEOL() {
			//Write(13); // Carriage Return char
			Write(10); // Line Feed (newline) char
		}

		public void WriteSpace() {
			Write(32); // Space char
		}

		public void WriteBool(bool value) {
			if (value) {
				WriteASCII("true");
			}
			else {
				WriteASCII("false");
			}
		}

		public void WriteInt(int value) {
			WriteASCII(value.ToString(CultureInfo.InvariantCulture));
		}

		public void WriteInt(long value) {
			WriteASCII(value.ToString());
		}

		public void WriteFloat(float value) {
			WriteASCII(value.ToString("#.#####;-#.#####;0", CultureInfo.InvariantCulture));
		}

		public void WriteLiteralString(string value) {
			WriteASCII("(");

			Write(PdfStringEncoding.GetLiteralStringBytes(value));

			WriteASCII(")");
		}

		public void WriteHexString(byte[] value) {
			WriteASCII("<");

			for (int i = 0; i < value.Length; i++) {
				WriteASCII(value[i].ToString("X2"));
			}

			WriteASCII(">");
		}

		public void WriteName(PdfName name) { // Struct type?
			WriteASCII("/" + name.Name); // TODO This is actually more complicated, as characters might need to be escaped
		}

		public void WriteNull() {
			WriteASCII("null");
		}

	}

}
