using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeboPdf.Fonts.TrueType {

	public enum GPOSLookupType : ushort {
		/// <summary>
		/// Adjust position of a single glyph
		/// </summary>
		SingleAdjustment = 1,
		/// <summary>
		/// Adjust position of a pair of glyphs
		/// </summary>
		PairAdjustment = 2,
		/// <summary>
		/// Attach cursive glyphs
		/// </summary>
		CursiveAttachment = 3,
		/// <summary>
		/// Attach a combining mark to a base glyph
		/// </summary>
		MarkToBaseAttachment = 4,
		/// <summary>
		/// Attach a combining mark to a ligature
		/// </summary>
		MarkToLigatureAttachment = 5,
		/// <summary>
		/// Attach a combining mark to another mark
		/// </summary>
		MarkToMarkAttachment = 6,
		/// <summary>
		/// Position one or more glyphs in context
		/// </summary>
		ContextPositioning = 7,
		/// <summary>
		/// Position one or more glyphs in chained context
		/// </summary>
		ChainedContextPositioning = 8,
		/// <summary>
		/// Extension mechanism for other positionings
		/// </summary>
		ExtensionPositioning = 9
	}

	public class OpenTypeGlyphPositioningTable { // GPOS

		public readonly OpenTypeScriptListTable ScriptListTable;
		public readonly OpenTypeFeatureListTable FeatureListTable;
		public readonly GlyphPositioningLookupTable[] LookupList;

		internal OpenTypeGlyphPositioningTable(OpenTypeScriptListTable scriptListTable, OpenTypeFeatureListTable featureListTable, GlyphPositioningLookupTable[] lookupList) {
			ScriptListTable = scriptListTable;
			FeatureListTable = featureListTable;
			LookupList = lookupList;
		}

		internal static OpenTypeLayoutTagSet ReadTags(FontFileReader reader, long offset) {

			reader.Position = offset;

			ushort majorVersion = reader.ReadUInt16();
			if (majorVersion != 1) { throw new FormatException($"Unknown glyph positioning table major version: {majorVersion}"); }

			ushort minorVersion = reader.ReadUInt16();

			ushort scriptListOffset = reader.ReadUInt16();
			ushort featureListOffset = reader.ReadUInt16();
			/*ushort lookupListOffset*/ _ = reader.ReadUInt16();

			uint? featureVariationsOffset;
			if (minorVersion == 1) { featureVariationsOffset = reader.ReadOffset32(); }
			else if (minorVersion == 0) { featureVariationsOffset = null; }
			else { throw new FormatException($"Unknown glyph positioning table minor version: {minorVersion}"); }

			IReadOnlyDictionary<string, IReadOnlySet<string>> scriptTags = OpenTypeScriptListTable.ReadTags(reader, offset + scriptListOffset);
			IReadOnlySet<string> featureTags = OpenTypeFeatureListTable.ReadTags(reader, offset + featureListOffset);

			return new OpenTypeLayoutTagSet(scriptTags, featureTags);
		}

		internal static OpenTypeGlyphPositioningTable Read(FontFileReader reader, long offset) {

			reader.Position = offset;

			ushort majorVersion = reader.ReadUInt16();
			if (majorVersion != 1) { throw new FormatException($"Unknown glyph positioning table major version: {majorVersion}"); }

			ushort minorVersion = reader.ReadUInt16();

			ushort scriptListOffset = reader.ReadUInt16();
			ushort featureListOffset = reader.ReadUInt16();
			ushort lookupListOffset = reader.ReadUInt16();

			uint? featureVariationsOffset;
			if (minorVersion == 1) { featureVariationsOffset = reader.ReadOffset32(); }
			else if (minorVersion == 0) { featureVariationsOffset = null; }
			else { throw new FormatException($"Unknown glyph positioning table minor version: {minorVersion}"); }

			OpenTypeScriptListTable scriptListTable = OpenTypeScriptListTable.Read(reader, offset + scriptListOffset);
			OpenTypeFeatureListTable featureListTable = OpenTypeFeatureListTable.Read(reader, offset + featureListOffset);
			OpenTypeLookupListTable lookupListTable = OpenTypeLookupListTable.Read(reader, offset + lookupListOffset);
			// Ignoring feature variations table for now

			GlyphPositioningLookupTable[] lookupList = new GlyphPositioningLookupTable[lookupListTable.Lookups.Length];
			for (int i = 0; i < lookupList.Length; i++) {
				GPOSLookupType lookupType = (GPOSLookupType)lookupListTable.Lookups[i].LookupType;

				IPositioningSubtable[] subtables = new IPositioningSubtable[lookupListTable.Lookups[i].SubtableOffsets.Length];

				for (int j = 0; j < subtables.Length; j++) {
					subtables[j] = ReadSubtable(reader, lookupListTable.Lookups[i].SubtableOffsets[j], lookupType, lookupList);
				}

				lookupList[i] = new GlyphPositioningLookupTable(
					lookupListTable.Lookups[i].LookupFlag,
					lookupListTable.Lookups[i].MarkAttachmentTypeMask,
					subtables,
					lookupListTable.Lookups[i].MarkFilteringSet
					);
			}

			return new OpenTypeGlyphPositioningTable(scriptListTable, featureListTable, lookupList);
		}

		private static IPositioningSubtable ReadSubtable(FontFileReader reader, long subtableOffset, GPOSLookupType lookupType, GlyphPositioningLookupTable[] lookupList) {
			return lookupType switch {
				GPOSLookupType.SingleAdjustment => OpenTypeSingleAdjustmentPositioningSubtable.Read(reader, subtableOffset),
				GPOSLookupType.PairAdjustment => OpenTypePairAdjustmentPositioningSubtable.Read(reader, subtableOffset),
				GPOSLookupType.CursiveAttachment => OpenTypeCursiveAttachmentPositioningSubtable.Read(reader, subtableOffset),
				GPOSLookupType.MarkToBaseAttachment => OpenTypeMarkToBaseAttachmentPositioningSubtable.Read(reader, subtableOffset),
				GPOSLookupType.MarkToLigatureAttachment => OpenTypeMarkToLigatureAttachmentPositioningSubtable.Read(reader, subtableOffset),
				GPOSLookupType.MarkToMarkAttachment => OpenTypeMarkToMarkAttachmentPositioningSubtable.Read(reader, subtableOffset),
				GPOSLookupType.ContextPositioning => OpenTypeContextualPositioningSubtable.Read(reader, subtableOffset, lookupList),
				GPOSLookupType.ChainedContextPositioning => OpenTypeChainedContextsPositioningSubtable.Read(reader, subtableOffset, lookupList),
				GPOSLookupType.ExtensionPositioning => ReadExtensionSubtable(reader, subtableOffset, lookupList),
				_ => throw new FormatException($"Unknown glyph positioning subtable lookup type: {(ushort)lookupType}")
			};
		}

		private static IPositioningSubtable ReadExtensionSubtable(FontFileReader reader, long subtableOffset, GlyphPositioningLookupTable[] lookupList) {
			reader.Position = subtableOffset;

			ushort substFormat = reader.ReadUInt16();

			if (substFormat == 1) {
				ushort extensionLookupType = reader.ReadUInt16();
				uint extensionOffset = reader.ReadUInt32(); // relative to the start of the ExtensionPosFormat1 subtable

				GPOSLookupType lookupType = (GPOSLookupType)extensionLookupType;

				return ReadSubtable(reader, subtableOffset + extensionOffset, lookupType, lookupList);
			}
			else {
				throw new FormatException($"Unknown extension positioning subtable format: {substFormat}");
			}
		}

		public GlyphPositioningLookupSet? GetLookups(OpenTypeLayoutTags layoutTags) {
			if (OpenTypeLayoutHelpers.GetLookups(layoutTags, ScriptListTable, FeatureListTable) is ushort[] lookupIdxs) {
				return new GlyphPositioningLookupSet(lookupIdxs.Select(i => LookupList[i]).ToArray());
			}
			else {
				return null;
			}
		}

	}

	public interface IPositioningSubtable {

		int PerformPositioning(PositionableGlyphRun run, int index);

		void PerformPositioning(PositionableGlyphRun run) {
			for (int i = 0; i < run.Count; i++) {
				i += PerformPositioning(run, i);
			}
		}

	}

	public class GlyphPositioningLookupTable {

		public readonly LookupFlag LookupFlag;
		public readonly ushort MarkAttachmentTypeMask;
		public readonly IPositioningSubtable[] Subtables;
		public readonly ushort MarkFilteringSet;

		public GlyphPositioningLookupTable(LookupFlag lookupFlag, ushort markAttachmentTypeMask, IPositioningSubtable[] subtables, ushort markFilteringSet) {
			LookupFlag = lookupFlag;
			MarkAttachmentTypeMask = markAttachmentTypeMask;
			Subtables = subtables;
			MarkFilteringSet = markFilteringSet;
		}

		public void PerformPositioning(PositionableGlyphRun run) {
			for (int i = 0; i < Subtables.Length; i++) {
				Subtables[i].PerformPositioning(run);
			}
		}

		public void PerformPositioning(PositionableGlyphRun run, int index) {
			for (int i = 0; i < Subtables.Length; i++) {
				Subtables[i].PerformPositioning(run, index);
			}
		}

	}

	public class GlyphPositioningLookupSet {

		public readonly GlyphPositioningLookupTable[] Lookups;

		public GlyphPositioningLookupSet(GlyphPositioningLookupTable[] lookups) {
			this.Lookups = lookups;
		}

		public void PerformPositioning(PositionableGlyphRun run) {
			for (int i = 0; i < Lookups.Length; i++) {
				Lookups[i].PerformPositioning(run);
			}
		}

	}

	public abstract class OpenTypeSingleAdjustmentPositioningSubtable : IPositioningSubtable {

		private OpenTypeSingleAdjustmentPositioningSubtable() { }

		public abstract int PerformPositioning(PositionableGlyphRun run, int index);

		internal static OpenTypeSingleAdjustmentPositioningSubtable Read(FontFileReader reader, long offset) {

			reader.Position = offset;

			ushort posFormat = reader.ReadUInt16();

			ushort coverageOffset = reader.ReadUInt16(); // from beginning of positioning subtable
			OpenTypeCoverageTable coverage = reader.ReadFrom(offset + coverageOffset, OpenTypeCoverageTable.Read);

			if (posFormat == 1) {
				ValueFormat valueFormat = (ValueFormat)reader.ReadUInt16();
				ValueRecord valueRecord = reader.ReadValueRecord(valueFormat);

				return new OpenTypeSingleAdjustmentPositioningFormat1(coverage, valueRecord);
			}
			else if (posFormat == 2) {
				ValueFormat valueFormat = (ValueFormat)reader.ReadUInt16();
				ushort valueCount = reader.ReadUInt16();
				ValueRecord[] valueRecords = reader.ReadValueRecord(valueFormat, valueCount);

				return new OpenTypeSingleAdjustmentPositioningFormat2(coverage, valueRecords);
			}
			else {
				throw new FormatException($"Unknown single adjustment subtable format: {posFormat}");
			}
		}

		private class OpenTypeSingleAdjustmentPositioningFormat1 : OpenTypeSingleAdjustmentPositioningSubtable {

			public readonly OpenTypeCoverageTable Coverage;
			public readonly ValueRecord ValueRecord;

			internal OpenTypeSingleAdjustmentPositioningFormat1(OpenTypeCoverageTable coverage, ValueRecord valueRecord) : base() {
				Coverage = coverage;
				ValueRecord = valueRecord;
			}

			public override int PerformPositioning(PositionableGlyphRun run, int index) {
				if (Coverage.IsCovered(run[index])) {
					run.AdjustPosition(index, ValueRecord);
				}
				return 0;
			}

		}

		private class OpenTypeSingleAdjustmentPositioningFormat2 : OpenTypeSingleAdjustmentPositioningSubtable {

			public readonly OpenTypeCoverageTable Coverage;
			public readonly ValueRecord[] ValueRecords;

			internal OpenTypeSingleAdjustmentPositioningFormat2(OpenTypeCoverageTable coverage, ValueRecord[] valueRecords) : base() {
				Coverage = coverage;
				ValueRecords = valueRecords;
			}

			public override int PerformPositioning(PositionableGlyphRun run, int index) {
				if (Coverage.CoverageIndex(run[index]) is ushort coverageIdx) {
					run.AdjustPosition(index, ValueRecords[coverageIdx]);
				}
				return 0;
			}

		}

	}

	public abstract class OpenTypePairAdjustmentPositioningSubtable : IPositioningSubtable {

		private OpenTypePairAdjustmentPositioningSubtable() { }

		public abstract int PerformPositioning(PositionableGlyphRun run, int index);

		internal static OpenTypePairAdjustmentPositioningSubtable Read(FontFileReader reader, long offset) {

			reader.Position = offset;

			ushort posFormat = reader.ReadUInt16();

			ushort coverageOffset = reader.ReadUInt16(); // from beginning of positioning subtable
			OpenTypeCoverageTable coverage = reader.ReadFrom(offset + coverageOffset, OpenTypeCoverageTable.Read);

			if (posFormat == 1) {
				ValueFormat valueFormat1 = (ValueFormat)reader.ReadUInt16();
				ValueFormat valueFormat2 = (ValueFormat)reader.ReadUInt16();
				ushort pairSetCount = reader.ReadUInt16();

				ushort[] pairSetOffsets = reader.ReadUInt16(pairSetCount); // from beginning of PairPos subtable
				OpenTypePairAdjustmentPositioningFormat1.PairSetTable[] pairSets = 
					reader.ReadFrom(offset, pairSetOffsets,
						(r, o) => OpenTypePairAdjustmentPositioningFormat1.PairSetTable.Read(r, o, valueFormat1, valueFormat2)
					);

				return new OpenTypePairAdjustmentPositioningFormat1(coverage, valueFormat1, valueFormat2, pairSets);
			}
			else if (posFormat == 2) {
				ValueFormat valueFormat1 = (ValueFormat)reader.ReadUInt16();
				ValueFormat valueFormat2 = (ValueFormat)reader.ReadUInt16();
				ushort classDef1Offset = reader.ReadUInt16(); // from beginning of PairPos subtable
				ushort classDef2Offset = reader.ReadUInt16(); // from beginning of PairPos subtable
				ushort class1Count = reader.ReadUInt16();
				ushort class2Count = reader.ReadUInt16();

				OpenTypePairAdjustmentPositioningFormat2.Class1Record[] class1Records = new OpenTypePairAdjustmentPositioningFormat2.Class1Record[class1Count];
				for(int i=0; i<class1Count; i++) {
					class1Records[i] = OpenTypePairAdjustmentPositioningFormat2.Class1Record.Read(reader, class2Count, valueFormat1, valueFormat2);
				}

				OpenTypeClassDefinitionTable classDef1 = reader.ReadFrom(offset + classDef1Offset, OpenTypeClassDefinitionTable.Read);
				OpenTypeClassDefinitionTable classDef2 = reader.ReadFrom(offset + classDef2Offset, OpenTypeClassDefinitionTable.Read);

				return new OpenTypePairAdjustmentPositioningFormat2(coverage, valueFormat1, valueFormat2, classDef1, classDef2, class1Records);
			}
			else {
				throw new FormatException($"Unknown pair adjustment subtable format: {posFormat}");
			}
		}

		private class OpenTypePairAdjustmentPositioningFormat1 : OpenTypePairAdjustmentPositioningSubtable {

			public readonly OpenTypeCoverageTable Coverage;
			public readonly ValueFormat ValueFormat1;
			public readonly ValueFormat ValueFormat2;
			public readonly PairSetTable[] PairSets;

			internal OpenTypePairAdjustmentPositioningFormat1(OpenTypeCoverageTable coverage, ValueFormat valueFormat1, ValueFormat valueFormat2, PairSetTable[] pairSets) : base() {
				Coverage = coverage;
				ValueFormat1 = valueFormat1;
				ValueFormat2 = valueFormat2;
				PairSets = pairSets;
			}

			public override int PerformPositioning(PositionableGlyphRun run, int index) {
				if (index < run.Count - 1 && Coverage.CoverageIndex(run[index]) is ushort coverageIdx) {
					PairSetTable pairSet = PairSets[coverageIdx];

					if (pairSet.PairValueRecords.TryGetValue(run[index + 1], out PairValueRecord? record)) {
						if (ValueFormat1 != ValueFormat.NONE) {
							run.AdjustPosition(index, record.ValueRecord1);
						}
						if (ValueFormat2 != ValueFormat.NONE) {
							run.AdjustPosition(index + 1, record.ValueRecord2);
						}
						return ValueFormat2 == ValueFormat.NONE ? 0 : 1;
					}
				}
				return 0;
			}

			public class PairSetTable {

				public readonly IReadOnlyDictionary<ushort, PairValueRecord> PairValueRecords;

				public PairSetTable(IReadOnlyDictionary<ushort, PairValueRecord> pairValueRecords) {
					PairValueRecords = pairValueRecords;
				}

				internal static PairSetTable Read(FontFileReader reader, long offset, ValueFormat valueFormat1, ValueFormat valueFormat2) {
					reader.Position = offset;

					ushort pairValueCount = reader.ReadUInt16();

					Dictionary<ushort, PairValueRecord> pairValueRecords = new Dictionary<ushort, PairValueRecord>();
					for (int i = 0; i < pairValueCount; i++) {
						PairValueRecord record = PairValueRecord.Read(reader, valueFormat1, valueFormat2);
						pairValueRecords[record.SecondGlyph] = record;
					}

					return new PairSetTable(pairValueRecords);
				}

			}

			public class PairValueRecord {

				public readonly ushort SecondGlyph;
				public readonly ValueRecord ValueRecord1;
				public readonly ValueRecord ValueRecord2;

				public PairValueRecord(ushort secondGlyph, ValueRecord valueRecord1, ValueRecord valueRecord2) {
					SecondGlyph = secondGlyph;
					ValueRecord1 = valueRecord1;
					ValueRecord2 = valueRecord2;
				}

				internal static PairValueRecord Read(FontFileReader reader, ValueFormat valueFormat1, ValueFormat valueFormat2) {
					ushort secondGlyph = reader.ReadUInt16();
					ValueRecord valueRecord1 = reader.ReadValueRecord(valueFormat1);
					ValueRecord valueRecord2 = reader.ReadValueRecord(valueFormat2);

					return new PairValueRecord(secondGlyph, valueRecord1, valueRecord2);
				}

			}

		}

		private class OpenTypePairAdjustmentPositioningFormat2 : OpenTypePairAdjustmentPositioningSubtable {

			public readonly OpenTypeCoverageTable Coverage;
			public readonly ValueFormat ValueFormat1;
			public readonly ValueFormat ValueFormat2;
			public readonly OpenTypeClassDefinitionTable ClassDef1;
			public readonly OpenTypeClassDefinitionTable ClassDef2;
			public readonly Class1Record[] Class1Records;

			internal OpenTypePairAdjustmentPositioningFormat2(OpenTypeCoverageTable coverage, ValueFormat valueFormat1, ValueFormat valueFormat2, OpenTypeClassDefinitionTable classDef1, OpenTypeClassDefinitionTable classDef2, Class1Record[] class1Records) : base() {
				Coverage = coverage;
				ValueFormat1 = valueFormat1;
				ValueFormat2 = valueFormat2;
				ClassDef1 = classDef1;
				ClassDef2 = classDef2;
				Class1Records = class1Records;
			}

			public override int PerformPositioning(PositionableGlyphRun run, int index) {
				if (index < run.Count - 1 && Coverage.IsCovered(run[index])) {
					ushort classVal1 = ClassDef1.GetClass(run[index]);
					ushort classVal2 = ClassDef2.GetClass(run[index + 1]);

					Class1Record class1Record = Class1Records[classVal1];
					Class2Record class2Record = class1Record[classVal2];

					if (ValueFormat1 != ValueFormat.NONE) {
						run.AdjustPosition(index, class2Record.ValueRecord1);
					}
					if (ValueFormat2 != ValueFormat.NONE) {
						run.AdjustPosition(index + 1, class2Record.ValueRecord2);
					}
					return ValueFormat2 == ValueFormat.NONE ? 0 : 1;
				}
				return 0;
			}

			public class Class1Record {

				public readonly Class2Record[] Class2Records;

				public Class2Record this[int index] {
					get => Class2Records[index];
				}

				public Class1Record(Class2Record[] class2Records) {
					Class2Records = class2Records;
				}

				public static Class1Record Read(FontFileReader reader, ushort class2Count, ValueFormat valueFormat1, ValueFormat valueFormat2) {
					Class2Record[] class2Records = new Class2Record[class2Count];
					for(int i=0; i<class2Count; i++) {
						class2Records[i] = Class2Record.Read(reader, valueFormat1, valueFormat2);
					}
					return new Class1Record(class2Records);
				}

			}

			public class Class2Record {

				public readonly ValueRecord ValueRecord1;
				public readonly ValueRecord ValueRecord2;

				public Class2Record(ValueRecord valueRecord1, ValueRecord valueRecord2) {
					ValueRecord1 = valueRecord1;
					ValueRecord2 = valueRecord2;
				}

				public static Class2Record Read(FontFileReader reader, ValueFormat valueFormat1, ValueFormat valueFormat2) {
					ValueRecord valueRecord1 = reader.ReadValueRecord(valueFormat1);
					ValueRecord valueRecord2 = reader.ReadValueRecord(valueFormat2);
					return new Class2Record(valueRecord1, valueRecord2);
				}

			}

		}

	}

	public abstract class OpenTypeCursiveAttachmentPositioningSubtable : IPositioningSubtable {

		private OpenTypeCursiveAttachmentPositioningSubtable() { }

		public abstract void PerformPositioning(PositionableGlyphRun run);
		public abstract int PerformPositioning(PositionableGlyphRun run, int index);

		internal static OpenTypeCursiveAttachmentPositioningSubtable Read(FontFileReader reader, long offset) {

			reader.Position = offset;

			ushort posFormat = reader.ReadUInt16();

			ushort coverageOffset = reader.ReadUInt16(); // from beginning of positioning subtable
			OpenTypeCoverageTable coverage = reader.ReadFrom(offset + coverageOffset, OpenTypeCoverageTable.Read);

			if (posFormat == 1) {
				ushort entryExitCount = reader.ReadUInt16();

				(AnchorTable? entryAnchor, AnchorTable? exitAnchor)[] extryExitRecords = new (AnchorTable?, AnchorTable?)[entryExitCount];

				for(int i=0; i<entryExitCount; i++) {
					ushort? entryAnchorOffset = reader.ReadOffset16(); // from beginning of CursivePos subtable
					ushort? exitAnchorOffset = reader.ReadOffset16(); // from beginning of CursivePos subtable

					AnchorTable? entryAnchor = reader.ReadFrom(offset + entryAnchorOffset, AnchorTable.Read);
					AnchorTable? exitAnchor = reader.ReadFrom(offset + exitAnchorOffset, AnchorTable.Read);

					extryExitRecords[i] = (entryAnchor, exitAnchor);
				}

				return new OpenTypeCursiveAttachmentPositioningFormat1(coverage, extryExitRecords);
			}
			else {
				throw new FormatException($"Unknown cursive attachment positioning subtable format: {posFormat}");
			}
		}

		private class OpenTypeCursiveAttachmentPositioningFormat1 : OpenTypeCursiveAttachmentPositioningSubtable {

			public readonly OpenTypeCoverageTable Coverage;
			public readonly (AnchorTable? entryAnchor, AnchorTable? exitAnchor)[] ExtryExitRecords;

			internal OpenTypeCursiveAttachmentPositioningFormat1(OpenTypeCoverageTable coverage, (AnchorTable? entryAnchor, AnchorTable? exitAnchor)[] extryExitRecords) : base() {
				Coverage = coverage;
				ExtryExitRecords = extryExitRecords;
			}

			public override void PerformPositioning(PositionableGlyphRun run) {
				// TODO Need to take account for text direction here, and cross-stream stuff
				for (int i = 0; i < run.Count - 1; i++) {
					if (Coverage.CoverageIndex(run[i]) is ushort coverageIdx1 && Coverage.CoverageIndex(run[i + 1]) is ushort coverageIdx2) {
						(_, AnchorTable? exitAnchor) = ExtryExitRecords[coverageIdx1];
						(AnchorTable? entryAnchor, _) = ExtryExitRecords[coverageIdx2];

						// TODO What to do here?
					}
				}
			}

			public override int PerformPositioning(PositionableGlyphRun run, int index) {
				if (index < run.Count - 1 &&  Coverage.CoverageIndex(run[index]) is ushort coverageIdx1 && Coverage.CoverageIndex(run[index + 1]) is ushort coverageIdx2) {
					(_, AnchorTable? exitAnchor) = ExtryExitRecords[coverageIdx1];
					(AnchorTable? entryAnchor, _) = ExtryExitRecords[coverageIdx2];

					// TODO What to do here?
				}
				return 0; // Yes?
			}

		}

	}

	public abstract class OpenTypeMarkToBaseAttachmentPositioningSubtable : IPositioningSubtable {

		private OpenTypeMarkToBaseAttachmentPositioningSubtable() { }

		public abstract int PerformPositioning(PositionableGlyphRun run, int index);

		internal static OpenTypeMarkToBaseAttachmentPositioningSubtable Read(FontFileReader reader, long offset) {

			reader.Position = offset;

			ushort posFormat = reader.ReadUInt16();

			if (posFormat == 1) {

				ushort markCoverageOffset = reader.ReadUInt16(); // from beginning of MarkBasePos subtable
				ushort baseCoverageOffset = reader.ReadUInt16(); // from beginning of MarkBasePos subtable
				OpenTypeCoverageTable markCoverage = reader.ReadFrom(offset + markCoverageOffset, OpenTypeCoverageTable.Read);
				OpenTypeCoverageTable baseCoverage = reader.ReadFrom(offset + baseCoverageOffset, OpenTypeCoverageTable.Read);

				ushort markClassCount = reader.ReadUInt16();

				ushort markArrayOffset = reader.ReadUInt16(); // from beginning of MarkBasePos subtable
				MarkArrayTable markArray = reader.ReadFrom(offset + markArrayOffset, MarkArrayTable.Read);
				
				ushort baseArrayOffset = reader.ReadUInt16(); // from beginning of MarkBasePos subtable
				reader.Position = offset + baseArrayOffset;

				ushort baseCount = reader.ReadUInt16();
				AnchorTable?[][] baseRecords = new AnchorTable?[baseCount][];
				for(int i=0; i<baseCount; i++) {
					ushort?[] baseAnchorOffsets = reader.ReadOffset16(markClassCount); // from beginning of BaseArray table
					AnchorTable?[] baseAnchors = reader.ReadFrom(offset + baseArrayOffset, baseAnchorOffsets, AnchorTable.Read);

					baseRecords[i] = baseAnchors;
				}

				return new OpenTypeMarkToBaseAttachmentPositioningFormat1(markCoverage, baseCoverage, markArray, baseRecords);
			}
			else {
				throw new FormatException($"Unknown mark-to-base attachment positioning subtable format: {posFormat}");
			}
		}

		private class OpenTypeMarkToBaseAttachmentPositioningFormat1 : OpenTypeMarkToBaseAttachmentPositioningSubtable {

			public readonly OpenTypeCoverageTable MarkCoverage;
			public readonly OpenTypeCoverageTable BaseCoverage;
			public readonly MarkArrayTable MarkArray;
			public readonly AnchorTable?[][] BaseRecords;

			internal OpenTypeMarkToBaseAttachmentPositioningFormat1(OpenTypeCoverageTable markCoverage, OpenTypeCoverageTable baseCoverage, MarkArrayTable markArray, AnchorTable?[][] baseRecords) {
				MarkCoverage = markCoverage;
				BaseCoverage = baseCoverage;
				MarkArray = markArray;
				BaseRecords = baseRecords;
			}

			public override int PerformPositioning(PositionableGlyphRun run, int index) {
				if (index > 0
					&& MarkCoverage.CoverageIndex(run[index]) is ushort markCoverageIdx
					&& BaseCoverage.CoverageIndex(run[index - 1]) is ushort baseCoverageIdx) {

					(ushort markClass, AnchorTable markAnchor) = MarkArray.MarkRecords[markCoverageIdx];
					AnchorTable? baseAttachmentAnchor = BaseRecords[baseCoverageIdx][markClass];

					if (baseAttachmentAnchor is not null) {
						(short xDelta, short yDelta) = AnchorTable.GetDelta(
							baseAttachmentAnchor,
							run.GetAdvanceTotal(index - 1),
							run.GetPlacement(index - 1),
							markAnchor);

						run.AdjustPlacement(index, xDelta, yDelta);
					}
				}

				return 0;
			}

		}

	}

	public abstract class OpenTypeMarkToLigatureAttachmentPositioningSubtable : IPositioningSubtable {

		private OpenTypeMarkToLigatureAttachmentPositioningSubtable() { }

		public abstract int PerformPositioning(PositionableGlyphRun run, int index);

		internal static OpenTypeMarkToLigatureAttachmentPositioningSubtable Read(FontFileReader reader, long offset) {

			reader.Position = offset;

			ushort posFormat = reader.ReadUInt16();

			if (posFormat == 1) {

				ushort markCoverageOffset = reader.ReadUInt16(); // from beginning of MarkLigPos subtable
				ushort ligatureCoverageOffset = reader.ReadUInt16(); // from beginning of MarkLigPos subtable
				OpenTypeCoverageTable markCoverage = reader.ReadFrom(offset + markCoverageOffset, OpenTypeCoverageTable.Read);
				OpenTypeCoverageTable ligatureCoverage = reader.ReadFrom(offset + ligatureCoverageOffset, OpenTypeCoverageTable.Read);

				ushort markClassCount = reader.ReadUInt16();

				ushort markArrayOffset = reader.ReadUInt16(); // from beginning of MarkLigPos subtable
				MarkArrayTable markArray = reader.ReadFrom(offset + markArrayOffset, MarkArrayTable.Read);

				ushort ligatureArrayOffset = reader.ReadUInt16(); // from beginning of MarkLigPos subtable
				reader.Position = offset + ligatureArrayOffset;

				ushort ligatureCount = reader.ReadUInt16();
				ushort[] ligatureAttachOffsets = reader.ReadUInt16(ligatureCount); // from beginning of LigatureArray table

				OpenTypeMarkToLigatureAttachmentPositioningFormat1.LigatureAttachTable[] ligatureAttachTables =
					reader.ReadFrom(offset + ligatureArrayOffset, ligatureAttachOffsets,
					(r, o) => OpenTypeMarkToLigatureAttachmentPositioningFormat1.LigatureAttachTable.Read(r, o, markClassCount));

				return new OpenTypeMarkToLigatureAttachmentPositioningFormat1(markCoverage, ligatureCoverage, markArray, ligatureAttachTables);
			}
			else {
				throw new FormatException($"Unknown mark-to-ligature attachment positioning subtable format: {posFormat}");
			}
		}

		private class OpenTypeMarkToLigatureAttachmentPositioningFormat1 : OpenTypeMarkToLigatureAttachmentPositioningSubtable {

			public readonly OpenTypeCoverageTable MarkCoverage;
			public readonly OpenTypeCoverageTable BaseCoverage;
			public readonly MarkArrayTable MarkArray;
			public readonly LigatureAttachTable[] LigatureAttachTables;

			internal OpenTypeMarkToLigatureAttachmentPositioningFormat1(OpenTypeCoverageTable markCoverage, OpenTypeCoverageTable baseCoverage, MarkArrayTable markArray, LigatureAttachTable[] ligatureAttachTables) {
				MarkCoverage = markCoverage;
				BaseCoverage = baseCoverage;
				MarkArray = markArray;
				LigatureAttachTables = ligatureAttachTables;
			}

			public override int PerformPositioning(PositionableGlyphRun run, int index) {
				// TODO What do?
				return 0;
			}

			public class LigatureAttachTable {

				public readonly AnchorTable?[][] ComponentRecords;

				public LigatureAttachTable(AnchorTable?[][] componentRecords) {
					ComponentRecords = componentRecords;
				}

				public static LigatureAttachTable Read(FontFileReader reader, long offset, ushort markClassCount) {

					reader.Position = offset;

					ushort componentCount = reader.ReadUInt16();

					AnchorTable?[][] componentRecords = new AnchorTable?[componentCount][];
					for (int i = 0; i < componentCount; i++) {
						ushort?[] ligatureAnchorOffsets = reader.ReadOffset16(markClassCount); // from beginning of LigatureAttach table
						componentRecords[i] = reader.ReadFrom(offset, ligatureAnchorOffsets, AnchorTable.Read);
					}

					return new LigatureAttachTable(componentRecords);
				}

			}

		}

	}

	public abstract class OpenTypeMarkToMarkAttachmentPositioningSubtable : IPositioningSubtable {

		private OpenTypeMarkToMarkAttachmentPositioningSubtable() { }

		public abstract int PerformPositioning(PositionableGlyphRun run, int index);

		internal static OpenTypeMarkToMarkAttachmentPositioningSubtable Read(FontFileReader reader, long offset) {

			reader.Position = offset;

			ushort posFormat = reader.ReadUInt16();

			if (posFormat == 1) {

				ushort mark1CoverageOffset = reader.ReadUInt16(); // from beginning of MarkMarkPos subtable
				ushort mark2CoverageOffset = reader.ReadUInt16(); // from beginning of MarkMarkPos subtable
				OpenTypeCoverageTable mark1Coverage = reader.ReadFrom(offset + mark1CoverageOffset, OpenTypeCoverageTable.Read);
				OpenTypeCoverageTable mark2Coverage = reader.ReadFrom(offset + mark2CoverageOffset, OpenTypeCoverageTable.Read);

				ushort markClassCount = reader.ReadUInt16();

				ushort mark1ArrayOffset = reader.ReadUInt16(); // from beginning of MarkMarkPos subtable
				MarkArrayTable mark1Array = reader.ReadFrom(offset + mark1ArrayOffset, MarkArrayTable.Read);

				ushort mark2ArrayOffset = reader.ReadUInt16(); // from beginning of MarkMarkPos subtable

				reader.Position = offset + mark2ArrayOffset;

				ushort mark2Count = reader.ReadUInt16();

				AnchorTable?[][] mark2Records = new AnchorTable?[mark2Count][];
				for (int i = 0; i < mark2Count; i++) {
					ushort?[] mark2AnchorOffsets = reader.ReadOffset16(markClassCount); // from beginning of Mark2Array table
					mark2Records[i] = reader.ReadFrom(offset + mark2ArrayOffset, mark2AnchorOffsets, AnchorTable.Read);
				}

				return new OpenTypeMarkToMarkAttachmentPositioningFormat1(mark1Coverage, mark2Coverage, mark1Array, mark2Records);
			}
			else {
				throw new FormatException($"Unknown mark-to-mark attachment positioning subtable format: {posFormat}");
			}
		}

		private class OpenTypeMarkToMarkAttachmentPositioningFormat1 : OpenTypeMarkToMarkAttachmentPositioningSubtable {

			public readonly OpenTypeCoverageTable Mark1Coverage;
			public readonly OpenTypeCoverageTable Mark2Coverage;
			public readonly MarkArrayTable Mark1Array;
			public readonly AnchorTable?[][] Mark2Records;

			internal OpenTypeMarkToMarkAttachmentPositioningFormat1(OpenTypeCoverageTable mark1Coverage, OpenTypeCoverageTable mark2Coverage, MarkArrayTable mark1Array, AnchorTable?[][] mark2Records) {
				Mark1Coverage = mark1Coverage;
				Mark2Coverage = mark2Coverage;
				Mark1Array = mark1Array;
				Mark2Records = mark2Records;
			}

			public override int PerformPositioning(PositionableGlyphRun run, int index) {
				if (index > 0
					&& Mark1Coverage.CoverageIndex(run[index]) is ushort mark1CoverageIdx
					&& Mark2Coverage.CoverageIndex(run[index - 1]) is ushort mark2CoverageIdx) {

					(ushort mark1Class, AnchorTable mark1Anchor) = Mark1Array.MarkRecords[mark1CoverageIdx];
					AnchorTable? mark2AttachmentAnchor = Mark2Records[mark2CoverageIdx][mark1Class];

					if (mark2AttachmentAnchor is not null) {
						(short xDelta, short yDelta) = AnchorTable.GetDelta(
							mark2AttachmentAnchor,
							run.GetAdvanceTotal(index - 1),
							run.GetPlacement(index - 1),
							mark1Anchor);

						run.AdjustPlacement(index, xDelta, yDelta);
					}
				}

				return 0;
			}

		}

	}

	public sealed class OpenTypeContextualPositioningSubtable : IPositioningSubtable {

		private readonly OpenTypeSequenceContextTable table;
		private readonly GlyphPositioningLookupTable[] lookupList;

		private OpenTypeContextualPositioningSubtable(OpenTypeSequenceContextTable table, GlyphPositioningLookupTable[] lookupList) {
			this.table = table;
			this.lookupList = lookupList;
		}

		internal static OpenTypeContextualPositioningSubtable Read(FontFileReader reader, long offset, GlyphPositioningLookupTable[] lookupList) {
			OpenTypeSequenceContextTable table = OpenTypeSequenceContextTable.Read(reader, offset);

			return new OpenTypeContextualPositioningSubtable(table, lookupList);
		}

		public void PerformPositioning(PositionableGlyphRun run) {
			SequenceLookupRecord[]?[] runRecords = table.FindRecords(run);

			for (int i = 0; i < runRecords.Length; i++) {
				if (runRecords[i] is SequenceLookupRecord[] records) {
					for (int r = 0; r < records.Length; r++) {
						GlyphPositioningLookupTable action = lookupList[records[r].LookupListIndex];
						action.PerformPositioning(run, i + records[r].SequenceIndex);
					}
				}
			}
		}

		public int PerformPositioning(PositionableGlyphRun run, int index) {
			// TODO This should never actually be called?

			SequenceLookupRecord[]? records = table.FindRecords(run, index);

			if (records is null) { return 0; }

			for (int r = 0; r < records.Length; r++) {
				GlyphPositioningLookupTable action = lookupList[records[r].LookupListIndex];
				action.PerformPositioning(run, index + records[r].SequenceIndex);
			}

			return 0; // Yes?
		}

	}

	public sealed class OpenTypeChainedContextsPositioningSubtable : IPositioningSubtable {

		private readonly OpenTypeChainedSequenceContextTable table;
		private readonly GlyphPositioningLookupTable[] lookupList;

		private OpenTypeChainedContextsPositioningSubtable(OpenTypeChainedSequenceContextTable table, GlyphPositioningLookupTable[] lookupList) {
			this.table = table;
			this.lookupList = lookupList;
		}

		internal static OpenTypeChainedContextsPositioningSubtable Read(FontFileReader reader, long offset, GlyphPositioningLookupTable[] lookupList) {
			OpenTypeChainedSequenceContextTable table = OpenTypeChainedSequenceContextTable.Read(reader, offset);

			return new OpenTypeChainedContextsPositioningSubtable(table, lookupList);
		}

		public void PerformPositioning(PositionableGlyphRun run) {
			SequenceLookupRecord[]?[] runRecords = table.FindRecords(run);

			for (int i = 0; i < runRecords.Length; i++) {
				if (runRecords[i] is SequenceLookupRecord[] records) {
					for (int r = 0; r < records.Length; r++) {
						GlyphPositioningLookupTable action = lookupList[records[r].LookupListIndex];
						action.PerformPositioning(run, i + records[r].SequenceIndex);
					}
				}
			}
		}

		public int PerformPositioning(PositionableGlyphRun run, int index) {
			// TODO This should never actually be called?

			SequenceLookupRecord[]? records = table.FindRecords(run, index);

			if (records is null) { return 0; }

			for (int r = 0; r < records.Length; r++) {
				GlyphPositioningLookupTable action = lookupList[records[r].LookupListIndex];
				action.PerformPositioning(run, index + records[r].SequenceIndex);
			}

			return 0; // Yes?
		}

	}

	[Flags]
	public enum ValueFormat : ushort {
		NONE = 0,
		/// <summary>
		/// Includes horizontal adjustment for placement
		/// </summary>
		X_PLACEMENT = 0x0001,
		/// <summary>
		/// Includes vertical adjustment for placement
		/// </summary>
		Y_PLACEMENT = 0x0002,
		/// <summary>
		/// Includes horizontal adjustment for advance
		/// </summary>
		X_ADVANCE = 0x0004,
		/// <summary>
		/// Includes vertical adjustment for advance
		/// </summary>
		Y_ADVANCE = 0x0008,
		/// <summary>
		/// Includes Device table (non-variable font) / VariationIndex table (variable font) for horizontal placement
		/// </summary>
		X_PLACEMENT_DEVICE = 0x0010,
		/// <summary>
		/// Includes Device table (non-variable font) / VariationIndex table (variable font) for vertical placement
		/// </summary>
		Y_PLACEMENT_DEVICE = 0x0020,
		/// <summary>
		/// Includes Device table (non-variable font) / VariationIndex table (variable font) for horizontal advance
		/// </summary>
		X_ADVANCE_DEVICE = 0x0040,
		/// <summary>
		/// Includes Device table (non-variable font) / VariationIndex table (variable font) for vertical advance
		/// </summary>
		Y_ADVANCE_DEVICE = 0x0080
	}

	public record class ValueRecord {

		public readonly short? XPlacement;
		public readonly short? YPlacement;
		public readonly short? XAdvance;
		public readonly short? YAdvance;

		public ValueRecord(short? xPlacement, short? yPlacement, short? xAdvance, short? yAdvance) {
			XPlacement = xPlacement;
			YPlacement = yPlacement;
			XAdvance = xAdvance;
			YAdvance = yAdvance;
		}

	}

	public static class ValueRecordUtils {

		public static ValueRecord ReadValueRecord(this FontFileReader reader, ValueFormat format) {
			short? xPlacement = null, yPlacement = null;
			short? xAdvance = null, yAdvance = null;

			if((format & ValueFormat.X_PLACEMENT) == ValueFormat.X_PLACEMENT) {
				xPlacement = reader.ReadInt16();
			}
			if ((format & ValueFormat.Y_PLACEMENT) == ValueFormat.Y_PLACEMENT) {
				yPlacement = reader.ReadInt16();
			}
			if ((format & ValueFormat.X_ADVANCE) == ValueFormat.X_ADVANCE) {
				xAdvance = reader.ReadInt16();
			}
			if ((format & ValueFormat.Y_ADVANCE) == ValueFormat.Y_ADVANCE) {
				yAdvance = reader.ReadInt16();
			}

			// Ignore device tables for now
			if ((format & ValueFormat.X_PLACEMENT_DEVICE) == ValueFormat.X_PLACEMENT_DEVICE) {
				reader.SkipOffset16(1);
			}
			if ((format & ValueFormat.Y_PLACEMENT_DEVICE) == ValueFormat.Y_PLACEMENT_DEVICE) {
				reader.SkipOffset16(1);
			}
			if ((format & ValueFormat.X_ADVANCE_DEVICE) == ValueFormat.X_ADVANCE_DEVICE) {
				reader.SkipOffset16(1);
			}
			if ((format & ValueFormat.Y_ADVANCE_DEVICE) == ValueFormat.Y_ADVANCE_DEVICE) {
				reader.SkipOffset16(1);
			}

			return new ValueRecord(xPlacement, yPlacement, xAdvance, yAdvance);
		}

		public static ValueRecord[] ReadValueRecord(this FontFileReader reader, ValueFormat format, int count) {
			ValueRecord[] records = new ValueRecord[count];
			for(int i=0; i<count; i++) {
				records[i] = reader.ReadValueRecord(format);
			}
			return records;
		}

	}

	public class AnchorTable {

		public readonly short XCoordinate;
		public readonly short YCoordinate;

		public AnchorTable(short xCoordinate, short yCoordinate) {
			XCoordinate = xCoordinate;
			YCoordinate = yCoordinate;
		}

		public static AnchorTable Read(FontFileReader reader, long offset) {

			reader.Position = offset;

			ushort anchorFormat = reader.ReadUInt16();
			short xCoordinate = reader.ReadInt16();
			short yCoordinate = reader.ReadInt16();

			if(anchorFormat == 1 || anchorFormat == 2 || anchorFormat == 3) {
				// Ignoring hinting/device/variation information for now
				return new AnchorTable(xCoordinate, yCoordinate);
			}
			else {
				throw new FormatException($"Unknown anchor table format: {anchorFormat}");
			}

		}

		public static (short xDelta, short yDelta) GetDelta(AnchorTable baseAnchor, (short x, short y) baseAdvance, (short x, short y) basePlacement, AnchorTable attachAnchor) {
			// TODO This does not take account of writing direction

			short finalBaseAnchorX = (short)(baseAnchor.XCoordinate + basePlacement.x);
			short finalBaseAnchorY = (short)(baseAnchor.YCoordinate + basePlacement.y);
			short xDelta = (short)(-attachAnchor.XCoordinate - baseAdvance.x + finalBaseAnchorX);
			short yDelta = (short)(-attachAnchor.YCoordinate + finalBaseAnchorY);

			return (xDelta, yDelta);
		}

	}

	public class MarkArrayTable {

		public readonly (ushort markClass, AnchorTable markAnchor)[] MarkRecords;

		public MarkArrayTable((ushort markClass, AnchorTable markAnchor)[] markRecords) {
			MarkRecords = markRecords;
		}

		public static MarkArrayTable Read(FontFileReader reader, long offset) {

			reader.Position = offset;

			ushort markCount = reader.ReadUInt16();

			(ushort markClass, AnchorTable markAnchor)[] markRecords = new (ushort, AnchorTable)[markCount];

			for (int i = 0; i < markCount; i++) {
				ushort markClass = reader.ReadUInt16();
				ushort markAnchorOffset = reader.ReadUInt16(); // from beginning of MarkArray table
				AnchorTable anchorTable = reader.ReadFrom(offset + markAnchorOffset, AnchorTable.Read);

				markRecords[i] = (markClass, anchorTable);
			}

			return new MarkArrayTable(markRecords);
		}

	}

}
