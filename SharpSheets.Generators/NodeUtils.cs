using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace SharpSheets.Generators {

	public static class NodeUtils {

		public static bool HasModifier(this MemberDeclarationSyntax syntax, SyntaxKind kind) {
			return syntax.Modifiers.Any(m => m.IsKind(kind));
		}

		public static bool IsPublic(this MemberDeclarationSyntax syntax) {
			return HasModifier(syntax, SyntaxKind.PublicKeyword);
		}

		public static bool IsPartial(this MemberDeclarationSyntax syntax) {
			return HasModifier(syntax, SyntaxKind.PartialKeyword);
		}

	}

}
