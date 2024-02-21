using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeboPdf.Fonts {

	public class FontGlyphUsage {

		public bool All { get; private set; }
		public IReadOnlySet<ushort> Glyphs => usedGlyphs;
		public IReadOnlySet<(ushort[] glyphs, ushort[] original)> Mappings => mappings;

		private readonly HashSet<ushort> usedGlyphs;
		private readonly HashSet<(ushort[] glyphs, ushort[] original)> mappings;

		public FontGlyphUsage() {
			usedGlyphs = new HashSet<ushort>();
			this.mappings = new HashSet<(ushort[], ushort[])>(GlyphMappingComparer.Instance);
			All = false;
		}

		public FontGlyphUsage(IEnumerable<ushort> glyphs) {
			usedGlyphs = new HashSet<ushort>(glyphs);
			this.mappings = new HashSet<(ushort[], ushort[])>(GlyphMappingComparer.Instance);
			All = false;
		}

		private FontGlyphUsage(HashSet<ushort> usedGlyphs, HashSet<(ushort[], ushort[])> mappings, bool allUsed) {
			this.usedGlyphs = new HashSet<ushort>(usedGlyphs);
			this.mappings = new HashSet<(ushort[] glyphs, ushort[] original)>(mappings, GlyphMappingComparer.Instance);
			All = allUsed;
		}

		public static FontGlyphUsage UseAll() {
			return new FontGlyphUsage(new HashSet<ushort>(), new HashSet<(ushort[], ushort[])>(), true);
		}

		public void AddGlyph(ushort glyph) {
			usedGlyphs.Add(glyph);
		}

		public void AddGlyphs(IEnumerable<ushort> glyphs) {
			usedGlyphs.UnionWith(glyphs);
		}

		public void AddAllGlyphs() {
			All = true;
		}

		public void AddMapping((ushort[] glyphs, ushort[] original) mapping) {
			mappings.Add(mapping);
		}

		public void AddMappings(IEnumerable<(ushort[] glyphs, ushort[] original)> mappings) {
			this.mappings.UnionWith(mappings);
		}

		public void UnionWith(FontGlyphUsage other) {
			this.All |= other.All;
			if (!this.All) {
				this.usedGlyphs.UnionWith(other.usedGlyphs);
			}
			this.mappings.UnionWith(other.mappings);
		}

		public static FontGlyphUsage Combine(FontGlyphUsage a, FontGlyphUsage b) {
			bool allUsed = a.All || b.All;

			HashSet<ushort> allGlyphs = new HashSet<ushort>();
			if (!allUsed) {
				allGlyphs.UnionWith(a.usedGlyphs);
				allGlyphs.UnionWith(b.usedGlyphs);
			}

			HashSet<(ushort[], ushort[])> allMappings = new HashSet<(ushort[], ushort[])>(GlyphMappingComparer.Instance);
			allMappings.UnionWith(a.mappings);
			allMappings.UnionWith(b.mappings);

			return new FontGlyphUsage(allGlyphs, allMappings, allUsed);
		}

	}

	public sealed class GlyphMappingComparer : IEqualityComparer<(ushort[], ushort[])>, IEqualityComparer<(ushort, ushort[])> {

		public static readonly GlyphMappingComparer Instance = new GlyphMappingComparer();

		private GlyphMappingComparer() { }

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

		public bool Equals((ushort, ushort[]) x, (ushort, ushort[]) y) {
			if (x.Item1 != y.Item1 || x.Item2.Length != y.Item2.Length) {
				return false;
			}

			for (int i = 0; i < x.Item2.Length; i++) {
				if (x.Item2[i] != y.Item2[i]) {
					return false;
				}
			}

			return true;
		}

		public int GetHashCode((ushort, ushort[]) pair) {
			HashCode hash = new HashCode();
			hash.Add(pair.Item1);
			//hash.Add(0); // Useful?
			for (int i = 0; i < pair.Item2.Length; i++) {
				hash.Add(pair.Item2[i]);
			}
			return hash.ToHashCode();
		}

	}

}
