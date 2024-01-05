using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SharpSheets.Markup.Parsing {

	public static class MarkupParsingConstants {

		// TODO Need to check all these hard-coded values

		private static readonly HashSet<string> renderedElements = new HashSet<string> {
			"line", "rect", "circle", "ellipse", "polyline", "polygon", "path",
			"g",
			"text", "textPath", "textRect", "tspan",
			"image",
			"symbol", "use",
			"textField", "checkField", "imageField"
		};
		private static readonly HashSet<string> nonRenderedElements = new HashSet<string> {
			"clipPath",
			"linearGradient", "radialGradient", "solidPaint"
		};

		private static readonly Dictionary<string, string[]> referenceElementComponents = new Dictionary<string, string[]> {
			{ "box", new string[] { "remaining" } },
			{ "labelledBox", new string[] { "remaining", "label" } },
			{ "titledBox", new string[] { "remaining" } },
			{ "bar", new string[] { "remaining", "label" } },
			{ "usageBar", new string[] { "label", "entry1", "entry2" } },
			{ "detail", Array.Empty<string>() }
			// TODO Add remaining types: TitleStylesBox? EntriedArea?
		};

		private static readonly HashSet<string> useElementIgnoredAttributes = new HashSet<string> {
			"x", "y", "width", "height", "href", "transform"
		};

		public static bool IsRenderedElement(string name) {
			return renderedElements.Contains(name);
		}

		public static bool IsNonRenderedElement(string name) {
			return nonRenderedElements.Contains(name);
		}

		public static bool IsReferenceElement(string name) {
			return referenceElementComponents.ContainsKey(name);
		}

		public static Regex GetReferenceElementChildrenRegex(string name) {
			return new Regex(string.Join("|", referenceElementComponents[name]));
		}

		public static bool IsIgnoredUseCloneAttribute(string name) {
			return useElementIgnoredAttributes.Contains(name);
		}

	}

}
