using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SharpSheets.Canvas;
using SharpSheets.Layouts;

namespace SharpSheets.Canvas.Text {

	public static class TextFitting {

		private class FontSizeParameters {
			public readonly float fontSize;
			public readonly float height;
			public readonly RichString[][] linesAtSize;
			public readonly float heightDiff;
			public readonly float widthDiff;
			public FontSizeParameters(float fontSize, float height, RichString[][] linesAtSize, float heightDiff, float widthDiff) {
				this.fontSize = fontSize;
				this.height = height;
				this.linesAtSize = linesAtSize;
				this.heightDiff = heightDiff;
				this.widthDiff = widthDiff;
			}
		}

		public static float FindFontSize(
			ISharpGraphicsData graphicsData,
			Rectangle rect, RichString text,
			ParagraphSpecification paragraphSpec, FontSizeSearchParams searchParams, TextHeightStrategy heightStrategy,
			bool indentFirstLine,
			out RichString[][] finalLines, out float finalHeight) { // TODO Should finalTextRect be Size rather than Rectangle?

			//Console.WriteLine($"FindFontSizeBisection({text.Text})");
			RichString[] wholeLines = text.Split('\n');

			FontSizeParameters GetHeight(float atSize) {
				RichString[][] linesAtSize = RichStringLineSplitting.SplitParagraphs(graphicsData, wholeLines, rect.Width, atSize, paragraphSpec, indentFirstLine);
				float height = RichStringLayout.CalculateHeight(graphicsData, linesAtSize, atSize, paragraphSpec, heightStrategy);
				float width = RichStringLayout.CalculateWidth(graphicsData, linesAtSize, atSize, paragraphSpec, indentFirstLine); // RichStringLayout.CalculateWidth(graphicsData, linesAtSize, paraAtSize, indentFirstLine);
				return new FontSizeParameters(atSize, height, linesAtSize, rect.Height - height, rect.Width - width);
			}

			FontSizeParameters max = GetHeight(searchParams.Max);

			if (max.heightDiff >= 0 && max.widthDiff >= 0) {
				finalLines = max.linesAtSize;
				finalHeight = max.height;
				return max.fontSize;
			}

			FontSizeParameters min = GetHeight(searchParams.Min);

			while (max.fontSize - min.fontSize > searchParams.Eps) {
				FontSizeParameters midpoint = GetHeight((max.fontSize + min.fontSize) / 2f);
				if (midpoint.heightDiff >= 0 && midpoint.widthDiff >= 0) {
					min = midpoint;
					if (midpoint.heightDiff == 0 && midpoint.widthDiff >= 0) break;
				}
				else {
					max = midpoint;
				}
				//Console.WriteLine($"({midpoint.heightDiff}, {midpoint.widthDiff}) => {max.fontSize} - {min.fontSize} = {max.fontSize - min.fontSize} >= {epsilon} ({max.fontSize - min.fontSize >= epsilon})");
			}

			finalLines = min.linesAtSize;
			finalHeight = min.height;
			return min.fontSize;
		}

		public static float FindFontSize(
			ISharpGraphicsData graphicsData,
			Rectangle rect, RichString text,
			ParagraphSpecification paragraphSpec, FontSizeSearchParams searchParams, TextHeightStrategy heightStrategy,
			bool indentFirstLine) {

			return FindFontSize(graphicsData, rect, text, paragraphSpec, searchParams, heightStrategy, indentFirstLine, out _, out _);
		}

		private class FontSizeParametersSingleLine {
			public readonly float fontSize;
			public readonly float heightDiff;
			public readonly float widthDiff;
			public FontSizeParametersSingleLine(float fontSize, float heightDiff, float widthDiff) {
				this.fontSize = fontSize;
				this.heightDiff = heightDiff;
				this.widthDiff = widthDiff;
			}
		}

		public static float FindFontSize(
			ISharpGraphicsData graphicsData,
			Rectangle rect, RichString text,
			ParagraphSpecification paragraphSpec, FontSizeSearchParams searchParams, TextHeightStrategy heightStrategy) {

			FontSizeParametersSingleLine GetHeight(float atSize) {
				float height = TextHeightUtils.GetHeight(graphicsData, text, atSize, paragraphSpec.GetLineHeight(atSize), heightStrategy);
				float width = graphicsData.GetWidth(text, atSize);
				return new FontSizeParametersSingleLine(atSize, rect.Height - height, rect.Width - width);
			}

			FontSizeParametersSingleLine max = GetHeight(searchParams.Max);

			if (max.heightDiff >= 0 && max.widthDiff >= 0) {
				return max.fontSize;
			}

			FontSizeParametersSingleLine min = GetHeight(searchParams.Min);

			while (max.fontSize - min.fontSize > searchParams.Eps) {
				FontSizeParametersSingleLine midpoint = GetHeight((max.fontSize + min.fontSize) / 2f);
				if (midpoint.heightDiff >= 0 && midpoint.widthDiff >= 0) {
					min = midpoint;
					if (midpoint.heightDiff == 0 && midpoint.widthDiff >= 0) break;
				}
				else {
					max = midpoint;
				}
				//Console.WriteLine($"({midpoint.heightDiff}, {midpoint.widthDiff}) => {max.fontSize} - {min.fontSize} = {max.fontSize - min.fontSize} >= {epsilon} ({max.fontSize - min.fontSize >= epsilon})");
			}

			return min.fontSize;
		}
	}

	public static class TextFittingUtils {

		/// <summary></summary>
		/// <exception cref="InvalidOperationException"></exception>
		public static ISharpCanvas FitRichText(this ISharpCanvas canvas, Rectangle rect, RichString richText, ParagraphSpecification paragraphSpec, FontSizeSearchParams searchParams, Justification justification, Alignment alignment, TextHeightStrategy heightStrategy, bool indentFirstLine) {
			float finalSpec = TextFitting.FindFontSize(canvas, rect, richText, paragraphSpec, searchParams, heightStrategy, indentFirstLine, out RichString[][] splitLines, out _);
			return canvas.DrawRichText(rect, splitLines, finalSpec, paragraphSpec, justification, alignment, heightStrategy, indentFirstLine);
		}

		/// <summary></summary>
		/// <exception cref="InvalidOperationException"></exception>
		public static ISharpCanvas FitRichTextLine(this ISharpCanvas canvas, Rectangle rect, RichString text, ParagraphSpecification paragraphSpec, FontSizeSearchParams searchParams, Justification justification, Alignment alignment, TextHeightStrategy heightStrategy) {
			float finalSize = TextFitting.FindFontSize(canvas, rect, text, paragraphSpec, searchParams, heightStrategy);

			canvas.SaveState();
			canvas.SetTextSize(finalSize);
			canvas.DrawRichText(rect, text, justification, alignment, heightStrategy);
			canvas.RestoreState();

			return canvas;
		}

		/// <summary></summary>
		/// <exception cref="InvalidOperationException"></exception>
		public static ISharpCanvas FitRichTextLine(this ISharpCanvas canvas, Rectangle rect, RichString text, FontSizeSearchParams searchParams, Justification justification, Alignment alignment, TextHeightStrategy heightStrategy) {
			ParagraphSpecification paraSpec = new ParagraphSpecification(1f, 0f, 0f, 0f);
			return canvas.FitRichTextLine(rect, text, paraSpec, searchParams, justification, alignment, heightStrategy);
		}

		#region Plain String Overloads
		/// <summary></summary>
		/// <exception cref="InvalidOperationException"></exception>
		public static ISharpCanvas FitText(this ISharpCanvas canvas, Rectangle rect, string text, ParagraphSpecification paragraphSpec, FontSizeSearchParams searchParams, Justification justification, Alignment alignment, TextHeightStrategy heightStrategy, bool indentFirstLine) {
			RichString richText = RichString.Create(text, canvas.GetTextFormat());
			return canvas.FitRichText(rect, richText, paragraphSpec, searchParams, justification, alignment, heightStrategy, indentFirstLine);
		}

		/// <summary></summary>
		/// <exception cref="InvalidOperationException"></exception>
		public static ISharpCanvas FitTextLine(this ISharpCanvas canvas, Rectangle rect, string text, ParagraphSpecification paragraphSpec, FontSizeSearchParams searchParams, Justification justification, Alignment alignment, TextHeightStrategy heightStrategy) {
			RichString richText = RichString.Create(text, canvas.GetTextFormat());
			return canvas.FitRichTextLine(rect, richText, paragraphSpec, searchParams, justification, alignment, heightStrategy);
		}

		/// <summary></summary>
		/// <exception cref="InvalidOperationException"></exception>
		public static ISharpCanvas FitTextLine(this ISharpCanvas canvas, Rectangle rect, string text, FontSizeSearchParams searchParams, Justification justification, Alignment alignment, TextHeightStrategy heightStrategy) {
			RichString richText = RichString.Create(text, canvas.GetTextFormat());
			return canvas.FitRichTextLine(rect, richText, searchParams, justification, alignment, heightStrategy);
		}
		#endregion
	}

	public readonly struct FontSizeSearchParams {
		public readonly float Min;
		public readonly float Max;
		public readonly float Eps;

		public FontSizeSearchParams(float min, float max, float eps) {
			this.Min = min;
			this.Max = max;
			this.Eps = eps;
		}
	}

}