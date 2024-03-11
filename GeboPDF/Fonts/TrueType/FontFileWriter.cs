using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GeboPdf.Utilities;

namespace GeboPdf.Fonts.TrueType {

	public class FontFileWriter {

		/*** ASCII Strings ***/

		public static byte[] ASCIIToBytes(string ascii) {
			return Encoding.ASCII.GetBytes(ascii);
		}

		/*** Helper Functions ***/

		private static void AdjustBytesForWriting(byte[] bytes) {
			if (BitConverter.IsLittleEndian) {
				Array.Reverse(bytes); // Ensure Big-endian
			}
		}

		/*** UInt16 ***/

		public static byte[] UInt16ToBytes(ushort value) {
			byte[] bytes = BitConverter.GetBytes(value);
			AdjustBytesForWriting(bytes);
			return bytes;
		}

		/*** UInt32 ***/

		public static byte[] UInt32ToBytes(uint value) {
			byte[] bytes = BitConverter.GetBytes(value);
			AdjustBytesForWriting(bytes);
			return bytes;
		}

		/*** Table Writing ***/

		public static void WriteHeaderAndTables(Stream source, uint scalerType, TrueTypeFontTable[] tables, Stream output) {

			uint outputInitialOffset = (uint)output.Position;

			uint[] totalLengths = new uint[tables.Length];
			for (int i = 0; i < tables.Length; i++) {
				totalLengths[i] = 4 * (((tables[i].length - 1) / 4) + 1); // Tables must be 4-byte-aligned (long aligned)
			}

			uint[] newOffsets = new uint[tables.Length];
			newOffsets[0] = outputInitialOffset + (uint)(12 + 16 * tables.Length); // 12 bytes for the front matter, and 16 bytes per table record
			for (int i = 1; i < tables.Length; i++) {
				newOffsets[i] = newOffsets[i - 1] + totalLengths[i - 1];
			}

			ushort maxPow2 = (ushort)Math.Pow(2, (int)Math.Log2(tables.Length)); // Highest power of 2 <= tables.Length
			ushort searchRange = (ushort)(maxPow2 * 16);
			ushort entrySelector = (ushort)Math.Log2(maxPow2);
			ushort rangeShift = (ushort)(tables.Length * 16 - searchRange);

			// Offset subtable (12 bytes)
			output.Write(UInt32ToBytes(scalerType));
			output.Write(UInt16ToBytes((ushort)tables.Length));
			output.Write(UInt16ToBytes(searchRange));
			output.Write(UInt16ToBytes(entrySelector));
			output.Write(UInt16ToBytes(rangeShift));

			for (int i = 0; i < tables.Length; i++) {
				//Console.WriteLine($"table {i} {tables[i].tag}, checksum {tables[i].checksum}, offset {newOffsets[i]}, length {tables[i].length} ({totalLengths[i]})");
				output.Write(ASCIIToBytes(tables[i].tag));
				output.Write(UInt32ToBytes(tables[i].checksum));
				output.Write(UInt32ToBytes(newOffsets[i]));
				output.Write(UInt32ToBytes(tables[i].length));
			}

			/*
			 * Strong assumption made here that the data in the tables will be contiguous
			 * and not interleaved. However, as detected such interleaving would require
			 * a full implementation of all font subtables, this will have to do for now.
			 */

			for (int i = 0; i < tables.Length; i++) {
				source.Position = tables[i].offset;

				source.CopyTo((int)tables[i].length, output);

				for (uint j = tables[i].length; j < totalLengths[i]; j++) {
					output.WriteByte(0); // Pad tables with zeros for 4-byte-alignment
				}
			}

		}

	}

}
