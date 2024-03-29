using GeboPdf.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeboPdf.Fonts.TrueType {

	public enum GSUBLookupType : ushort {
		/// <summary>
		/// Replace one glyph with one glyph
		/// </summary>
		Single = 1,
		/// <summary>
		/// Replace one glyph with more than one glyph
		/// </summary>
		Multiple = 2,
		/// <summary>
		/// Replace one glyph with one of many glyphs
		/// </summary>
		Alternate = 3,
		/// <summary>
		/// Replace multiple glyphs with one glyph
		/// </summary>
		Ligature = 4,
		/// <summary>
		/// Replace one or more glyphs in context
		/// </summary>
		Context = 5,
		/// <summary>
		/// Replace one or more glyphs in chained context
		/// </summary>
		ChainingContext = 6,
		/// <summary>
		/// Extension mechanism for other substitutions (i.e. this excludes the Extension type substitution itself)
		/// </summary>
		ExtensionSubstitution = 7,
		/// <summary>
		/// Applied in reverse order, replace single glyph in chaining context
		/// </summary>
		ReverseChainingContextSingle = 8
	}

	public class OpenTypeGlyphSubstitutionTable { // GSUB

		public readonly OpenTypeScriptListTable ScriptListTable;
		public readonly OpenTypeFeatureListTable FeatureListTable;
		public readonly GlyphSubstitutionLookupTable[] LookupList;

		internal OpenTypeGlyphSubstitutionTable(OpenTypeScriptListTable scriptListTable, OpenTypeFeatureListTable featureListTable, GlyphSubstitutionLookupTable[] lookupList) {
			ScriptListTable = scriptListTable;
			FeatureListTable = featureListTable;
			LookupList = lookupList;
		}

		internal static OpenTypeLayoutTagSet ReadTags(FontFileReader reader, long offset) {
			reader.Position = offset;

			ushort majorVersion = reader.ReadUInt16();
			if (majorVersion != 1) { throw new FormatException($"Unknown glyph substitution table major version: {majorVersion}"); }

			ushort minorVersion = reader.ReadUInt16();

			ushort scriptListOffset = reader.ReadUInt16();
			ushort featureListOffset = reader.ReadUInt16();
			/*ushort lookupListOffset*/ _ = reader.ReadUInt16();

			uint? featureVariationsOffset;
			if (minorVersion == 1) { featureVariationsOffset = reader.ReadOffset32(); }
			else if (minorVersion == 0) { featureVariationsOffset = null; }
			else { throw new FormatException($"Unknown glyph substitution table minor version: {minorVersion}"); }

			IReadOnlyDictionary<string, IReadOnlySet<string>> scriptTags = OpenTypeScriptListTable.ReadTags(reader, offset + scriptListOffset);
			IReadOnlySet<string> featureTags = OpenTypeFeatureListTable.ReadTags(reader, offset + featureListOffset);

			return new OpenTypeLayoutTagSet(scriptTags, featureTags);
		}

		internal static OpenTypeGlyphSubstitutionTable Read(FontFileReader reader, long offset) {

			reader.Position = offset;

			ushort majorVersion = reader.ReadUInt16();
			if (majorVersion != 1) { throw new FormatException($"Unknown glyph substitution table major version: {majorVersion}"); }

			ushort minorVersion = reader.ReadUInt16();

			ushort scriptListOffset = reader.ReadUInt16();
			ushort featureListOffset = reader.ReadUInt16();
			ushort lookupListOffset = reader.ReadUInt16();

			uint? featureVariationsOffset;
			if (minorVersion == 1) { featureVariationsOffset = reader.ReadOffset32(); }
			else if (minorVersion == 0) { featureVariationsOffset = null; }
			else { throw new FormatException($"Unknown glyph substitution table minor version: {minorVersion}"); }

			OpenTypeScriptListTable scriptListTable = OpenTypeScriptListTable.Read(reader, offset + scriptListOffset);
			OpenTypeFeatureListTable featureListTable = OpenTypeFeatureListTable.Read(reader, offset + featureListOffset);
			OpenTypeLookupListTable lookupListTable = OpenTypeLookupListTable.Read(reader, offset + lookupListOffset);
			// Ignoring feature variations table for now

			GlyphSubstitutionLookupTable[] lookupList = new GlyphSubstitutionLookupTable[lookupListTable.Lookups.Length];
			for (int i = 0; i < lookupList.Length; i++) {
				GSUBLookupType lookupType = (GSUBLookupType)lookupListTable.Lookups[i].LookupType;

				ISubstitutionSubtable[] subtables = new ISubstitutionSubtable[lookupListTable.Lookups[i].SubtableOffsets.Length];

				for(int j=0; j<subtables.Length; j++) {
					subtables[j] = ReadSubtable(reader, lookupListTable.Lookups[i].SubtableOffsets[j], lookupType, lookupList);
				}

				lookupList[i] = new GlyphSubstitutionLookupTable(
					lookupListTable.Lookups[i].LookupFlag,
					lookupListTable.Lookups[i].MarkAttachmentTypeMask,
					subtables,
					lookupListTable.Lookups[i].MarkFilteringSet
					);
			}

			return new OpenTypeGlyphSubstitutionTable(scriptListTable, featureListTable, lookupList);
		}

		private static ISubstitutionSubtable ReadSubtable(FontFileReader reader, long subtableOffset, GSUBLookupType lookupType, GlyphSubstitutionLookupTable[] lookupList) {
			return lookupType switch {
				GSUBLookupType.Single => OpenTypeSingleSubstitutionSubtable.Read(reader, subtableOffset),
				GSUBLookupType.Multiple => OpenTypeMultipleSubstitutionSubtable.Read(reader, subtableOffset),
				GSUBLookupType.Alternate => OpenTypeAlternateSubstitutionSubtable.Read(reader, subtableOffset),
				GSUBLookupType.Ligature => OpenTypeLigatureSubstitutionSubtable.Read(reader, subtableOffset),
				GSUBLookupType.Context => OpenTypeContextualSubstitutionSubtable.Read(reader, subtableOffset, lookupList),
				GSUBLookupType.ChainingContext => OpenTypeChainedContextsSubstitutionSubtable.Read(reader, subtableOffset, lookupList),
				GSUBLookupType.ExtensionSubstitution => ReadExtensionSubtable(reader, subtableOffset, lookupList),
				GSUBLookupType.ReverseChainingContextSingle => OpenTypeReverseChainingContextualSingleSubstitutionSubtable.Read(reader, subtableOffset),
				_ => throw new FormatException($"Unknown glyph substitution subtable lookup type: {(ushort)lookupType}")
			};
		}

		private static ISubstitutionSubtable ReadExtensionSubtable(FontFileReader reader, long subtableOffset, GlyphSubstitutionLookupTable[] lookupList) {
			reader.Position = subtableOffset;

			ushort substFormat = reader.ReadUInt16();

			if (substFormat == 1) {
				ushort extensionLookupType = reader.ReadUInt16();
				uint extensionOffset = reader.ReadUInt32(); // relative to the start of the ExtensionSubstFormat1 subtable

				GSUBLookupType lookupType = (GSUBLookupType)extensionLookupType;

				return ReadSubtable(reader, subtableOffset + extensionOffset, lookupType, lookupList);
			}
			else {
				throw new FormatException($"Unknown extension substitution subtable format: {substFormat}");
			}
		}

		public GlyphSubstitutionLookupSet? GetLookups(OpenTypeLayoutTags layoutTags) {
			if(OpenTypeLayoutHelpers.GetLookups(layoutTags, ScriptListTable, FeatureListTable) is ushort[] lookupIdxs) {
				return new GlyphSubstitutionLookupSet(lookupIdxs.Select(i => LookupList[i]).ToArray());
			}
			else {
				return null;
			}
		}

		public GlyphSubstitutionLookupSet? GetLookups(string featureTag) {
			if (OpenTypeLayoutHelpers.GetLookups(featureTag, FeatureListTable) is ushort[] lookupIdxs) {
				return new GlyphSubstitutionLookupSet(lookupIdxs.Select(i => LookupList[i]).ToArray());
			}
			else {
				return null;
			}
		}

	}

	public interface ISubstitutionSubtable {

		bool Forward { get; }
		int PerformSubstitution(SubstitutionGlyphRun run, int index);

		void PerformSubstitution(SubstitutionGlyphRun run) {
			if (Forward) {
				for (int i = 0; i < run.Count; i++) {
					i += PerformSubstitution(run, i);
				}
			}
			else { // Backwards
				for (int i = run.Count - 1; i >= 0; i--) {
					i += PerformSubstitution(run, i);
				}
			}
		}

	}

	public class GlyphSubstitutionLookupTable {

		public readonly LookupFlag LookupFlag;
		public readonly ushort MarkAttachmentTypeMask;
		public readonly ISubstitutionSubtable[] Subtables;
		public readonly ushort MarkFilteringSet;

		public GlyphSubstitutionLookupTable(LookupFlag lookupFlag, ushort markAttachmentTypeMask, ISubstitutionSubtable[] subtables, ushort markFilteringSet) {
			LookupFlag = lookupFlag;
			MarkAttachmentTypeMask = markAttachmentTypeMask;
			Subtables = subtables;
			MarkFilteringSet = markFilteringSet;
		}

		public void PerformSubstitution(SubstitutionGlyphRun run) {
			for(int i=0; i<Subtables.Length; i++) {
				Subtables[i].PerformSubstitution(run);
			}
		}

		public void PerformSubstitution(SubstitutionGlyphRun run, int index) {
			for (int i = 0; i < Subtables.Length; i++) {
				Subtables[i].PerformSubstitution(run, index);
			}
		}

	}

	public class GlyphSubstitutionLookupSet {

		public readonly GlyphSubstitutionLookupTable[] Lookups;
		public int Count => Lookups.Length;

		public GlyphSubstitutionLookupSet(GlyphSubstitutionLookupTable[] lookups) {
			this.Lookups = lookups;
		}

		public void PerformSubstitutions(SubstitutionGlyphRun run) {
			for (int i = 0; i < Lookups.Length; i++) {
				Lookups[i].PerformSubstitution(run);
			}
		}

	}

	public abstract class OpenTypeSingleSubstitutionSubtable : ISubstitutionSubtable {

		private OpenTypeSingleSubstitutionSubtable() { }

		public bool Forward => true;
		public abstract int PerformSubstitution(SubstitutionGlyphRun run, int index);
		public abstract IEnumerable<(ushort initial, ushort final)> GetExamples();

		internal static OpenTypeSingleSubstitutionSubtable Read(FontFileReader reader, long offset) {

			reader.Position = offset;

			ushort substFormat = reader.ReadUInt16();

			ushort coverageOffset = reader.ReadUInt16(); // from beginning of substitution subtable
			OpenTypeCoverageTable coverage = reader.ReadFrom(offset + coverageOffset, OpenTypeCoverageTable.Read);

			if (substFormat == 1) {
				short deltaGlyphID = reader.ReadInt16();

				return new OpenTypeSingleSubstitutionTableFormat1(coverage, deltaGlyphID);
			}
			else if (substFormat == 2) {
				ushort glyphCount = reader.ReadUInt16();
				ushort[] substituteGlyphIDs = reader.ReadUInt16(glyphCount);

				return new OpenTypeSingleSubstitutionTableFormat2(coverage, substituteGlyphIDs);
			}
			else {
				throw new FormatException($"Unknown single substitution subtable format: {substFormat}");
			}
		}

		private class OpenTypeSingleSubstitutionTableFormat1 : OpenTypeSingleSubstitutionSubtable {

			public readonly OpenTypeCoverageTable Coverage;
			public readonly short DeltaGlyphID;

			internal OpenTypeSingleSubstitutionTableFormat1(OpenTypeCoverageTable coverage, short deltaGlyphID) : base() {
				Coverage = coverage;
				DeltaGlyphID = deltaGlyphID;
			}

			public override int PerformSubstitution(SubstitutionGlyphRun run, int index) {
				if (Coverage.IsCovered(run[index])) {
					run.Replace(index, (ushort)((run[index] + DeltaGlyphID) % 65536));
				}
				return 0;
			}

			public override IEnumerable<(ushort, ushort)> GetExamples() {
				for (ushort ci = 0; ci < Coverage.Length; ci++) {
					ushort glyph = Coverage.GetGlyph(ci);
					yield return (glyph, (ushort)(glyph + DeltaGlyphID));
				}
			}

		}

		private class OpenTypeSingleSubstitutionTableFormat2 : OpenTypeSingleSubstitutionSubtable {

			public readonly OpenTypeCoverageTable Coverage;
			public readonly ushort[] SubstituteGlyphIDs;

			internal OpenTypeSingleSubstitutionTableFormat2(OpenTypeCoverageTable coverage, ushort[] substituteGlyphIDs) : base() {
				Coverage = coverage;
				SubstituteGlyphIDs = substituteGlyphIDs;
			}

			public override int PerformSubstitution(SubstitutionGlyphRun run, int index) {
				if (Coverage.CoverageIndex(run[index]) is ushort coverageIdx) {
					run.Replace(index, SubstituteGlyphIDs[coverageIdx]);
				}
				return 0;
			}

			public override IEnumerable<(ushort, ushort)> GetExamples() {
				for (ushort ci = 0; ci < Coverage.Length; ci++) {
					ushort glyph = Coverage.GetGlyph(ci);
					yield return (glyph, SubstituteGlyphIDs[ci]);
				}
			}

		}

	}

	public abstract class OpenTypeMultipleSubstitutionSubtable : ISubstitutionSubtable {

		private OpenTypeMultipleSubstitutionSubtable() { }

		public bool Forward => true;
		public abstract int PerformSubstitution(SubstitutionGlyphRun run, int index);
		public abstract IEnumerable<(ushort, ushort[])> GetExamples();

		internal static OpenTypeMultipleSubstitutionSubtable Read(FontFileReader reader, long offset) {

			reader.Position = offset;

			ushort substFormat = reader.ReadUInt16();

			if (substFormat == 1) {
				ushort coverageOffset = reader.ReadUInt16(); // from beginning of substitution subtable
				OpenTypeCoverageTable coverage = reader.ReadFrom(offset + coverageOffset, OpenTypeCoverageTable.Read);

				ushort sequenceCount = reader.ReadUInt16();
				ushort[] sequenceOffsets = reader.ReadUInt16(sequenceCount); // from beginning of substitution subtable

				ushort[][] substituteGlyphSequences = new ushort[sequenceCount][];

				for (int i = 0; i < sequenceCount; i++) {
					reader.Position = offset + sequenceOffsets[i];
					ushort glyphCount = reader.ReadUInt16();
					substituteGlyphSequences[i] = reader.ReadUInt16(glyphCount);
				}

				return new OpenTypeMultipleSubstitutionTableFormat1(coverage, substituteGlyphSequences);
			}
			else {
				throw new FormatException($"Unknown multiple substitution subtable format: {substFormat}");
			}
		}

		private class OpenTypeMultipleSubstitutionTableFormat1 : OpenTypeMultipleSubstitutionSubtable {

			public readonly OpenTypeCoverageTable Coverage;
			public readonly ushort[][] SubstituteGlyphSequences;

			internal OpenTypeMultipleSubstitutionTableFormat1(OpenTypeCoverageTable coverage, ushort[][] substituteGlyphSequences) : base() {
				Coverage = coverage;
				SubstituteGlyphSequences = substituteGlyphSequences;
			}

			public override int PerformSubstitution(SubstitutionGlyphRun run, int index) {
				if (Coverage.CoverageIndex(run[index]) is ushort coverageIdx) {
					run.Replace(index, SubstituteGlyphSequences[coverageIdx]);
					return SubstituteGlyphSequences[coverageIdx].Length - 1;
				}
				return 0;
			}

			public override IEnumerable<(ushort, ushort[])> GetExamples() {
				for(ushort ci=0; ci<Coverage.Length; ci++) {
					ushort glyph = Coverage.GetGlyph(ci);
					yield return (glyph, SubstituteGlyphSequences[ci]);
				}
			}

		}

	}

	public abstract class OpenTypeAlternateSubstitutionSubtable : ISubstitutionSubtable {

		private OpenTypeAlternateSubstitutionSubtable() { }

		public bool Forward => true;
		public abstract int PerformSubstitution(SubstitutionGlyphRun run, int index);
		public abstract IEnumerable<(ushort, ushort[])> GetExamples();

		internal static OpenTypeAlternateSubstitutionSubtable Read(FontFileReader reader, long offset) {

			reader.Position = offset;

			ushort substFormat = reader.ReadUInt16();

			if (substFormat == 1) {
				ushort coverageOffset = reader.ReadUInt16(); // from beginning of substitution subtable
				OpenTypeCoverageTable coverage = reader.ReadFrom(offset + coverageOffset, OpenTypeCoverageTable.Read);

				ushort alternateSetCount = reader.ReadUInt16();
				ushort[] alternateSetOffsets = reader.ReadUInt16(alternateSetCount); // from beginning of substitution subtable

				ushort[][] alternateSets = new ushort[alternateSetCount][];

				for (int i = 0; i < alternateSetCount; i++) {
					reader.Position = offset + alternateSetOffsets[i];
					ushort glyphCount = reader.ReadUInt16();
					alternateSets[i] = reader.ReadUInt16(glyphCount);
				}

				return new OpenTypeAlternateSubstitutionTableFormat1(coverage, alternateSets);
			}
			else {
				throw new FormatException($"Unknown alternate substitution subtable format: {substFormat}");
			}
		}

		private class OpenTypeAlternateSubstitutionTableFormat1 : OpenTypeAlternateSubstitutionSubtable {

			public readonly OpenTypeCoverageTable Coverage;
			public readonly ushort[][] AlternateSets;

			internal OpenTypeAlternateSubstitutionTableFormat1(OpenTypeCoverageTable coverage, ushort[][] alternateSets) : base() {
				Coverage = coverage;
				AlternateSets = alternateSets;
			}

			public override int PerformSubstitution(SubstitutionGlyphRun run, int index) {
				if (Coverage.CoverageIndex(run[index]) is ushort coverageIdx) {
					run.Replace(index, AlternateSets[coverageIdx][0]); // TODO How do we specify which alternative to use?
				}
				return 0;
			}

			public override IEnumerable<(ushort, ushort[])> GetExamples() {
				for (ushort ci = 0; ci < Coverage.Length; ci++) {
					ushort glyph = Coverage.GetGlyph(ci);
					yield return (glyph, AlternateSets[ci]);
				}
			}

		}

	}

	public abstract class OpenTypeLigatureSubstitutionSubtable : ISubstitutionSubtable {

		private OpenTypeLigatureSubstitutionSubtable() { }

		public bool Forward => true;
		public abstract int PerformSubstitution(SubstitutionGlyphRun run, int index);
		public abstract IEnumerable<(ushort[], ushort)> GetExamples();

		internal static OpenTypeLigatureSubstitutionSubtable Read(FontFileReader reader, long offset) {

			reader.Position = offset;

			ushort substFormat = reader.ReadUInt16();

			if (substFormat == 1) {
				ushort coverageOffset = reader.ReadUInt16(); // from beginning of substitution subtable
				OpenTypeCoverageTable coverage = reader.ReadFrom(offset + coverageOffset, OpenTypeCoverageTable.Read);

				ushort ligatureSetCount = reader.ReadUInt16();
				ushort[] ligatureSetOffsets = reader.ReadUInt16(ligatureSetCount); // from beginning of substitution subtable

				OpenTypeLigatureSubstitutionTableFormat1.LigatureSetTable[] ligatureSets = new OpenTypeLigatureSubstitutionTableFormat1.LigatureSetTable[ligatureSetCount];
				for (int i = 0; i < ligatureSetCount; i++) {
					ligatureSets[i] = reader.ReadFrom(offset + ligatureSetOffsets[i], OpenTypeLigatureSubstitutionTableFormat1.LigatureSetTable.Read);
				}

				return new OpenTypeLigatureSubstitutionTableFormat1(coverage, ligatureSets);
			}
			else {
				throw new FormatException($"Unknown ligature substitution subtable format: {substFormat}");
			}
		}

		private class OpenTypeLigatureSubstitutionTableFormat1 : OpenTypeLigatureSubstitutionSubtable {

			public readonly OpenTypeCoverageTable Coverage;
			public readonly LigatureSetTable[] LigatureSets;

			internal OpenTypeLigatureSubstitutionTableFormat1(OpenTypeCoverageTable coverage, LigatureSetTable[] ligatureSets) : base() {
				Coverage = coverage;
				LigatureSets = ligatureSets;
			}

			public override int PerformSubstitution(SubstitutionGlyphRun run, int index) {
				if (Coverage.CoverageIndex(run[index]) is ushort coverageIdx) {
					LigatureSets[coverageIdx].PerformSubstitution(run, index);
				}
				return 0;
			}

			public override IEnumerable<(ushort[], ushort)> GetExamples() {
				for (ushort ci = 0; ci < Coverage.Length; ci++) {
					ushort glyph = Coverage.GetGlyph(ci);
					foreach ((ushort[], ushort) example in LigatureSets[ci].GetExamples(glyph)) {
						yield return example;
					}
				}
			}

			public class LigatureSetTable {

				public readonly LigatureTable[] Ligatures;

				public LigatureSetTable(LigatureTable[] ligatures) {
					Ligatures = ligatures;
				}

				internal static LigatureSetTable Read(FontFileReader reader, long offset) {
					reader.Position = offset;

					ushort ligatureCount = reader.ReadUInt16();
					ushort[] ligatureOffsets = reader.ReadUInt16(ligatureCount); // from beginning of the LigatureSet table

					LigatureTable[] ligatures = new LigatureTable[ligatureCount];
					for (int i = 0; i < ligatureCount; i++) {
						ligatures[i] = reader.ReadFrom(offset + ligatureOffsets[i], LigatureTable.Read);
					}

					return new LigatureSetTable(ligatures);
				}

				public void PerformSubstitution(SubstitutionGlyphRun run, int i) {
					for (int t = 0; t < Ligatures.Length; t++) {
						if (Ligatures[t].IsMatch(run, i)) {
							run.Replace(i, Ligatures[t].GlyphCount, Ligatures[t].LigatureGlyph);
							return;
						}
					}
				}

				public IEnumerable<(ushort[], ushort)> GetExamples(ushort firstGlyph) {
					for (int t = 0; t < Ligatures.Length; t++) {
						yield return (
							new ushort[] { firstGlyph }.Concat(Ligatures[t].ComponentGlyphIDs).ToArray(),
							Ligatures[t].LigatureGlyph
							);
					}
				}

			}

			public class LigatureTable {

				public int GlyphCount => ComponentGlyphIDs.Length + 1;
				public readonly ushort LigatureGlyph;
				public readonly ushort[] ComponentGlyphIDs;

				public LigatureTable(ushort ligatureGlyph, ushort[] componentGlyphIDs) {
					LigatureGlyph = ligatureGlyph;
					ComponentGlyphIDs = componentGlyphIDs;
				}

				internal static LigatureTable Read(FontFileReader reader, long offset) {
					reader.Position = offset;

					ushort ligatureGlyph = reader.ReadUInt16();
					ushort componentCount = reader.ReadUInt16();
					ushort[] componentGlyphIDs = reader.ReadUInt16(componentCount - 1);

					return new LigatureTable(ligatureGlyph, componentGlyphIDs);
				}

				public bool IsMatch(SubstitutionGlyphRun run, int i) {
					if (i < run.Count - ComponentGlyphIDs.Length) { // There have to be enough glyphs to check
						for (int j = 0; j < ComponentGlyphIDs.Length; j++) {
							if (run[i + 1 + j] != ComponentGlyphIDs[j]) { // First glyph already checked by this point
								return false;
							}
						}
						return true;
					}
					else {
						return false;
					}
				}

			}

		}

	}

	public sealed class OpenTypeContextualSubstitutionSubtable : ISubstitutionSubtable {

		private readonly OpenTypeSequenceContextTable table;
		private readonly GlyphSubstitutionLookupTable[] lookupList;

		private OpenTypeContextualSubstitutionSubtable(OpenTypeSequenceContextTable table, GlyphSubstitutionLookupTable[] lookupList) {
			this.table = table;
			this.lookupList = lookupList;
		}

		internal static OpenTypeContextualSubstitutionSubtable Read(FontFileReader reader, long offset, GlyphSubstitutionLookupTable[] lookupList) {
			OpenTypeSequenceContextTable table = OpenTypeSequenceContextTable.Read(reader, offset);

			return new OpenTypeContextualSubstitutionSubtable(table, lookupList);
		}

		public bool Forward => false;

		public void PerformSubstitution(SubstitutionGlyphRun run) {
			SequenceLookupRecord[]?[] runRecords = table.FindRecords(run);

			for (int i = runRecords.Length - 1; i >= 0; i--) {
				if (runRecords[i] is SequenceLookupRecord[] records) {
					for (int r = 0; r < records.Length; r++) {
						GlyphSubstitutionLookupTable action = lookupList[records[r].LookupListIndex];
						action.PerformSubstitution(run, i + records[r].SequenceIndex);
					}
				}
			}
		}

		public int PerformSubstitution(SubstitutionGlyphRun run, int index) {
			// TODO This should never actually be called?

			SequenceLookupRecord[]? records = table.FindRecords(run, index);

			if (records is null) { return 0; }

			for (int r = 0; r < records.Length; r++) {
				GlyphSubstitutionLookupTable action = lookupList[records[r].LookupListIndex];
				action.PerformSubstitution(run, index + records[r].SequenceIndex);
			}

			return 0; // Yes?
		}

		public IEnumerable<(ushort[], ushort[])> GetExamples() {
			foreach ((ushort[] initial, SequenceLookupRecord[] records) in table.GetExamples()) {
				SubstitutionGlyphRun initialRun = new SubstitutionGlyphRun(initial);
				for (int r = 0; r < records.Length; r++) {
					GlyphSubstitutionLookupTable action = lookupList[records[r].LookupListIndex];
					action.PerformSubstitution(initialRun, records[r].SequenceIndex);
				}
				yield return (initial, initialRun.ToArray());
			}
		}

	}

	public sealed class OpenTypeChainedContextsSubstitutionSubtable : ISubstitutionSubtable {

		private readonly OpenTypeChainedSequenceContextTable table;
		private readonly GlyphSubstitutionLookupTable[] lookupList;

		private OpenTypeChainedContextsSubstitutionSubtable(OpenTypeChainedSequenceContextTable table, GlyphSubstitutionLookupTable[] lookupList) {
			this.table = table;
			this.lookupList = lookupList;
		}

		internal static OpenTypeChainedContextsSubstitutionSubtable Read(FontFileReader reader, long offset, GlyphSubstitutionLookupTable[] lookupList) {
			OpenTypeChainedSequenceContextTable table = OpenTypeChainedSequenceContextTable.Read(reader, offset);

			return new OpenTypeChainedContextsSubstitutionSubtable(table, lookupList);
		}

		public bool Forward => false;

		public void PerformSubstitution(SubstitutionGlyphRun run) {
			SequenceLookupRecord[]?[] runRecords = table.FindRecords(run);

			for (int i = runRecords.Length - 1; i >= 0; i--) {
				if (runRecords[i] is SequenceLookupRecord[] records) {
					for (int r = 0; r < records.Length; r++) {
						GlyphSubstitutionLookupTable action = lookupList[records[r].LookupListIndex];
						action.PerformSubstitution(run, i + records[r].SequenceIndex);
					}
				}
			}
		}

		public int PerformSubstitution(SubstitutionGlyphRun run, int index) {
			// TODO This should never actually be called?

			SequenceLookupRecord[]? records = table.FindRecords(run, index);

			if (records is null) { return 0; }

			for (int r = 0; r < records.Length; r++) {
				GlyphSubstitutionLookupTable action = lookupList[records[r].LookupListIndex];
				action.PerformSubstitution(run, index + records[r].SequenceIndex);
			}

			return 0; // Yes?
		}

		public IEnumerable<(ushort[] backtrack, ushort[] initial, ushort[] lookahead, ushort[] final)> GetExamples() {
			foreach ((ushort[] backtrack, ushort[] initial, ushort[] lookahead, SequenceLookupRecord[] records) in table.GetExamples()) {
				int idx = backtrack.Length; // Start of initial context within full chained context
				SubstitutionGlyphRun initialRun = new SubstitutionGlyphRun(backtrack.Concat(initial).Concat(lookahead));
				for (int r = 0; r < records.Length; r++) {
					GlyphSubstitutionLookupTable action = lookupList[records[r].LookupListIndex];
					action.PerformSubstitution(initialRun, idx + records[r].SequenceIndex);
				}
				ushort[] final = initialRun.ToArray();
				if(final.Length >= backtrack.Length + lookahead.Length) {
					yield return (backtrack, initial, lookahead, final[backtrack.Length..^lookahead.Length]);
				}
				else { // Something went wrong
					yield return (Array.Empty<ushort>(), backtrack.Concat(initial).Concat(lookahead).ToArray(), Array.Empty<ushort>(), final);
				}
			}
		}

	}

	public abstract class OpenTypeReverseChainingContextualSingleSubstitutionSubtable : ISubstitutionSubtable {

		private OpenTypeReverseChainingContextualSingleSubstitutionSubtable() { }

		public bool Forward => false;
		public abstract int PerformSubstitution(SubstitutionGlyphRun run, int index);
		public abstract IEnumerable<(ushort[], ushort[])> GetExamples();

		internal static OpenTypeReverseChainingContextualSingleSubstitutionSubtable Read(FontFileReader reader, long offset) {

			reader.Position = offset;

			ushort substFormat = reader.ReadUInt16();

			if (substFormat == 1) {
				ushort coverageOffset = reader.ReadUInt16(); // from beginning of substitution subtable
				OpenTypeCoverageTable coverage = reader.ReadFrom(offset + coverageOffset, OpenTypeCoverageTable.Read);

				ushort backtrackGlyphCount = reader.ReadUInt16();
				ushort[] backtrackCoverageOffsets = reader.ReadUInt16(backtrackGlyphCount);
				OpenTypeCoverageTable[] backtrackCoverages = reader.ReadFrom(offset, backtrackCoverageOffsets, OpenTypeCoverageTable.Read);

				ushort lookaheadGlyphCount = reader.ReadUInt16();
				ushort[] lookaheadCoverageOffsets = reader.ReadUInt16(lookaheadGlyphCount);
				OpenTypeCoverageTable[] lookaheadCoverages = reader.ReadFrom(offset, lookaheadCoverageOffsets, OpenTypeCoverageTable.Read);

				ushort glyphCount = reader.ReadUInt16();
				ushort[] substituteGlyphIDs = reader.ReadUInt16(glyphCount);

				return new OpenTypeReverseChainingContextualSingleSubstitutionTableFormat1(coverage, backtrackCoverages, lookaheadCoverages, substituteGlyphIDs);
			}
			else {
				throw new FormatException($"Unknown reverse chaining contextual single substitution subtable format: {substFormat}");
			}
		}

		private class OpenTypeReverseChainingContextualSingleSubstitutionTableFormat1 : OpenTypeReverseChainingContextualSingleSubstitutionSubtable {

			public readonly OpenTypeCoverageTable Coverage;
			public int BacktrackGlyphCount => BacktrackCoverages.Length;
			public readonly OpenTypeCoverageTable[] BacktrackCoverages;
			public int LookaheadGlyphCount => LookaheadCoverages.Length;
			public readonly OpenTypeCoverageTable[] LookaheadCoverages;
			public int GlyphCount => SubstituteGlyphIDs.Length;
			public readonly ushort[] SubstituteGlyphIDs;

			internal OpenTypeReverseChainingContextualSingleSubstitutionTableFormat1(OpenTypeCoverageTable coverage, OpenTypeCoverageTable[] backtrackCoverages, OpenTypeCoverageTable[] lookaheadCoverages, ushort[] substituteGlyphIDs) : base() {
				Coverage = coverage;
				BacktrackCoverages = backtrackCoverages;
				LookaheadCoverages = lookaheadCoverages;
				SubstituteGlyphIDs = substituteGlyphIDs;
			}

			public override int PerformSubstitution(SubstitutionGlyphRun run, int index) {
				if (Coverage.CoverageIndex(run[index]) is ushort coverageIdx && IsMatch(run, index)) {
					run.Replace(index, SubstituteGlyphIDs[coverageIdx]);
				}
				return 0;
			}

			public override IEnumerable<(ushort[], ushort[])> GetExamples() {
				ushort[] before = BacktrackCoverages.Select(c => c.GetGlyph(0)).ToArray();
				ushort[] after = LookaheadCoverages.Select(c => c.GetGlyph(0)).ToArray();
				for (ushort ci = 0; ci < Coverage.Length; ci++) {
					ushort glyph = Coverage.GetGlyph(ci);
					ushort replacement = SubstituteGlyphIDs[ci];
					yield return (
						before.Append(glyph).Concat(after).ToArray(),
						before.Append(replacement).Concat(after).ToArray()
						);
				}
			}

			public bool IsMatch(SubstitutionGlyphRun run, int index) {
				if (index < BacktrackGlyphCount) { return false; }
				if (index + LookaheadGlyphCount >= run.Count) { return false; }

				// Current glyph already checked
				/*
				if (!Coverage.IsCovered(run[index])) {
					return false;
				}
				*/

				for (int b = 0; b < BacktrackCoverages.Length; b++) {
					if (!BacktrackCoverages[b].IsCovered(run[index - BacktrackCoverages.Length + b])) {
						return false;
					}
				}

				for (int a = 0; a < LookaheadCoverages.Length; a++) {
					if (!LookaheadCoverages[a].IsCovered(run[index + 1 + a])) {
						return false;
					}
				}

				return true;
			}

		}

	}

}
