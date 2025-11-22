using SharpSheets.Markup.Parsing;
using SharpSheets.Utilities;

namespace SharpSheets.Documentation {

	public class EnumValDoc {
		public readonly string type;
		public readonly string name;
		public readonly DocumentationString? description;

		public EnumValDoc(string type, string name, DocumentationString? description) {
			this.type = type;
			this.name = name;
			this.description = description;
		}
	}

	public class EnumDoc {
		public readonly string type;
		public readonly EnumValDoc[] values;
		public readonly DocumentationString? description;

		public EnumDoc(string type, EnumValDoc[] values, DocumentationString? description) {
			this.type = type;
			this.values = values;
			this.description = description;
		}
	}

	public static class SharpDocumentation {

		public static EnumDoc? GetEnumDoc(Type type) {
			if (type is MarkupEnumType markupEnumType) {
				return markupEnumType.Documentation;
			}
			else if (EnumDocs.TryGetDocumentation((type.FullName ?? "").Replace("+", "."), out EnumDoc? enumDoc)) {
				return enumDoc;
			}
			else if (type.IsEnum) {
				return new EnumDoc(type.Name, Enum.GetNames(type).Select(n => new EnumValDoc(type.Name, n, null)).ToArray(), null);

			}
			else {
				return null;
			}
		}

		public static EnumDoc? GetBuiltInEnumDocFromName(string name) {
			if (EnumDocs.TryGetDocumentation(name, out EnumDoc? enumDoc)) {
				return enumDoc;
			}
			else if(TypeUtils.GetTypeByName(name, StringComparison.OrdinalIgnoreCase) is Type namedType) {
				return GetEnumDoc(namedType);
			}
			else {
				return null;
			}
		}

	}
}