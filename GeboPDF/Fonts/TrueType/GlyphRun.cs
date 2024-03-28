using GeboPdf.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeboPdf.Fonts.TrueType {

	public abstract class GlyphRun {

		public abstract int Count { get; }
		public abstract ushort this[int index] { get; }

		public abstract ushort[] ToArray();

	}

	public class SubstitutionGlyphRun : GlyphRun {

		private readonly List<ushort> glyphs;

		public sealed override int Count => glyphs.Count;

		public sealed override ushort this[int index] {
			get => glyphs[index];
			//set => glyphs[index] = value;
		}

		public SubstitutionGlyphRun() {
			glyphs = new List<ushort>();
		}

		public SubstitutionGlyphRun(IEnumerable<ushort> glyphs) {
			this.glyphs = new List<ushort>(glyphs);
		}

		/// <summary>
		/// Replace one glyph with another.
		/// </summary>
		/// <param name="index"></param>
		/// <param name="glyph"></param>
		public virtual void Replace(int index, ushort glyph) {
			glyphs[index] = glyph;
		}

		/// <summary>
		/// Replace one glyph with several.
		/// </summary>
		/// <param name="index"></param>
		/// <param name="glyph"></param>
		public virtual void Replace(int index, ushort[] glyphs) {
			this.glyphs.RemoveAt(index);
			this.glyphs.InsertRange(index, glyphs);
		}

		/// <summary>
		/// Replace several glyphs with one.
		/// </summary>
		/// <param name="index"></param>
		/// <param name="glyph"></param>
		public virtual void Replace(int index, int length, ushort glyph) {
			this.glyphs.RemoveRange(index, length);
			this.glyphs.Insert(index, glyph);
		}

		public override ushort[] ToArray() {
			return glyphs.ToArray();
		}

	}

	public class TrackedSubstitutionGlyphRun : SubstitutionGlyphRun {

		public readonly List<int> origins; // Length = Number of glyphs in final run (contains indexes into originals array)
		public readonly List<ushort[]> originals; // Length = Number of origin blocks in original run (contains arrays of glyph indexes in origin blocks)

		public TrackedSubstitutionGlyphRun(IEnumerable<ushort> glyphs) : base(glyphs) {
			this.origins = new List<int>();
			this.originals = new List<ushort[]>();
			for (int i = 0; i < this.Count; i++) {
				this.origins.Add(i);
				this.originals.Add(new ushort[] { this[i] });
			}
		}

		/// <summary>
		/// Replace one glyph with another.
		/// </summary>
		/// <param name="index"></param>
		/// <param name="glyph"></param>
		public override void Replace(int index, ushort glyph) {
			// No need to change the originals/origins here

			// Update glyph run
			base.Replace(index, glyph);
		}

		/// <summary>
		/// Replace one glyph with several.
		/// </summary>
		/// <param name="index"></param>
		/// <param name="glyphs"></param>
		public override void Replace(int index, ushort[] glyphs) {
			// Find the block the glyph is coming from, and repeat it glyphCount-1 times
			int block = this.origins[index]; // Origin block
			this.origins.InsertCount(index, block, glyphs.Length - 1);

			// Update glyph run
			base.Replace(index, glyphs);
		}

		/// <summary>
		/// Replace several glyphs with one.
		/// </summary>
		/// <param name="index"></param>
		/// <param name="length"></param>
		/// <param name="glyph"></param>
		public override void Replace(int index, int length, ushort glyph) {
			// Collect and concatenate originals

			int startBlock = origins[index], endBlock = origins[index + length - 1];

			if(startBlock != endBlock) {
				// Spans multiple blocks
				// Collect block contents
				List<ushort> originalSequence1 = new List<ushort>();
				for (int i = startBlock; i <= endBlock; i++) {
					originalSequence1.AddRange(this.originals[i]);
				}
				// Remove initial blocks
				this.originals.RemoveRange(startBlock, endBlock - startBlock + 1);
				// Insert concatenated block
				this.originals.Insert(startBlock, originalSequence1.ToArray());
			}
			// If all from same block, don't have to do anything

			// Update origin indexes
			this.origins.RemoveRange(index, length);
			this.origins.Insert(index, startBlock);
			for(int i=index+1; i<this.origins.Count; i++) {
				this.origins[i] -= endBlock - startBlock;
			}

			// Update glyph run
			base.Replace(index, length, glyph);
		}

		public (ushort[] glyphs, ushort[] original)[] GetMappings() {
			if (Count == 0) {
				return Array.Empty<(ushort[], ushort[])>();
			}

			HashSet<(ushort[], ushort[])> mappings = new HashSet<(ushort[], ushort[])>(GlyphMappingComparer.Instance);

			int block = this.origins[0];
			int blockStart = 0;
			for (int i = 1; i < origins.Count; i++) {
				if (origins[i] != block) {

					if (!IsSubSequenceEqual(blockStart, i - blockStart, this.originals[block])) {
						mappings.Add((GetSubArray(blockStart, i - blockStart), this.originals[block]));
					}

					block = origins[i];
					blockStart = i;
				}
			}

			if (!IsSubSequenceEqual(blockStart, this.Count - blockStart, this.originals[block])) {
				mappings.Add((GetSubArray(blockStart, this.Count - blockStart), this.originals[block]));
			}

			return mappings.ToArray();
		}

		private ushort[] GetSubArray(int start, int length) {
			ushort[] array = new ushort[length];
			for (int i = 0; i < length; i++) {
				array[i] = this[start + i];
			}
			return array;
		}

		private bool IsSubSequenceEqual(int start, int length, ushort[] values) {
			if (length != values.Length) { return false; }
			for (int i = 0; i < length; i++) {
				if (this[start + i] != values[i]) {
					return false;
				}
			}
			return true;
		}

	}

	public abstract class PositionedGlyphRun : GlyphRun {

		public abstract (short xPlacement, short yPlacement, ushort xAdvanceBase, ushort yAdvanceBase, short xAdvanceDelta, short yAdvanceDelta) GetPosition(int index);
		public abstract (ushort xAdvanceBase, ushort yAdvanceBase) GetAdvanceBase(int index);
		public abstract (short xAdvanceDelta, short yAdvanceDelta) GetAdvanceDelta(int index);
		public abstract (short xAdvance, short yAdvance) GetAdvanceTotal(int index);
		public abstract (short xPlacement, short yPlacement) GetPlacement(int index);

	}

	public class PositionableGlyphRun : PositionedGlyphRun {

		private readonly ushort[] glyphs;

		protected readonly ushort[] baseXAdvance;
		protected readonly ushort[] baseYAdvance;

		protected readonly short[] xPlacement;
		protected readonly short[] yPlacement;
		protected readonly short[] xAdvanceDelta;
		protected readonly short[] yAdvanceDelta;

		public override int Count => glyphs.Length;

		public override ushort this[int index] {
			get => glyphs[index];
		}

		public PositionableGlyphRun(ushort[] glyphs, ushort[] baseXAdvance, ushort[] baseYAdvance) {
			if (glyphs.Length != baseXAdvance.Length || glyphs.Length != baseYAdvance.Length) {
				throw new ArgumentException($"Glyphs sequence length ({glyphs.Length}) does not match provided advance sequence lengths ({baseXAdvance.Length}, {baseYAdvance.Length}).");
			}
			this.glyphs = glyphs;
			this.baseXAdvance = baseXAdvance;
			this.baseYAdvance = baseYAdvance;
			this.xPlacement = new short[glyphs.Length];
			this.yPlacement = new short[glyphs.Length];
			this.xAdvanceDelta = new short[glyphs.Length];
			this.yAdvanceDelta = new short[glyphs.Length];
		}

		public void AdjustPosition(int index, ValueRecord record) {
			xPlacement[index] += record.XPlacement ?? 0;
			yPlacement[index] += record.YPlacement ?? 0;
			xAdvanceDelta[index] += record.XAdvance ?? 0;
			yAdvanceDelta[index] += record.YAdvance ?? 0;
		}

		public void AdjustPlacement(int index, short xPlacement, short yPlacement) {
			this.xPlacement[index] += xPlacement;
			this.yPlacement[index] += yPlacement;
		}

		public override (short xPlacement, short yPlacement, ushort xAdvanceBase, ushort yAdvanceBase, short xAdvanceDelta, short yAdvanceDelta) GetPosition(int index) {
			return (xPlacement[index], yPlacement[index], baseXAdvance[index], baseYAdvance[index], xAdvanceDelta[index], yAdvanceDelta[index]);
		}

		public override (ushort xAdvanceBase, ushort yAdvanceBase) GetAdvanceBase(int index) {
			return (baseXAdvance[index], baseYAdvance[index]);
		}

		public override (short xAdvanceDelta, short yAdvanceDelta) GetAdvanceDelta(int index) {
			return (xAdvanceDelta[index], yAdvanceDelta[index]);
		}

		public override (short xAdvance, short yAdvance) GetAdvanceTotal(int index) {
			return ((short)(baseXAdvance[index] + xAdvanceDelta[index]), (short)(baseYAdvance[index] + yAdvanceDelta[index]));
		}

		public override (short xPlacement, short yPlacement) GetPlacement(int index) {
			return (xPlacement[index], yPlacement[index]);
		}

		public override ushort[] ToArray() {
			return glyphs;
		}

	}

	public class PositionedScaledGlyphRun : PositionedGlyphRun {

		protected readonly PositionedGlyphRun basis;
		protected readonly ushort unitsPerEm;

		public override int Count => basis.Count;
		public override ushort this[int index] => basis[index];

		public PositionedScaledGlyphRun(PositionedGlyphRun basis, ushort unitsPerEm) {
			this.basis = basis;
			this.unitsPerEm = unitsPerEm;
		}

		public override (short xPlacement, short yPlacement, ushort xAdvanceBase, ushort yAdvanceBase, short xAdvanceDelta, short yAdvanceDelta) GetPosition(int index) {
			(short xPlacement, short yPlacement, ushort xAdvanceBase, ushort yAdvanceBase, short xAdvanceDelta, short yAdvanceDelta) = basis.GetPosition(index);
			return (
				ProcessShort(xPlacement),
				ProcessShort(yPlacement),
				ProcessUShort(xAdvanceBase),
				ProcessUShort(yAdvanceBase),
				ProcessShort(xAdvanceDelta),
				ProcessShort(yAdvanceDelta)
				);
		}

		public override (ushort xAdvanceBase, ushort yAdvanceBase) GetAdvanceBase(int index) {
			(ushort xAdvanceBase, ushort yAdvanceBase) = basis.GetAdvanceBase(index);
			return (
				ProcessUShort(xAdvanceBase),
				ProcessUShort(yAdvanceBase)
				);
		}

		public override (short xAdvanceDelta, short yAdvanceDelta) GetAdvanceDelta(int index) {
			(short xAdvanceDelta, short yAdvanceDelta) = basis.GetAdvanceDelta(index);
			return (
				ProcessShort(xAdvanceDelta),
				ProcessShort(yAdvanceDelta)
				);
		}

		public override (short xAdvance, short yAdvance) GetAdvanceTotal(int index) {
			(short xAdvance, short yAdvance) = basis.GetAdvanceTotal(index);
			return (
				ProcessShort(xAdvance),
				ProcessShort(yAdvance)
				);
		}

		public override (short xPlacement, short yPlacement) GetPlacement(int index) {
			(short xPlacement, short yPlacement) = basis.GetPlacement(index);
			return (
				ProcessShort(xPlacement),
				ProcessShort(yPlacement)
				);
		}

		private short ProcessShort(short value) {
			return (short)(1000.0 * (value / (double)unitsPerEm));
		}

		private ushort ProcessUShort(ushort value) {
			return (ushort)(1000.0 * (value / (double)unitsPerEm));
		}

		public override ushort[] ToArray() => basis.ToArray();

	}

}
