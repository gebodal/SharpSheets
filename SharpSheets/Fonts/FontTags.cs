using GeboPdf.Fonts.TrueType;
using SharpSheets.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SharpSheets.Fonts {

	public class FontTags : IEquatable<FontTags> {

		private static readonly string defaultScriptTag = "latn";

		public static readonly FontTags Default = new FontTags(defaultScriptTag, null, OpenTypeLayoutTags.GetDefaults(defaultScriptTag, null));

		public readonly string ScriptTag;
		public readonly string? LangSysTag;
		public IReadOnlySet<string> FeatureTags => featureTags;
		private readonly HashSet<string> featureTags;

		public FontTags(string? scriptTag, string? langSysTag, IReadOnlySet<string> featureTags) {
			ScriptTag = scriptTag ?? defaultScriptTag;
			LangSysTag = langSysTag;
			this.featureTags = new HashSet<string>(featureTags);
		}

		private static readonly Regex pattern = new Regex(@"^\s*\{(?<content>[^\}]*)\}\s*$");

		public static FontTags Parse(string text) {
			Match match = pattern.Match(text);
			if (!match.Success) { throw new FormatException("Font tags string badly formatted."); }

			string content = match.Groups["content"].Value;

			string[] parts;
			if(content.Contains(',') || !string.IsNullOrWhiteSpace(content)) {
				parts = content.SplitAndTrim(',');
			}
			else {
				parts = Array.Empty<string>();
			}

			string? scriptTag = null;
			string? langSysTag = null;

			Dictionary<string, bool> featureTags = new Dictionary<string, bool>();

			foreach (string part in parts) {
				string[] components = part.SplitAndTrim(':');
				if (components.Length == 1 && !string.IsNullOrWhiteSpace(components[0])) {
					if (components[0][0] == '!') {
						featureTags[components[0][1..]] = false;
					}
					else {
						featureTags[components[0]] = true;
					}
				}
				else if (components.Length == 2 && !string.IsNullOrWhiteSpace(components[1])) {
					if (StringComparer.OrdinalIgnoreCase.Equals(components[0], "script")) {
						scriptTag = components[1].PadRight(4);
					}
					else if(StringComparer.OrdinalIgnoreCase.Equals(components[0], "langsys")) {
						langSysTag = components[1].PadRight(4);
					}
					else {
						throw new FormatException($"Unrecognized tag type: {components[0]}");
					}
				}
				else {
					throw new FormatException($"Font tags component badly formatted: {part}");
				}
			}

			scriptTag ??= defaultScriptTag;

			HashSet<string> features = new HashSet<string>(OpenTypeLayoutTags.GetDefaults(scriptTag, langSysTag));

			foreach ((string feature, bool active) in featureTags) {
				if (active) { features.Add(feature); }
				else { features.Remove(feature); }
			}

			return new FontTags(scriptTag, langSysTag, features);
		}

		private static readonly IEqualityComparer<HashSet<string>> SetComparer = HashSet<string>.CreateSetComparer();

		public bool Equals(FontTags? other) {
			if(other is null) { return false; }

			return this.ScriptTag == other.ScriptTag
				&& this.LangSysTag == other.LangSysTag
				&& SetComparer.Equals(this.featureTags, other.featureTags);
		}

		public override bool Equals(object? obj) {
			return Equals(obj as FontTags);
		}

		public override int GetHashCode() {
			return HashCode.Combine(ScriptTag, LangSysTag, SetComparer.GetHashCode(featureTags));
		}

		public static bool Equals(FontTags? a, FontTags? b) {
			if (a is null) {
				return b is null;
			}
			return a.Equals(b);
		}

	}

}
