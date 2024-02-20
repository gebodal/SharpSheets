using GeboPdf.Documents;
using GeboPdf.Fonts;
using GeboPdf.Fonts.TrueType;
using GeboPdf.IO;
using GeboPdf.Objects;
using GeboPdf.Patterns;
using GeboPdf.Utilities;
using GeboPdf.XObjects;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace GeboPdf.Graphics {

	public class GraphicsStream : AbstractPdfStream {

		[Flags]
		protected enum GraphicsStreamState : int {
			None = 0,
			PageDescription = 1 << 0,
			Path = 1 << 1,
			Clipping = 1 << 2,
			Text = 1 << 3,
			Shading = 1 << 4,
			InlineImage = 1 << 5,
			External = 1 << 6
		}

		protected class PdfInvalidGraphicsStateException : PdfInvalidGraphicsOperationException {

			private static string GetMessage(GraphicsStreamState currentState, GraphicsStreamState requiredState) {
				return $"Cannot perform this action while in {currentState} state, must be {requiredState}.";
			}

			public PdfInvalidGraphicsStateException(GraphicsStreamState currentState, GraphicsStreamState requiredState) : base(GetMessage(currentState, requiredState)) { }

		}

		protected readonly PdfResourcesDictionary resources;

		private readonly MemoryStream _memoryStream; // Can we do better than this?
		protected GraphicsStreamState streamLevel;

		//private PdfFont currentFont;
		//private PdfColorSpace currentStrokingColorSpace;
		//private PdfColorSpace currentNonStrokingColorSpace;

		private PdfGraphicsState state;
		private readonly Stack<PdfGraphicsState> _stateStack;

		protected readonly Stack<PdfName?> markedContentSequences;

		protected readonly PdfStreamWriter writer;
		private readonly bool useEOL;

		public GraphicsStream(PdfResourcesDictionary resources, bool useEOL) {
			this.resources = resources;

			this.useEOL = useEOL;

			_memoryStream = new MemoryStream();
			writer = new PdfStreamWriter(_memoryStream);
			streamLevel = GraphicsStreamState.PageDescription;
			markedContentSequences = new Stack<PdfName?>();

			state = new PdfGraphicsState();
			_stateStack = new Stack<PdfGraphicsState>();
		}

		public override bool AllowEncoding { get; } = true;

		public override MemoryStream GetStream() {
			return _memoryStream;
		}

		public override int Count => 0;
		public override IEnumerator<KeyValuePair<PdfName, PdfObject>> GetEnumerator() { yield break; }

		public override int GetHashCode() => base.GetHashCode();
		public override bool Equals(object? obj) {
			if (ReferenceEquals(this, obj)) {
				return true;
			}
			else if (obj is GraphicsStream other) {
				return base.Equals(other);
			}
			return false;
		}

		protected void WriteOperator(string operatorStr) {
			writer.WriteASCII(operatorStr);

			if (useEOL) {
				writer.WriteEOL();
			}
			else {
				writer.WriteSpace();
			}
		}

		protected void WriteFloats(params float[] values) {
			for(int i=0; i<values.Length; i++) {
				if (i > 0) {
					writer.WriteSpace();
				}
				writer.WriteFloat(values[i]);
			}
		}

		protected void WriteColorFloats(params float[] values) {
			for (int i = 0; i < values.Length; i++) {
				if (i > 0) {
					writer.WriteSpace();
				}
				writer.WriteFloat(ClampColor(values[i]));
			}
		}

		protected void WriteFloatArray(float[] array) {
			writer.WriteASCII("[");
			WriteFloats(array);
			writer.WriteASCII("]");
		}

		protected void WriteText(string text) {
			if (state.Font == null) {
				throw new PdfInvalidGraphicsOperationException("There is no font currently set for this graphics stream.");
			}

			byte[] textBytes = state.Font.font.GetBytes(text);

			writer.WriteASCII("<");
			writer.WriteASCII(HexWriter.ToString(textBytes));
			writer.WriteASCII(">");
		}

		protected void WriteText(ushort[] glyphIDs) {
			if (state.Font == null) {
				throw new PdfInvalidGraphicsOperationException("There is no font currently set for this graphics stream.");
			}

			//byte[] textBytes = state.Font.font.GetBytes(text);

			byte[] bytes = new byte[glyphIDs.Length * 2];
			for(int i=0; i<glyphIDs.Length; i++) {
				byte[] glyphBytes = BitConverter.GetBytes(glyphIDs[i]);
				if (BitConverter.IsLittleEndian) {
					Array.Reverse(glyphBytes);
				}
				bytes[i * 2] = glyphBytes[0];
				bytes[i * 2 + 1] = glyphBytes[1];
			}

			writer.WriteASCII("<");
			writer.WriteASCII(HexWriter.ToString(bytes));
			writer.WriteASCII(">");
		}

		private static float ClampColor(float value) {
			return Math.Max(0f, Math.Min(1f, value));
		}

		#region Special Graphics State

		/// <returns> The graphics state stack depth after this operation </returns>
		public int SaveState() {
			if (streamLevel != GraphicsStreamState.PageDescription) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.PageDescription);
			}

			_stateStack.Push(state);
			state = new PdfGraphicsState(state);

			WriteOperator("q");

			return _stateStack.Count;
		}

		/// <returns> The graphics state stack depth after this operation </returns>
		public int RestoreState() {
			if (streamLevel != GraphicsStreamState.PageDescription) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.PageDescription);
			}

			if(_stateStack.Count == 0) {
				throw new PdfInvalidGraphicsOperationException("Cannot restore graphics state, as the state stack is empty.");
			}
			state = _stateStack.Pop();

			WriteOperator("Q");

			return _stateStack.Count;
		}

		public GraphicsStream ConcatenateMatrix(float a, float b, float c, float d, float e, float f) {
			if (streamLevel != GraphicsStreamState.PageDescription) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.PageDescription);
			}

			if (!(a == 1f && b == 0f && c == 0f && d == 1f && e == 0f && f == 0f)) { // Otherwise we're concatenating the Identity matrix, and we can ignore this
				WriteFloats(a, b, c, d, e, f);
				writer.WriteSpace();
				WriteOperator("cm");
			}

			return this;
		}

		public GraphicsStream ConcatenateMatrix(Transform transform) {
			return ConcatenateMatrix(transform.a, transform.b, transform.c, transform.d, transform.e, transform.f);
		}

		#endregion

		#region General Graphics State

		public GraphicsStream LineWidth(float lineWidth) {
			if (!(streamLevel == GraphicsStreamState.PageDescription || streamLevel == GraphicsStreamState.Text)) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.PageDescription | GraphicsStreamState.Text);
			}

			if (state.Linewidth != lineWidth) {
				state.Linewidth = lineWidth;

				writer.WriteFloat(lineWidth);
				writer.WriteSpace();
				WriteOperator("w");
			}

			return this;
		}

		public GraphicsStream LineCapStyle(LineCapStyle lineCap) {
			if (!(streamLevel == GraphicsStreamState.PageDescription || streamLevel == GraphicsStreamState.Text)) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.PageDescription | GraphicsStreamState.Text);
			}

			if (state.Linecapstyle != lineCap) {
				state.Linecapstyle = lineCap;

				writer.WriteInt((int)lineCap);
				writer.WriteSpace();
				WriteOperator("J");
			}

			return this;
		}

		public GraphicsStream LineJoinStyle(LineJoinStyle lineJoin) {
			if (!(streamLevel == GraphicsStreamState.PageDescription || streamLevel == GraphicsStreamState.Text)) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.PageDescription | GraphicsStreamState.Text);
			}

			if (state.Linejoinstyle != lineJoin) {
				state.Linejoinstyle = lineJoin;

				writer.WriteInt((int)lineJoin);
				writer.WriteSpace();
				WriteOperator("j");
			}

			return this;
		}

		public GraphicsStream MitreLimit(float mitreLimit) {
			if (!(streamLevel == GraphicsStreamState.PageDescription || streamLevel == GraphicsStreamState.Text)) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.PageDescription | GraphicsStreamState.Text);
			}

			if (state.Miterlimit != mitreLimit) {
				state.Miterlimit = mitreLimit;

				writer.WriteFloat(mitreLimit);
				writer.WriteSpace();
				WriteOperator("M");
			}

			return this;
		}

		public GraphicsStream LineDashPattern(float[]? dashArray, float dashPhase) {
			if (!(streamLevel == GraphicsStreamState.PageDescription || streamLevel == GraphicsStreamState.Text)) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.PageDescription | GraphicsStreamState.Text);
			}

			PdfDashArray newlinedashpattern = new PdfDashArray(dashArray, dashPhase);

			if (!state.Linedashpattern.Equals(newlinedashpattern)) {
				state.Linedashpattern = newlinedashpattern;

				WriteFloatArray(dashArray ?? Array.Empty<float>()); // TODO Is this fallback correct?
				writer.WriteSpace();
				writer.WriteFloat(dashPhase);
				writer.WriteSpace();
				WriteOperator("d");
			}

			return this;
		}

		// TODO Rendering Intent

		// Flatness Tolerence?

		public GraphicsStream SetGraphicsState(PdfGraphicsStateParameterDictionary graphicsStateDict) {
			if (!(streamLevel == GraphicsStreamState.PageDescription || streamLevel == GraphicsStreamState.Text)) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.PageDescription | GraphicsStreamState.Text);
			}

			bool changes = false;

			if (graphicsStateDict.linewidth.HasValue && state.Linewidth != graphicsStateDict.linewidth.Value) {
				state.Linewidth = graphicsStateDict.linewidth.Value;
				changes = true;
			}
			if (graphicsStateDict.linecapstyle.HasValue && state.Linecapstyle != graphicsStateDict.linecapstyle.Value) {
				state.Linecapstyle = graphicsStateDict.linecapstyle.Value;
				changes = true;
			}
			if (graphicsStateDict.linejoinstyle.HasValue && state.Linejoinstyle != graphicsStateDict.linejoinstyle.Value) {
				state.Linejoinstyle = graphicsStateDict.linejoinstyle.Value;
				changes = true;
			}
			if (graphicsStateDict.miterlimit.HasValue && state.Miterlimit != graphicsStateDict.miterlimit.Value) {
				state.Miterlimit = graphicsStateDict.miterlimit.Value;
				changes = true;
			}
			if (graphicsStateDict.linedashpattern != null && !state.Linedashpattern.Equals(graphicsStateDict.linedashpattern)) {
				state.Linedashpattern = graphicsStateDict.linedashpattern;
				changes = true;
			}
			if (graphicsStateDict.font != null && !graphicsStateDict.font.Equals(state.Font)) { // !state.Font.Equals(graphicsStateDict.font)
				state.Font = graphicsStateDict.font;
				changes = true;
			}
			if (graphicsStateDict.strokingAlphaConstant.HasValue && state.StrokingAlphaConstant != graphicsStateDict.strokingAlphaConstant.Value) {
				state.StrokingAlphaConstant = graphicsStateDict.strokingAlphaConstant.Value;
				changes = true;
			}
			if (graphicsStateDict.nonStrokingAlphaConstant.HasValue && state.NonStrokingAlphaConstant != graphicsStateDict.nonStrokingAlphaConstant.Value) {
				state.NonStrokingAlphaConstant = graphicsStateDict.nonStrokingAlphaConstant.Value;
				changes = true;
			}

			if (changes) {
				resources.AddGraphicsState(graphicsStateDict, out PdfName stateName);

				writer.WriteName(stateName);
				writer.WriteSpace();
				WriteOperator("gs");
			}

			return this;
		}

		#endregion

		#region Path Construction

		public GraphicsStream Move(float x, float y) {
			if (!(streamLevel == GraphicsStreamState.PageDescription || streamLevel == GraphicsStreamState.Path)) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.PageDescription | GraphicsStreamState.Path);
			}
			WriteFloats(x, y);
			writer.WriteSpace();
			WriteOperator("m");
			streamLevel = GraphicsStreamState.Path;
			return this;
		}

		public GraphicsStream Line(float x, float y) {
			if (streamLevel != GraphicsStreamState.Path) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.Path);
			}
			WriteFloats(x, y);
			writer.WriteSpace();
			WriteOperator("l");
			streamLevel = GraphicsStreamState.Path;
			return this;
		}

		public GraphicsStream Cubic(float x1, float y1, float x2, float y2, float x3, float y3) {
			if (streamLevel != GraphicsStreamState.Path) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.Path);
			}
			WriteFloats(x1, y1, x2, y2, x3, y3);
			writer.WriteSpace();
			WriteOperator("c");
			streamLevel = GraphicsStreamState.Path;
			return this;
		}

		public GraphicsStream Quadratic(float x2, float y2, float x3, float y3) {
			if (streamLevel != GraphicsStreamState.Path) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.Path);
			}
			WriteFloats(x2, y2, x3, y3);
			writer.WriteSpace();
			WriteOperator("v"); // v or y?
			streamLevel = GraphicsStreamState.Path;
			return this;
		}

		public GraphicsStream Close() {
			if (streamLevel != GraphicsStreamState.Path) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.Path);
			}
			WriteOperator("h");
			streamLevel = GraphicsStreamState.Path;
			return this;
		}

		public GraphicsStream Rectangle(float x, float y, float width, float height) {
			if (!(streamLevel == GraphicsStreamState.PageDescription || streamLevel == GraphicsStreamState.Path)) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.PageDescription | GraphicsStreamState.Path);
			}
			WriteFloats(x, y, width, height);
			writer.WriteSpace();
			WriteOperator("re");
			streamLevel = GraphicsStreamState.Path;
			return this;
		}

		#endregion

		#region Path Painting

		public GraphicsStream Stroke() {
			if (!(streamLevel == GraphicsStreamState.Path || streamLevel == GraphicsStreamState.Clipping)) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.Path | GraphicsStreamState.Clipping);
			}
			WriteOperator("S");
			streamLevel = GraphicsStreamState.PageDescription;
			return this;
		}

		public GraphicsStream FillNonZero() {
			if (!(streamLevel == GraphicsStreamState.Path || streamLevel == GraphicsStreamState.Clipping)) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.Path | GraphicsStreamState.Clipping);
			}
			WriteOperator("f");
			streamLevel = GraphicsStreamState.PageDescription;
			return this;
		}

		public GraphicsStream FillEvenOdd() {
			if (!(streamLevel == GraphicsStreamState.Path || streamLevel == GraphicsStreamState.Clipping)) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.Path | GraphicsStreamState.Clipping);
			}
			WriteOperator("f*");
			streamLevel = GraphicsStreamState.PageDescription;
			return this;
		}

		public GraphicsStream FillStrokeNonZero() {
			if (!(streamLevel == GraphicsStreamState.Path || streamLevel == GraphicsStreamState.Clipping)) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.Path | GraphicsStreamState.Clipping);
			}
			WriteOperator("B");
			streamLevel = GraphicsStreamState.PageDescription;
			return this;
		}

		public GraphicsStream FillStrokeEvenOdd() {
			if (!(streamLevel == GraphicsStreamState.Path || streamLevel == GraphicsStreamState.Clipping)) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.Path | GraphicsStreamState.Clipping);
			}
			WriteOperator("B*");
			streamLevel = GraphicsStreamState.PageDescription;
			return this;
		}

		public GraphicsStream EndPath() {
			if (!(streamLevel == GraphicsStreamState.Path || streamLevel == GraphicsStreamState.Clipping)) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.Path | GraphicsStreamState.Clipping);
			}
			WriteOperator("n");
			streamLevel = GraphicsStreamState.PageDescription;
			return this;
		}

		#endregion

		#region Clipping

		public GraphicsStream ClipNonZero() {
			if (streamLevel != GraphicsStreamState.Path) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.Path);
			}
			WriteOperator("W");
			streamLevel = GraphicsStreamState.Clipping;
			return this;
		}

		public GraphicsStream ClipEvenOdd() {
			if (streamLevel != GraphicsStreamState.Path) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.Path);
			}
			WriteOperator("W*");
			streamLevel = GraphicsStreamState.Clipping;
			return this;
		}

		#endregion

		#region Text

		#region Text Objects

		public GraphicsStream BeginText() {
			if (streamLevel != GraphicsStreamState.PageDescription) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.PageDescription);
			}

			// Used to prevent interleaving of text objects and marked sequences
			markedContentSequences.Push(null);

			WriteOperator("BT");
			streamLevel = GraphicsStreamState.Text;

			return this;
		}

		public GraphicsStream EndText() {
			if (streamLevel != GraphicsStreamState.Text) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.Text);
			}

			if(markedContentSequences.Peek() != null) {
				throw new PdfInvalidGraphicsOperationException("Cannot end current text object before marked content sequence is closed.");
			}
			markedContentSequences.Pop();

			WriteOperator("ET");
			streamLevel = GraphicsStreamState.PageDescription;
			return this;
		}

		#endregion Text Objects

		#region Text State

		public GraphicsStream CharacterSpacing(float charSpace) {
			if (!(streamLevel == GraphicsStreamState.PageDescription || streamLevel == GraphicsStreamState.Text)) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.PageDescription | GraphicsStreamState.Text);
			}

			if (state.CharacterSpacing != charSpace) {
				state.CharacterSpacing = charSpace;

				writer.WriteFloat(charSpace);
				writer.WriteSpace();
				WriteOperator("Tc");
			}

			return this;
		}

		public GraphicsStream WordSpacing(float wordSpace) {
			if (!(streamLevel == GraphicsStreamState.PageDescription || streamLevel == GraphicsStreamState.Text)) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.PageDescription | GraphicsStreamState.Text);
			}

			if (state.WordSpacing != wordSpace) {
				state.WordSpacing = wordSpace;

				writer.WriteFloat(wordSpace);
				writer.WriteSpace();
				WriteOperator("Tw");
			}

			return this;
		}

		public GraphicsStream TextHorizontalScale(float scale) {
			if (!(streamLevel == GraphicsStreamState.PageDescription || streamLevel == GraphicsStreamState.Text)) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.PageDescription | GraphicsStreamState.Text);
			}

			if (state.TextHorizontalScaling != scale) {
				state.TextHorizontalScaling = scale;

				writer.WriteFloat(scale);
				writer.WriteSpace();
				WriteOperator("Tz");
			}

			return this;

		}

		public GraphicsStream TextLeading(float leading) {
			if (!(streamLevel == GraphicsStreamState.PageDescription || streamLevel == GraphicsStreamState.Text)) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.PageDescription | GraphicsStreamState.Text);
			}

			if (state.TextLeading != leading) {
				state.TextLeading = leading;

				writer.WriteFloat(leading);
				writer.WriteSpace();
				WriteOperator("TL");
			}

			return this;
		}

		public GraphicsStream FontAndSize(PdfFont font, float size) {
			if (!(streamLevel == GraphicsStreamState.PageDescription || streamLevel == GraphicsStreamState.Text)) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.PageDescription | GraphicsStreamState.Text);
			}

			PdfFontSetting newFont = new PdfFontSetting(font, size);

			if (state.Font != newFont) {
				resources.AddFont(font, out PdfName fontName);
				state.Font = newFont;

				writer.WriteName(fontName);
				writer.WriteSpace();
				writer.WriteFloat(size);
				writer.WriteSpace();
				WriteOperator("Tf");
			}

			return this;
		}

		public GraphicsStream TextRenderingMode(TextRenderingMode render) {
			if (!(streamLevel == GraphicsStreamState.PageDescription || streamLevel == GraphicsStreamState.Text)) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.PageDescription | GraphicsStreamState.Text);
			}

			if (state.TextRenderingMode != render) {
				state.TextRenderingMode = render;

				writer.WriteInt((int)render);
				writer.WriteSpace();
				WriteOperator("Tr");
			}

			return this;
		}

		public GraphicsStream TextRise(float rise) {
			if (!(streamLevel == GraphicsStreamState.PageDescription || streamLevel == GraphicsStreamState.Text)) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.PageDescription | GraphicsStreamState.Text);
			}

			if (state.TextRise != rise) {
				state.TextRise = rise;

				writer.WriteFloat(rise);
				writer.WriteSpace();
				WriteOperator("Ts");
			}

			return this;
		}

		#endregion Text State

		#region Text Positioning

		public GraphicsStream MoveToStart(float tx, float ty) {
			if (streamLevel != GraphicsStreamState.Text) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.Text);
			}
			WriteFloats(tx, ty);
			writer.WriteSpace();
			WriteOperator("Td");
			return this;
		}

		public GraphicsStream MoveToStartSetLeading(float tx, float ty) {
			if (streamLevel != GraphicsStreamState.Text) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.Text);
			}

			state.TextLeading = -ty;

			WriteFloats(tx, ty);
			writer.WriteSpace();
			WriteOperator("TD");

			return this;
		}

		public GraphicsStream SetTextMatrix(float a, float b, float c, float d, float e, float f) {
			if (streamLevel != GraphicsStreamState.Text) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.Text);
			}
			WriteFloats(a, b, c, d, e, f);
			writer.WriteSpace();
			WriteOperator("Tm");
			return this;
		}

		public GraphicsStream SetTextMatrix(Transform transform) {
			return SetTextMatrix(transform.a, transform.b, transform.c, transform.d, transform.e, transform.f);
		}

		public GraphicsStream MoveToStart() {
			if (streamLevel != GraphicsStreamState.Text) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.Text);
			}
			WriteOperator("T*");
			return this;
		}

		#endregion Text Positioning

		#region Text Showing

		public GraphicsStream ShowText(string text) {
			if (streamLevel != GraphicsStreamState.Text) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.Text);
			}
			WriteText(text);
			writer.WriteSpace();
			WriteOperator("Tj");
			return this;
		}

		public GraphicsStream MoveNextLineShowText(string text) {
			if (streamLevel != GraphicsStreamState.Text) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.Text);
			}
			WriteText(text);
			writer.WriteSpace();
			WriteOperator("'");
			return this;
		}

		public GraphicsStream MoveNextLineShowText(float aw, float ac, string text) {
			if (streamLevel != GraphicsStreamState.Text) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.Text);
			}

			state.WordSpacing = aw;
			state.CharacterSpacing = ac;

			WriteFloats(aw, ac);
			writer.WriteSpace();
			WriteText(text);
			writer.WriteSpace();
			WriteOperator("\"");

			return this;
		}

		public GraphicsStream ShowTextWithPositioning(params (string text, float? offset)[] array) {
			if (streamLevel != GraphicsStreamState.Text) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.Text);
			}

			writer.WriteASCII("[");
			writer.WriteSpace();

			for (int i = 0; i < array.Length; i++) {
				if (!string.IsNullOrEmpty(array[i].text)) {
					WriteText(array[i].text);
					writer.WriteSpace();
				}
				if (array[i].offset.HasValue) {
					writer.WriteFloat(array[i].offset!.Value);
					writer.WriteSpace();
				}
			}

			writer.WriteASCII("]");
			writer.WriteSpace();
			WriteOperator("TJ");

			return this;
		}

		private GraphicsStream ShowTextWithPositioning(params (ushort[] glyphs, float? offset)[] array) {
			if (streamLevel != GraphicsStreamState.Text) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.Text);
			}

			writer.WriteASCII("[");
			writer.WriteSpace();

			for (int i = 0; i < array.Length; i++) {
				if (array[i].glyphs.Length > 0) {
					WriteText(array[i].glyphs);
					writer.WriteSpace();
				}
				if (array[i].offset.HasValue) {
					writer.WriteFloat(array[i].offset!.Value);
					writer.WriteSpace();
				}
			}

			writer.WriteASCII("]");
			writer.WriteSpace();
			WriteOperator("TJ");

			return this;
		}

		public GraphicsStream ShowTextCalculateKerning(string text) {
			if (text.Length > 1 && state.Font?.font is PdfGlyphFont glyphFont) { // Is this a little brittle?

				PositionedGlyphRun positionedGlyphs = glyphFont.GetGlyphRun(text);

				List<(ushort[] glyphs, float? offset)> positioned = new List<(ushort[], float?)>();

				List<ushort> builder = new List<ushort>();

				for(int i=0; i< positionedGlyphs.Count; i++) {
					builder.Add(positionedGlyphs[i]);
					(short xAdvDelta, _) = positionedGlyphs.GetAdvance(i);
					// TODO Need to make use of placement data here
					if(xAdvDelta != 0) {
						positioned.Add((builder.ToArray(), -xAdvDelta));
						builder.Clear();
					}
				}

				if (builder.Count > 0) {
					positioned.Add((builder.ToArray(), null));
				}

				return ShowTextWithPositioning(positioned.ToArray());
			}
			else {
				return ShowText(text);
			}
		}

		#endregion Text Showing

		#endregion Text

		#region Color

		public GraphicsStream SetStrokingColorSpace(PdfColorSpace colorSpace) {
			if (!(streamLevel == GraphicsStreamState.PageDescription || streamLevel == GraphicsStreamState.Text)) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.PageDescription | GraphicsStreamState.Text);
			}

			if (state.StrokingColorSpace != colorSpace || state.StrokingColor != colorSpace.DefaultValues) {
				state.StrokingColorSpace = colorSpace;
				state.StrokingColor = colorSpace.DefaultValues;

				if (colorSpace.IsBuiltIn) {
					writer.WriteName(colorSpace.BuiltInName);
				}
				else {
					resources.AddColorSpace(colorSpace, out PdfName colorspaceName);
					writer.WriteName(colorspaceName);
				}

				writer.WriteSpace();
				WriteOperator("CS");
			}

			return this;
		}

		public GraphicsStream SetNonStrokingColorSpace(PdfColorSpace colorSpace) {
			if (!(streamLevel == GraphicsStreamState.PageDescription || streamLevel == GraphicsStreamState.Text)) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.PageDescription | GraphicsStreamState.Text);
			}

			if (state.NonStrokingColorSpace != colorSpace || state.NonStrokingColor != colorSpace.DefaultValues) {
				state.NonStrokingColorSpace = colorSpace;
				state.NonStrokingColor = colorSpace.DefaultValues;

				if (colorSpace.IsBuiltIn) {
					writer.WriteName(colorSpace.BuiltInName);
				}
				else {
					resources.AddColorSpace(colorSpace, out PdfName colorspaceName);
					writer.WriteName(colorspaceName);
				}

				writer.WriteSpace();
				WriteOperator("cs");
			}

			return this;
		}

		public GraphicsStream SetStrokingPattern(IPdfPattern pattern, float[]? values) {
			if (!(streamLevel == GraphicsStreamState.PageDescription || streamLevel == GraphicsStreamState.Text)) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.PageDescription | GraphicsStreamState.Text);
			}

			if (pattern is PdfTilingPattern tilingPattern1 && tilingPattern1.PaintType == PdfTilingPaintType.Uncolored) {
				if (!state.StrokingColorSpace.IsUncoloredPatternColorSpace) {
					throw new PdfInvalidGraphicsOperationException("The color space has not been appropriately set for this uncolored tiling stroking pattern.");
				}
			}
			else if (state.StrokingColorSpace != PdfColorSpace.PatternNoParams) {
				throw new PdfInvalidGraphicsOperationException("The color space has not been appropriately set for this stroking pattern.");
			}

			PdfPatternColor newColor = new PdfPatternColor(pattern, state.StrokingColorSpace, values);

			if (state.StrokingColor != newColor) {
				resources.AddPattern(pattern, out PdfName patternName);

				int colorSpaceComponents = state.StrokingColorSpace.NumComponents;
				int numValues = values?.Length ?? 0;
				if (colorSpaceComponents != numValues) {
					throw new PdfInvalidGraphicsOperationException($"Invalid number of color values received with pattern (expected {colorSpaceComponents}, got {numValues}).");
				}

				//state.strokingColorSpace = pattern.ColorSpace;
				state.StrokingColor = newColor;

				if (numValues > 0) {
					WriteColorFloats(values!); // We can be certain values is not null here
					writer.WriteSpace();
				}

				writer.WriteName(patternName);
				writer.WriteSpace();
				WriteOperator("SCN");
			}

			return this;
		}

		public GraphicsStream SetNonStrokingPattern(IPdfPattern pattern, float[]? values) {
			if (!(streamLevel == GraphicsStreamState.PageDescription || streamLevel == GraphicsStreamState.Text)) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.PageDescription | GraphicsStreamState.Text);
			}

			if (pattern is PdfTilingPattern tilingPattern1 && tilingPattern1.PaintType == PdfTilingPaintType.Uncolored) {
				if (!state.NonStrokingColorSpace.IsUncoloredPatternColorSpace) {
					throw new PdfInvalidGraphicsOperationException("The color space has not been appropriately set for this uncolored tiling non-stroking pattern.");
				}
			}
			else if (state.NonStrokingColorSpace != PdfColorSpace.PatternNoParams) {
				throw new PdfInvalidGraphicsOperationException("The color space has not been appropriately set for this non-stroking pattern.");
			}

			PdfPatternColor newColor = new PdfPatternColor(pattern, state.NonStrokingColorSpace, values);

			if (state.NonStrokingColor != newColor) {
				resources.AddPattern(pattern, out PdfName patternName);

				int colorSpaceComponents = state.NonStrokingColorSpace.NumComponents;
				int numValues = values?.Length ?? 0;
				if (colorSpaceComponents != numValues) {
					throw new PdfInvalidGraphicsOperationException($"Invalid number of color values received with pattern (expected {colorSpaceComponents}, got {numValues}).");
				}

				//state.nonStrokingColorSpace = pattern.ColorSpace;
				state.NonStrokingColor = newColor;

				if (numValues > 0) {
					WriteColorFloats(values!); // We can be certain values is not null here
					writer.WriteSpace();
				}

				writer.WriteName(patternName);
				writer.WriteSpace();
				WriteOperator("scn");
			}

			return this;
		}

		public GraphicsStream SetStrokingGray(float gray) {
			if (!(streamLevel == GraphicsStreamState.PageDescription || streamLevel == GraphicsStreamState.Text)) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.PageDescription | GraphicsStreamState.Text);
			}

			PdfDeviceColor newColor = new PdfGrayColor(gray);

			if (state.StrokingColorSpace != PdfColorSpace.DeviceGray || state.StrokingColor != newColor) {
				state.StrokingColorSpace = PdfColorSpace.DeviceGray; // DefaultGray?
				state.StrokingColor = newColor;

				writer.WriteFloat(ClampColor(gray));
				writer.WriteSpace();
				WriteOperator("G");
			}

			return this;
		}

		public GraphicsStream SetNonStrokingGray(float gray) {
			if (!(streamLevel == GraphicsStreamState.PageDescription || streamLevel == GraphicsStreamState.Text)) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.PageDescription | GraphicsStreamState.Text);
			}

			PdfDeviceColor newColor = new PdfGrayColor(gray);

			if (state.NonStrokingColorSpace != PdfColorSpace.DeviceGray || state.NonStrokingColor != newColor) {
				state.NonStrokingColorSpace = PdfColorSpace.DeviceGray; // DefaultGray?
				state.NonStrokingColor = newColor;

				writer.WriteFloat(ClampColor(gray));
				writer.WriteSpace();
				WriteOperator("g");
			}

			return this;
		}

		public GraphicsStream SetStrokingRGB(float r, float g, float b) {
			if (!(streamLevel == GraphicsStreamState.PageDescription || streamLevel == GraphicsStreamState.Text)) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.PageDescription | GraphicsStreamState.Text);
			}

			PdfDeviceColor newColor = new PdfRGBColor(r, g, b);

			if (state.StrokingColorSpace != PdfColorSpace.DeviceRGB || state.StrokingColor != newColor) {
				state.StrokingColorSpace = PdfColorSpace.DeviceRGB; // DefaultRGB?
				state.StrokingColor = newColor;

				WriteFloats(ClampColor(r), ClampColor(g), ClampColor(b));
				writer.WriteSpace();
				WriteOperator("RG");
			}

			return this;
		}

		public GraphicsStream SetNonStrokingRGB(float r, float g, float b) {
			if (!(streamLevel == GraphicsStreamState.PageDescription || streamLevel == GraphicsStreamState.Text)) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.PageDescription | GraphicsStreamState.Text);
			}

			PdfDeviceColor newColor = new PdfRGBColor(r, g, b);

			if (state.NonStrokingColorSpace != PdfColorSpace.DeviceRGB || state.NonStrokingColor != newColor) {
				state.NonStrokingColorSpace = PdfColorSpace.DeviceRGB; // DefaultRGB?
				state.NonStrokingColor = newColor;

				WriteFloats(ClampColor(r), ClampColor(g), ClampColor(b));
				writer.WriteSpace();
				WriteOperator("rg");
			}

			return this;
		}

		public GraphicsStream SetStrokingCMYK(float c, float m, float y, float k) {
			if (!(streamLevel == GraphicsStreamState.PageDescription || streamLevel == GraphicsStreamState.Text)) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.PageDescription | GraphicsStreamState.Text);
			}

			PdfDeviceColor newColor = new PdfCMYKColor(c, m, y, k);

			if (state.StrokingColorSpace != PdfColorSpace.DeviceCMYK || state.StrokingColor != newColor) {
				state.StrokingColorSpace = PdfColorSpace.DeviceCMYK; // DefaultCMYK?
				state.StrokingColor = newColor;

				WriteFloats(ClampColor(c), ClampColor(m), ClampColor(y), ClampColor(k));
				writer.WriteSpace();
				WriteOperator("K");
			}

			return this;
		}

		public GraphicsStream SetNonStrokingCMYK(float c, float m, float y, float k) {
			if (!(streamLevel == GraphicsStreamState.PageDescription || streamLevel == GraphicsStreamState.Text)) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.PageDescription | GraphicsStreamState.Text);
			}

			PdfDeviceColor newColor = new PdfCMYKColor(c, m, y, k);

			if (state.NonStrokingColorSpace != PdfColorSpace.DeviceCMYK || state.NonStrokingColor != newColor) {
				state.NonStrokingColorSpace = PdfColorSpace.DeviceCMYK; // DefaultCMYK?
				state.NonStrokingColor = newColor;

				WriteFloats(ClampColor(c), ClampColor(m), ClampColor(y), ClampColor(k));
				writer.WriteSpace();
				WriteOperator("k");
			}

			return this;
		}

		public GraphicsStream SetStrokingColor(PdfDeviceColor color) {
			if (!(streamLevel == GraphicsStreamState.PageDescription || streamLevel == GraphicsStreamState.Text)) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.PageDescription | GraphicsStreamState.Text);
			}

			if (state.StrokingColorSpace != color.ColorSpace || state.StrokingColor != color) {
				state.StrokingColorSpace = color.ColorSpace; // Default color space?
				state.StrokingColor = color;

				WriteFloats(color.Values);
				writer.WriteSpace();

				if (color is PdfGrayColor) {
					WriteOperator("G");
				}
				else if (color is PdfRGBColor) {
					WriteOperator("RG");
				}
				else if (color is PdfCMYKColor) {
					WriteOperator("K");
				}
				else {
					throw new NotImplementedException($"Unrecognised PdfDeviceColor of type {color.GetType().Name}.");
				}
			}

			return this;
		}

		public GraphicsStream SetNonStrokingColor(PdfDeviceColor color) {
			if (!(streamLevel == GraphicsStreamState.PageDescription || streamLevel == GraphicsStreamState.Text)) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.PageDescription | GraphicsStreamState.Text);
			}

			if (state.NonStrokingColorSpace != color.ColorSpace || state.NonStrokingColor != color) {
				state.NonStrokingColorSpace = color.ColorSpace; // Default color space?
				state.NonStrokingColor = color;

				WriteFloats(color.Values);
				writer.WriteSpace();

				if (color is PdfGrayColor) {
					WriteOperator("g");
				}
				else if (color is PdfRGBColor) {
					WriteOperator("rg");
				}
				else if (color is PdfCMYKColor) {
					WriteOperator("k");
				}
				else {
					throw new NotImplementedException($"Unrecognised PdfDeviceColor of type {color.GetType().Name}.");
				}
			}

			return this;
		}

		public GraphicsStream SetStrokingAlphaConstant(float alpha) {
			PdfGraphicsStateParameterDictionary paramDict = new PdfGraphicsStateParameterDictionary(strokingAlphaConstant: ClampColor(alpha));
			return SetGraphicsState(paramDict);
		}

		public GraphicsStream SetNonStrokingAlphaConstant(float alpha) {
			PdfGraphicsStateParameterDictionary paramDict = new PdfGraphicsStateParameterDictionary(nonStrokingAlphaConstant: ClampColor(alpha));
			return SetGraphicsState(paramDict);
		}

		#endregion

		#region Shading Patterns

		public GraphicsStream PaintShading(PdfShadingDictionary shading) {
			if (streamLevel != GraphicsStreamState.PageDescription) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.PageDescription);
			}
			resources.AddShading(shading, out PdfName shadingName);
			writer.WriteName(shadingName);
			writer.WriteSpace();
			WriteOperator("sh");
			return this;
		}

		#endregion

		#region External Objects 

		public GraphicsStream PaintXObject(PdfXObject xObject) {
			if (streamLevel != GraphicsStreamState.PageDescription) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.PageDescription);
			}
			resources.AddXObject(xObject, out PdfName xObjectName);
			writer.WriteName(xObjectName);
			writer.WriteSpace();
			WriteOperator("Do");
			return this;
		}

		#endregion

		#region Marked Content

		public GraphicsStream BeginMarkedContent(PdfName tag) {
			if (!(streamLevel == GraphicsStreamState.PageDescription || streamLevel == GraphicsStreamState.Text)) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.PageDescription | GraphicsStreamState.Text);
			}

			markedContentSequences.Push(tag);

			writer.WriteName(tag);
			writer.WriteSpace();
			WriteOperator("BMC");

			return this;
		}

		public GraphicsStream EndMarkedContent() {
			if (!(streamLevel == GraphicsStreamState.PageDescription || streamLevel == GraphicsStreamState.Text)) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.PageDescription | GraphicsStreamState.Text);
			}

			// null value indicates that the last thing opened was a text object, and therefore we cannot close the sequence (no interleaving allowed)
			if(markedContentSequences.Count > 0 && markedContentSequences.Peek() == null) {
				throw new PdfInvalidGraphicsOperationException("Cannot end current marked content sequence before text object is closed.");
			}
			if(markedContentSequences.Count < 1) {
				throw new PdfInvalidGraphicsOperationException("There is no marked content sequence to end at this point in the stream.");
			}

			WriteOperator("EMC");

			return this;
		}

		#endregion

		#region Appearance Streams

		public static PdfString GetTextFieldDefaultAppearance(PdfResourcesDictionary resources, PdfFont font, float fontsize, PdfDeviceColor color) {
			GraphicsStream defaultAppearanceStream = new GraphicsStream(resources, false) {
				streamLevel = GraphicsStreamState.Text
			};

			defaultAppearanceStream.FontAndSize(font, fontsize);
			defaultAppearanceStream.SetNonStrokingColor(color);

			return new PdfTextString(Encoding.ASCII.GetString(defaultAppearanceStream._memoryStream.ToArray()).Trim());
		}

		public static PdfString GetTextFieldDefaultAppearance(PdfResourcesDictionary resources, PdfDeviceColor color) {
			GraphicsStream defaultAppearanceStream = new GraphicsStream(resources, false) {
				streamLevel = GraphicsStreamState.Text
			};

			defaultAppearanceStream.SetNonStrokingColor(color);

			return new PdfTextString(Encoding.ASCII.GetString(defaultAppearanceStream._memoryStream.ToArray()).Trim());
		}

		#endregion
	}

}
