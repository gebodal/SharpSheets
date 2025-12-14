using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SharpSheets.Markup.Parsing {

	public static partial class MarkupParsingConstants {

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

		[GeneratedRegex(@"^remaining$")]
		private static partial Regex RemainingRegex();

		[GeneratedRegex(@"^(?:remaining|label)$")]
		private static partial Regex RemainingLabelRegex();

		[GeneratedRegex(@"^entry[0-9]+$")]
		private static partial Regex EntryRegex();

		[GeneratedRegex(@"^(?:label|entry1|entry2)$")]
		private static partial Regex UsageBarElementsRegex();

		private static readonly Dictionary<string, Regex?> referenceElementComponents = new Dictionary<string, Regex?> {
			{ "box", RemainingRegex() },
			{ "labelledBox", RemainingLabelRegex() },
			{ "titledBox", RemainingRegex() },
			{ "entried", EntryRegex() },
			{ "bar", RemainingLabelRegex() },
			{ "usageBar", UsageBarElementsRegex() },
			{ "detail", null },
			// TODO Add remaining types: TitleStylesBox?
			{ "widget", RemainingRegex() }
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

		public static Regex? GetReferenceElementChildrenRegex(string name) {
			//return new Regex(string.Join("|", referenceElementComponents[name]));
			return referenceElementComponents[name];
		}

		public static bool IsIgnoredUseCloneAttribute(string name) {
			return useElementIgnoredAttributes.Contains(name);
		}

	}

}
