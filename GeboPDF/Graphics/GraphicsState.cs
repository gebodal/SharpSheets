using GeboPdf.Fonts;
using GeboPdf.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeboPdf.Graphics {

	public class PdfGraphicsState {

		/// <summary> Default colorspace: DeviceGray </summary>
		public static readonly PdfColorSpace DefaultColorspace = PdfColorSpace.DeviceGray;
		/// <summary> Default color: 0.0 (DeviceGray Black) </summary>
		public static readonly PdfColor DefaultColor = PdfGrayColor.Black;
		/// <summary> Default character spacing: 0.0 </summary>
		public static readonly float DefaultCharacterSpacing = 0.0f;
		/// <summary> Default word spacing: 0.0 </summary>
		public static readonly float DefaultWordSpacing = 0.0f;
		/// <summary> Default horizontal scaling: 100.0 </summary>
		public static readonly float DefaultHorizontalScaling = 100.0f;
		/// <summary> Default text leading: 0.0 </summary>
		public static readonly float DefaultTextLeading = 0.0f;
		/// <summary> Default font: null (None) </summary>
		public static readonly PdfFontSetting? DefaultFont = null; // There is no default value for [font size], which must be manually set in each content stream
		/// <summary> Default text rendering mode: FillText </summary>
		public static readonly TextRenderingMode DefaultTextRenderingMode = TextRenderingMode.FillText;
		/// <summary> Default text rise: 0.0 </summary>
		public static readonly float DefaultTextRise = 0.0f;
		/// <summary> Default line width: 1.0 </summary>
		public static readonly float DefaultLineWidth = 1.0f;
		/// <summary> Default line cap style: Butt </summary>
		public static readonly LineCapStyle DefaultLineCapStyle = LineCapStyle.Butt;
		/// <summary> Default line join style: Mitre </summary>
		public static readonly LineJoinStyle DefaultLineJoinStyle = LineJoinStyle.Mitre;
		/// <summary> Default miter limit: 10.0 </summary>
		public static readonly float DefaultMiterLimit = 10.0f;
		/// <summary> Default line dash pattern: [] 0 </summary>
		public static readonly PdfDashArray DefaultLineDashPattern = new PdfDashArray(null, 0f);
		/// <summary> Default alpha constant: 1.0 </summary>
		public static readonly float DefaultAlphaConstant = 1.0f;

		// Current Transformation Matrix?
		public PdfColorSpace StrokingColorSpace { get; set; } = DefaultColorspace;
		public PdfColorSpace NonStrokingColorSpace { get; set; } = DefaultColorspace;
		public PdfColor StrokingColor { get; set; } = DefaultColor;
		public PdfColor NonStrokingColor { get; set; } = DefaultColor;
		public float CharacterSpacing { get; set; } = DefaultCharacterSpacing;
		public float WordSpacing { get; set; } = DefaultWordSpacing;
		public float TextHorizontalScaling { get; set; } = DefaultHorizontalScaling;
		public float TextLeading { get; set; } = DefaultTextLeading;
		/// <summary> Array of [font size] (indirect reference and number) </summary>
		public PdfFontSetting? Font { get; set; } = DefaultFont;
		public TextRenderingMode TextRenderingMode { get; set; } = DefaultTextRenderingMode;
		// Text knockout?
		public float TextRise { get; set; } = DefaultTextRise;
		public float Linewidth { get; set; } = DefaultLineWidth;
		public LineCapStyle Linecapstyle { get; set; } = DefaultLineCapStyle;
		public LineJoinStyle Linejoinstyle { get; set; } = DefaultLineJoinStyle;
		public float Miterlimit { get; set; } = DefaultMiterLimit;
		public PdfDashArray Linedashpattern { get; set; } = DefaultLineDashPattern;
		// Rendering intent (not currently used in GeboPDF?)
		// Stroke adjustment?
		// Blend mode?
		// Soft mask?
		/// <summary> Corresponds to /CA </summary>
		public float StrokingAlphaConstant { get; set; } = DefaultAlphaConstant;
		/// <summary> Corresponds to /ca </summary>
		public float NonStrokingAlphaConstant { get; set; } = DefaultAlphaConstant;
		// Alpha source flag?

		public PdfGraphicsState() { }

		public PdfGraphicsState(PdfGraphicsState basis) : this() {
			StrokingColorSpace = basis.StrokingColorSpace;
			NonStrokingColorSpace = basis.NonStrokingColorSpace;
			StrokingColor = basis.StrokingColor;
			NonStrokingColor = basis.NonStrokingColor;
			Font = basis.Font;
			Linewidth = basis.Linewidth;
			Linecapstyle = basis.Linecapstyle;
			Linejoinstyle = basis.Linejoinstyle;
			Miterlimit = basis.Miterlimit;
			Linedashpattern = basis.Linedashpattern;
			StrokingAlphaConstant = basis.StrokingAlphaConstant;
			NonStrokingAlphaConstant = basis.NonStrokingAlphaConstant;
		}

	}

	public class PdfGraphicsStateParameterDictionary : AbstractPdfDictionary {

		public readonly float? linewidth;
		public readonly LineCapStyle? linecapstyle;
		public readonly LineJoinStyle? linejoinstyle;
		public readonly float? miterlimit;
		public readonly PdfDashArray? linedashpattern;
		// Rendering intent not currently used in GeboPDF
		// Overprint? Non-stroking overprint? Overprint mode?
		/// <summary> Array of [font size] (indirect reference and number) </summary>
		public readonly PdfFontSetting? font;
		// Black generation function? BG2?
		// Undercolor removal function? UCR2?
		// Transfer function? TR2?
		// Halftone dictionary?
		// Flatness tolerance? Smoothness tolerance?
		// Blend mode?
		// SMask?
		/// <summary> Corresponds to /CA </summary>
		public readonly float? strokingAlphaConstant;
		/// <summary> Corresponds to /ca </summary>
		public readonly float? nonStrokingAlphaConstant;

		public PdfGraphicsStateParameterDictionary(
			float? linewidth = null,
			LineCapStyle? linecapstyle = null,
			LineJoinStyle? linejoinstyle = null,
			float? miterlimit = null,
			PdfDashArray? linedashpattern = null,
			PdfFontSetting? font = null,
			float? strokingAlphaConstant = null,
			float? nonStrokingAlphaConstant = null
			) {

			this.linewidth = linewidth;
			this.linecapstyle = linecapstyle;
			this.linejoinstyle = linejoinstyle;
			this.miterlimit = miterlimit;
			this.linedashpattern = linedashpattern;
			this.font = font;
			this.strokingAlphaConstant = strokingAlphaConstant;
			this.nonStrokingAlphaConstant = nonStrokingAlphaConstant;
		}

		// Alpha source flag?
		// Text knockout?

		public override int Count {
			get {
				int count = 0;
				if (linewidth.HasValue) { count += 1; }
				if (linecapstyle.HasValue) { count += 1; }
				if (linejoinstyle.HasValue) { count += 1; }
				if (miterlimit.HasValue) { count += 1; }
				if (linedashpattern != null) { count += 1; }
				if (font != null) { count += 1; }
				if (strokingAlphaConstant.HasValue) { count += 1; }
				if (nonStrokingAlphaConstant.HasValue) { count += 1; }
				return count;
			}
		}

		public override IEnumerator<KeyValuePair<PdfName, PdfObject>> GetEnumerator() {
			if (linewidth.HasValue) { yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.LineWidth, new PdfFloat(linewidth.Value)); }
			if (linecapstyle.HasValue) { yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.LineCapStyle, new PdfInt((int)linecapstyle.Value)); }
			if (linejoinstyle.HasValue) { yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.LineJoinStyle, new PdfInt((int)linejoinstyle.Value)); }
			if (miterlimit.HasValue) { yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.MiterLimit, new PdfFloat(miterlimit.Value)); }
			if (linedashpattern != null) { yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.LineDashPattern, MakeLineDashPattern(linedashpattern)); }
			if (font != null) { yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.GraphicsStateFont, MakeFontSetting(font)); }
			if (strokingAlphaConstant.HasValue) { yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.StrokingAlpha, new PdfFloat(strokingAlphaConstant.Value)); }
			if (nonStrokingAlphaConstant.HasValue) { yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.NonStrokingAlpha, new PdfFloat(nonStrokingAlphaConstant.Value)); }
		}

		private static PdfArray MakeLineDashPattern(PdfDashArray pattern) {
			return new PdfArray(
				pattern.dashArray != null ? PdfArray.MakeArray(pattern.dashArray) : new PdfArray(),
				new PdfFloat(pattern.dashPhase)
				);
		}

		private static PdfArray MakeFontSetting(PdfFontSetting fontSetting) {
			return new PdfArray(fontSetting.font.FontReference, new PdfFloat(fontSetting.size));
		}

	}

	public enum LineCapStyle : int { Butt = 0, Round = 1, Square = 2 }
	public enum LineJoinStyle : int { Mitre = 0, Round = 1, Bevel = 2 }

	public enum TextRenderingMode : int {
		FillText = 0,
		StrokeText = 1,
		FillThenStrokeText = 2,
		NeitherFillNorStrokeText = 3,
		FillTextAndAddToPathForClipping = 4,
		StrokeTextAndAddToPathForClipping = 5,
		FillThenStrokeTextAndAddToPathForClipping = 6,
		AddTextToPathForClipping = 7
	}

	public class PdfDashArray {
		public readonly float[]? dashArray;
		public readonly float dashPhase;

		public PdfDashArray(float[]? dashArray, float dashPhase) {
			this.dashArray = dashArray;
			this.dashPhase = dashPhase;
		}

		public override int GetHashCode() {
			return HashCode.Combine(dashArray, dashPhase);
		}

		public override bool Equals(object? obj) {
			if(obj is PdfDashArray other) {
				return dashPhase == other.dashPhase && ((dashArray is null && other.dashArray is null) || Enumerable.SequenceEqual(dashArray ?? Array.Empty<float>(), other.dashArray ?? Array.Empty<float>()));
			}
			return false;
		}

		public static bool operator ==(PdfDashArray? a, PdfDashArray? b) {
			if (a is null) { return b is null; }
			else { return a.Equals(b); }
		}
		public static bool operator !=(PdfDashArray? a, PdfDashArray? b) {
			if (a is null) { return b is not null; }
			else { return !a.Equals(b); }
		}
	}

	public class PdfFontSetting {
		public readonly PdfFont font;
		public readonly float size;

		public PdfFontSetting(PdfFont font, float size) {
			this.font = font;
			this.size = size;
		}

		public override int GetHashCode() {
			return HashCode.Combine(font, size);
		}

		public override bool Equals(object? obj) {
			if (obj is PdfFontSetting other) {
				return size == other.size && font.Equals(other.font);
			}
			return false;
		}

		public static bool operator ==(PdfFontSetting? a, PdfFontSetting? b) {
			if (a is null) { return b is null; }
			else { return a.Equals(b); }
		}
		public static bool operator !=(PdfFontSetting? a, PdfFontSetting? b) {
			if (a is null) { return b is not null; }
			else { return !a.Equals(b); }
		}
	}

}
