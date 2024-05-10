using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace GeboPDF.Text {

	public static class Unicode {

		private static readonly IReadOnlyDictionary<uint, string> CodepointNames;
		private static readonly IReadOnlyDictionary<(uint start, uint end), string> CodepointRangeNames;

		private static readonly IReadOnlyDictionary<uint, int> CodepointCombiningClasses;

		public static bool TryGetUnicodeName(uint codepoint, [MaybeNullWhen(false)] out string name) {
			if (CodepointNames.TryGetValue(codepoint, out string? charName)) {
				name = charName;
				return true;
			}

			foreach (((uint start, uint end), string rangeName) in CodepointRangeNames) {
				if (codepoint >= start && codepoint <= end) {
					name = $"{rangeName} {codepoint - start:X}";
					return true;
				}
			}

			name = null;
			return false;
		}

		public static bool TryGetCombiningClass(uint codepoint, out int combiningClass) => CodepointCombiningClasses.TryGetValue(codepoint, out combiningClass);

		static Unicode() {
			Dictionary<uint, string> codepointNames = new Dictionary<uint, string>();
			Dictionary<string, uint> firsts = new Dictionary<string, uint>();
			Dictionary<string, uint> lasts = new Dictionary<string, uint>();

			Dictionary<uint, int> codepointCombiningClasses = new Dictionary<uint, int>();

			Assembly assembly = typeof(Unicode).Assembly;

			string unicodeResource = assembly.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("UnicodeData15-1-0.txt")) ?? throw new InvalidOperationException("Could not find Unicode data file.");

			using (System.IO.Stream stream = assembly.GetManifestResourceStream(unicodeResource)!)
			using (System.IO.StreamReader reader = new System.IO.StreamReader(stream)) {
				while (reader.Peek() >= 0) {
					string? line = reader.ReadLine()?.Trim();

					if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#')) {
						continue;
					}

					string[] parts = line.Split(';');

					uint codePoint = uint.Parse(parts[0], NumberStyles.HexNumber);
					string characterName = parts[1];
					int combiningClass = int.Parse(parts[3]);

					if (characterName.EndsWith(", First>")) {
						firsts[characterName[1..^8]] = codePoint;
					}
					else if (characterName.EndsWith(", Last>")) {
						lasts[characterName[1..^7]] = codePoint;
					}
					else {
						if (characterName.StartsWith('<') && characterName.EndsWith('>') && !string.IsNullOrWhiteSpace(parts[10])) {
							codepointNames[codePoint] = parts[10]; // Use Unicode 1.0 name if the current name is vague
						}
						else {
							codepointNames[codePoint] = characterName;
						}
					}

					codepointCombiningClasses[codePoint] = combiningClass;
				}
			}

			Dictionary<(uint start, uint end), string> codepointRangeNames = new Dictionary<(uint, uint), string>();
			foreach ((string group, uint firstCodePoint) in firsts) {
				if (lasts.TryGetValue(group, out uint lastCodePoint)) {
					codepointRangeNames[(firstCodePoint, lastCodePoint)] = group;
				}
			}

			CodepointNames = codepointNames;
			CodepointRangeNames = codepointRangeNames;

			CodepointCombiningClasses = codepointCombiningClasses;
		}

	}
}
