using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeboPdf.Utilities {

	public static class Deflate1950 {
		// https://stackoverflow.com/a/2331025/11002708

		public static MemoryStream Compress(MemoryStream original) {

			MemoryStream compressedStreamData = new MemoryStream();

			compressedStreamData.WriteByte(0x78);
			compressedStreamData.WriteByte(0x01);

			using (DeflateStream compressor = new DeflateStream(compressedStreamData, CompressionMode.Compress, true)) {
				original.WriteTo(compressor);
			}

			int adler32 = Adler32Checksum(original.ToArray());

			byte[] adlerBytes = BitConverter.GetBytes(adler32);
			if (BitConverter.IsLittleEndian) {
				Array.Reverse(adlerBytes);
			}

			compressedStreamData.Write(adlerBytes, 0, 4);

			return compressedStreamData;
		}

		private static readonly int Adler32Modulus = 65521;
		private static int Adler32Checksum(byte[] data) {
			int a = 1;
			int b = 0;

			for (int counter = 0; counter < data.Length; ++counter) {
				a = (a + (data[counter])) % Adler32Modulus;
				b = (b + a) % Adler32Modulus;
			}

			return ((b * 65536) + a);
		}

		public static MemoryStream Decompress(MemoryStream encoded, int predictor = 1, int columns = 1) {

			//encoded.Seek(2, SeekOrigin.Begin); // Drop first two bytes
			encoded.Position = 2;

			MemoryStream decompressedStream = new MemoryStream();

			using (DeflateStream decompressor = new DeflateStream(encoded, CompressionMode.Decompress, false)) {
				decompressor.CopyTo(decompressedStream);
			}

			if (predictor != 1) {
				if (predictor == 2) {
					throw new NotImplementedException("TIFF Predictors not implemented for FlateDecode filter decoding.");
				}
				else if(predictor >=10 && predictor <= 14) {
					byte[] defiltered = PngDefilter(decompressedStream.ToArray(), columns, 1);
					decompressedStream = new MemoryStream(defiltered, 0, defiltered.Length, false, true);
				}
				else {
					throw new NotImplementedException($"Predictor value of {predictor} not implemented for FlateDecode filter decoding.");
				}
			}

			return decompressedStream;
		}

		private static byte[] PngDefilter(byte[] data, int columns, int bytesPerPixel) {

			int rowLength = columns + 1;
			if(data.Length % rowLength != 0) {
				throw new FormatException($"Input data not of correct shape (expected multiple of {rowLength}, got {data.Length}).");
			}
			int rowCount = data.Length / rowLength;

			byte[] result = new byte[rowCount * columns];

			byte[] previous = new byte[columns];
			for (int i = 0; i < previous.Length; i++) { previous[i] = 0; }

			for (int row = 0; row < rowCount; row += 1) {
				byte type = data[row * rowLength];

				if (type < 0 || type > 4) {
					throw new FormatException($"Invalid PNG filter type: {type}");
				}

				for (int i = 0; i < columns; i++) {
					byte encoded = data[(row * rowLength) + 1 + i];
					int output;
					if (type == 1) { // Sub filter
						output = encoded + (i < bytesPerPixel ? 0 : result[(row * columns) + i - bytesPerPixel]);
					}
					else if (type == 2) { // Up filter
						output = encoded + previous[i];
					}
					else if (type == 3) { // Average filter
						output = encoded + /*floor*/(((i < bytesPerPixel ? 0 : result[(row * columns) + i - bytesPerPixel]) + previous[i]) / 2); // Integer division (positive) deals with floor
					}
					else if (type == 4) { // Paeth filter
						output = encoded + PaethPredictor(
							(byte)(i < bytesPerPixel ? 0 : result[(row * columns) + i - bytesPerPixel]),
							previous[i],
							(byte)((i < bytesPerPixel) ? 0 : previous[i - bytesPerPixel])
							);
					}
					else { // type == 0
						output = encoded;
					}
					result[(row * columns) + i] = (byte)(output % 256);

				}

				for (int i = 0; i < columns; i++) {
					previous[i] = result[(row * columns) + i];
				}
			}

			return result;
		}

		private static byte PaethPredictor(byte a, byte b, byte c) {
			// a = left, b = above, c = upper left
			int p = a + b - c; // initial estimate
			int pa = Math.Abs(p - a); // distances to a, b, c
			int pb = Math.Abs(p - b);
			int pc = Math.Abs(p - c);
			// return nearest of a, b, c,
			// breaking ties in order a, b, c.
			if (pa <= pb && pa <= pc) return a;
			else if (pb <= pc) return b;
			else return c;
		}
	}

}
