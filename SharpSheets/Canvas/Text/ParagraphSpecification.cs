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

	public struct ParagraphSpecification {
		//public readonly float FontSize; // TODO It doesn't make a huge amount of sense that this is here, actually...
		
		/// <summary> Distance between baseline positions, as a factor of fontsize. </summary>
		public readonly float LineSpacing;
		/// <summary> Absolute distance (in points) between paragraphs. </summary>
		public readonly float ParagraphSpacing;

		private ParagraphIndent paragraphIndent;

		public float Indent => paragraphIndent.Indent;
		public float HangingIndent => paragraphIndent.Hanging;

		//public float LineHeight => FontSize * LineSpacing;

		public ParagraphSpecification(float lineSpacing, float paragraphSpacing, ParagraphIndent paragraphIndent) {
			//FontSize = fontsize;
			LineSpacing = lineSpacing;
			ParagraphSpacing = paragraphSpacing;
			this.paragraphIndent = paragraphIndent;
		}

		public ParagraphSpecification(float lineSpacing, float paragraphSpacing, float paragraphIndent, float paragraphHanging) {
			//FontSize = fontsize;
			LineSpacing = lineSpacing;
			ParagraphSpacing = paragraphSpacing;
			this.paragraphIndent = new ParagraphIndent(paragraphIndent, paragraphHanging);
		}

		public float GetLineHeight(float fontSize) {
			return fontSize * LineSpacing;
		}

		/*
		public ParagraphSpecification AtFontSize(float fontsize) {
			return new ParagraphSpecification(fontsize, LineSpacing, ParagraphSpacing, paragraphIndent);
		}
		*/

		public override int GetHashCode() {
			unchecked {
				int hash = 17;
				//hash = hash * 31 + FontSize.GetHashCode();
				hash = hash * 31 + LineSpacing.GetHashCode();
				hash = hash * 31 + ParagraphSpacing.GetHashCode();
				hash = hash * 31 + Indent.GetHashCode();
				hash = hash * 31 + HangingIndent.GetHashCode();
				return hash;
			}
		}

		public override bool Equals(object? obj) {
			if (obj is ParagraphSpecification spec) {
				//return FontSize == spec.FontSize && LineSpacing == spec.LineSpacing && ParagraphSpacing == spec.ParagraphSpacing && Indent == spec.Indent && HangingIndent == spec.HangingIndent;
				return LineSpacing == spec.LineSpacing && ParagraphSpacing == spec.ParagraphSpacing && Indent == spec.Indent && HangingIndent == spec.HangingIndent;
			}
			else {
				return false;
			}
		}
	}

}
