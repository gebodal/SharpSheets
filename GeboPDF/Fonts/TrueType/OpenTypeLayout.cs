using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

namespace GeboPdf.Fonts.TrueType {

	public static class OpenTypeLayoutHelpers {

		public static ushort[]? GetLookups(OpenTypeLayoutTags layoutTags, OpenTypeScriptListTable scriptListTable, OpenTypeFeatureListTable featureListTable) {

			if (scriptListTable.ScriptRecords.TryGetValue(layoutTags.ScriptTag, out OpenTypeScriptTable? scriptTable)) {
				OpenTypeLanguageSystemTable? langSysTable = scriptTable.GetLangSysTable(layoutTags.LangSysTag);

				if (langSysTable is not null) {
					List<OpenTypeFeatureTable> features = new List<OpenTypeFeatureTable>();

					if (langSysTable.RequiredFeatureIndex.HasValue) {
						features.Add(featureListTable.FeatureRecords[langSysTable.RequiredFeatureIndex.Value].feature);
					}

					for (int i = 0; i < langSysTable.FeatureIndices.Length; i++) {
						(string featureTag, OpenTypeFeatureTable featureTable) = featureListTable.FeatureRecords[langSysTable.FeatureIndices[i]];
						if (layoutTags.FeatureTags.Contains(featureTag)) {
							features.Add(featureTable);
						}
					}

					HashSet<ushort> lookupIdxs = new HashSet<ushort>();

					for (int f = 0; f < features.Count; f++) {
						lookupIdxs.UnionWith(features[f].LookupListIndices);
					}

					if (lookupIdxs.Count > 0) {
						return lookupIdxs.OrderBy(i => i).ToArray();
					}
				}
			}

			return null;
		}

	}

	public class OpenTypeLayoutTags {

		public readonly string ScriptTag;
		public readonly string? LangSysTag;
		public readonly IReadOnlySet<string> FeatureTags;

		public OpenTypeLayoutTags(string scriptTag, string? langSysTag, IEnumerable<string> featureTags) {
			ScriptTag = scriptTag;
			LangSysTag = langSysTag;
			FeatureTags = new HashSet<string>(featureTags);
		}

		public static IReadOnlySet<string> GetDefaults(string scriptTag, string? langSysTag) {
			HashSet<string> defaults = new HashSet<string>() {
				"ccmp", "curs", "locl", "mark", "mkmk", // Always
				"calt", "clig", "cpsp", "liga", "opbd", "rand", // Active by default
				"kern" // Active by default for horizontal text
				// "rvrn" // If using variational fonts
				// "size" // Should be active by default, but contributes no subtables?
				// "valt", "vert", "vkrn", "vrt2" // Active by default for vertical text
			};
			return defaults;
		}

		/// <summary>
		/// Keys are script tags, and values are script names.
		/// </summary>
		public static IReadOnlyDictionary<string, string> ScriptTagsRegistry { get; } // Tag -> Script name
		/// <summary>
		/// Keys are langSys tags, and values are language system names.
		/// </summary>
		public static IReadOnlyDictionary<string, string> LangSysTagsRegistry { get; } // Tag -> Language system name
		/// <summary>
		/// Keys are feature tags, and values are friendly feature names.
		/// </summary>
		public static IReadOnlyDictionary<string, string> FeatureTagsRegistry { get; } // Tag -> Friendly feature name


		static OpenTypeLayoutTags() {

			string[][] scriptTagsData = ResourceFileReading.ReadResourceFile("scriptTags.txt");
			string[][] langSysTagsData = ResourceFileReading.ReadResourceFile("languageTags.txt");
			string[][] featureTagsData = ResourceFileReading.ReadResourceFile("featureTags.txt");

			ScriptTagsRegistry = scriptTagsData.ToDictionary(r => r[1], r => r[0]);
			LangSysTagsRegistry = langSysTagsData.ToDictionary(r => r[1], r => r[0]);
			FeatureTagsRegistry = featureTagsData.ToDictionary(r => r[0], r => r[1]);

		}

	}

	public class OpenTypeLayoutTagSet {

		public IReadOnlyDictionary<string, IReadOnlySet<string>> ScriptTags { get; private set; }
		public IReadOnlySet<string> FeatureTags { get; private set; }

		public OpenTypeLayoutTagSet(IReadOnlyDictionary<string, IReadOnlySet<string>> scriptTags, IReadOnlySet<string> featureTags) {
			ScriptTags = scriptTags;
			FeatureTags = featureTags;
		}

		public void UnionWith(OpenTypeLayoutTagSet other) {
			Dictionary<string, HashSet<string>> scriptTags = new Dictionary<string, HashSet<string>>();

			void AddScriptTags(IReadOnlyDictionary<string, IReadOnlySet<string>> sTags) {
				foreach ((string sTag, IReadOnlySet<string> langSysTags) in sTags) {
					if (!scriptTags.ContainsKey(sTag)) {
						scriptTags[sTag] = new HashSet<string>();
					}
					scriptTags[sTag].UnionWith(langSysTags);
				}
			}
			AddScriptTags(this.ScriptTags);
			AddScriptTags(other.ScriptTags);

			this.ScriptTags = scriptTags.ToDictionary(kv => kv.Key, kv => (IReadOnlySet<string>)kv.Value);

			HashSet<string> featureTags = new HashSet<string>();
			featureTags.UnionWith(this.FeatureTags);
			featureTags.UnionWith(other.FeatureTags);

			this.FeatureTags = featureTags;
		}

		public static OpenTypeLayoutTagSet Empty() {
			return new OpenTypeLayoutTagSet(new Dictionary<string, IReadOnlySet<string>>(), new HashSet<string>());
		}

	}

	public class OpenTypeScriptListTable {

		public readonly Dictionary<string, OpenTypeScriptTable> ScriptRecords;

		internal OpenTypeScriptListTable(Dictionary<string, OpenTypeScriptTable> scriptRecords) {
			ScriptRecords = scriptRecords;
		}

		internal static OpenTypeScriptListTable Read(FontFileReader reader, long offset) {

			reader.Position = offset;

			ushort scriptCount = reader.ReadUInt16();

			Dictionary<string, OpenTypeScriptTable> scriptRecords = new Dictionary<string, OpenTypeScriptTable>();

			for (int i = 0; i < scriptCount; i++) {
				string scriptTag = reader.ReadASCIIString(4);
				ushort scriptOffset = reader.ReadUInt16(); // From beginning of script list (i.e. offset)

				OpenTypeScriptTable scriptTable = reader.ReadFrom(offset + scriptOffset, OpenTypeScriptTable.Read);

				scriptRecords[scriptTag] = scriptTable;
			}

			return new OpenTypeScriptListTable(scriptRecords);
		}

		internal static IReadOnlyDictionary<string, IReadOnlySet<string>> ReadTags(FontFileReader reader, long offset) {

			reader.Position = offset;

			ushort scriptCount = reader.ReadUInt16();

			Dictionary<string, IReadOnlySet<string>> scriptTags = new Dictionary<string, IReadOnlySet<string>>();

			for (int i = 0; i < scriptCount; i++) {
				string scriptTag = reader.ReadASCIIString(4);
				ushort scriptOffset = reader.ReadUInt16(); // From beginning of script list (i.e. offset)

				IReadOnlySet<string> langSysTags = reader.ReadFrom(offset + scriptOffset, OpenTypeScriptTable.ReadTags);

				scriptTags.Add(scriptTag, langSysTags);
			}

			return scriptTags;
		}

	}

	public class OpenTypeScriptTable {

		public readonly OpenTypeLanguageSystemTable? DefaultLangSysTable;
		public readonly Dictionary<string, OpenTypeLanguageSystemTable> LangSysRecords;

		internal OpenTypeScriptTable(OpenTypeLanguageSystemTable? defaultLangSysTable, Dictionary<string, OpenTypeLanguageSystemTable> langSysRecords) {
			DefaultLangSysTable = defaultLangSysTable;
			LangSysRecords = langSysRecords;
		}

		internal static OpenTypeScriptTable Read(FontFileReader reader, long offset) {

			reader.Position = offset;

			ushort? defaultLangSysOffset = reader.ReadOffset16();
			OpenTypeLanguageSystemTable? defaultLangSysTable = reader.ReadFrom(offset + defaultLangSysOffset, OpenTypeLanguageSystemTable.Read);

			ushort langSysCount = reader.ReadUInt16();

			Dictionary<string, OpenTypeLanguageSystemTable> langSysRecords = new Dictionary<string, OpenTypeLanguageSystemTable>();

			for (int i = 0; i < langSysCount; i++) {
				string langSysTag = reader.ReadASCIIString(4);
				ushort langSysOffset = reader.ReadUInt16(); // From beginning of script table (i.e. offset)

				OpenTypeLanguageSystemTable langSysTable = reader.ReadFrom(offset + langSysOffset, OpenTypeLanguageSystemTable.Read);

				langSysRecords[langSysTag] = langSysTable;
			}

			return new OpenTypeScriptTable(defaultLangSysTable, langSysRecords);
		}

		internal static IReadOnlySet<string> ReadTags(FontFileReader reader, long offset) {

			reader.Position = offset;

			/*ushort? defaultLangSysOffset*/ _ = reader.ReadOffset16();

			ushort langSysCount = reader.ReadUInt16();

			HashSet<string> langSysTags = new HashSet<string>();

			for (int i = 0; i < langSysCount; i++) {
				string langSysTag = reader.ReadASCIIString(4);
				/*ushort langSysOffset*/ _ = reader.ReadUInt16(); // From beginning of script table (i.e. offset)

				langSysTags.Add(langSysTag);
			}

			return langSysTags;
		}

		public OpenTypeLanguageSystemTable? GetLangSysTable(string? langSysTag) {
			if(langSysTag is not null && LangSysRecords.TryGetValue(langSysTag, out OpenTypeLanguageSystemTable? langSysTable)) {
				return langSysTable;
			}
			else {
				return DefaultLangSysTable;
			}
		}

	}

	public class OpenTypeLanguageSystemTable {

		public readonly ushort? RequiredFeatureIndex;
		public readonly ushort[] FeatureIndices;

		internal OpenTypeLanguageSystemTable(ushort? requiredFeatureIndex, ushort[] featureIndices) {
			RequiredFeatureIndex = requiredFeatureIndex;
			FeatureIndices = featureIndices;
		}

		internal static OpenTypeLanguageSystemTable Read(FontFileReader reader, long offset) {

			reader.Position = offset;

			/*ushort? lookupOrderOffset*/ _ = reader.ReadUInt16(); // Currently unused
			
			ushort requiredFeatureIndexValue = reader.ReadUInt16();
			ushort? requiredFeatureIndex = requiredFeatureIndexValue == 0xFFFF ? null : requiredFeatureIndexValue;

			ushort featureIndexCount = reader.ReadUInt16();

			ushort[] featureIndices = reader.ReadUInt16(featureIndexCount);

			return new OpenTypeLanguageSystemTable(requiredFeatureIndex, featureIndices);
		}

	}

	public class OpenTypeFeatureListTable {

		public readonly (string tag, OpenTypeFeatureTable feature)[] FeatureRecords;

		internal OpenTypeFeatureListTable((string tag, OpenTypeFeatureTable feature)[] featureRecords) {
			FeatureRecords = featureRecords;
		}

		internal static OpenTypeFeatureListTable Read(FontFileReader reader, long offset) {

			reader.Position = offset;

			ushort featureCount = reader.ReadUInt16();

			(string tag, OpenTypeFeatureTable feature)[] featureRecords = new (string tag, OpenTypeFeatureTable feature)[featureCount];

			for (int i = 0; i < featureCount; i++) {
				string featureTag = reader.ReadASCIIString(4);
				ushort featureOffset = reader.ReadUInt16(); // From beginning of feature list (i.e. offset)

				OpenTypeFeatureTable featureTable = reader.ReadFrom(offset + featureOffset, OpenTypeFeatureTable.Read);

				featureRecords[i] = (featureTag, featureTable);
			}

			return new OpenTypeFeatureListTable(featureRecords);
		}

		internal static IReadOnlySet<string> ReadTags(FontFileReader reader, long offset) {

			reader.Position = offset;

			ushort featureCount = reader.ReadUInt16();

			HashSet<string> featureTags = new HashSet<string>();

			for (int i = 0; i < featureCount; i++) {
				string featureTag = reader.ReadASCIIString(4);
				/*ushort featureOffset*/ _ = reader.ReadUInt16(); // From beginning of feature list (i.e. offset)

				featureTags.Add(featureTag);
			}

			return featureTags;
		}

	}

	public class OpenTypeFeatureTable {

		/// <summary>
		/// Offset to Feature Parameters Table relative to start of font file.
		/// </summary>
		public readonly long? FeatureParamsOffset;
		public readonly ushort[] LookupListIndices;

		internal OpenTypeFeatureTable(long? featureParamsOffset, ushort[] lookupListIndices) {
			FeatureParamsOffset = featureParamsOffset;
			LookupListIndices = lookupListIndices;
		}

		internal static OpenTypeFeatureTable Read(FontFileReader reader, long offset) {

			reader.Position = offset;

			ushort? featureParamsOffset = reader.ReadOffset16(); // offset relative to the beginning of the Feature Table (i.e. offset)
			ushort lookupIndexCount = reader.ReadUInt16();

			ushort[] lookupListIndices = reader.ReadUInt16(lookupIndexCount);

			long? fullFeatureParamsOffset = offset + featureParamsOffset;

			return new OpenTypeFeatureTable(fullFeatureParamsOffset, lookupListIndices);
		}

	}

	public class OpenTypeLookupListTable {

		public readonly OpenTypeLookupTable[] Lookups;

		internal OpenTypeLookupListTable(OpenTypeLookupTable[] lookups) {
			Lookups = lookups;
		}

		internal static OpenTypeLookupListTable Read(FontFileReader reader, long offset) {

			reader.Position = offset;

			ushort lookupCount = reader.ReadUInt16();

			ushort[] lookupOffsets = reader.ReadUInt16(lookupCount); // offsets to Lookup tables, from beginning of LookupList (i.e. offset)

			OpenTypeLookupTable[] lookups = new OpenTypeLookupTable[lookupCount];
			for (int i = 0; i < lookupCount; i++) {
				lookups[i] = reader.ReadFrom(offset + lookupOffsets[i], OpenTypeLookupTable.Read);
			}

			return new OpenTypeLookupListTable(lookups);
		}

	}

	[Flags]
	public enum LookupFlag : ushort {
		/// <summary>
		/// This bit relates only to the correct processing of the cursive attachment lookup type (GPOS lookup type 3). When this bit is set, the last glyph in a given sequence to which the cursive attachment lookup is applied, will be positioned on the baseline.
		/// </summary>
		RIGHT_TO_LEFT = 1 << 1,
		/// <summary>
		/// If set, skips over base glyphs
		/// </summary>
		IGNORE_BASE_GLYPHS = 1 << 2,
		/// <summary>
		/// If set, skips over ligatures
		/// </summary>
		IGNORE_LIGATURES = 1 << 3,
		/// <summary>
		/// If set, skips over all combining marks
		/// </summary>
		IGNORE_MARKS = 1 << 4,
		/// <summary>
		/// If set, indicates that the lookup table structure is followed by a MarkFilteringSet field. The layout engine skips over all mark glyphs not in the mark filtering set indicated.
		/// </summary>
		USE_MARK_FILTERING_SET = 1 << 5
	}

	public class OpenTypeLookupTable {

		public readonly ushort LookupType;
		public readonly LookupFlag LookupFlag;
		public readonly ushort MarkAttachmentTypeMask;
		public readonly long[] SubtableOffsets; // Absolute offsets within FontFile
		public readonly ushort MarkFilteringSet;

		internal OpenTypeLookupTable(ushort lookupType, LookupFlag lookupFlag, ushort markAttachmentTypeMask, long[] subtableOffsets, ushort markFilteringSet) {
			LookupType = lookupType;
			LookupFlag = lookupFlag;
			MarkAttachmentTypeMask = markAttachmentTypeMask;
			SubtableOffsets = subtableOffsets;
			MarkFilteringSet = markFilteringSet;
		}

		internal static OpenTypeLookupTable Read(FontFileReader reader, long offset) {

			reader.Position = offset;

			ushort lookupType = reader.ReadUInt16();
			ushort lookupFlagValue = reader.ReadUInt16();
			LookupFlag lookupFlag = (LookupFlag)(lookupFlagValue & 0b11111);
			ushort markAttachmentTypeMask = (ushort)(lookupFlagValue & (ushort)0xFF00);
			ushort subTableCount = reader.ReadUInt16();

			long[] subtableOffsets = new long[subTableCount]; // reader.ReadUInt16(subTableCount);

			for(int i=0; i< subTableCount; i++) {
				subtableOffsets[i] = offset + reader.ReadUInt16();
			}

			ushort markFilteringSet = reader.ReadUInt16();

			return new OpenTypeLookupTable(lookupType, lookupFlag, markAttachmentTypeMask, subtableOffsets, markFilteringSet);
		}

	}

	public abstract class OpenTypeCoverageTable {

		private OpenTypeCoverageTable() { }

		public abstract int Length { get; }
		public abstract ushort? CoverageIndex(ushort glyph);
		public abstract bool IsCovered(ushort glyph);

		internal static OpenTypeCoverageTable Read(FontFileReader reader, long offset) {

			reader.Position = offset;

			ushort coverageFormat = reader.ReadUInt16();

			if (coverageFormat == 1) {
				ushort glyphCount = reader.ReadUInt16();

				Dictionary<ushort, ushort> coverageIndexes = new Dictionary<ushort, ushort>();
				for (ushort coverageIdx = 0; coverageIdx < glyphCount; coverageIdx++) {
					ushort glyph = reader.ReadUInt16();
					coverageIndexes[glyph] = coverageIdx;
				}

				return new OpenTypeCoverageTableFormat1(coverageIndexes);
			}
			else if (coverageFormat == 2) {
				ushort rangeCount = reader.ReadUInt16();

				(ushort startGlyphID, ushort endGlyphID, ushort startCoverageIndex)[] rangeRecords = new (ushort, ushort, ushort)[rangeCount];
				for (int i = 0; i < rangeCount; i++) {
					ushort startGlyphID = reader.ReadUInt16();
					ushort endGlyphID = reader.ReadUInt16();
					ushort startCoverageIndex = reader.ReadUInt16();
					rangeRecords[i] = (startGlyphID, endGlyphID, startCoverageIndex);
				}

				return new OpenTypeCoverageTableFormat2(rangeRecords);
			}
			else {
				throw new FormatException($"Unknown coverage table format: {coverageFormat}");
			}
		}

		private class OpenTypeCoverageTableFormat1 : OpenTypeCoverageTable {

			public readonly Dictionary<ushort, ushort> CoverageIndexes;

			internal OpenTypeCoverageTableFormat1(Dictionary<ushort, ushort> coverageIndexes) : base() {
				CoverageIndexes = coverageIndexes;
			}

			public override int Length => CoverageIndexes.Count;

			public override ushort? CoverageIndex(ushort glyph) {
				if(CoverageIndexes.TryGetValue(glyph, out ushort index)) { return index; }
				else { return null; }
			}

			public override bool IsCovered(ushort glyph) {
				return CoverageIndexes.ContainsKey(glyph);
			}

		}

		private class OpenTypeCoverageTableFormat2 : OpenTypeCoverageTable {

			public readonly (ushort startGlyphID, ushort endGlyphID, ushort startCoverageIndex)[] RangeRecords;

			internal OpenTypeCoverageTableFormat2((ushort startGlyphID, ushort endGlyphID, ushort startCoverageIndex)[] rangeRecords) : base() {
				RangeRecords = rangeRecords;
				Length = GetLength(RangeRecords);
			}

			public override int Length { get; }

			private static int GetLength((ushort startGlyphID, ushort endGlyphID, ushort startCoverageIndex)[] rangeRecords) {
				int count = 0;
				for(int i=0; i<rangeRecords.Length; i++) {
					count += rangeRecords[i].endGlyphID - rangeRecords[i].startGlyphID + 1;
				}
				return count;
			}

			public override ushort? CoverageIndex(ushort glyph) {
				for (int i = 0; i < RangeRecords.Length; i++) {
					if(glyph >= RangeRecords[i].startGlyphID && glyph <= RangeRecords[i].endGlyphID) {
						return (ushort)(RangeRecords[i].startCoverageIndex + glyph - RangeRecords[i].startGlyphID);
					}
				}

				return null;
			}

			public override bool IsCovered(ushort glyph) {
				for (int i = 0; i < RangeRecords.Length; i++) {
					if (glyph >= RangeRecords[i].startGlyphID && glyph <= RangeRecords[i].endGlyphID) {
						return true;
					}
				}

				return false;
			}

		}

	}

	public abstract class OpenTypeClassDefinitionTable {

		private OpenTypeClassDefinitionTable() { }

		public abstract ushort GetClass(ushort glyph);

		internal static OpenTypeClassDefinitionTable Read(FontFileReader reader, long offset) {

			reader.Position = offset;

			ushort classFormat = reader.ReadUInt16();

			if (classFormat == 1) {
				ushort startGlyphID = reader.ReadUInt16();
				ushort glyphCount = reader.ReadUInt16();

				ushort[] classValueArray = new ushort[glyphCount];
				for (int i = 0; i < glyphCount; i++) {
					classValueArray[i] = reader.ReadUInt16();
				}

				return new OpenTypeClassDefinitionTableFormat1(startGlyphID, classValueArray);
			}
			else if (classFormat == 2) {
				ushort classRangeCount = reader.ReadUInt16();

				(ushort startGlyphID, ushort endGlyphID, ushort classValue)[] classRangeRecords = new (ushort, ushort, ushort)[classRangeCount];
				for (int i = 0; i < classRangeCount; i++) {
					ushort startGlyphID = reader.ReadUInt16();
					ushort endGlyphID = reader.ReadUInt16();
					ushort classValue = reader.ReadUInt16();
					classRangeRecords[i] = (startGlyphID, endGlyphID, classValue);
				}

				return new OpenTypeClassDefinitionTableFormat2(classRangeRecords);
			}
			else {
				throw new FormatException($"Unknown class definition table format: {classFormat}");
			}
		}

		private class OpenTypeClassDefinitionTableFormat1 : OpenTypeClassDefinitionTable {

			public readonly ushort StartGlyphID;
			public readonly ushort[] ClassValueArray;

			internal OpenTypeClassDefinitionTableFormat1(ushort startGlyphID, ushort[] classValueArray) : base() {
				StartGlyphID = startGlyphID;
				ClassValueArray = classValueArray;
			}

			public override ushort GetClass(ushort glyph) {
				int index = StartGlyphID - glyph;
				if (index >= 0 && index < ClassValueArray.Length) {
					return ClassValueArray[index];
				}
				else {
					return 0;
				}
			}

		}

		private class OpenTypeClassDefinitionTableFormat2 : OpenTypeClassDefinitionTable {

			public readonly (ushort startGlyphID, ushort endGlyphID, ushort classValue)[] ClassRangeRecords;

			internal OpenTypeClassDefinitionTableFormat2((ushort startGlyphID, ushort endGlyphID, ushort classValue)[] classRangeRecords) : base() {
				ClassRangeRecords = classRangeRecords;
			}


			public override ushort GetClass(ushort glyph) {
				for (int i = 0; i < ClassRangeRecords.Length; i++) {
					if (glyph >= ClassRangeRecords[i].startGlyphID && glyph <= ClassRangeRecords[i].endGlyphID) {
						return ClassRangeRecords[i].classValue;
					}
				}

				return 0;
			}
		}

	}

	public struct SequenceLookupRecord {
		public ushort SequenceIndex;
		public ushort LookupListIndex;

		public SequenceLookupRecord(ushort sequenceIndex, ushort lookupListIndex) {
			SequenceIndex = sequenceIndex;
			LookupListIndex = lookupListIndex;
		}
	}

	internal static class SequenceLookupRecordUtils {

		public static SequenceLookupRecord ReadSequenceLookupRecord(this FontFileReader reader) {
			ushort sequenceIndex = reader.ReadUInt16();
			ushort lookupListIndex = reader.ReadUInt16();
			return new SequenceLookupRecord(sequenceIndex, lookupListIndex);
		}

		public static SequenceLookupRecord[] ReadSequenceLookupRecord(this FontFileReader reader, int count) {
			SequenceLookupRecord[] array = new SequenceLookupRecord[count];
			for (int i = 0; i < count; i++) {
				array[i] = reader.ReadSequenceLookupRecord();
			}
			return array;
		}

	}

	/*
	public class TableCollection<T> {

		public readonly T[] Tables;

		private TableCollection(T[] tables) {
			Tables = tables;
		}

		internal static TableCollection<T> ReadFromCountedOffsets(FontFileReader reader, long offset, Func<FontFileReader, long, T> tableParser) {
			ushort tableCount = reader.ReadUInt16();
			ushort[] tableOffsets = reader.ReadUInt16(tableCount); // from beginning of this table

			T[] tables = new T[tableCount];
			for (int i = 0; i < tableCount; i++) {
				tables[i] = reader.ReadFrom(offset + tableOffsets[i], tableParser);
			}

			return new TableCollection<T>(tables);
		}

	}
	*/

	public abstract class OpenTypeSequenceContextTable {

		private OpenTypeSequenceContextTable() { }

		public abstract SequenceLookupRecord[]?[] FindRecords(GlyphRun run);
		public abstract SequenceLookupRecord[]? FindRecords(GlyphRun run, int index);

		internal static OpenTypeSequenceContextTable Read(FontFileReader reader, long offset) {

			reader.Position = offset;

			ushort format = reader.ReadUInt16();

			if (format == 1) {
				ushort coverageOffset = reader.ReadUInt16(); // from beginning of SequenceContextFormat1 table
				OpenTypeCoverageTable coverage = reader.ReadFrom(offset + coverageOffset, OpenTypeCoverageTable.Read);
				ushort seqRuleSetCount = reader.ReadUInt16();
				ushort?[] seqRuleSetOffsets = reader.ReadOffset16(seqRuleSetCount); // from beginning of SequenceContextFormat1 table

				OpenTypeSequenceContextFormat1.SequenceRuleSetTable?[] seqRuleSets = new OpenTypeSequenceContextFormat1.SequenceRuleSetTable?[seqRuleSetCount];
				for (int i = 0; i < seqRuleSetCount; i++) {
					seqRuleSets[i] = reader.ReadFrom(offset + seqRuleSetOffsets[i], OpenTypeSequenceContextFormat1.SequenceRuleSetTable.Read);
				}

				return new OpenTypeSequenceContextFormat1(coverage, seqRuleSets);
			}
			else if (format == 2) {
				ushort coverageOffset = reader.ReadUInt16(); // from beginning of SequenceContextFormat2 table
				OpenTypeCoverageTable coverage = reader.ReadFrom(offset + coverageOffset, OpenTypeCoverageTable.Read);
				ushort classDefOffset = reader.ReadUInt16(); // from beginning of SequenceContextFormat2 table
				OpenTypeClassDefinitionTable classDef = reader.ReadFrom(offset + classDefOffset, OpenTypeClassDefinitionTable.Read);

				ushort classSeqRuleSetCount = reader.ReadUInt16();
				ushort?[] classSeqRuleSetOffsets = reader.ReadOffset16(classSeqRuleSetCount); // from beginning of SequenceContextFormat2 table

				OpenTypeSequenceContextFormat2.ClassSequenceRuleSetTable?[] classSeqRuleSets = new OpenTypeSequenceContextFormat2.ClassSequenceRuleSetTable?[classSeqRuleSetCount];
				for (int i = 0; i < classSeqRuleSetCount; i++) {
					classSeqRuleSets[i] = reader.ReadFrom(offset + classSeqRuleSetOffsets[i], OpenTypeSequenceContextFormat2.ClassSequenceRuleSetTable.Read);
				}

				return new OpenTypeSequenceContextFormat2(coverage, classDef, classSeqRuleSets);
			}
			else if (format == 3) {
				ushort glyphCount = reader.ReadUInt16();
				ushort seqLookupCount = reader.ReadUInt16();
				ushort[] coverageOffsets = reader.ReadUInt16(glyphCount); // from beginning of SequenceContextFormat3 subtable
				SequenceLookupRecord[] seqLookupRecords = reader.ReadSequenceLookupRecord(seqLookupCount);

				OpenTypeCoverageTable[] coverageTables = new OpenTypeCoverageTable[glyphCount];
				for(int i=0; i<glyphCount; i++) {
					coverageTables[i] = reader.ReadFrom(offset + coverageOffsets[i], OpenTypeCoverageTable.Read);
				}

				return new OpenTypeSequenceContextFormat3(coverageTables, seqLookupRecords);
			}
			else {
				throw new FormatException($"Unknown sequence context table format: {format}");
			}
		}

		private class OpenTypeSequenceContextFormat1 : OpenTypeSequenceContextTable {

			public readonly OpenTypeCoverageTable Coverage;
			public readonly SequenceRuleSetTable?[] SeqRuleSets;

			internal OpenTypeSequenceContextFormat1(OpenTypeCoverageTable coverage, SequenceRuleSetTable?[] seqRuleSets) : base() {
				Coverage = coverage;
				SeqRuleSets = seqRuleSets;
			}

			public override SequenceLookupRecord[]?[] FindRecords(GlyphRun run) {
				SequenceLookupRecord[]?[] records = new SequenceLookupRecord[run.Count][];
				for (int i = 0; i < run.Count; i++) {
					if (Coverage.CoverageIndex(run[i]) is ushort coverageIdx
						&& coverageIdx < SeqRuleSets.Length
						&& SeqRuleSets[coverageIdx] is SequenceRuleSetTable seqRuleSet
						&& seqRuleSet.GetRule(run, i) is SequenceRuleTable seqRule) {

						records[i] = seqRule.SeqLookupRecords;
						i += seqRule.GlyphCount - 1;
					}
					else {
						records[i] = null;
					}
				}
				return records;
			}

			public override SequenceLookupRecord[]? FindRecords(GlyphRun run, int index) {
				if (Coverage.CoverageIndex(run[index]) is ushort coverageIdx
						&& coverageIdx < SeqRuleSets.Length
						&& SeqRuleSets[coverageIdx] is SequenceRuleSetTable seqRuleSet
						&& seqRuleSet.GetRule(run, index) is SequenceRuleTable seqRule) {

					return seqRule.SeqLookupRecords;
				}
				else {
					return null;
				}
			}

			public class SequenceRuleSetTable {

				public readonly SequenceRuleTable[] SeqRules;

				public SequenceRuleSetTable(SequenceRuleTable[] seqRules) {
					SeqRules = seqRules;
				}

				internal static SequenceRuleSetTable Read(FontFileReader reader, long offset) {
					reader.Position = offset;

					ushort seqRuleCount = reader.ReadUInt16();
					ushort[] seqRuleOffsets = reader.ReadUInt16(seqRuleCount); // from beginning of the SequenceRuleSet table

					SequenceRuleTable[] seqRules = new SequenceRuleTable[seqRuleCount];
					for (int i = 0; i < seqRuleCount; i++) {
						seqRules[i] = reader.ReadFrom(offset + seqRuleOffsets[i], SequenceRuleTable.Read);
					}

					return new SequenceRuleSetTable(seqRules);
				}

				public SequenceRuleTable? GetRule(GlyphRun run, int index) {
					for (int s = 0; s < SeqRules.Length; s++) {
						if (SeqRules[s].IsMatch(run, index)) {
							return SeqRules[s];
						}
					}
					return null;
				}

			}

			public class SequenceRuleTable {

				public int GlyphCount => InputSequence.Length + 1;
				public readonly ushort[] InputSequence;
				public readonly SequenceLookupRecord[] SeqLookupRecords;

				public SequenceRuleTable(ushort[] inputSequence, SequenceLookupRecord[] seqLookupRecords) {
					InputSequence = inputSequence;
					SeqLookupRecords = seqLookupRecords;
				}

				internal static SequenceRuleTable Read(FontFileReader reader, long offset) {

					reader.Position = offset;

					ushort glyphCount = reader.ReadUInt16();
					ushort seqLookupCount = reader.ReadUInt16();
					ushort[] inputSequence = reader.ReadUInt16(glyphCount - 1);
					SequenceLookupRecord[] seqLookupRecords = reader.ReadSequenceLookupRecord(seqLookupCount);

					return new SequenceRuleTable(inputSequence, seqLookupRecords);
				}

				public bool IsMatch(GlyphRun run, int index) {
					for (int j = 0; j < InputSequence.Length; j++) {
						if (run[index + 1 + j] != InputSequence[j]) { // First glyph already checked by this point
							return false;
						}
					}
					return true;
				}

			}

		}

		private class OpenTypeSequenceContextFormat2 : OpenTypeSequenceContextTable {

			public readonly OpenTypeCoverageTable Coverage;
			public readonly OpenTypeClassDefinitionTable ClassDef;
			public readonly ClassSequenceRuleSetTable?[] ClassSeqRuleSets;

			internal OpenTypeSequenceContextFormat2(OpenTypeCoverageTable coverage, OpenTypeClassDefinitionTable classDef, ClassSequenceRuleSetTable?[] classSeqRuleSets) : base() {
				Coverage = coverage;
				ClassDef = classDef;
				ClassSeqRuleSets = classSeqRuleSets;
			}

			public override SequenceLookupRecord[]?[] FindRecords(GlyphRun run) {
				SequenceLookupRecord[]?[] records = new SequenceLookupRecord[run.Count][];
				for (int i = 0; i < run.Count; i++) {
					if (Coverage.IsCovered(run[i])
						&& ClassDef.GetClass(run[i]) is ushort classVal
						&& classVal < ClassSeqRuleSets.Length
						&& ClassSeqRuleSets[classVal] is ClassSequenceRuleSetTable classSeqRuleSet
						&& classSeqRuleSet.GetRule(run, i, ClassDef) is ClassSequenceRuleTable classSeqRule) {

						records[i] = classSeqRule.SeqLookupRecords;
						i += classSeqRule.GlyphCount - 1;
					}
					else {
						records[i] = null;
					}
				}
				return records;
			}

			public override SequenceLookupRecord[]? FindRecords(GlyphRun run, int index) {
				if (Coverage.IsCovered(run[index])
					&& ClassDef.GetClass(run[index]) is ushort classVal
					&& classVal < ClassSeqRuleSets.Length
					&& ClassSeqRuleSets[classVal] is ClassSequenceRuleSetTable classSeqRuleSet
					&& classSeqRuleSet.GetRule(run, index, ClassDef) is ClassSequenceRuleTable classSeqRule) {

					return classSeqRule.SeqLookupRecords;
				}
				else {
					return null;
				}
			}

			public class ClassSequenceRuleSetTable {

				public readonly ClassSequenceRuleTable[] ClassSeqRules;

				public ClassSequenceRuleSetTable(ClassSequenceRuleTable[] classSeqRules) {
					ClassSeqRules = classSeqRules;
				}

				internal static ClassSequenceRuleSetTable Read(FontFileReader reader, long offset) {
					reader.Position = offset;

					ushort classSeqRuleCount = reader.ReadUInt16();
					ushort[] classSeqRuleOffsets = reader.ReadUInt16(classSeqRuleCount); // from beginning of the ClassSequenceRuleSet table

					ClassSequenceRuleTable[] classSeqRules = new ClassSequenceRuleTable[classSeqRuleCount];
					for (int i = 0; i < classSeqRuleCount; i++) {
						classSeqRules[i] = reader.ReadFrom(offset + classSeqRuleOffsets[i], ClassSequenceRuleTable.Read);
					}

					return new ClassSequenceRuleSetTable(classSeqRules);
				}

				public ClassSequenceRuleTable? GetRule(GlyphRun run, int index, OpenTypeClassDefinitionTable classDef) {
					for (int s = 0; s < ClassSeqRules.Length; s++) {
						if (ClassSeqRules[s].IsMatch(run, index, classDef)) {
							return ClassSeqRules[s];
						}
					}
					return null;
				}

			}

			public class ClassSequenceRuleTable {

				public int GlyphCount => InputSequence.Length + 1;
				public readonly ushort[] InputSequence;
				public readonly SequenceLookupRecord[] SeqLookupRecords;

				public ClassSequenceRuleTable(ushort[] inputSequence, SequenceLookupRecord[] seqLookupRecords) {
					InputSequence = inputSequence;
					SeqLookupRecords = seqLookupRecords;
				}

				internal static ClassSequenceRuleTable Read(FontFileReader reader, long offset) {

					reader.Position = offset;

					ushort glyphCount = reader.ReadUInt16();
					ushort seqLookupCount = reader.ReadUInt16();
					ushort[] inputSequence = reader.ReadUInt16(glyphCount - 1);
					SequenceLookupRecord[] seqLookupRecords = reader.ReadSequenceLookupRecord(seqLookupCount);

					return new ClassSequenceRuleTable(inputSequence, seqLookupRecords);
				}

				public bool IsMatch(GlyphRun run, int index, OpenTypeClassDefinitionTable classDef) {
					for (int j = 0; j < InputSequence.Length; j++) {
						if (classDef.GetClass(run[index + 1 + j]) != InputSequence[j]) { // First glyph already checked by this point
							return false;
						}
					}
					return true;
				}

			}

		}

		private class OpenTypeSequenceContextFormat3 : OpenTypeSequenceContextTable {

			public int GlyphCount => CoverageTables.Length;
			public readonly OpenTypeCoverageTable[] CoverageTables;
			public readonly SequenceLookupRecord[] SeqLookupRecords;

			internal OpenTypeSequenceContextFormat3(OpenTypeCoverageTable[] coverageTables, SequenceLookupRecord[] seqLookupRecords) : base() {
				CoverageTables = coverageTables;
				SeqLookupRecords = seqLookupRecords;
			}

			public override SequenceLookupRecord[]?[] FindRecords(GlyphRun run) {
				SequenceLookupRecord[]?[] records = new SequenceLookupRecord[run.Count][];
				for (int i = 0; i < run.Count; i++) {
					if(IsMatch(run, i)) {
						records[i] = SeqLookupRecords;
						i += CoverageTables.Length - 1;
					}
				}
				return records;
			}

			public override SequenceLookupRecord[]? FindRecords(GlyphRun run, int index) {
				if (IsMatch(run, index)) {
					return SeqLookupRecords;
				}
				else {
					return null;
				}
			}

			private bool IsMatch(GlyphRun run, int index) {
				for (int i = 0; i < CoverageTables.Length; i++) {
					if (!CoverageTables[i].IsCovered(run[index + i])) {
						return false;
					}
				}
				return true;
			}

		}

	}

	public abstract class OpenTypeChainedSequenceContextTable {

		private OpenTypeChainedSequenceContextTable() { }

		public abstract SequenceLookupRecord[]?[] FindRecords(GlyphRun run);
		public abstract SequenceLookupRecord[]? FindRecords(GlyphRun run, int index);

		internal static OpenTypeChainedSequenceContextTable Read(FontFileReader reader, long offset) {

			reader.Position = offset;

			ushort format = reader.ReadUInt16();

			if (format == 1) {
				ushort coverageOffset = reader.ReadUInt16(); // from beginning of ChainSequenceContextFormat1 table
				OpenTypeCoverageTable coverage = reader.ReadFrom(offset + coverageOffset, OpenTypeCoverageTable.Read);
				ushort chainedSeqRuleSetCount = reader.ReadUInt16();
				ushort?[] chainedSeqRuleSetOffsets = reader.ReadOffset16(chainedSeqRuleSetCount); // from beginning of ChainSequenceContextFormat1 table

				OpenTypeChainedSequenceContextFormat1.ChainedSequenceRuleSetTable?[] chainedSeqRuleSets = new OpenTypeChainedSequenceContextFormat1.ChainedSequenceRuleSetTable?[chainedSeqRuleSetCount];
				for (int i = 0; i < chainedSeqRuleSetCount; i++) {
					chainedSeqRuleSets[i] = reader.ReadFrom(offset + chainedSeqRuleSetOffsets[i], OpenTypeChainedSequenceContextFormat1.ChainedSequenceRuleSetTable.Read);
				}

				return new OpenTypeChainedSequenceContextFormat1(coverage, chainedSeqRuleSets);
			}
			else if (format == 2) {
				ushort coverageOffset = reader.ReadUInt16(); // from beginning of ChainedSequenceContextFormat2 table
				OpenTypeCoverageTable coverage = reader.ReadFrom(offset + coverageOffset, OpenTypeCoverageTable.Read);

				ushort backtrackClassDefOffset = reader.ReadUInt16(); // from beginning of ChainedSequenceContextFormat2 table
				OpenTypeClassDefinitionTable backtrackClassDef = reader.ReadFrom(offset + backtrackClassDefOffset, OpenTypeClassDefinitionTable.Read);
				ushort inputClassDefOffset = reader.ReadUInt16(); // from beginning of ChainedSequenceContextFormat2 table
				OpenTypeClassDefinitionTable inputClassDef = reader.ReadFrom(offset + inputClassDefOffset, OpenTypeClassDefinitionTable.Read);
				ushort lookaheadClassDefOffset = reader.ReadUInt16(); // from beginning of ChainedSequenceContextFormat2 table
				OpenTypeClassDefinitionTable lookaheadClassDef = reader.ReadFrom(offset + lookaheadClassDefOffset, OpenTypeClassDefinitionTable.Read);

				ushort chainedClassSeqRuleSetCount = reader.ReadUInt16();
				ushort?[] chainedClassSeqRuleSetOffsets = reader.ReadOffset16(chainedClassSeqRuleSetCount); // from beginning of ChainedSequenceContextFormat2 table

				OpenTypeChainedSequenceContextFormat2.ChainedClassSequenceRuleSetTable?[] chainedClassSeqRuleSets = new OpenTypeChainedSequenceContextFormat2.ChainedClassSequenceRuleSetTable?[chainedClassSeqRuleSetCount];
				for (int i = 0; i < chainedClassSeqRuleSetCount; i++) {
					chainedClassSeqRuleSets[i] = reader.ReadFrom(offset + chainedClassSeqRuleSetOffsets[i], OpenTypeChainedSequenceContextFormat2.ChainedClassSequenceRuleSetTable.Read);
				}

				return new OpenTypeChainedSequenceContextFormat2(coverage, backtrackClassDef, inputClassDef, lookaheadClassDef, chainedClassSeqRuleSets);
			}
			else if (format == 3) {
				ushort backtrackGlyphCount = reader.ReadUInt16();
				ushort[] backtrackCoverageOffsets = reader.ReadUInt16(backtrackGlyphCount);
				ushort inputGlyphCount = reader.ReadUInt16();
				ushort[] inputCoverageOffsets = reader.ReadUInt16(inputGlyphCount);
				ushort lookaheadGlyphCount = reader.ReadUInt16();
				ushort[] lookaheadCoverageOffsets = reader.ReadUInt16(lookaheadGlyphCount);
				ushort seqLookupCount = reader.ReadUInt16();
				SequenceLookupRecord[] seqLookupRecords = reader.ReadSequenceLookupRecord(seqLookupCount);

				OpenTypeCoverageTable[] backtrackCoverageTables = new OpenTypeCoverageTable[backtrackGlyphCount];
				for (int i = 0; i < backtrackGlyphCount; i++) {
					backtrackCoverageTables[i] = reader.ReadFrom(offset + backtrackCoverageOffsets[i], OpenTypeCoverageTable.Read);
				}

				OpenTypeCoverageTable[] inputCoverageTables = new OpenTypeCoverageTable[inputGlyphCount];
				for (int i = 0; i < inputGlyphCount; i++) {
					inputCoverageTables[i] = reader.ReadFrom(offset + inputCoverageOffsets[i], OpenTypeCoverageTable.Read);
				}

				OpenTypeCoverageTable[] lookaheadCoverageTables = new OpenTypeCoverageTable[lookaheadGlyphCount];
				for (int i = 0; i < lookaheadGlyphCount; i++) {
					lookaheadCoverageTables[i] = reader.ReadFrom(offset + lookaheadCoverageOffsets[i], OpenTypeCoverageTable.Read);
				}

				return new OpenTypeChainedSequenceContextFormat3(backtrackCoverageTables, inputCoverageTables, lookaheadCoverageTables, seqLookupRecords);
			}
			else {
				throw new FormatException($"Unknown chained sequence context table format: {format}");
			}
		}

		private class OpenTypeChainedSequenceContextFormat1 : OpenTypeChainedSequenceContextTable {

			public readonly OpenTypeCoverageTable Coverage;
			public readonly ChainedSequenceRuleSetTable?[] ChainedSeqRuleSets;

			internal OpenTypeChainedSequenceContextFormat1(OpenTypeCoverageTable coverage, ChainedSequenceRuleSetTable?[] chainedSeqRuleSets) {
				Coverage = coverage;
				ChainedSeqRuleSets = chainedSeqRuleSets;
			}

			public override SequenceLookupRecord[]?[] FindRecords(GlyphRun run) {
				SequenceLookupRecord[]?[] records = new SequenceLookupRecord[run.Count][];
				for (int i = 0; i < run.Count; i++) {
					if (Coverage.CoverageIndex(run[i]) is ushort coverageIdx
						&& coverageIdx < ChainedSeqRuleSets.Length
						&& ChainedSeqRuleSets[coverageIdx] is ChainedSequenceRuleSetTable chainedSeqRuleSet
						&& chainedSeqRuleSet.GetRule(run, i) is ChainedSequenceRuleTable chainedSeqRule) {

						records[i] = chainedSeqRule.SeqLookupRecords;
						i += chainedSeqRule.InputGlyphCount - 1;
					}
					else {
						records[i] = null;
					}
				}
				return records;
			}

			public override SequenceLookupRecord[]? FindRecords(GlyphRun run, int index) {
				if (Coverage.CoverageIndex(run[index]) is ushort coverageIdx
					&& coverageIdx < ChainedSeqRuleSets.Length
					&& ChainedSeqRuleSets[coverageIdx] is ChainedSequenceRuleSetTable chainedSeqRuleSet
					&& chainedSeqRuleSet.GetRule(run, index) is ChainedSequenceRuleTable chainedSeqRule) {

					return chainedSeqRule.SeqLookupRecords;
				}
				else {
					return null;
				}
			}

			public class ChainedSequenceRuleSetTable {

				public readonly ChainedSequenceRuleTable[] ChainedSeqRules;

				public ChainedSequenceRuleSetTable(ChainedSequenceRuleTable[] chainedSeqRules) {
					ChainedSeqRules = chainedSeqRules;
				}

				internal static ChainedSequenceRuleSetTable Read(FontFileReader reader, long offset) {
					reader.Position = offset;

					ushort chainedSeqRuleCount = reader.ReadUInt16();
					ushort[] chainedSeqRuleOffsets = reader.ReadUInt16(chainedSeqRuleCount); // from beginning of the ChainedSequenceRuleSet table

					ChainedSequenceRuleTable[] chainedSeqRules = new ChainedSequenceRuleTable[chainedSeqRuleCount];
					for (int i = 0; i < chainedSeqRuleCount; i++) {
						chainedSeqRules[i] = reader.ReadFrom(offset + chainedSeqRuleOffsets[i], ChainedSequenceRuleTable.Read);
					}

					return new ChainedSequenceRuleSetTable(chainedSeqRules);
				}

				public ChainedSequenceRuleTable? GetRule(GlyphRun run, int index) {
					for (int s = 0; s < ChainedSeqRules.Length; s++) {
						if (ChainedSeqRules[s].IsMatch(run, index)) {
							return ChainedSeqRules[s];
						}
					}
					return null;
				}

			}

			public class ChainedSequenceRuleTable {

				public int BacktrackGlyphCount => BacktrackSequence.Length;
				public readonly ushort[] BacktrackSequence;
				public int InputGlyphCount => InputSequence.Length + 1;
				public readonly ushort[] InputSequence;
				public int LookaheadGlyphCount => LookaheadSequence.Length;
				public readonly ushort[] LookaheadSequence;
				public readonly SequenceLookupRecord[] SeqLookupRecords;

				public ChainedSequenceRuleTable(ushort[] backtrackSequence, ushort[] inputSequence, ushort[] lookaheadSequence, SequenceLookupRecord[] seqLookupRecords) {
					BacktrackSequence = backtrackSequence;
					InputSequence = inputSequence;
					LookaheadSequence = lookaheadSequence;
					SeqLookupRecords = seqLookupRecords;
				}

				internal static ChainedSequenceRuleTable Read(FontFileReader reader, long offset) {

					reader.Position = offset;

					ushort backtrackGlyphCount = reader.ReadUInt16();
					ushort[] backtrackSequence = reader.ReadUInt16(backtrackGlyphCount);
					ushort inputGlyphCount = reader.ReadUInt16();
					ushort[] inputSequence = reader.ReadUInt16(inputGlyphCount - 1);
					ushort lookaheadGlyphCount = reader.ReadUInt16();
					ushort[] lookaheadSequence = reader.ReadUInt16(lookaheadGlyphCount);
					ushort seqLookupCount = reader.ReadUInt16();
					SequenceLookupRecord[] seqLookupRecords = reader.ReadSequenceLookupRecord(seqLookupCount);

					return new ChainedSequenceRuleTable(backtrackSequence, inputSequence, lookaheadSequence, seqLookupRecords);
				}

				public bool IsMatch(GlyphRun run, int index) {
					if (index < BacktrackGlyphCount) { return false; }
					if (index + LookaheadGlyphCount >= run.Count) { return false; }

					for (int b = 0; b < BacktrackSequence.Length; b++) {
						if (run[index - BacktrackSequence.Length + b] != BacktrackSequence[b]) {
							return false;
						}
					}

					for (int j = 0; j < InputSequence.Length; j++) {
						if (run[index + 1 + j] != InputSequence[j]) { // First glyph already checked by this point
							return false;
						}
					}

					for (int a = 0; a < LookaheadSequence.Length; a++) {
						if (run[index + InputGlyphCount + a] != LookaheadSequence[a]) {
							return false;
						}
					}

					return true;
				}

			}

		}

		private class OpenTypeChainedSequenceContextFormat2 : OpenTypeChainedSequenceContextTable {

			public readonly OpenTypeCoverageTable Coverage;
			public readonly OpenTypeClassDefinitionTable BacktrackClassDef;
			public readonly OpenTypeClassDefinitionTable InputClassDef;
			public readonly OpenTypeClassDefinitionTable LookaheadClassDef;
			public readonly ChainedClassSequenceRuleSetTable?[] ChainedClassSeqRuleSets;

			internal OpenTypeChainedSequenceContextFormat2(OpenTypeCoverageTable coverage, OpenTypeClassDefinitionTable backtrackClassDef, OpenTypeClassDefinitionTable inputClassDef, OpenTypeClassDefinitionTable lookaheadClassDef, ChainedClassSequenceRuleSetTable?[] chainedClassSeqRuleSets) : base() {
				Coverage = coverage;
				BacktrackClassDef = backtrackClassDef;
				InputClassDef = inputClassDef;
				LookaheadClassDef = lookaheadClassDef;
				ChainedClassSeqRuleSets = chainedClassSeqRuleSets;
			}

			public override SequenceLookupRecord[]?[] FindRecords(GlyphRun run) {
				SequenceLookupRecord[]?[] records = new SequenceLookupRecord[run.Count][];
				for (int i = 0; i < run.Count; i++) {
					if (Coverage.IsCovered(run[i])
						&& InputClassDef.GetClass(run[i]) is ushort classVal
						&& classVal < ChainedClassSeqRuleSets.Length
						&& ChainedClassSeqRuleSets[classVal] is ChainedClassSequenceRuleSetTable chainedClassSeqRuleSet
						&& chainedClassSeqRuleSet.GetRule(run, i, BacktrackClassDef, InputClassDef, LookaheadClassDef) is ChainedClassSequenceRuleTable chainedClassSeqRule) {

						records[i] = chainedClassSeqRule.SeqLookupRecords;
						i += chainedClassSeqRule.InputGlyphCount - 1;
					}
					else {
						records[i] = null;
					}
				}
				return records;
			}

			public override SequenceLookupRecord[]? FindRecords(GlyphRun run, int index) {
				if (Coverage.IsCovered(run[index])
					&& InputClassDef.GetClass(run[index]) is ushort classVal
					&& classVal < ChainedClassSeqRuleSets.Length
					&& ChainedClassSeqRuleSets[classVal] is ChainedClassSequenceRuleSetTable chainedClassSeqRuleSet
					&& chainedClassSeqRuleSet.GetRule(run, index, BacktrackClassDef, InputClassDef, LookaheadClassDef) is ChainedClassSequenceRuleTable chainedClassSeqRule) {

					return chainedClassSeqRule.SeqLookupRecords;
				}
				else {
					return null;
				}
			}

			public class ChainedClassSequenceRuleSetTable {

				public readonly ChainedClassSequenceRuleTable[] ChainedClassSeqRules;

				public ChainedClassSequenceRuleSetTable(ChainedClassSequenceRuleTable[] chainedClassSeqRules) {
					ChainedClassSeqRules = chainedClassSeqRules;
				}

				internal static ChainedClassSequenceRuleSetTable Read(FontFileReader reader, long offset) {
					reader.Position = offset;

					ushort classSeqRuleCount = reader.ReadUInt16();
					ushort[] classSeqRuleOffsets = reader.ReadUInt16(classSeqRuleCount); // from beginning of the ChainedClassSequenceRuleSet table

					ChainedClassSequenceRuleTable[] classSeqRules = new ChainedClassSequenceRuleTable[classSeqRuleCount];
					for (int i = 0; i < classSeqRuleCount; i++) {
						classSeqRules[i] = reader.ReadFrom(offset + classSeqRuleOffsets[i], ChainedClassSequenceRuleTable.Read);
					}

					return new ChainedClassSequenceRuleSetTable(classSeqRules);
				}

				public ChainedClassSequenceRuleTable? GetRule(GlyphRun run, int index, OpenTypeClassDefinitionTable backtrackClassDef, OpenTypeClassDefinitionTable inputClassDef, OpenTypeClassDefinitionTable lookaheadClassDef) {
					for (int s = 0; s < ChainedClassSeqRules.Length; s++) {
						if (ChainedClassSeqRules[s].IsMatch(run, index, backtrackClassDef, inputClassDef, lookaheadClassDef)) {
							return ChainedClassSeqRules[s];
						}
					}
					return null;
				}

			}

			public class ChainedClassSequenceRuleTable {

				public int BacktrackGlyphCount => BacktrackSequence.Length;
				public readonly ushort[] BacktrackSequence;
				public int InputGlyphCount => InputSequence.Length + 1;
				public readonly ushort[] InputSequence;
				public int LookaheadGlyphCount => LookaheadSequence.Length;
				public readonly ushort[] LookaheadSequence;
				public readonly SequenceLookupRecord[] SeqLookupRecords;

				public ChainedClassSequenceRuleTable(ushort[] backtrackSequence, ushort[] inputSequence, ushort[] lookaheadSequence, SequenceLookupRecord[] seqLookupRecords) {
					BacktrackSequence = backtrackSequence;
					InputSequence = inputSequence;
					LookaheadSequence = lookaheadSequence;
					SeqLookupRecords = seqLookupRecords;
				}

				internal static ChainedClassSequenceRuleTable Read(FontFileReader reader, long offset) {

					reader.Position = offset;

					ushort backtrackGlyphCount = reader.ReadUInt16();
					ushort[] backtrackSequence = reader.ReadUInt16(backtrackGlyphCount);
					ushort inputGlyphCount = reader.ReadUInt16();
					ushort[] inputSequence = reader.ReadUInt16(inputGlyphCount - 1);
					ushort lookaheadGlyphCount = reader.ReadUInt16();
					ushort[] lookaheadSequence = reader.ReadUInt16(lookaheadGlyphCount);
					ushort seqLookupCount = reader.ReadUInt16();
					SequenceLookupRecord[] seqLookupRecords = reader.ReadSequenceLookupRecord(seqLookupCount);

					return new ChainedClassSequenceRuleTable(backtrackSequence, inputSequence, lookaheadSequence, seqLookupRecords);
				}

				public bool IsMatch(GlyphRun run, int index, OpenTypeClassDefinitionTable backtrackClassDef, OpenTypeClassDefinitionTable inputClassDef, OpenTypeClassDefinitionTable lookaheadClassDef) {
					if (index < BacktrackGlyphCount) { return false; }
					if (index + LookaheadGlyphCount >= run.Count) { return false; }

					for (int b = 0; b < BacktrackSequence.Length; b++) {
						if (backtrackClassDef.GetClass(run[index - BacktrackSequence.Length + b]) != BacktrackSequence[b]) {
							return false;
						}
					}

					for (int j = 0; j < InputSequence.Length; j++) {
						if (inputClassDef.GetClass(run[index + 1 + j]) != InputSequence[j]) { // First glyph already checked by this point
							return false;
						}
					}

					for (int a = 0; a < LookaheadSequence.Length; a++) {
						if (lookaheadClassDef.GetClass(run[index + InputGlyphCount + a]) != LookaheadSequence[a]) {
							return false;
						}
					}

					return true;
				}

			}

		}

		private class OpenTypeChainedSequenceContextFormat3 : OpenTypeChainedSequenceContextTable {

			public int BacktrackGlyphCount => BacktrackCoverageTables.Length;
			public readonly OpenTypeCoverageTable[] BacktrackCoverageTables;
			public int InputGlyphCount => InputCoverageTables.Length;
			public readonly OpenTypeCoverageTable[] InputCoverageTables;
			public int LookaheadGlyphCount => LookaheadCoverageTables.Length;
			public readonly OpenTypeCoverageTable[] LookaheadCoverageTables;
			public readonly SequenceLookupRecord[] SeqLookupRecords;

			internal OpenTypeChainedSequenceContextFormat3(OpenTypeCoverageTable[] backtrackCoverageTables, OpenTypeCoverageTable[] inputCoverageTables, OpenTypeCoverageTable[] lookaheadCoverageTables, SequenceLookupRecord[] seqLookupRecords) : base() {
				BacktrackCoverageTables = backtrackCoverageTables;
				InputCoverageTables = inputCoverageTables;
				LookaheadCoverageTables = lookaheadCoverageTables;
				SeqLookupRecords = seqLookupRecords;
			}

			public override SequenceLookupRecord[]?[] FindRecords(GlyphRun run) {
				SequenceLookupRecord[]?[] records = new SequenceLookupRecord[run.Count][];
				for (int i = 0; i < run.Count; i++) {
					if (IsMatch(run, i)) {
						records[i] = SeqLookupRecords;
						i += InputCoverageTables.Length - 1;
					}
				}
				return records;
			}

			public override SequenceLookupRecord[]? FindRecords(GlyphRun run, int index) {
				if (IsMatch(run, index)) {
					return SeqLookupRecords;
				}
				else {
					return null;
				}
			}

			public bool IsMatch(GlyphRun run, int index) {
				if (index < BacktrackGlyphCount) { return false; }
				if (index + LookaheadGlyphCount >= run.Count) { return false; }

				for (int b = 0; b < BacktrackCoverageTables.Length; b++) {
					if (!BacktrackCoverageTables[b].IsCovered(run[index - BacktrackCoverageTables.Length + b])) {
						return false;
					}
				}

				for (int j = 0; j < InputCoverageTables.Length; j++) {
					if (!InputCoverageTables[j].IsCovered(run[index + j])) {
						return false;
					}
				}

				for (int a = 0; a < LookaheadCoverageTables.Length; a++) {
					if (!LookaheadCoverageTables[a].IsCovered(run[index + InputGlyphCount + a])) {
						return false;
					}
				}

				return true;
			}

		}

	}

	public static class FontFileReaderUtils {

		public static T ReadFrom<T>(this FontFileReader reader, long offset, Func<FontFileReader, long, T> parser) {
			long position = reader.Position;
			T result = parser(reader, offset);
			reader.Position = position;
			return result;
		}

		public static T? ReadFrom<T>(this FontFileReader reader, long? offset, Func<FontFileReader, long, T> parser) {
			if (offset.HasValue) {
				return reader.ReadFrom(offset.Value, parser);
			}
			else {
				return default;
			}
		}

		public static T ReadFromOffset16<T>(this FontFileReader reader, long referenceOffset, Func<FontFileReader, long, T> parser) {
			ushort valueOffset = reader.ReadUInt16();
			return reader.ReadFrom(referenceOffset + valueOffset, parser);
		}

		public static T ReadFromOffset32<T>(this FontFileReader reader, long referenceOffset, Func<FontFileReader, long, T> parser) {
			uint valueOffset = reader.ReadUInt32();
			return reader.ReadFrom(referenceOffset + valueOffset, parser);
		}

		public static T[] ReadFrom<T>(this FontFileReader reader, long referenceOffset, ushort[] offsets, Func<FontFileReader, long, T> parser) {
			T[] array = new T[offsets.Length];
			for (int i = 0; i < offsets.Length; i++) {
				array[i] = reader.ReadFrom(referenceOffset + offsets[i], parser);
			}
			return array;
		}

		public static T[] ReadFrom<T>(this FontFileReader reader, long referenceOffset, uint[] offsets, Func<FontFileReader, long, T> parser) {
			T[] array = new T[offsets.Length];
			for (int i = 0; i < offsets.Length; i++) {
				array[i] = reader.ReadFrom(referenceOffset + offsets[i], parser);
			}
			return array;
		}

		public static T?[] ReadFrom<T>(this FontFileReader reader, long referenceOffset, ushort?[] offsets, Func<FontFileReader, long, T> parser) {
			T?[] array = new T?[offsets.Length];
			for (int i = 0; i < offsets.Length; i++) {
				if (offsets[i].HasValue) {
					array[i] = reader.ReadFrom(referenceOffset + offsets[i]!.Value, parser);
				}
				else {
					array[i] = default;
				}
			}
			return array;
		}

		public static T?[] ReadFrom<T>(this FontFileReader reader, long referenceOffset, uint?[] offsets, Func<FontFileReader, long, T> parser) {
			T?[] array = new T?[offsets.Length];
			for (int i = 0; i < offsets.Length; i++) {
				if (offsets[i].HasValue) {
					array[i] = reader.ReadFrom(referenceOffset + offsets[i]!.Value, parser);
				}
				else {
					array[i] = default;
				}
			}
			return array;
		}

	}

}
