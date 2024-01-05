using SharpSheets.Evaluations;
using SharpSheets.Utilities;
using SharpSheets.Layouts;
using System.Collections.Generic;
using System.Linq;
using SharpSheets.Canvas;
using SharpSheets.Canvas.Text;
using SharpSheets.Markup.Canvas;

namespace SharpSheets.Markup.Elements {

	public static class TextElementUtils {

		/// <summary></summary>
		/// <exception cref="MarkupCanvasStateException"></exception>
		/// <exception cref="EvaluationCalculationException"></exception>
		public static void ApplyGraphicsParameters(IStyledElement element, MarkupCanvas canvas) {
			// If we have a clip path, apply it
			if (element.StyleSheet.ClipPath != null) {
				element.StyleSheet.ClipPath.Apply(canvas);
			}

			canvas.SetTextFormatAndSize(element.StyleSheet.FontStyle, element.StyleSheet.FontSize);

			TextRenderingMode mode = TextRenderingMode.INVISIBLE;

			if (element.StyleSheet.TextColor != null) {
				mode |= TextRenderingMode.FILL;

				canvas.SetTextColor(element.StyleSheet.TextColor);
			}

			if (element.StyleSheet.Stroke != null) {
				bool stroke;
				if (element.StyleSheet.Stroke is SolidPaint solidPaint) {
					ColorExpression strokeColor = solidPaint.Color;
					if (element.StyleSheet.StrokeOpacity != null) {
						strokeColor = strokeColor.WithOpacity(element.StyleSheet.StrokeOpacity);
					}
					canvas.SetStrokeColor(strokeColor);
					stroke = (canvas.GetStrokePaint() is CanvasSolidPaint strokePaint) ? strokePaint.Color.A > 0 : true;
				}
				else {
					element.StyleSheet.Stroke.Apply(canvas, PaintApplication.STROKE);
					stroke = true;
				}

				if (element.StyleSheet.StrokeLineCap != null) { canvas.SetLineCapStyle(element.StyleSheet.StrokeLineCap); }
				if (element.StyleSheet.StrokeLineJoin != null) { canvas.SetLineJoinStyle(element.StyleSheet.StrokeLineJoin); }
				if (element.StyleSheet.StrokeMiterLimit != null) { canvas.SetMiterLimit(element.StyleSheet.StrokeMiterLimit); }
				if (element.StyleSheet.StrokeWidth != null) { canvas.SetLineWidth(element.StyleSheet.StrokeWidth); }

				if (stroke) { mode |= TextRenderingMode.STROKE; }
			}

			canvas.SetTextRenderingMode(mode);
		}

	}

	/// <summary>
	/// This element renders text, either directly, or from &lt;tspan&gt; and &lt;textPath&gt;
	/// children.
	/// </summary>
	public class Text : IDrawableElement {

		public string? ID { get; }
		public StyleSheet StyleSheet { get; }

		private readonly FloatExpression x;
		private readonly FloatExpression y;
		private readonly FloatExpression? dx;
		private readonly FloatExpression? dy;
		// rotate?

		private readonly ITextPiece[] textContent;

		/// <summary>
		/// Constructor for Text.
		/// </summary>
		/// <param name="_id" default="null">A unique name for this element.</param>
		/// <param name="styleSheet">StyleSheet for this element.</param>
		/// <param name="_x" default="0">The x coordinate for the starting point of the text baseline.</param>
		/// <param name="_y" default="0">The y coordinate for the starting point of the text baseline.</param>
		/// <param name="_dx" default="null">An optional horizontal offset for the text start position.</param>
		/// <param name="_dy" default="null">An optional vertical offset for the text start position.</param>
		/// <param name="textContent">Child textual elements of this text element.</param>
		public Text(string? _id, StyleSheet styleSheet, XLengthExpression _x, YLengthExpression _y, XLengthExpression? _dx, YLengthExpression? _dy, IEnumerable<ITextPiece> textContent) {
			ID = _id;
			StyleSheet = styleSheet;
			this.x = _x;
			this.y = _y;
			this.dx = _dx;
			this.dy = _dy;
			this.textContent = textContent.ToArray();
		}

		public void Draw(MarkupCanvas canvas) {
			canvas.SaveState();

			if (!StyleSheet.Enabled.Evaluate(canvas.Environment)) {
				return;
			}

			// Does this work?
			if (StyleSheet.DrawingCoords != null) {
				canvas.SetDrawingCoords(StyleSheet.DrawingCoords); // TODO Are we still using this?
			}

			// Apply any transform we may have been given
			if (StyleSheet.Transform != null) {
				canvas.ApplyTransform(StyleSheet.Transform);
			}

			// Need to apply text style information from StyleSheet
			TextElementUtils.ApplyGraphicsParameters(this, canvas);

			TextAnchor anchor = StyleSheet.TextAnchor?.Evaluate(canvas.Environment) ?? TextAnchor.Start;

			DrawPoint start = canvas.TransformPoint(x ?? 0, y ?? 0);
			Vector normal = new Vector(0, 1);
			Vector offset = canvas.TransformVector(dx ?? 0, dy ?? 0);
			start += offset;

			Queue<ITextPiece> pieces = new Queue<ITextPiece>(textContent.Where(s => s.StyleSheet.Enabled.Evaluate(canvas.Environment)));

			bool first = true;

			while (pieces.Count > 0) {
				List<TSpan> spans = new List<TSpan>();
				while (pieces.Count > 0 && pieces.Peek() is TSpan peekedSpan) {
					if (spans.Count > 0 && (peekedSpan.x != null || peekedSpan.y != null)) {
						break;
					}
					else {
						spans.Add((TSpan)pieces.Dequeue());
					}
				}
				if (spans.Count > 0) {

					DrawPoint startInvert = canvas.InverseTransformPoint(start);
					start = canvas.TransformPoint(new DrawPointExpression(spans[0].x ?? startInvert.X, spans[0].y ?? startInvert.Y));

					Vector direction = new Vector(normal.Y, -normal.X);
					float directionRotation = direction.Rotation();

					string[] texts = new string[spans.Count];
					float[] textWidths = new float[spans.Count];
					float[] dxs = new float[spans.Count];
					float totalWidth = 0f;
					for (int i = 0; i < spans.Count; i++) {
						texts[i] = spans[i].text.Evaluate(canvas.Environment);

						if (first) {
							texts[i] = texts[i].TrimStart();
							first = false;
						}
						if (i == spans.Count - 1 && pieces.Count == 0) { // Last text piece
							texts[i] = texts[i].TrimEnd();
						}

						textWidths[i] = canvas.GetWidth((StringExpression)texts[i], spans[i].StyleSheet.FontStyle, spans[i].StyleSheet.FontSize);
						dxs[i] = spans[i].dx != null ? canvas.TransformLength(spans[i].dx!) : 0f;
						totalWidth += textWidths[i] + dxs[i];
					}

					float startOffset;
					if (anchor == TextAnchor.Start) {
						startOffset = 0f;
					}
					else if (anchor == TextAnchor.Middle) {
						startOffset = totalWidth / 2;
					}
					else { // anchor == TextAnchor.End
						startOffset = totalWidth;
					}

					start -= startOffset * direction;

					for (int i = 0; i < spans.Count; i++) {
						TSpan span = spans[i];

						float dy = spans[i].dy != null ? canvas.TransformLength(spans[i].dy!) : 0f;
						start += new Vector(dxs[i], dy).Rotate(directionRotation);

						canvas.SaveState();
						TextElementUtils.ApplyGraphicsParameters(span, canvas);

						canvas.ApplyTransform(Transform.Rotate(directionRotation, start.X, start.Y));
						canvas.DrawTextExact(texts[i], start.X, start.Y);

						canvas.RestoreState();

						start += new Vector(textWidths[i], 0f).Rotate(directionRotation);
					}
				}
				else if (pieces.Count > 0 && pieces.Peek() is TextPath) {
					TextPath textPath = (TextPath)pieces.Dequeue();

					bool trimStart = first;
					bool trimEnd = pieces.Count == 0;
					first = false;

					textPath.Draw(canvas, out DrawPointExpression? end, out VectorExpression? normalExpression, trimStart, trimEnd);

					if (end != null) { start = canvas.TransformPoint(end); }
					normal = canvas.Evaluate(normalExpression, new Vector(0, 1));
				}
			}

			canvas.RestoreState();
		}
	}

	/// <summary>
	/// This element renders text inside a rectangular area, either directly, or from &lt;tspan&gt;
	/// children. The text can be dynamically resized to fit the available area, or drawn with
	/// a fixed font size. The justification and alignment of the text can also be specified.
	/// </summary>
	public class TextRect : IDrawableElement {

		public string? ID { get; }
		public StyleSheet StyleSheet { get; }

		private readonly RectangleExpression area;
		private readonly BoolExpression fitText;
		private readonly FloatExpression? minFontSize;
		private readonly FloatExpression? maxFontSize;
		private readonly EnumExpression<Justification> justification;
		private readonly EnumExpression<SharpSheets.Canvas.Text.Alignment> alignment;
		private readonly EnumExpression<TextHeightStrategy> heightStrategy;
		private readonly FloatExpression lineSpacing;
		private readonly FloatExpression paragraphSpacing;
		private readonly BoolExpression singleLine;

		private readonly TSpan[] textContent;

		/// <summary>
		/// Constructor for TextRect.
		/// </summary>
		/// <param name="_id" default="null">A unique name for this element.</param>
		/// <param name="styleSheet">StyleSheet for this element.</param>
		/// <param name="_x" default="0">The x coordinate for the text area, corresponding
		/// to the left edge of the rectangle.</param>
		/// <param name="_y" default="0">The y coordinate for the text area, corresponding
		/// to the bottom edge of the rectangle.</param>
		/// <param name="_width" default="$width">The width of the text area rectangle.</param>
		/// <param name="_height" default="$height">The height of the text area rectangle.</param>
		/// <param name="_fit_text" default="false">A flag to indicate that the text should be
		/// dynamically resized to fit the available area, within the minimum and maximum
		/// font sizes specified.</param>
		/// <param name="min_font_size" default="null">The minimum font size to use if the
		/// text is to be dynamically resized.</param>
		/// <param name="max_font_size" default="null">The maximum font size to use if the
		/// text is to be dynamically resized.</param>
		/// <param name="justification" default="LEFT">The horizontal justification for the
		/// text within the text area rectangle.</param>
		/// <param name="alignment" default="BOTTOM">The vertical alignment for the
		/// text within the text area rectangle.</param>
		/// <param name="height_strategy" default="LineHeightBaseline">The height calculation
		/// strategy to use when arranging the text within the text area.</param>
		/// <param name="line_spacing" default="1.0">The line spacing, which is the distance
		/// between successive text baselines, measured in multiples of the current fontsize.</param>
		/// <param name="paragraph_spacing" default="0.0">The spacing to be used between paragraphs
		/// of text, measured in points. This spacing is in addition to any line spacing.</param>
		/// <param name="_single_line" default="false">A flag to indicate that the text should
		/// be drawn all on one line.</param>
		/// <param name="textContent">Child tspan elements of this text element.</param>
		public TextRect(string? _id, StyleSheet styleSheet,
			XLengthExpression _x,
			YLengthExpression _y,
			XLengthExpression _width,
			YLengthExpression _height,
			BoolExpression _fit_text, FloatExpression? min_font_size, FloatExpression? max_font_size,
			EnumExpression<Justification> justification, EnumExpression<SharpSheets.Canvas.Text.Alignment> alignment,
			EnumExpression<TextHeightStrategy> height_strategy,
			FloatExpression line_spacing, FloatExpression paragraph_spacing,
			BoolExpression _single_line,
			IEnumerable<TSpan> textContent) {

			ID = _id;
			StyleSheet = styleSheet;
			this.area = new RectangleExpression(_x, _y, _width, _height);
			this.textContent = textContent.ToArray();
			this.fitText = _fit_text;
			this.minFontSize = min_font_size;
			this.maxFontSize = max_font_size;
			this.justification = justification;
			this.alignment = alignment;
			this.heightStrategy = height_strategy;
			this.lineSpacing = line_spacing;
			this.paragraphSpacing = paragraph_spacing;
			this.singleLine = _single_line;
		}

		public void Draw(MarkupCanvas canvas) {
			canvas.SaveState();

			if (!StyleSheet.Enabled.Evaluate(canvas.Environment)) {
				return;
			}

			// Does this work?
			if (StyleSheet.DrawingCoords != null) {
				canvas.SetDrawingCoords(StyleSheet.DrawingCoords);
			}

			// Apply any transform we may have been given
			if (StyleSheet.Transform != null) {
				canvas.ApplyTransform(StyleSheet.Transform);
			}

			// Need to apply text style information from StyleSheet
			TextElementUtils.ApplyGraphicsParameters(this, canvas);

			List<TSpan> spans = textContent.Where(s => s.StyleSheet.Enabled.Evaluate(canvas.Environment)).ToList();

			RichString[] spanStrings = new RichString[spans.Count];
			for (int i = 0; i < spans.Count; i++) {
				if (spans[i].StyleSheet.Enabled.Evaluate(canvas.Environment)) {
					string spanText = spans[i].text.Evaluate(canvas.Environment);
					TextFormat spanFormat = spans[i].StyleSheet.FontStyle?.Evaluate(canvas.Environment) ?? canvas.GetTextFormat();

					if (i == 0) {
						spanText = spanText.TrimStart();
					}
					if (i == spans.Count - 1) {
						spanText = spanText.TrimEnd();
					}

					spanStrings[i] = RichString.Create(spanText, spanFormat);
				}
			}

			RichString formattedText = RichString.Join((RichString)"", spanStrings);

			/*
			Console.WriteLine(canvas.GetWidth(new StringExpression(formattedText.Text), TextFormat.REGULAR, 6f));
			string expr = $"width(\"{formattedText.Text}\", \"{"regular"}\", 6)";
			EvaluationNode node = Evaluation.Parse(expr, canvas.Environment);
			object result = node.Evaluate(canvas.Environment);
			Console.WriteLine(result);
			*/

			bool fitText = this.fitText?.Evaluate(canvas.Environment) ?? false;
			bool singleLine = this.singleLine?.Evaluate(canvas.Environment) ?? false;

			if (fitText) {
				if (singleLine) {
					canvas.FitRichTextLine(area, new RichStringExpression(formattedText), maxFontSize, lineSpacing, justification, alignment, heightStrategy);
				}
				else {
					canvas.FitRichText(area, new RichStringExpression(formattedText), minFontSize, maxFontSize, lineSpacing, paragraphSpacing, justification, alignment, heightStrategy);
				}
			}
			else {
				if (singleLine) {
					canvas.DrawRichText(area, new RichStringExpression(formattedText), justification, alignment, heightStrategy);
				}
				else {
					canvas.DrawRichText(area, new RichStringExpression(formattedText), lineSpacing, paragraphSpacing, justification, alignment, heightStrategy);
				}
			}

			if (canvas.CollectingDiagnostics) { canvas.RegisterArea(this, area); }

			canvas.RestoreState();
		}
	}

	public interface ITextPiece : IStyledElement { }

	/// <summary>
	/// A text span element, used to provide additional styling for a single text segment
	/// within another text element.
	/// </summary>
	public class TSpan : ITextPiece {

		public string? ID { get; }
		public StyleSheet StyleSheet { get; }

		public readonly FloatExpression? x;
		public readonly FloatExpression? y;
		public readonly FloatExpression? dx;
		public readonly FloatExpression? dy;
		// rotate?

		public readonly TextExpression text;

		/// <summary>
		/// Constructor for TSpan.
		/// </summary>
		/// <param name="_id" default="null">A unique name for this element.</param>
		/// <param name="styleSheet">StyleSheet for this element.</param>
		/// /// <param name="_x" default="null">The x coordinate for the starting point of the text baseline.</param>
		/// <param name="_y" default="null">The y coordinate for the starting point of the text baseline.</param>
		/// <param name="_dx" default="null">Shifts the text position horizontally from the previous text element.</param>
		/// <param name="_dy" default="null">Shifts the text position vertically from the previous text element.</param>
		/// <param name="text"></param>
		public TSpan(string? _id, StyleSheet styleSheet, XLengthExpression? _x, YLengthExpression? _y, FloatExpression? _dx, FloatExpression? _dy, TextExpression text) {
			// TODO What arg type should "text" be?
			ID = _id;
			StyleSheet = styleSheet;
			this.x = _x;
			this.y = _y;
			this.dx = _dx;
			this.dy = _dy;
			this.text = text;
		}
	}

	/// <summary>
	/// Indicates a side of a path.
	/// </summary>
	public enum PathSide {
		/// <summary>
		/// The left side of the path.
		/// </summary>
		LEFT,
		/// <summary>
		/// The right side of the path.
		/// </summary>
		RIGHT
	}

	/// <summary>
	/// Indicates if and how a path should be continued once its end has
	/// been reached.
	/// </summary>
	public enum ContinueStyle {
		/// <summary>
		/// The path should not be continued, and any additional content
		/// should not be rendered.
		/// </summary>
		NONE,
		/// <summary>
		/// The path should be continued from its end position, using it's
		/// final direction, and any additional content should be rendered
		/// in this direction.
		/// </summary>
		CONTINUE,
		/// <summary>
		/// The path should be looped, with any additional content after the
		/// end of the path has been reached being continued from the start
		/// of the path.
		/// </summary>
		LOOP
	}

	/// <summary>
	/// This element draws text along a specified path.
	/// </summary>
	public class TextPath : ITextPiece {

		public string? ID { get; }
		public StyleSheet StyleSheet { get; }

		private readonly IShapeElement pathElem;
		private readonly LengthExpression startOffset; // TODO This should be some kind of "Distance", that can be a percentage
		private readonly EnumExpression<PathSide> side;
		// spacing?
		private readonly EnumExpression<ContinueStyle> continuePastEnd;

		private readonly TSpan[] spans;

		/// <summary>
		/// Constructor for TextPath.
		/// </summary>
		/// <param name="_id" default="null">A unique name for this element.</param>
		/// <param name="styleSheet">StyleSheet for this element.</param>
		/// <param name="_path">A path along which the text should be rendered.</param>
		/// <param name="_startOffset" default="0">An offset from the start of the path,
		/// at which the text contents should begin rendering. This may be expressed as
		/// either an absolute value, or as a percentage.</param>
		/// <param name="_side" default="LEFT">The side of the path on which the text
		/// should be rendered.</param>
		/// <param name="_continue" default="NONE">The strategy to use when the text length
		/// is greater than the path length. The text may be continued, such that it is
		/// all rendered.</param>
		/// <param name="spans">Child tspan elements of this text path element.</param>
		public TextPath(string? _id, StyleSheet styleSheet, IShapeElement _path, LengthExpression _startOffset, EnumExpression<PathSide> _side, EnumExpression<ContinueStyle> _continue, IEnumerable<TSpan> spans) {
			this.ID = _id;
			this.StyleSheet = styleSheet;
			this.pathElem = _path;
			this.startOffset = _startOffset;
			this.side = _side;
			this.continuePastEnd = _continue;
			this.spans = spans.ToArray();
		}

		private static Vector GetDirection(Vector normal, PathSide side) {
			Vector cNorm = side == PathSide.LEFT ? normal : new Vector(-normal.X, -normal.Y);
			return new Vector(cNorm.Y, -cNorm.X); // Text direction
		}

		/// <summary></summary>
		/// <exception cref="EvaluationCalculationException"></exception>
		/// <exception cref="MarkupCanvasStateException"></exception>
		/// <exception cref="InvalidOperationException"></exception>
		public void Draw(MarkupCanvas canvas, out DrawPointExpression? end, out VectorExpression? endNormal, bool trimStart, bool trimEnd) {

			IPathCalculator? path = pathElem?.GetPath(canvas);

			if (path == null) {
				end = null;
				endNormal = null;
				return;
			}

			Rectangle? bounds = null;

			Length startOffsetLength = canvas.Evaluate(this.startOffset, Length.Zero);
			float startOffsetAbs = startOffsetLength.GetLength(path.Length);

			float startOffset = this.startOffset != null ? canvas.TransformLength(startOffsetAbs) : 0f;
			PathSide side = canvas.Evaluate(this.side, PathSide.LEFT);
			ContinueStyle continuePastEnd = canvas.Evaluate(this.continuePastEnd, ContinueStyle.NONE);

			float position;
			int direction;
			if (side == PathSide.LEFT) {
				position = startOffset;
				direction = 1;
			}
			else { // side == PathSide.RIGHT
				position = path.Length - startOffset;
				direction = -1;
			}

			TextAnchor textAnchor = canvas.Evaluate(StyleSheet.TextAnchor, TextAnchor.Start);
			if (textAnchor == TextAnchor.End || textAnchor == TextAnchor.Middle) {
				float textLength = 0f;
				foreach (TSpan span in spans) {
					float dx = span.dx != null ? canvas.TransformLength(span.dx) : 0f;
					float textWidth = canvas.GetWidth(span.text, span.StyleSheet.FontStyle, span.StyleSheet.FontSize);
					textLength += dx + textWidth;
				}

				if (textAnchor == TextAnchor.End) {
					//position += direction * (path.Length - textLength - 1e-9f); // Small offset to avoid characters being missed at very end (necessary?)
					position -= direction * textLength; // Small offset to avoid characters being missed at very end (necessary?)
				}
				else { // textAnchor == TextAnchor.Middle
					//position += direction * ((path.Length - textLength) / 2f);
					position -= direction * (textLength / 2f);
				}
			}

			canvas.SaveState();

			// Need to apply text style information from StyleSheet
			TextElementUtils.ApplyGraphicsParameters(this, canvas);

			//bool reachedEnd = false;
			DrawPoint? positionLocation = null;
			Vector? positionNormal = null;
			Vector? positionDirection = null;
			//foreach(TSpan span in spans) {
			for (int k = 0; k < spans.Length; k++) {
				TSpan span = spans[k];

				canvas.SaveState();
				TextElementUtils.ApplyGraphicsParameters(span, canvas);

				float dx = span.dx != null ? canvas.TransformLength(span.dx) : 0f;
				float dy = span.dy != null ? canvas.TransformLength(span.dy) : 0f;

				position += direction * dx;

				string text = span.text.Evaluate(canvas.Environment);

				if (k == 0 && trimStart) {
					text = text.TrimStart();
				}
				if (k == spans.Length - 1 && trimEnd) {
					text = text.TrimEnd();
				}

				for (int i = 0; i < text.Length; i++) {
					string c = text[i].ToString();
					float charWidth = canvas.GetWidth((StringExpression)c, span.StyleSheet.FontStyle, span.StyleSheet.FontSize);
					float pointPosition = position + direction * 0.5f * charWidth;
					if (continuePastEnd == ContinueStyle.LOOP) {
						//Console.Write($"Position: {pointPosition}, ");
						//pointPosition %= path.Length;
						pointPosition = (pointPosition % path.Length + path.Length) % path.Length;
						//Console.WriteLine($"After: {pointPosition}");
					}
					DrawPoint? point = path.PointAt(pointPosition, out Vector? normal);

					Vector textDirection;

					if (point.HasValue) {
						textDirection = GetDirection(normal!.Value, side);
					}
					else if (continuePastEnd == ContinueStyle.CONTINUE && positionLocation.HasValue) {
						point = positionLocation;
						normal = positionNormal;
						textDirection = positionDirection!.Value;
					}
					else {
						// Just update for next char and move on
						position += direction * charWidth;
						continue;
					}

					float directionRotation = textDirection.Rotation();

					DrawPoint xy = point.Value + new Vector(-0.5f * charWidth, dy).Rotate(directionRotation);

					canvas.SaveState();
					canvas.ApplyTransform(Transform.Rotate(directionRotation, xy.X, xy.Y));
					canvas.DrawTextExact(c, xy.X, xy.Y);
					//canvas.DrawTextExact(c, point.Value.X, point.Value.Y);
					canvas.RestoreState();

					// Update for next char
					position += direction * charWidth;

					positionLocation = point + (textDirection * charWidth);
					positionNormal = normal;
					positionDirection = textDirection;

					if (canvas.CollectingDiagnostics) {
						if (bounds == null) {
							bounds = new Rectangle(point.Value.X, point.Value.Y, 0f, 0f);
						}
						bounds = bounds.Include(point.Value);
						bounds = bounds.Include(point.Value + (normal!.Value * canvas.GetAscent(new StringExpression(c), span.StyleSheet.FontStyle, span.StyleSheet.FontSize)));
					}
				}

				canvas.RestoreState();

				//if (reachedEnd) { break; }
			}

			DrawPoint? finalPoint = path.PointAt(position, out Vector? finalNorm);
			if (!finalPoint.HasValue && positionLocation.HasValue) {
				finalPoint = positionLocation;
				finalNorm = positionNormal;
			}
			else if (!finalPoint.HasValue) {
				finalPoint = path.PointAt(path.Length, out finalNorm);
			}

			end = canvas.InverseTransformPoint(finalPoint!.Value);
			if (side == PathSide.LEFT) {
				endNormal = finalNorm!.Value;
			}
			else {
				endNormal = new Vector(-finalNorm!.Value.X, -finalNorm!.Value.Y);
			}

			if (canvas.CollectingDiagnostics && bounds is Rectangle) {
				canvas.RegisterArea(this, bounds);
				//Console.WriteLine(bounds);
			}

			canvas.RestoreState();
		}
	}

}
