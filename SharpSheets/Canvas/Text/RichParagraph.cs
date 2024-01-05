using SharpSheets.Canvas;
using System.Collections.Generic;

namespace SharpSheets.Canvas.Text {

	// TODO Is the layout here properly taking account of TextHeightStrategy, and being drawn correctly downstream?

	public class RichParagraph {

		private readonly RichString[] tokens;
		private readonly RichString delimiter = new RichString(" ", TextFormat.REGULAR);

		public int TokenCount => tokens.Length;

		private RichParagraph(RichString[] tokens) {
			this.tokens = tokens;
		}

		public RichParagraph(RichString text) {
			this.tokens = text.Length > 0 ? text.Split(' ') : Array.Empty<RichString>();
		}

		private RichParagraph Clone() {
			// What is going on here? This is not a proper Clone.
			return new RichParagraph(tokens);
		}

		public RichString[] GetLines(ISharpGraphicsData graphicsData, float width, float fontSize, ParagraphSpecification paragraphSpec, bool indent) {
			float delimWidth = graphicsData.GetWidth(delimiter, fontSize);

			float[] widths = new float[tokens.Length];
			for (int i = 0; i < tokens.Length; i++) {
				widths[i] = graphicsData.GetWidth(tokens[i], fontSize);
			}

			if (tokens.Length > 0) {
				List<RichString> lines = new List<RichString>();

				int end = tokens.Length;
				int start = 0;

				while (start < end) {
					//Console.WriteLine($"Next line: start {start} -> {end}");

					//float lineIndent = (lines.Count > 0 || (!indentFirst && paragraph == 0)) ? paragraphSpec.HangingIndent : paragraphSpec.Indent;
					float lineIndent = RichStringLayout.LineIndent(0, lines.Count, indent, paragraphSpec.Indent, paragraphSpec.HangingIndent); // (lines.Count == 0 || (indentFirst && paragraph == 0)) ? paragraphSpec.Indent : paragraphSpec.HangingIndent;
					float totalWidth = lineIndent + widths[start];
					int head = start + 1;

					//Console.WriteLine($"Starting width: {totalWidth} (head {head})");

					while (head < end) {
						float headWidth = widths[head];

						if (totalWidth + delimWidth + headWidth <= width) {
							totalWidth += delimWidth + headWidth;
							head++;
							//Console.WriteLine($"Intermediate width... {totalWidth} (head {head})");
						}
						else {
							break;
						}
					}

					//Console.WriteLine($"Line: {start} - {head}");

					lines.Add(JoinArray(tokens, start, head - start, delimiter));

					start = head;
				}

				return lines.ToArray();
			}
			else {
				return Array.Empty<RichString>();
			}
		}

		public RichParagraph? SplitByHeight(ISharpGraphicsData graphicsData, float width, float height, float fontSize, ParagraphSpecification paragraphSpec, bool indent, out RichParagraph? remaining) {
			float delimWidth = graphicsData.GetWidth(delimiter, fontSize);

			float[] widths = new float[tokens.Length];
			for (int i = 0; i < tokens.Length; i++) {
				widths[i] = graphicsData.GetWidth(tokens[i], fontSize);
			}

			float lineHeight = paragraphSpec.GetLineHeight(fontSize); // paragraphSpec.FontSize * paragraphSpec.LineSpacing;

			if (tokens.Length > 0) {
				int end = tokens.Length;
				int start = 0;

				float totalHeight = 0f;
				int linesCount = 0;

				while (start < end) {

					if (totalHeight + lineHeight > height) {
						SplitAtPosition(start, out RichParagraph? first, out remaining);
						return first;
					}

					float lineIndent = RichStringLayout.LineIndent(0, linesCount, indent, paragraphSpec.Indent, paragraphSpec.HangingIndent);
					float totalWidth = lineIndent + widths[start];
					int head = start + 1;

					while (head < end) {
						float headWidth = widths[head];

						if (totalWidth + delimWidth + headWidth <= width) {
							totalWidth += delimWidth + headWidth;
							head++;
						}
						else {
							break;
						}
					}

					linesCount++;
					totalHeight += lineHeight;
					start = head;
				}
			}
			else {
				if (lineHeight > height) {
					remaining = Clone();
					return null;
				}
			}

			remaining = null;
			return Clone();
		}

		private void SplitAtPosition(int token, out RichParagraph? a, out RichParagraph? b) {
			if (token > 0) {
				RichString[] partsA = new RichString[token];
				for (int k = 0; k < token; k++) {
					partsA[k] = tokens[k];
				}
				a = new RichParagraph(partsA);
			}
			else {
				a = null;
			}

			if (tokens.Length > token) {
				RichString[] partsB = new RichString[tokens.Length - token];
				for (int k = token; k < tokens.Length; k++) {
					partsB[k - token] = tokens[k];
				}
				b = new RichParagraph(partsB);
			}
			else {
				b = null;
			}
		}

		private static RichString JoinArray(RichString[] array, int start, int length, RichString delimiter) {
			int end = start + length;
			//if (end >= array.Length) { end = array.Length; }

			int charCount = 0;
			for (int i = start; i < end; i++) {
				if (i != start) { charCount += delimiter.Length; }
				charCount += array[i].Length;
			}

			char[] chars = new char[charCount];
			TextFormat[] formats = new TextFormat[charCount];

			int charIndex = 0;
			for (int i = start; i < end; i++) {
				if (i != start) {
					for (int d = 0; d < delimiter.Length; d++) {
						chars[charIndex] = delimiter.chars[d];
						formats[charIndex] = delimiter.formats[d];
						charIndex++;
					}
				}

				for (int c = 0; c < array[i].Length; c++) {
					chars[charIndex] = array[i].chars[c];
					formats[charIndex] = array[i].formats[c];
					charIndex++;
				}
			}

			return new RichString(chars, formats);
		}

		public override string ToString() {
			return JoinArray(tokens, 0, tokens.Length, delimiter).Formatted;
		}

	}

	/// <summary>
	/// Stores information about <see cref="RichString"/> sentence splitting and token widths for more efficient line splitting calculations.
	/// </summary>
	public class RichParagraphs {

		private readonly RichString[][] parts;
		private readonly RichString delimiter = new RichString(" ", TextFormat.REGULAR);

		public int ParagraphCount { get; }
		public int TokenCount { get; }

		//public readonly RichString[] Paragraphs;

		private RichParagraphs(RichString[][] parts, int tokenCount) {
			this.parts = parts;
			ParagraphCount = parts.Length;
			TokenCount = tokenCount;
		}

		public RichParagraphs(RichString[] paragraphs) {
			int tokenCount = 0;
			this.parts = new RichString[paragraphs.Length][];
			for (int i = 0; i < paragraphs.Length; i++) {
				if (paragraphs[i].Length > 0) {
					this.parts[i] = paragraphs[i].Split(' ');
					tokenCount += this.parts[i].Length;
				}
				else {
					this.parts[i] = Array.Empty<RichString>();
				}
			}

			//this.Paragraphs = paragraphs;
			ParagraphCount = this.parts.Length;
			TokenCount = tokenCount;
		}

		public RichParagraphs(RichString text) : this(text.Split('\n')) { }

		private RichParagraphs Clone() {
			// What is going on here? This is not a proper Clone.
			return new RichParagraphs(parts, TokenCount);
		}

		public RichString[][] GetLines(ISharpGraphicsData graphicsData, float width, float fontSize, ParagraphSpecification paragraphSpec, bool indentFirst) {
			float delimWidth = graphicsData.GetWidth(delimiter, fontSize);
			
			float[][] widths = new float[parts.Length][];
			for (int i = 0; i < parts.Length; i++) {
				widths[i] = new float[parts[i].Length];
				for(int j=0; j<parts[i].Length; j++) {
					widths[i][j] = graphicsData.GetWidth(parts[i][j], fontSize);
				}
			}

			RichString[][] result = new RichString[parts.Length][];

			for (int paragraph = 0; paragraph < parts.Length; paragraph++) {
				//Console.WriteLine($"Check paragraph {paragraph} ({parts[paragraph].Length} tokens)");
				if (parts[paragraph].Length > 0) {
					List<RichString> lines = new List<RichString>();

					int end = parts[paragraph].Length;
					int start = 0;

					while (start < end) {
						//Console.WriteLine($"Next line: start {start} -> {end}");

						//float lineIndent = (lines.Count > 0 || (!indentFirst && paragraph == 0)) ? paragraphSpec.HangingIndent : paragraphSpec.Indent;
						float lineIndent = RichStringLayout.LineIndent(paragraph, lines.Count, indentFirst, paragraphSpec.Indent, paragraphSpec.HangingIndent); // (lines.Count == 0 || (indentFirst && paragraph == 0)) ? paragraphSpec.Indent : paragraphSpec.HangingIndent;
						float totalWidth = lineIndent + widths[paragraph][start];
						int head = start + 1;

						//Console.WriteLine($"Starting width: {totalWidth} (head {head})");

						while(head < end) {
							float headWidth = widths[paragraph][head];

							if(totalWidth + delimWidth + headWidth <= width) {
								totalWidth += delimWidth + headWidth;
								head++;
								//Console.WriteLine($"Intermediate width... {totalWidth} (head {head})");
							}
							else {
								break;
							}
						}

						//Console.WriteLine($"Line: {start} - {head}");

						lines.Add(JoinArray(parts[paragraph], start, head - start, delimiter));

						start = head;
					}

					result[paragraph] = lines.ToArray();
				}
				else {
					result[paragraph] = Array.Empty<RichString>();
				}
			}

			return result;
		}

		public RichParagraphs? SplitByHeight(ISharpGraphicsData graphicsData, float width, float height, float fontSize, ParagraphSpecification paragraphSpec, bool indentFirst, out RichParagraphs? remaining, out bool splitInParagraph) {
			float delimWidth = graphicsData.GetWidth(delimiter, fontSize);

			float[][] widths = new float[parts.Length][];
			for (int i = 0; i < parts.Length; i++) {
				widths[i] = new float[parts[i].Length];
				for (int j = 0; j < parts[i].Length; j++) {
					widths[i][j] = graphicsData.GetWidth(parts[i][j], fontSize);
				}
			}

			float totalHeight = 0f;
			float lineHeight = paragraphSpec.GetLineHeight(fontSize); // paragraphSpec.FontSize * paragraphSpec.LineSpacing;

			for (int paragraph = 0; paragraph < parts.Length; paragraph++) {
				if (parts[paragraph].Length > 0) {
					int end = parts[paragraph].Length;
					int start = 0;

					int linesCount = 0;

					while (start < end) {

						if(totalHeight + lineHeight > height) {
							SplitAtPosition(paragraph, start, out RichParagraphs? first, out remaining);
							splitInParagraph = start > 0;
							return first;
						}

						//float lineIndent = linesCount > 0 ? paragraphSpec.HangingIndent : paragraphSpec.Indent;
						float lineIndent = RichStringLayout.LineIndent(paragraph, linesCount, indentFirst, paragraphSpec.Indent, paragraphSpec.HangingIndent);// (linesCount == 0 || (indentFirst && paragraph == 0)) ? paragraphSpec.Indent : paragraphSpec.HangingIndent;
						float totalWidth = lineIndent + widths[paragraph][start];
						int head = start + 1;

						while (head < end) {
							float headWidth = widths[paragraph][head];

							if (totalWidth + delimWidth + headWidth <= width) {
								totalWidth += delimWidth + headWidth;
								head++;
							}
							else {
								break;
							}
						}

						linesCount++;
						totalHeight += lineHeight;
						start = head;
					}
				}
				else {
					if (totalHeight + lineHeight + paragraphSpec.ParagraphSpacing > height) {
						SplitAtPosition(paragraph, 0, out RichParagraphs? first, out remaining);
						splitInParagraph = false;
						return first;
					}
				}

				if(paragraph != parts.Length - 1) {
					totalHeight += paragraphSpec.ParagraphSpacing;
				}
			}

			remaining = null;
			splitInParagraph = false;
			return Clone();
		}

		private void SplitAtPosition(int paragraph, int token, out RichParagraphs? a, out RichParagraphs? b) {
			if (token > 0 || paragraph > 0) {
				RichString[][] partsA = new RichString[paragraph + 1][];
				int tokenCountA = 0;
				for (int i = 0; i < paragraph; i++) {
					partsA[i] = new RichString[parts[i].Length];
					for (int j = 0; j < parts[i].Length; j++) {
						partsA[i][j] = parts[i][j];
						tokenCountA++;
					}
				}
				partsA[paragraph] = new RichString[token];
				for (int k = 0; k < token; k++) {
					partsA[paragraph][k] = parts[paragraph][k];
					tokenCountA++;
				}
				a = new RichParagraphs(partsA, tokenCountA);
			}
			else {
				a = null;
			}

			if (parts.Length > paragraph) {
				RichString[][] partsB = new RichString[parts.Length - paragraph][];
				int tokenCountB = 0;
				partsB[0] = new RichString[parts[paragraph].Length - token];
				for (int k = token; k < parts[paragraph].Length; k++) {
					partsB[0][k - token] = parts[paragraph][k];
					tokenCountB++;
				}
				for (int i = paragraph + 1; i < parts.Length; i++) {
					int partsBIndex = i - paragraph;
					partsB[partsBIndex] = new RichString[parts[i].Length];
					for (int j = 0; j < parts[i].Length; j++) {
						partsB[partsBIndex][j] = parts[i][j];
						tokenCountB++;
					}
				}
				b = new RichParagraphs(partsB, tokenCountB);
			}
			else {
				b = null;
			}
		}

		private static RichString JoinArray(RichString[] array, int start, int length, RichString delimiter) {
			int end = start + length;
			//if (end >= array.Length) { end = array.Length; }

			int charCount = 0;
			for (int i = start; i < end; i++) {
				if (i != start) { charCount += delimiter.Length; }
				charCount += array[i].Length;
			}

			char[] chars = new char[charCount];
			TextFormat[] formats = new TextFormat[charCount];

			int charIndex = 0;
			for (int i = start; i < end; i++) {
				if (i != start) {
					for(int d=0; d<delimiter.Length; d++) {
						chars[charIndex] = delimiter.chars[d];
						formats[charIndex] = delimiter.formats[d];
						charIndex++;
					}
				}

				for(int c=0; c<array[i].Length; c++) {
					chars[charIndex] = array[i].chars[c];
					formats[charIndex] = array[i].formats[c];
					charIndex++;
				}
			}

			return new RichString(chars, formats);
		}

	}

}
