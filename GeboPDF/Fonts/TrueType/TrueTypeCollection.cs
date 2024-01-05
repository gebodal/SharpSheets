using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeboPdf.Fonts.TrueType {

	public class TrueTypeCollection {

		public readonly TrueTypeFontFile[] fonts;

		public readonly uint numFonts;

		public TrueTypeCollection(uint numFonts, TrueTypeFontFile[] fonts) {
			this.numFonts = numFonts;
			this.fonts = fonts;
		}

		public static TrueTypeCollection Open(string fontProgramPath) {
			using (FileStream fontFileStream = new FileStream(fontProgramPath, FileMode.Open, FileAccess.Read)) {
				FontFileReader fontReader = new FontFileReader(fontFileStream);
				TrueTypeCollection fontFile = TrueTypeCollection.Open(fontReader);
				//fontFileStream.Close();
				return fontFile;
			}
		}

		public static TrueTypeCollection Open(FontFileReader reader) {

			// TODO Need a check here that we are actually reading a font collection file

			byte[] ttcTag = reader.ReadUInt8(4);
			ushort majorVersion = reader.ReadUInt16();
			ushort minorVersion = reader.ReadUInt16();
			uint numFonts = reader.ReadUInt32();

			uint[] tableDirectoryOffsets = new uint[numFonts];

			for (int i = 0; i < numFonts; i++) {
				tableDirectoryOffsets[i] = reader.ReadUInt32();
			}

			TrueTypeFontFile[] fonts = new TrueTypeFontFile[numFonts];

			for (int i = 0; i < numFonts; i++) {
				reader.Position = tableDirectoryOffsets[i];

				fonts[i] = TrueTypeFontFile.Open(reader);
			}

			return new TrueTypeCollection(numFonts, fonts);
		}

	}

	public class TrueTypeCollectionData {

		public readonly TrueTypeFontFileData[] fonts;

		public readonly uint numFonts;

		public TrueTypeCollectionData(uint numFonts, TrueTypeFontFileData[] fonts) {
			this.numFonts = numFonts;
			this.fonts = fonts;
		}

		public static TrueTypeCollectionData Open(string fontProgramPath) {
			using (FileStream fontFileStream = new FileStream(fontProgramPath, FileMode.Open, FileAccess.Read)) {
				FontFileReader fontReader = new FontFileReader(fontFileStream);
				return Open(fontReader);
			}
		}

		public static TrueTypeCollectionData Open(FontFileReader reader) {

			byte[] ttcTag = reader.ReadUInt8(4);
			ushort majorVersion = reader.ReadUInt16();
			ushort minorVersion = reader.ReadUInt16();
			uint numFonts = reader.ReadUInt32();

			uint[] tableDirectoryOffsets = new uint[numFonts];

			for (int i = 0; i < numFonts; i++) {
				tableDirectoryOffsets[i] = reader.ReadUInt32();
			}

			TrueTypeFontFileData[] fonts = new TrueTypeFontFileData[numFonts];

			for (int i = 0; i < numFonts; i++) {
				reader.Position = tableDirectoryOffsets[i];

				fonts[i] = TrueTypeFontFileData.Open(reader);
			}

			return new TrueTypeCollectionData(numFonts, fonts);
		}

	}

}
