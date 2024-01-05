using GeboPdf.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeboPdf.Fonts {

	public readonly struct BBox {
		public readonly int llx, lly, urx, ury;

		public BBox(int llx, int lly, int urx, int ury) {
			this.llx = llx;
			this.lly = lly;
			this.urx = urx;
			this.ury = ury;
		}
	}

	[System.Diagnostics.DebuggerDisplay("{value} ({(char)value}), {bBox}, {width}")]
	public readonly struct CharMetric {
		public readonly uint value;
		public readonly BBox bBox;
		public readonly int width;

		public CharMetric(uint value, BBox bBox, int width) {
			this.value = value;
			this.bBox = bBox;
			this.width = width;
		}
	}

	public class AfmFile {

		public readonly Dictionary<uint, CharMetric> metrics;
		public readonly BBox bBox;

		public AfmFile(Dictionary<uint, CharMetric> metrics, BBox bBox) {
			this.metrics = metrics;
			this.bBox = bBox;
		}

		private enum AfmParseState { General, CharMetrics }

		public static AfmFile ReadFile(Stream stream) {

			AfmParseState parseState = AfmParseState.General;

			BBox bBox = default;
			List<CharMetric> charMetrics = new List<CharMetric>();

			using (StreamReader reader = new StreamReader(stream)) {
				while (reader.Peek() >= 0) {
					string line = reader.ReadLine()!.TrimEnd(); // Null-forgiving acceptable here?

					if (parseState == AfmParseState.CharMetrics) {
						if (line.StartsWith("EndCharMetrics")) {
							parseState = AfmParseState.General;
						}
						else if(ParseCharMetric(line, out CharMetric parsed)) {
							charMetrics.Add(parsed);
						}
					}
					else {
						if (line.StartsWith("Comment")) {
							continue;
						}
						if (line.StartsWith("StartCharMetrics")) {
							parseState = AfmParseState.CharMetrics;
						}
						else if (line.StartsWith("FontBBox")) {
							bBox = ParseBBox(line.Substring(8).Trim());
						}
					}

				}
			}

			Dictionary<uint, CharMetric> metrics = new Dictionary<uint, CharMetric>();
			foreach(CharMetric metric in charMetrics) {
				if (!metrics.ContainsKey(metric.value)) {
					metrics.Add(metric.value, metric);
				}
				/*
				else {
					Console.WriteLine($"Repeated: {metric.value} " + (char)metric.value);
				}
				*/
			}

			return new AfmFile(metrics, bBox);
		}

		private static bool ParseCharMetric(string metric, out CharMetric parsed) {
			(string t, string d)[] parts = metric.Split(';')
				.Where(s => !string.IsNullOrWhiteSpace(s))
				.Select(s => {
					string[] p = s.Trim().Split(new char[] { ' ' }, 2);
					return (p[0].Trim(), p[1].Trim());
				}).ToArray();

			uint value = 0;
			BBox bBox = default;
			int width = 0;
			char character = (char)0;

			foreach ((string t, string d) in parts) {
				if (t == "C") {
					int parsedValue = int.Parse(d);
					if(parsedValue > 0) {
						value = (uint)parsedValue;
					}
				}
				else if (t == "WX") {
					width = int.Parse(d);
				}
				else if (t == "B") {
					bBox = ParseBBox(d);
				}
				else if(t == "N") {
					character = PdfEncoding.GetCharacter(d);
				}
			}

			if(character != (char)0) {
				value = (uint)character;
			}

			if(value > 0) {
				parsed = new CharMetric(value, bBox, width);
				return true;
			}
			else {
				parsed = default;
				return false;
			}
		}

		private static BBox ParseBBox(string bBox) {
			int[] pairs = bBox.Split().Select(s => int.Parse(s)).ToArray();
			return new BBox(pairs[0], pairs[1], pairs[2], pairs[3]);
		}

	}

}
