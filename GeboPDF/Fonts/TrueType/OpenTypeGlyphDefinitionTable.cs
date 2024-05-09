using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeboPdf.Fonts.TrueType {

	public enum GlyphClassDefEnum : ushort {
		/// <summary>
		/// All other glyphs in the font
		/// </summary>
		Unassigned = 0,
		/// <summary>
		/// Base glyph (single character, spacing glyph)
		/// </summary>
		Base = 1,
		/// <summary>
		/// Ligature glyph (multiple character, spacing glyph)
		/// </summary>
		Ligature = 2,
		/// <summary>
		/// Mark glyph (non-spacing combining glyph)
		/// </summary>
		Mark = 3,
		/// <summary>
		/// Component glyph (part of single character, spacing glyph)
		/// </summary>
		Component = 4
	}

	public class OpenTypeGlyphDefinitionTable {

		public readonly OpenTypeClassDefinitionTable? glyphClassDef;
		public readonly AttachmentPointListTable? attachList;
		public readonly LigatureCaretListTable? ligCaretList;
		public readonly OpenTypeClassDefinitionTable? markAttachClassDef;
		public readonly MarkGlyphSetsTable? markGlyphSetsDef;

		public OpenTypeGlyphDefinitionTable(OpenTypeClassDefinitionTable? glyphClassDef, AttachmentPointListTable? attachList, LigatureCaretListTable? ligCaretList, OpenTypeClassDefinitionTable? markAttachClassDef, MarkGlyphSetsTable? markGlyphSetsDef) {
			this.glyphClassDef = glyphClassDef;
			this.attachList = attachList;
			this.ligCaretList = ligCaretList;
			this.markAttachClassDef = markAttachClassDef;
			this.markGlyphSetsDef = markGlyphSetsDef;
		}

		internal static OpenTypeGlyphDefinitionTable Read(FontFileReader reader, long offset) {

			reader.Position = offset;

			ushort majorVersion = reader.ReadUInt16();
			if (majorVersion != 1) { throw new FormatException($"Unknown glyph definition table major version: {majorVersion}"); }

			ushort minorVersion = reader.ReadUInt16();
			if (!(minorVersion == 0 || minorVersion == 2 || minorVersion == 3)) {
				throw new FormatException($"Unknown glyph definition table minor version: {minorVersion}");
			}

			ushort? glyphClassDefOffset = reader.ReadOffset16();
			ushort? attachListOffset = reader.ReadOffset16();
			ushort? ligCaretListOffset = reader.ReadOffset16();
			ushort? markAttachClassDefOffset = reader.ReadOffset16();

			ushort? markGlyphSetsDefOffset = null;
			if (minorVersion == 2 || minorVersion == 3) {
				markGlyphSetsDefOffset = reader.ReadOffset16();
			}

			ushort? itemVarStoreOffset = null;
			if (minorVersion == 3) {
				itemVarStoreOffset = reader.ReadOffset16();
			}

			OpenTypeClassDefinitionTable? glyphClassDef = reader.ReadFrom(offset + glyphClassDefOffset, OpenTypeClassDefinitionTable.Read);
			AttachmentPointListTable? attachList = reader.ReadFrom(offset + attachListOffset, AttachmentPointListTable.Read);
			LigatureCaretListTable? ligCaretList = reader.ReadFrom(offset + ligCaretListOffset, LigatureCaretListTable.Read);
			OpenTypeClassDefinitionTable? markAttachClassDef = reader.ReadFrom(offset + markAttachClassDefOffset, OpenTypeClassDefinitionTable.Read);

			MarkGlyphSetsTable? markGlyphSetsDef = reader.ReadFrom(offset + markGlyphSetsDefOffset, MarkGlyphSetsTable.Read);

			// TODO Implement item variation store table

			return new OpenTypeGlyphDefinitionTable(glyphClassDef, attachList, ligCaretList, markAttachClassDef, markGlyphSetsDef);
		}

		public bool SkipGlyph(ushort glyph, LookupFlag lookupFlag, byte? markAttachmentTypeMask, ushort? markFilteringSet) {
			if (glyphClassDef is not null && glyphClassDef.GetClass(glyph) is ushort glyphClass && glyphClass != (ushort)GlyphClassDefEnum.Unassigned) {
				if (lookupFlag.HasFlag(LookupFlag.IGNORE_BASE_GLYPHS) && glyphClass == (ushort)GlyphClassDefEnum.Base) {
					return true;
				}
				if (lookupFlag.HasFlag(LookupFlag.IGNORE_LIGATURES) && glyphClass == (ushort)GlyphClassDefEnum.Ligature) {
					return true;
				}
				if (lookupFlag.HasFlag(LookupFlag.IGNORE_MARKS) && glyphClass == (ushort)GlyphClassDefEnum.Mark) {
					return true;
				}
			}

			if(markAttachmentTypeMask.HasValue && markAttachClassDef is not null && markAttachClassDef.GetClass(glyph) != markAttachmentTypeMask.Value) {
				return true;
			}

			if (markFilteringSet.HasValue && markGlyphSetsDef is not null && !markGlyphSetsDef.coverages[markFilteringSet.Value].IsCovered(glyph)) {
				return true;
			}

			return false;
		}

	}

	public class AttachmentPointListTable {

		public readonly OpenTypeCoverageTable coverage;
		public readonly ushort[][] attachPoints;

		public AttachmentPointListTable(OpenTypeCoverageTable coverage, ushort[][] attachPoints) {
			this.coverage = coverage;
			this.attachPoints = attachPoints;
		}

		internal static AttachmentPointListTable Read(FontFileReader reader, long offset) {

			reader.Position = offset;

			ushort coverageOffset = reader.ReadUInt16();
			ushort glyphCount = reader.ReadUInt16();
			ushort[] attachPointOffsets = reader.ReadUInt16(glyphCount);

			OpenTypeCoverageTable coverage = reader.ReadFrom(offset + coverageOffset, OpenTypeCoverageTable.Read);

			ushort[][] attachPoints = new ushort[glyphCount][];
			for (int i = 0; i < glyphCount; i++) {
				reader.Position = offset + attachPointOffsets[i];

				ushort pointCount = reader.ReadUInt16();
				ushort[] pointIndices = reader.ReadUInt16(pointCount);

				attachPoints[i] = pointIndices;
			}

			return new AttachmentPointListTable(coverage, attachPoints);
		}

	}

	public class LigatureCaretListTable {

		public readonly OpenTypeCoverageTable coverage;
		public readonly LigatureGlyphTable[] ligGlyphs;

		public LigatureCaretListTable(OpenTypeCoverageTable coverage, LigatureGlyphTable[] ligGlyphs) {
			this.coverage = coverage;
			this.ligGlyphs = ligGlyphs;
		}

		internal static LigatureCaretListTable Read(FontFileReader reader, long offset) {

			reader.Position = offset;

			ushort coverageOffset = reader.ReadUInt16();
			ushort ligGlyphCount = reader.ReadUInt16();
			ushort[] ligGlyphOffsets = reader.ReadUInt16(ligGlyphCount);

			OpenTypeCoverageTable coverage = reader.ReadFrom(offset + coverageOffset, OpenTypeCoverageTable.Read);
			LigatureGlyphTable[] ligGlyphs = reader.ReadFrom(offset, ligGlyphOffsets, LigatureGlyphTable.Read);

			return new LigatureCaretListTable(coverage, ligGlyphs);
		}

		public class LigatureGlyphTable {

			public readonly CaretValueTable[] caretValues;

			public LigatureGlyphTable(CaretValueTable[] caretValues) {
				this.caretValues = caretValues;
			}

			internal static LigatureGlyphTable Read(FontFileReader reader, long offset) {

				reader.Position = offset;

				ushort caretCount = reader.ReadUInt16();
				ushort[] caretValueOffsets = reader.ReadUInt16(caretCount);

				CaretValueTable[] caretValues = reader.ReadFrom(offset, caretValueOffsets, CaretValueTable.Read);

				return new LigatureGlyphTable(caretValues);
			}

		}

		public abstract class CaretValueTable {

			internal static CaretValueTable Read(FontFileReader reader, long offset) {

				reader.Position = offset;

				ushort caretValueFormat = reader.ReadUInt16();

				if (caretValueFormat == 1) {
					short coordinate = reader.ReadInt16();
					return new CaretValueTableFormat1(coordinate);
				}
				else if (caretValueFormat == 2) {
					ushort caretValuePointIndex = reader.ReadUInt16();
					return new CaretValueTableFormat2(caretValuePointIndex);
				}
				else if (caretValueFormat == 3) {
					short coordinate = reader.ReadInt16();
					ushort deviceOffset = reader.ReadUInt16();
					return new CaretValueTableFormat3(coordinate, deviceOffset);
				}
				else {
					throw new FormatException($"Unknown caret value table format: {caretValueFormat}");
				}

			}

			private class CaretValueTableFormat1 : CaretValueTable {

				public readonly short coordinate;

				public CaretValueTableFormat1(short coordinate) {
					this.coordinate = coordinate;
				}

			}

			private class CaretValueTableFormat2 : CaretValueTable {

				public readonly ushort caretValuePointIndex;

				public CaretValueTableFormat2(ushort caretValuePointIndex) {
					this.caretValuePointIndex = caretValuePointIndex;
				}

			}

			private class CaretValueTableFormat3 : CaretValueTable {

				public readonly short coordinate;
				public readonly ushort deviceOffset;

				public CaretValueTableFormat3(short coordinate, ushort deviceOffset) {
					this.coordinate = coordinate;
					this.deviceOffset = deviceOffset;
				}

			}

		}

	}

	public class MarkGlyphSetsTable {

		public readonly OpenTypeCoverageTable[] coverages;

		public MarkGlyphSetsTable(OpenTypeCoverageTable[] coverages) {
			this.coverages = coverages;
		}

		internal static MarkGlyphSetsTable Read(FontFileReader reader, long offset) {

			reader.Position = offset;

			ushort format = reader.ReadUInt16();
			if(format != 1) { throw new FormatException($"Unknown mark glyph sets table format: {format}"); }

			ushort markGlyphSetCount = reader.ReadUInt16();
			uint[] coverageOffsets = reader.ReadUInt32(markGlyphSetCount);

			OpenTypeCoverageTable[] coverages = reader.ReadFrom(offset, coverageOffsets, OpenTypeCoverageTable.Read);

			return new MarkGlyphSetsTable(coverages);
		}

	}

}
