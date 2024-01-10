using SharpSheets.Evaluations;
using SharpSheets.Layouts;
using SharpSheets.Canvas.Text;
using SharpSheets.Shapes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using SharpSheets.Canvas;
using SharpSheets.Documentation;
using SharpSheets.Utilities;
using SharpSheets.Widgets;

namespace SharpSheets.Markup.Parsing {

	public static class MarkupEvaluationTypes {

		public static readonly EvaluationType DIMENSION = EvaluationType.FromSystemType(typeof(Dimension));
		public static readonly EvaluationType MARGINS = EvaluationType.FromSystemType(typeof(Margins));

		// public static readonly EvaluationType UFLOAT = new EvaluationType() // TODO Can this be done neatly?

		public static readonly EvaluationType FILE_PATH = EvaluationType.FromSystemType(typeof(SharpSheets.Utilities.FilePath));

		// Enum types
		public static readonly EvaluationType TEXT_FORMAT = EvaluationType.FromSystemType(typeof(TextFormat));
		public static readonly EvaluationType TEXT_HEIGHT_STRATEGY = EvaluationType.FromSystemType(typeof(TextHeightStrategy));
		public static readonly EvaluationType JUSTIFICATION = EvaluationType.FromSystemType(typeof(Justification));
		public static readonly EvaluationType ALIGNMENT = EvaluationType.FromSystemType(typeof(Alignment));
		public static readonly EvaluationType LAYOUT = EvaluationType.FromSystemType(typeof(Layout));
		public static readonly EvaluationType CHECK_TYPE = EvaluationType.FromSystemType(typeof(CheckType));

		// TODO Is this right?
		public static readonly EvaluationType WIDGET = EvaluationType.CustomType("Widget", Enumerable.Empty<TypeField>(), typeof(IWidget));

		// TODO There are missing types here
		public static readonly EvaluationType CONTAINER = EvaluationType.CustomType("TitledBox", Enumerable.Empty<TypeField>(), typeof(IContainerShape));
		public static readonly EvaluationType BOX = EvaluationType.CustomType("Box", Enumerable.Empty<TypeField>(), typeof(IBox));
		public static readonly EvaluationType LABELLED_BOX = EvaluationType.CustomType("LabelledBox", Enumerable.Empty<TypeField>(), typeof(ILabelledBox));
		public static readonly EvaluationType BAR = EvaluationType.CustomType("Bar", Enumerable.Empty<TypeField>(), typeof(IBar));
		public static readonly EvaluationType USAGE_BAR = EvaluationType.CustomType("UsageBar", Enumerable.Empty<TypeField>(), typeof(IUsageBar));
		public static readonly EvaluationType DETAIL = EvaluationType.CustomType("Detail", Enumerable.Empty<TypeField>(), typeof(IDetail));

		private static readonly Dictionary<string, EvaluationType> typeRegistry = new Dictionary<string, EvaluationType> {
			{ "float", EvaluationType.FLOAT },
			{ "ufloat", EvaluationType.UFLOAT },
			{ "int", EvaluationType.INT },
			{ "uint", EvaluationType.UINT },
			{ "bool", EvaluationType.BOOL },
			{ "string", EvaluationType.STRING },
			{ "color", EvaluationType.COLOR },
			{ "dimension", DIMENSION },
			{ "margins", MARGINS },
			{ "filepath", FILE_PATH },
			{ "textformat", TEXT_FORMAT },
			{ "textheightstrategy", TEXT_HEIGHT_STRATEGY },
			{ "justification", JUSTIFICATION },
			{ "alignment", ALIGNMENT },
			{ "checktype", CHECK_TYPE },
			{ "widget", WIDGET },
			{ "titledbox", CONTAINER }, // Confusing name, but makes more sense to user?
			{ "box", BOX },
			{ "labelledbox", LABELLED_BOX },
			{ "bar", BAR },
			{ "usagebar", USAGE_BAR },
			{ "detail", DETAIL }
			// TODO More types here?
		};

		private static readonly Regex arrayTupleRegex = new Regex(@"\[(?<tuple>[0-9]+)?\]", RegexOptions.IgnoreCase);
		/// <summary></summary>
		/// <exception cref="FormatException"></exception>
		public static EvaluationType ParseArgumentType(string text, string? description, XMLElement[] options) {
			text = Regex.Replace(text, @"\s+", "");
			string textKey = text.ToLowerInvariant();
			
			Match arrayMatch = arrayTupleRegex.Match(text);
			if (arrayMatch.Success) {
				// The "top level" array specification comes first, followed by descreasingly significant "[]"
				string baseTypeStr = text.Substring(0, arrayMatch.Index) + text.Substring(arrayMatch.Index + arrayMatch.Length);
				// A bit clunky, but it should work
				EvaluationType baseType = ParseArgumentType(baseTypeStr, description, options);
				if (arrayMatch.Groups["tuple"].Success) {
					int tupleSize = int.Parse(arrayMatch.Groups["tuple"].Value);
					return baseType.MakeTuple(tupleSize);
				}
				else {
					return baseType.MakeArray();
				}
			}
			else if (options != null && options.Length > 0) {
				if (typeRegistry.ContainsKey(textKey)) {
					throw new FormatException("Cannot name a custom enum after a built-in type.");
				}
				
				return EvaluationType.FromSystemType(MakeCustomEnumType(text, description, options)); // TODO Don't like this. Why should this class have to deal with XMLElements?
			}
			else if (typeRegistry.TryGetValue(textKey, out EvaluationType? argType)) {
				return argType;
			}
			else {
				throw new FormatException("Invalid argument type string.");
			}
		}

		private static MarkupEnumType MakeCustomEnumType(string text, string? description, XMLElement[] options) {
			EnumValDoc[] enumVals = options.Select(opt => {
				string? valName = opt.GetAttribute1("name", false)?.Value;
				if(valName is null) { return null; }
				string? valDoc = opt.GetAttribute1("desc", false)?.Value;
				return new EnumValDoc(text, valName, !string.IsNullOrWhiteSpace(valDoc) ? new DocumentationString(valDoc) : null);
			}).WhereNotNull().ToArray();

			return new MarkupEnumType(text, description, enumVals);
		}
	}

}
