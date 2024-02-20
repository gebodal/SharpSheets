using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeboPdf.Fonts {

	public class FontGlyphUsage {

		public bool All { get; private set; }
		public IReadOnlySet<ushort> Glyphs => usedGlyphs;

		private readonly HashSet<ushort> usedGlyphs;

		public FontGlyphUsage() {
			usedGlyphs = new HashSet<ushort>();
			All = false;
		}

		private FontGlyphUsage(HashSet<ushort> usedGlyphs, bool allUsed) {
			this.usedGlyphs = new HashSet<ushort>(usedGlyphs);
			All = allUsed;
		}

		public static FontGlyphUsage UseAll() {
			return new FontGlyphUsage(new HashSet<ushort>(), true);
		}

		public void AddGlyph(ushort g) {
			usedGlyphs.Add(g);
		}

		public void AddAll() {
			All = true;
		}

		public void UnionWith(FontGlyphUsage other) {
			this.All |= other.All;
			if (!this.All) {
				this.usedGlyphs.UnionWith(other.usedGlyphs);
			}
		}

		public static FontGlyphUsage Combine(FontGlyphUsage a, FontGlyphUsage b) {
			bool allUsed = a.All || b.All;

			HashSet<ushort> allGlyphs = new HashSet<ushort>();
			if (!allUsed) {
				allGlyphs.UnionWith(a.usedGlyphs);
				allGlyphs.UnionWith(b.usedGlyphs);
			}

			return new FontGlyphUsage(allGlyphs, allUsed);
		}

	}

}
