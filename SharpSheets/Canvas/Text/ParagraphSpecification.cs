using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpSheets.Canvas.Text {

	public readonly struct ParagraphIndent {
		public readonly float Indent;
		public readonly float Hanging;

		public ParagraphIndent(float indent, float hanging) {
			Indent = indent;
			Hanging = hanging;
		}
	}

	public readonly struct ParagraphSpecification {
		
		/// <summary> Distance between baseline positions, as a factor of fontsize. </summary>
		public readonly float LineSpacing;
		/// <summary> Absolute distance (in points) between paragraphs. </summary>
		public readonly float ParagraphSpacing;

		private readonly ParagraphIndent paragraphIndent;

		public float Indent => paragraphIndent.Indent;
		public float HangingIndent => paragraphIndent.Hanging;

		public ParagraphSpecification(float lineSpacing, float paragraphSpacing, ParagraphIndent paragraphIndent) {
			LineSpacing = lineSpacing;
			ParagraphSpacing = paragraphSpacing;
			this.paragraphIndent = paragraphIndent;
		}

		public ParagraphSpecification(float lineSpacing, float paragraphSpacing, float paragraphIndent, float paragraphHanging) {
			LineSpacing = lineSpacing;
			ParagraphSpacing = paragraphSpacing;
			this.paragraphIndent = new ParagraphIndent(paragraphIndent, paragraphHanging);
		}

		public float GetLineHeight(float fontSize) {
			return fontSize * LineSpacing;
		}

		public override int GetHashCode() {
			return HashCode.Combine(LineSpacing, ParagraphSpacing, Indent, HangingIndent);
		}

		public override bool Equals(object? obj) {
			if (obj is ParagraphSpecification spec) {
				return LineSpacing == spec.LineSpacing && ParagraphSpacing == spec.ParagraphSpacing && Indent == spec.Indent && HangingIndent == spec.HangingIndent;
			}
			else {
				return false;
			}
		}

		public static bool operator ==(ParagraphSpecification left, ParagraphSpecification right) {
			return left.Equals(right);
		}

		public static bool operator !=(ParagraphSpecification left, ParagraphSpecification right) {
			return !(left == right);
		}
	}

}
