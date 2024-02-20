using GeboPdf.Objects;
using GeboPdf.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeboPdf.Fonts {

	public class CMapWriter {

		private readonly Stream _stream;

		public CMapWriter(Stream stream) {
			this._stream = stream;
		}

		private void Write(byte b) {
			_stream.WriteByte(b);
		}
		private void Write(byte[] bytes) {
			_stream.Write(bytes, 0, bytes.Length);
		}

		private void WriteASCII(string s) {
			Write(Encoding.ASCII.GetBytes(s));
		}

		public void WriteEOL() {
			//Write(13); // Carriage Return char
			Write(10); // Line Feed (newline) char
		}

		public void WriteSpace() {
			Write(32); // Space char
		}

		private void WriteASCIILine(string s) {
			WriteASCII(s);
			WriteEOL();
		}

		private void WriteGID(ushort value) {
			WriteASCII("<");
			byte byte1 = (byte)(value >> 8);
			byte byte2 = (byte)(value & 0xFF);
			WriteASCII(HexWriter.ToString(byte1));
			WriteASCII(HexWriter.ToString(byte2));
			WriteASCII(">");
		}

		private void WriteCodepoint(uint value) {
			WriteASCII("<");
			string codeStr = char.ConvertFromUtf32((int)value);
			byte[] bytes = Encoding.BigEndianUnicode.GetBytes(codeStr);
			string bytesStr = HexWriter.ToString(bytes);
			WriteASCII(bytesStr);
			WriteASCII(">");
		}

		private void WriteCodepoints(uint[] value) {
			WriteASCII("<");
			for (int i = 0; i < value.Length; i++) {
				string codeStr = char.ConvertFromUtf32((int)value[i]);
				byte[] bytes = Encoding.BigEndianUnicode.GetBytes(codeStr);
				string bytesStr = HexWriter.ToString(bytes);
				WriteASCII(bytesStr);
			}
			WriteASCII(">");
		}

		/*
		public static CMapWriter Create(Stream stream, string name, string registry, string ordering, int supplement, int version, WritingMode wMode) {

			CMapWriter writer = new CMapWriter(stream);

			writer.WriteASCIILine("%!PS-Adobe-3.0 Resource-CMap");
			writer.WriteASCIILine("%%DocumentNeededResources: ProcSet CIDInit");
			writer.WriteASCIILine("%%IncludeResource: ProcSet CIDInit");
			writer.WriteASCIILine($"%%BeginResource: CMap {name}");
			writer.WriteASCIILine($"%%Title: ({name} {registry} {ordering} {supplement.ToString()})");
			writer.WriteASCIILine($"%%Version: {version.ToString()}");

			writer.WriteASCIILine("/CIDInit /ProcSet findresource begin");

			writer.WriteASCIILine("12 dict begin"); // For reasons, Adobe recommends to allocate 5 more elements than appear to be used in the code.

			writer.WriteASCIILine("begincmap");

			// CIDSystemInfo dictionary
			writer.WriteASCIILine("/CIDSystemInfo 3 dict dup begin");
			writer.WriteASCIILine($"/Registry ({registry}) def");
			writer.WriteASCIILine($"/Ordering ({ordering}) def");
			writer.WriteASCIILine($"/Supplement {supplement.ToString()} def");
			writer.WriteASCIILine("end def");

			// CMap Name, Version, and Type
			writer.WriteASCIILine($"/CMapName /{name} def");
			writer.WriteASCIILine($"/CMapVersion {version.ToString()} def");
			writer.WriteASCIILine("/CMapType 1 def"); // Should this be 1 or 0?

			// Unique Identification Numbers
			// TODO How to handle these?
			writer.WriteASCIILine("/UIDOffset 950 def");
			writer.WriteASCIILine("/XUID [1 10 25343] def");

			// Writing Mode
			writer.WriteASCIILine($"/WMode {((int)wMode).ToString()} def");

			// Codespace
			// TODO Implement!

			writer.WriteASCIILine("endcmap");

			writer.WriteASCIILine("CMapName currentdict /CMap defineresource pop"); // Create resource instance

			writer.WriteASCIILine("end"); // End "12 dict begin"
			
			writer.WriteASCIILine("end"); // End CIDInit findresource

			writer.WriteASCIILine("%%EndResource"); // End CMap resource
			writer.WriteASCIILine("%%EOF");

			return new CMapWriter(stream);
		}
		*/

		public static PdfCmapStream CreateToUnicode(Dictionary<uint, ushort> unicodeToGID, string registry, string ordering, int supplement, int version, WritingMode wMode) {

			CIDSystemInfo cidSystemInfo = new CIDSystemInfo(
					registry,
					ordering,
					supplement
				);
			string name = $"{cidSystemInfo.Registry}-{cidSystemInfo.Ordering}-{cidSystemInfo.Supplement:000}";
			int cmapType = 2;

			MemoryStream stream = new MemoryStream();
			CMapWriter writer = new CMapWriter(stream);

			writer.WriteASCIILine("%!PS-Adobe-3.0 Resource-CMap");
			writer.WriteASCIILine("%%DocumentNeededResources: ProcSet CIDInit");
			writer.WriteASCIILine("%%IncludeResource: ProcSet CIDInit");
			writer.WriteASCIILine($"%%BeginResource: CMap {name}");
			writer.WriteASCIILine($"%%Title: ({name} {registry} {ordering} {supplement.ToString()})");
			writer.WriteASCIILine($"%%Version: {version.ToString()}");

			writer.WriteASCIILine("/CIDInit /ProcSet findresource begin");

			writer.WriteASCIILine("12 dict begin"); // For reasons, Adobe recommends to allocate 5 more elements than appear to be used in the code.

			writer.WriteASCIILine("begincmap");

			// CIDSystemInfo dictionary
			writer.WriteASCIILine("/CIDSystemInfo 3 dict dup begin");
			writer.WriteASCIILine($"/Registry ({registry}) def");
			writer.WriteASCIILine($"/Ordering ({ordering}) def");
			writer.WriteASCIILine($"/Supplement {supplement.ToString()} def");
			writer.WriteASCIILine("end def");

			// CMap Name, Version, and Type
			writer.WriteASCIILine($"/CMapName /{name} def");
			writer.WriteASCIILine($"/CMapVersion {version.ToString()} def");
			writer.WriteASCIILine($"/CMapType {cmapType.ToString()} def"); // Should this be 1 or 0?

			// Unique Identification Numbers
			// TODO How to handle these?
			//writer.WriteASCIILine("/UIDOffset 950 def");
			//writer.WriteASCIILine("/XUID [1 10 25343] def");

			// Writing Mode
			writer.WriteASCIILine($"/WMode {((int)wMode).ToString()} def");

			// Codespace
			writer.WriteASCIILine("1 begincodespacerange");
			writer.WriteASCIILine("<0000> <FFFF>");
			writer.WriteASCIILine("endcodespacerange");

			// Character ranges
			WriteToUnicodeMappings(writer, unicodeToGID, new HashSet<(ushort, ushort[])>());

			writer.WriteASCIILine("endcmap");

			writer.WriteASCIILine("CMapName currentdict /CMap defineresource pop"); // Create resource instance

			writer.WriteASCIILine("end"); // End "12 dict begin"

			writer.WriteASCIILine("end"); // End CIDInit findresource

			writer.WriteASCIILine("%%EndResource"); // End CMap resource
			writer.WriteASCIILine("%%EOF");

			return new PdfCmapStream(stream, name, cidSystemInfo);
		}

		public static PdfCmapStream CreateToUnicode(IReadOnlyDictionary<uint, ushort> unicodeToGID, IReadOnlySet<(ushort gid, ushort[] original)> mappings, string orderingName) {

			// These are required for the CMap, but there meaning here is unclear
			// Is this sufficient?
			CIDSystemInfo cidSystemInfo = new CIDSystemInfo(
					"Adobe", // "GeboPDF",
					"UCS", // orderingName, // "UCS", // What on Earth does this mean?
					0
				);
			string name = $"{cidSystemInfo.Registry}-Identity-{cidSystemInfo.Ordering}";

			MemoryStream stream = new MemoryStream();
			CMapWriter writer = new CMapWriter(stream);

			// Initialise data objects and state
			writer.WriteASCIILine("/CIDInit /ProcSet findresource begin");
			writer.WriteASCIILine("12 dict begin"); // Need to allocate 5 more elements than appear to be used in the code.
			writer.WriteASCIILine("begincmap");

			// CIDSystemInfo dictionary
			writer.WriteASCIILine("/CIDSystemInfo");
			writer.WriteASCIILine($"<< /Registry ({cidSystemInfo.Registry})");
			writer.WriteASCIILine($"/Ordering ({cidSystemInfo.Ordering})");
			writer.WriteASCIILine($"/Supplement {cidSystemInfo.Supplement.ToString()}");
			writer.WriteASCIILine(">> def");

			// CMap Name, and Type
			writer.WriteASCIILine($"/CMapName /{name} def");
			writer.WriteASCIILine("/CMapType 2 def"); // 2? This right?

			// Codespace
			writer.WriteASCIILine("1 begincodespacerange");
			writer.WriteASCIILine("<0000> <FFFF>");
			writer.WriteASCIILine("endcodespacerange");

			// Character ranges
			WriteToUnicodeMappings(writer, unicodeToGID, mappings);

			// End the open data objects and finalise CMap resource
			writer.WriteASCIILine("endcmap");
			writer.WriteASCIILine("CMapName currentdict /CMap defineresource pop"); // Create resource instance
			writer.WriteASCIILine("end"); // End "12 dict begin"
			writer.WriteASCII("end"); // End CIDInit findresource

			return new PdfCmapStream(stream, name, cidSystemInfo);
		}

		private static void WriteToUnicodeMappings(CMapWriter writer, IReadOnlyDictionary<uint, ushort> unicodeToGID, IReadOnlySet<(ushort gid, ushort[] original)> mappings) {

			SortedDictionary<ushort, uint> sortedGIDToUnicode = GetGIDToUnicodeMap(unicodeToGID);

			List<(ushort gid, uint[] unicode)> chars = new List<(ushort, uint[])>();
			List<(ushort startGID, ushort endGID, uint startUnicode)> ranges = new List<(ushort, ushort, uint)>();

			int count = 0;
			ushort previousGID = 0;
			uint previousCodepoint = 0;
			ushort startGID = 0;
			uint startCodepoint = 0;
			foreach(KeyValuePair<ushort, uint> i in sortedGIDToUnicode) {
				ushort gid = i.Key;
				uint codepoint = i.Value;

				if (count > 0) {
					if (!(gid == previousGID + 1 && codepoint == previousCodepoint + 1) || ((gid >> 8) != (previousGID >> 8))) {
						if (previousGID > startGID) {
							ranges.Add((startGID, previousGID, startCodepoint));
						}
						else {
							chars.Add((startGID, new uint[] { startCodepoint }));
						}
						startGID = gid;
						startCodepoint = codepoint;
					}
				}
				else {
					startGID = gid;
					startCodepoint = codepoint;
				}

				previousGID = gid;
				previousCodepoint = codepoint;
				count++;
			}

			foreach((ushort gid, ushort[] original) in mappings) {
				uint[] unicode = new uint[original.Length];
				for(int i=0; i<original.Length; i++) {
					unicode[i] = sortedGIDToUnicode[original[i]];
				}
				chars.Add((gid, unicode));
			}

			chars.Sort((c1,c2) => c1.gid.CompareTo(c2.gid));
			ranges.Sort((r1,r2) => r1.startGID.CompareTo(r2.startGID));

			int remainingChars = chars.Count;
			while (remainingChars > 0) {
				int startIndex = chars.Count - remainingChars;
				int numChars = Math.Min(remainingChars, 100);
				int endIndex = startIndex + numChars;

				writer.WriteASCIILine($"{numChars} beginbfchar");

				for (int i = startIndex; i < endIndex; i++) {
					(ushort gid, uint[] unicode) value = chars[i];
					writer.WriteGID(value.gid);
					writer.WriteSpace();
					writer.WriteCodepoints(value.unicode);
					writer.WriteEOL();
				}

				writer.WriteASCIILine("endbfchar");

				remainingChars -= numChars;
			}

			int remainingRanges = ranges.Count;
			while (remainingRanges > 0) {
				int startIndex = ranges.Count - remainingRanges;
				int numRanges = Math.Min(remainingRanges, 100);
				int endIndex = startIndex + numRanges;

				writer.WriteASCIILine($"{numRanges} beginbfrange");

				for (int i = startIndex; i < endIndex; i++) {
					(ushort startGID, ushort endGID, uint startUnicode) value = ranges[i];
					writer.WriteGID(value.startGID);
					writer.WriteSpace();
					writer.WriteGID(value.endGID);
					writer.WriteSpace();
					writer.WriteCodepoint(value.startUnicode);
					writer.WriteEOL();
				}

				writer.WriteASCIILine("endbfrange");

				remainingRanges -= numRanges;
			}

		}

		private static SortedDictionary<ushort, uint> GetGIDToUnicodeMap(IReadOnlyDictionary<uint, ushort> unicodeToGID) {
			SortedDictionary<ushort, uint> sortedGIDToUnicode = new SortedDictionary<ushort, uint>();

			foreach(KeyValuePair<uint, ushort> entry in unicodeToGID) {
				if(sortedGIDToUnicode.TryGetValue(entry.Value, out uint codepoint)) {
					sortedGIDToUnicode[entry.Value] = Math.Min(codepoint, entry.Key);
				}
				else {
					sortedGIDToUnicode.Add(entry.Value, entry.Key);
				}
			}

			return sortedGIDToUnicode;
		}

	}

	public struct CIDSystemInfo {
		public readonly string Registry;
		public readonly string Ordering;
		public readonly int Supplement;

		public CIDSystemInfo(string registry, string ordering, int supplement) {
			Registry = registry;
			Ordering = ordering;
			Supplement = supplement;
		}

		public PdfDictionary GetDictionary() {
			return new PdfDictionary() {
				{ PdfNames.Registry, new PdfTextString(Registry) },
				{ PdfNames.Ordering, new PdfTextString(Ordering) },
				{ PdfNames.Supplement, new PdfInt(Supplement) }
			};
		}
	}

	public enum WritingMode : int {
		/// <summary>
		/// Signifies horizontal writing from left to right
		/// </summary>
		Horizontal = 0,
		/// <summary>
		/// Signifies vertical writing from top to bottom
		/// </summary>
		Vertical = 1
	}

	public class PdfCmapStream : AbstractPdfStream {

		private readonly MemoryStream cmap;
		private readonly string name;
		private readonly CIDSystemInfo cidSystemInfo;

		public PdfCmapStream(MemoryStream cmap, string name, CIDSystemInfo cidSystemInfo) {
			this.cmap = cmap;
			this.name = name;
			this.cidSystemInfo = cidSystemInfo;
		}

		public override bool AllowEncoding { get; } = true;

		public override MemoryStream GetStream() {
			return cmap;
		}

		public override int Count => 3;

		public override IEnumerator<KeyValuePair<PdfName, PdfObject>> GetEnumerator() {
			yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.Type, PdfNames.CMap);
			yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.CMapName, new PdfName(name));
			yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.CIDSystemInfo, cidSystemInfo.GetDictionary());
		}

	}

}
