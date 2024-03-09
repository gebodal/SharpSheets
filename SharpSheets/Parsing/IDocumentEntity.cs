using System.Collections.Generic;

namespace SharpSheets.Parsing {

	public interface IDocumentEntity {
		/// <summary> A single word that describes the type of entity this is. </summary>
		string SimpleName { get; }
		/// <summary> A detailed name that includes the child index of this entity among its parent's children. </summary>
		string DetailedName { get; }
		/// <summary> A full path of the entity in the document tree. </summary>
		string FullName { get; }
		/// <summary> Document span that this entity originates from. </summary>
		DocumentSpan Location { get; }
		/// <summary> Depth of this entity in the document tree. </summary>
		int Depth { get; }

		/// <summary> The parent node of this entity. </summary>
		IDocumentEntity? Parent { get; }
		/// <summary> The child nodes of this entity. </summary>
		IEnumerable<IDocumentEntity> Children { get; }
	}

	public static class DocumentEntity {

		public static readonly IEqualityComparer<IDocumentEntity> EntityComparer = new DocumentEntityEqualityComparer();

		private sealed class DocumentEntityEqualityComparer : IEqualityComparer<IDocumentEntity> {
			public int GetHashCode(IDocumentEntity value) {
				return HashCode.Combine(value.FullName, value.Location, value.Depth);
			}

			public bool Equals(IDocumentEntity? left, IDocumentEntity? right) {
				if (left is null || right is null) {
					return left is null && right is null;
				}
				return left.FullName == right.FullName && left.Location.Equals(right.Location) && left.Depth == right.Depth;
			}
		}

	}

	public static class DocumentEntityUtils {
		public static IEnumerable<IDocumentEntity> TraverseChildren(this IDocumentEntity context) {
			foreach (IDocumentEntity child in context.Children) {
				yield return child;
				foreach (IDocumentEntity c in child.TraverseChildren()) {
					yield return c;
				}
			}
		}

		public static IEnumerable<IDocumentEntity> TraverseParents(this IDocumentEntity context) {
			IDocumentEntity? currentParent = context.Parent;
			while (currentParent != null) {
				yield return currentParent;
				currentParent = currentParent.Parent;
			}
		}
	}

	public readonly struct DocumentSpan : IEquatable<DocumentSpan> {
		/// <summary> Offset (zero-index) of the starting character for this span in the document. A value of less than 0 indicates that this span is imaginary.</summary>
		public int Offset { get; }
		/// <summary> Line index of this span in the document. A value of less than 0 indicates that this span is imaginary.</summary>
		public int Line { get; }
		/// <summary> Column index for the start of this span in the specified line. A value less than 0 indicates that this span takes up the whole line. </summary>
		public int Column { get; }
		/// <summary> The length of this span (the number of characters). A value less than 0 indicates that this span takes up the rest of the line after <see cref="Column"/>. </summary>
		public int Length { get; }

		/// <summary> Instance of an imaginary span (i.e. one which does not actually exist in the document), with values (-1,-1,-1). </summary>
		public static readonly DocumentSpan Imaginary = new DocumentSpan(-1, -1, -1, -1);

		public DocumentSpan(int offset, int line, int column, int length) {
			Offset = offset;
			Line = line;
			Column = column;
			Length = length;
		}

		public DocumentSpan(int offset, int line) : this(offset, line, -1, -1) { }

		public override int GetHashCode() {
			return HashCode.Combine(Offset, Line, Column, Length);
		}

		public static bool Equals(DocumentSpan? x, DocumentSpan? y) {
			if (x is null || y is null) { return x is null && y is null; }
			else { return x.Equals(y); }
		}

		public override bool Equals(object? obj) {
			if (obj is DocumentSpan span) {
				return this.Equals(span);
			}
			else {
				return false;
			}
		}
		public override string ToString() {
			return $"DocumentSpan({Offset}, {Line}, {Column}, {Length})";
		}

		public bool Equals(DocumentSpan other) {
			return Offset == other.Offset && Line == other.Line && Column == other.Column && Length == other.Length;
		}

		public static bool operator ==(DocumentSpan a, DocumentSpan b) {
			return a.Equals(b);
		}
		public static bool operator !=(DocumentSpan a, DocumentSpan b) {
			return !a.Equals(b);
		}
	}
}
