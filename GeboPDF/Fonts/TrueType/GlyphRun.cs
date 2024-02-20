using GeboPDF.Utilities;
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

		private class GlyphMappingComparer : IEqualityComparer<(ushort[], ushort[])> {

			public static readonly GlyphMappingComparer Instance = new GlyphMappingComparer();

			public bool Equals((ushort[], ushort[]) x, (ushort[], ushort[]) y) {
				if (x.Item1.Length != y.Item1.Length || x.Item2.Length != y.Item2.Length) {
					return false;
				}

				for (int i = 0; i < x.Item1.Length; i++) {
					if (x.Item1[i] != y.Item1[i]) {
						return false;
					}
				}

				for (int i = 0; i < x.Item2.Length; i++) {
					if (x.Item2[i] != y.Item2[i]) {
						return false;
					}
				}

				return true;
			}

			public int GetHashCode((ushort[], ushort[]) pair) {
				HashCode hash = new HashCode();
				for (int i = 0; i < pair.Item1.Length; i++) {
					hash.Add(pair.Item1[i]);
				}
				//hash.Add(0); // Useful?
				for (int i = 0; i < pair.Item2.Length; i++) {
					hash.Add(pair.Item2[i]);
				}
				return hash.ToHashCode();
			}

		}

	}

	public class PositionedGlyphRun : GlyphRun {

		private readonly ushort[] glyphs;

		protected readonly short[] xPlacement;
		protected readonly short[] yPlacement;
		protected readonly short[] xAdvance;
		protected readonly short[] yAdvance;

		public override int Count => glyphs.Length;

		public override ushort this[int index] {
			get => glyphs[index];
		}

		public PositionedGlyphRun(ushort[] glyphs) {
			this.glyphs = glyphs;
			this.xPlacement = new short[glyphs.Length];
			this.yPlacement = new short[glyphs.Length];
			this.xAdvance = new short[glyphs.Length];
			this.yAdvance = new short[glyphs.Length];
		}

		public void AdjustPosition(int index, ValueRecord record) {
			xPlacement[index] += record.XPlacement ?? 0;
			yPlacement[index] += record.YPlacement ?? 0;
			xAdvance[index] += record.XAdvance ?? 0;
			yAdvance[index] += record.YAdvance ?? 0;
		}

		public virtual (short xPlacement, short yPlacement, short xAdvance, short yAdvance) GetPosition(int index) {
			return (xPlacement[index], yPlacement[index], xAdvance[index], yAdvance[index]);
		}

		public virtual (short xAdvance, short yAdvance) GetAdvance(int index) {
			return (xAdvance[index], yAdvance[index]);
		}

		public virtual (short xPlacement, short yPlacement) GetPlacement(int index) {
			return (xPlacement[index], yPlacement[index]);
		}

		public override ushort[] ToArray() {
			return glyphs;
		}

	}

	public class PositionedScaledGlyphRun : PositionedGlyphRun {

		protected readonly ushort unitsPerEm;

		public PositionedScaledGlyphRun(ushort[] glyphs, ushort unitsPerEm) : base(glyphs) {
			this.unitsPerEm = unitsPerEm;
		}

		public override (short xPlacement, short yPlacement, short xAdvance, short yAdvance) GetPosition(int index) {
			return (
				ProcessShort(xPlacement[index]),
				ProcessShort(yPlacement[index]),
				ProcessShort(xAdvance[index]),
				ProcessShort(yAdvance[index])
				);
		}

		public override (short xAdvance, short yAdvance) GetAdvance(int index) {
			return (
				ProcessShort(xAdvance[index]),
				ProcessShort(yAdvance[index])
				);
		}

		public override (short xPlacement, short yPlacement) GetPlacement(int index) {
			return (
				ProcessShort(xPlacement[index]),
				ProcessShort(yPlacement[index])
				);
		}

		private short ProcessShort(short value) {
			return (short)(1000.0 * (value / (double)unitsPerEm));
		}

	}

}
