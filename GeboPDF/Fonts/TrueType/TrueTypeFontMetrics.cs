using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeboPdf.Fonts.TrueType {

	public static class TrueTypeFontMetrics {

		public static (ushort[] advanceWidths, short[] ascents, short[] descents, Dictionary<uint, short>? kerning) GetMetrics(this TrueTypeFontFile fontFile) {

			ushort[] advanceWidths = new ushort[fontFile.numGlyphs];
			short[] ascents = new short[fontFile.numGlyphs];
			short[] descents = new short[fontFile.numGlyphs];

			for (int i = 0; i < fontFile.numGlyphs; i++) {
				// Advance width
				advanceWidths[i] = fontFile.hmtx.advanceWidths[i];

				// Ascent & Descent
				short yMax, yMin;
				if (fontFile.glyf != null) {
					yMax = fontFile.glyf.glyphs[i].yMax;
					yMin = fontFile.glyf.glyphs[i].yMin;
				}
				else if (fontFile.os2 != null) {
					yMax = fontFile.os2.sTypoAscender ?? fontFile.head.yMax;
					yMin = fontFile.os2.sTypoDescender ?? fontFile.head.yMin;
				}
				else {
					yMax = fontFile.head.yMax;
					yMin = fontFile.head.yMin;
				}
				ascents[i] = yMax;
				descents[i] = yMin;
			}

			Dictionary<uint, short>? kerning = null;
			if (fontFile.kern is not null) {
				kerning = new Dictionary<uint, short>();

				foreach (uint pair in fontFile.kern.Subtables.SelectMany(s => s.Values.Keys).Distinct().OrderBy(p => p)) {

					short kernValue = 0;

					for (int s = 0; s < fontFile.kern.Subtables.Length; s++) {
						TrueTypeKerningSubtable subtable = fontFile.kern.Subtables[s];
						// TODO Only deals with horizontal writing
						if (subtable.Coverage.IsKerningValues() && subtable.Coverage.IsHorizontal() && !subtable.Coverage.IsCrossStream()) {
							if (subtable.Values.TryGetValue(pair, out short value)) {
								if (subtable.Coverage.IsOverride()) {
									kernValue = value;
								}
								else {
									kernValue += value;
								}
							}
						}
					}

					kerning[pair] = kernValue;
				}
			}

			return (advanceWidths, ascents, descents, kerning);
		}

	}

}
