using GeboPdf.Documents;
using GeboPdf.Fonts;
using GeboPdf.Fonts.TrueType;
using GeboPdf.IO;
using GeboPdf.Objects;
using GeboPdf.Patterns;
using GeboPdf.Utilities;
using GeboPdf.XObjects;
using System.Data;
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

		private readonly GraphicsOperation allOperations;
		protected GraphicsOperationsSet operations;
		private readonly Stack<GraphicsOperationsSet> _operationsStack;
		protected GraphicsStreamState streamLevel;

		private PdfGraphicsState state;
		private readonly Stack<PdfGraphicsState> _stateStack;

		protected readonly Stack<PdfName?> markedContentSequences;

		private readonly bool useEOL;

		public GraphicsStream(PdfResourcesDictionary resources, bool useEOL) {
			this.resources = resources;

			this.useEOL = useEOL;

			operations = new GraphicsOperationsSet();
			allOperations = operations;
			_operationsStack = new Stack<GraphicsOperationsSet>();
			streamLevel = GraphicsStreamState.PageDescription;

			markedContentSequences = new Stack<PdfName?>();

			state = new PdfGraphicsState();
			_stateStack = new Stack<PdfGraphicsState>();
		}

		public override bool AllowEncoding { get; } = true;

		public override MemoryStream GetStream() {
			MemoryStream stream = new MemoryStream();
			PdfGraphicsStreamWriter writer = new PdfGraphicsStreamWriter(stream, useEOL);
			allOperations.WriteTo(writer);
			return stream;
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

		protected byte[] GetTextBytes(string text) {
			if (state.Font == null) {
				throw new PdfInvalidGraphicsOperationException("There is no font currently set for this graphics stream.");
			}

			byte[] textBytes = state.Font.font.GetBytes(text);

			if (text.Length > 0 && state.Font?.font is PdfGlyphFont glyphFont) {
				resources.RegisterFontUsage(glyphFont, new FontGlyphUsage(glyphFont.GetGlyphs(text)));
			}

			return textBytes;
		}

		protected byte[] GetTextBytes(ushort[] glyphIDs) {
			if (state.Font == null) {
				throw new PdfInvalidGraphicsOperationException("There is no font currently set for this graphics stream.");
			}

			byte[] bytes = new byte[glyphIDs.Length * 2];
			for (int i = 0; i < glyphIDs.Length; i++) {
				byte[] glyphBytes = BitConverter.GetBytes(glyphIDs[i]);
				if (BitConverter.IsLittleEndian) {
					Array.Reverse(glyphBytes);
				}
				bytes[i * 2] = glyphBytes[0];
				bytes[i * 2 + 1] = glyphBytes[1];
			}

			return bytes;
		}

		#region Special Graphics State

		/// <returns> The graphics state stack depth after this operation </returns>
		public int SaveState() {
			if (streamLevel != GraphicsStreamState.PageDescription) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.PageDescription);
			}

			_stateStack.Push(state);
			state = new PdfGraphicsState(state);

			_operationsStack.Push(operations);
			GraphicsStateOperationsSet nextOperations = new GraphicsStateOperationsSet();
			operations.Append(nextOperations);
			operations = nextOperations;

			return _stateStack.Count;
		}

		/// <returns> The graphics state stack depth after this operation </returns>
		public int RestoreState() {
			if (streamLevel != GraphicsStreamState.PageDescription) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.PageDescription);
			}

			if (_stateStack.Count == 0) {
				throw new PdfInvalidGraphicsOperationException("Cannot restore graphics state, as the state stack is empty.");
			}
			state = _stateStack.Pop();

			operations = _operationsStack.Pop();

			return _stateStack.Count;
		}

		public GraphicsStream ConcatenateMatrix(float a, float b, float c, float d, float e, float f) {
			return ConcatenateMatrix(Transform.Matrix(a, b, c, d, e, f));
		}

		public GraphicsStream ConcatenateMatrix(Transform matrix) {
			if (streamLevel != GraphicsStreamState.PageDescription) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.PageDescription);
			}

			if (matrix != Transform.Identity) { // We can safely ignore Identity here
				operations.Append(new ConcatMatrixOperation(matrix));
			}

			return this;
		}

		#endregion

		#region General Graphics State

		public GraphicsStream LineWidth(float lineWidth) {
			if (!(streamLevel == GraphicsStreamState.PageDescription || streamLevel == GraphicsStreamState.Text)) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.PageDescription | GraphicsStreamState.Text);
			}

			if (state.Linewidth != lineWidth) {
				state.Linewidth = lineWidth;

				operations.Append(new LineWidthOperation(lineWidth));
			}

			return this;
		}

		public GraphicsStream LineCapStyle(LineCapStyle lineCap) {
			if (!(streamLevel == GraphicsStreamState.PageDescription || streamLevel == GraphicsStreamState.Text)) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.PageDescription | GraphicsStreamState.Text);
			}

			if (state.Linecapstyle != lineCap) {
				state.Linecapstyle = lineCap;

				operations.Append(new LineCapOperation(lineCap));
			}

			return this;
		}

		public GraphicsStream LineJoinStyle(LineJoinStyle lineJoin) {
			if (!(streamLevel == GraphicsStreamState.PageDescription || streamLevel == GraphicsStreamState.Text)) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.PageDescription | GraphicsStreamState.Text);
			}

			if (state.Linejoinstyle != lineJoin) {
				state.Linejoinstyle = lineJoin;

				operations.Append(new LineJoinOperation(lineJoin));
			}

			return this;
		}

		public GraphicsStream MitreLimit(float mitreLimit) {
			if (!(streamLevel == GraphicsStreamState.PageDescription || streamLevel == GraphicsStreamState.Text)) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.PageDescription | GraphicsStreamState.Text);
			}

			mitreLimit = Math.Max(1f, mitreLimit); // Minimum value of 1

			if (state.Miterlimit != mitreLimit) {
				state.Miterlimit = mitreLimit;

				operations.Append(new MitreLimitOperation(mitreLimit));
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

				operations.Append(new LineDashPatternOperation(dashArray, dashPhase));
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

				operations.Append(new SetGraphicsStateOperation(stateName));
			}

			return this;
		}

		#endregion

		#region Path Construction

		public GraphicsStream Move(float x, float y) {
			if (!(streamLevel == GraphicsStreamState.PageDescription || streamLevel == GraphicsStreamState.Path)) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.PageDescription | GraphicsStreamState.Path);
			}
			operations.Append(new MoveOperation(x, y));
			streamLevel = GraphicsStreamState.Path;
			return this;
		}

		public GraphicsStream Line(float x, float y) {
			if (streamLevel != GraphicsStreamState.Path) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.Path);
			}
			operations.Append(new LineOperation(x, y));
			streamLevel = GraphicsStreamState.Path;
			return this;
		}

		public GraphicsStream Cubic(float x1, float y1, float x2, float y2, float x3, float y3) {
			if (streamLevel != GraphicsStreamState.Path) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.Path);
			}
			operations.Append(new CubicOperation(x1, y1, x2, y2, x3, y3));
			streamLevel = GraphicsStreamState.Path;
			return this;
		}

		public GraphicsStream Quadratic(float x2, float y2, float x3, float y3) {
			if (streamLevel != GraphicsStreamState.Path) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.Path);
			}
			operations.Append(new QuadraticOperation(x2, y2, x3, y3));
			streamLevel = GraphicsStreamState.Path;
			return this;
		}

		public GraphicsStream Close() {
			if (streamLevel != GraphicsStreamState.Path) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.Path);
			}
			operations.Append(new CloseOperation());
			streamLevel = GraphicsStreamState.Path;
			return this;
		}

		public GraphicsStream Rectangle(float x, float y, float width, float height) {
			if (!(streamLevel == GraphicsStreamState.PageDescription || streamLevel == GraphicsStreamState.Path)) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.PageDescription | GraphicsStreamState.Path);
			}
			operations.Append(new RectangleOperation(x, y, width, height));
			streamLevel = GraphicsStreamState.Path;
			return this;
		}

		#endregion

		#region Path Painting

		public GraphicsStream Stroke() {
			if (!(streamLevel == GraphicsStreamState.Path || streamLevel == GraphicsStreamState.Clipping)) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.Path | GraphicsStreamState.Clipping);
			}
			operations.Append(new StrokeOperation());
			streamLevel = GraphicsStreamState.PageDescription;
			return this;
		}

		public GraphicsStream FillNonZero() {
			if (!(streamLevel == GraphicsStreamState.Path || streamLevel == GraphicsStreamState.Clipping)) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.Path | GraphicsStreamState.Clipping);
			}
			operations.Append(new FillNonZeroOperation());
			streamLevel = GraphicsStreamState.PageDescription;
			return this;
		}

		public GraphicsStream FillEvenOdd() {
			if (!(streamLevel == GraphicsStreamState.Path || streamLevel == GraphicsStreamState.Clipping)) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.Path | GraphicsStreamState.Clipping);
			}
			operations.Append(new FillEvenOddOperation());
			streamLevel = GraphicsStreamState.PageDescription;
			return this;
		}

		public GraphicsStream FillStrokeNonZero() {
			if (!(streamLevel == GraphicsStreamState.Path || streamLevel == GraphicsStreamState.Clipping)) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.Path | GraphicsStreamState.Clipping);
			}
			operations.Append(new FillStrokeNonZeroOperation());
			streamLevel = GraphicsStreamState.PageDescription;
			return this;
		}

		public GraphicsStream FillStrokeEvenOdd() {
			if (!(streamLevel == GraphicsStreamState.Path || streamLevel == GraphicsStreamState.Clipping)) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.Path | GraphicsStreamState.Clipping);
			}
			operations.Append(new FillStrokeEvenOddOperation());
			streamLevel = GraphicsStreamState.PageDescription;
			return this;
		}

		public GraphicsStream EndPath() {
			if (!(streamLevel == GraphicsStreamState.Path || streamLevel == GraphicsStreamState.Clipping)) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.Path | GraphicsStreamState.Clipping);
			}
			operations.Append(new EndPathOperation());
			streamLevel = GraphicsStreamState.PageDescription;
			return this;
		}

		#endregion

		#region Clipping

		public GraphicsStream ClipNonZero() {
			if (streamLevel != GraphicsStreamState.Path) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.Path);
			}
			operations.Append(new ClipNonZeroOperation());
			streamLevel = GraphicsStreamState.Clipping;
			return this;
		}

		public GraphicsStream ClipEvenOdd() {
			if (streamLevel != GraphicsStreamState.Path) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.Path);
			}
			operations.Append(new ClipEvenOddOperation());
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

			operations.Append(new BeginTextOperation());
			streamLevel = GraphicsStreamState.Text;

			return this;
		}

		public GraphicsStream EndText() {
			if (streamLevel != GraphicsStreamState.Text) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.Text);
			}

			if (markedContentSequences.Peek() != null) {
				throw new PdfInvalidGraphicsOperationException("Cannot end current text object before marked content sequence is closed.");
			}
			markedContentSequences.Pop();

			operations.Append(new EndTextOperation());
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

				operations.Append(new CharacterSpacingOperation(charSpace));
			}

			return this;
		}

		public GraphicsStream WordSpacing(float wordSpace) {
			if (!(streamLevel == GraphicsStreamState.PageDescription || streamLevel == GraphicsStreamState.Text)) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.PageDescription | GraphicsStreamState.Text);
			}

			if (state.WordSpacing != wordSpace) {
				state.WordSpacing = wordSpace;

				operations.Append(new WordSpacingOperation(wordSpace));
			}

			return this;
		}

		public GraphicsStream TextHorizontalScale(float scale) {
			if (!(streamLevel == GraphicsStreamState.PageDescription || streamLevel == GraphicsStreamState.Text)) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.PageDescription | GraphicsStreamState.Text);
			}

			if (state.TextHorizontalScaling != scale) {
				state.TextHorizontalScaling = scale;

				operations.Append(new TextHorizontalScaleOperation(scale));
			}

			return this;

		}

		public GraphicsStream TextLeading(float leading) {
			if (!(streamLevel == GraphicsStreamState.PageDescription || streamLevel == GraphicsStreamState.Text)) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.PageDescription | GraphicsStreamState.Text);
			}

			if (state.TextLeading != leading) {
				state.TextLeading = leading;

				operations.Append(new TextLeadingOperation(leading));
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

				operations.Append(new FontAndSizeOperation(fontName, size));
			}

			return this;
		}

		public GraphicsStream TextRenderingMode(TextRenderingMode render) {
			if (!(streamLevel == GraphicsStreamState.PageDescription || streamLevel == GraphicsStreamState.Text)) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.PageDescription | GraphicsStreamState.Text);
			}

			if (state.TextRenderingMode != render) {
				state.TextRenderingMode = render;

				operations.Append(new TextRenderingModeOperation(render));
			}

			return this;
		}

		public GraphicsStream TextRise(float rise) {
			if (!(streamLevel == GraphicsStreamState.PageDescription || streamLevel == GraphicsStreamState.Text)) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.PageDescription | GraphicsStreamState.Text);
			}

			if (state.TextRise != rise) {
				state.TextRise = rise;

				operations.Append(new TextRiseOperation(rise));
			}

			return this;
		}

		#endregion Text State

		#region Text Positioning

		public GraphicsStream MoveToStart(float tx, float ty) {
			if (streamLevel != GraphicsStreamState.Text) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.Text);
			}
			operations.Append(TextMoveToStartOperation.Create(tx, ty));
			return this;
		}

		public GraphicsStream MoveToStartSetLeading(float tx, float ty) {
			if (streamLevel != GraphicsStreamState.Text) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.Text);
			}

			state.TextLeading = -ty;

			operations.Append(new TextMoveToStartSetLeadingOperation(tx, ty));

			return this;
		}

		public GraphicsStream SetTextMatrix(float a, float b, float c, float d, float e, float f) {
			return SetTextMatrix(Transform.Matrix(a, b, c, d, e, f));
		}

		public GraphicsStream SetTextMatrix(Transform transform) {
			if (streamLevel != GraphicsStreamState.Text) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.Text);
			}
			operations.Append(new SetTextMatrixOperation(transform));
			return this;
		}

		public GraphicsStream MoveToStart() {
			if (streamLevel != GraphicsStreamState.Text) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.Text);
			}
			operations.Append(TextMoveToStartOperation.Create());
			return this;
		}

		#endregion Text Positioning

		#region Text Showing

		public GraphicsStream ShowText(string text) {
			if (streamLevel != GraphicsStreamState.Text) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.Text);
			}
			operations.Append(new ShowTextOperation(GetTextBytes(text)));
			return this;
		}

		public GraphicsStream MoveNextLineShowText(string text) {
			if (streamLevel != GraphicsStreamState.Text) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.Text);
			}
			operations.Append(new MoveNextLineShowTextOperation(GetTextBytes(text)));
			return this;
		}

		public GraphicsStream MoveNextLineShowText(float aw, float ac, string text) {
			if (streamLevel != GraphicsStreamState.Text) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.Text);
			}

			state.WordSpacing = aw;
			state.CharacterSpacing = ac;

			operations.Append(new SetSpacingMoveNextLineShowTextOperation(aw, ac, GetTextBytes(text)));

			return this;
		}

		public GraphicsStream ShowTextWithPositioning(params (string text, float? offset)[] array) {
			if (streamLevel != GraphicsStreamState.Text) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.Text);
			}

			operations.Append(new ShowTextWithPositioningOperation(array.Select(i => (GetTextBytes(i.text), i.offset)).ToArray()));

			return this;
		}

		private GraphicsStream ShowTextWithPositioning(params (ushort[] glyphs, short? offset)[] array) {
			if (streamLevel != GraphicsStreamState.Text) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.Text);
			}

			operations.Append(new ShowTextWithPositioningOperation(array.Select<(ushort[] glyphs, short? offset), (byte[], float?)>(i => (GetTextBytes(i.glyphs), i.offset.HasValue ? (float)i.offset : null)).ToArray()));

			return this;
		}

		public GraphicsStream ShowTextCalculateKerning(string text) {
			if (text.Length > 0 && state.Font?.font is PdfGlyphFont glyphFont) { // Is this a little brittle?

				PositionedGlyphRun positionedGlyphs = glyphFont.GetGlyphRun(text, out FontGlyphUsage fontUsage);
				resources.RegisterFontUsage(glyphFont, fontUsage);

				List<(ushort[] glyphs, short? offset)> positioned1 = new List<(ushort[], short?)>();

				void AddEntry(ushort[] gids, short? offset) {
					offset = offset.HasValue && offset.Value == 0 ? null : offset;
					if (gids.Length == 0 && offset.HasValue && positioned1.Count > 0) {
						positioned1[^1] = (positioned1[^1].glyphs, (short)((positioned1[^1].offset ?? 0) + offset.Value));
					}
					else if (gids.Length > 0 && positioned1.Count > 0 && !positioned1[^1].offset.HasValue) {
						positioned1[^1] = (positioned1[^1].glyphs.Concat(gids).ToArray(), offset);
					}
					else if (gids.Length > 0 || offset.HasValue) {
						positioned1.Add((gids, offset));
					}
				}

				List<ushort> builder = new List<ushort>();

				// TODO It might be good to compare against current text rise, and only adjust if different
				float baseTestRise = state.TextRise;
				short currentRise = 0;

				for (int i = 0; i < positionedGlyphs.Count; i++) {
					(short xPlacement, short yPlacement) = positionedGlyphs.GetPlacement(i);
					if (xPlacement != 0) {
						if (builder.Count > 0) {
							AddEntry(builder.ToArray(), null);
							builder.Clear();
						}

						AddEntry(Array.Empty<ushort>(), (short)-xPlacement);
					}
					if (yPlacement != currentRise) {
						if (builder.Count > 0) {
							AddEntry(builder.ToArray(), null);
							builder.Clear();
						}
						if (positioned1.Count > 0) {
							ShowTextWithPositioning(positioned1.ToArray());
							positioned1.Clear();
						}
						TextRise(baseTestRise + PdfFont.ConvertDesignSpaceValue(yPlacement, state.Font.size));
						currentRise = yPlacement;
					}

					builder.Add(positionedGlyphs[i]);

					(short xAdvDelta, _) = positionedGlyphs.GetAdvanceDelta(i);
					if (xAdvDelta != 0 || xPlacement != 0) {
						AddEntry(builder.ToArray(), (short)(-xAdvDelta + xPlacement));
						builder.Clear();
					}
				}

				if (builder.Count > 0) {
					AddEntry(builder.ToArray(), null);
				}
				if (positioned1.Count > 0) {
					ShowTextWithPositioning(positioned1.ToArray());
				}

				if (currentRise != 0) {
					TextRise(baseTestRise);
				}

				return this;
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

				PdfName colorspaceName;
				if (colorSpace.IsBuiltIn) {
					colorspaceName = colorSpace.BuiltInName;
				}
				else {
					resources.AddColorSpace(colorSpace, out colorspaceName);
				}

				operations.Append(new SetStrokingColorSpaceOperation(colorspaceName));
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

				PdfName colorspaceName;
				if (colorSpace.IsBuiltIn) {
					colorspaceName = colorSpace.BuiltInName;
				}
				else {
					resources.AddColorSpace(colorSpace, out colorspaceName);
				}

				operations.Append(new SetNonStrokingColorSpaceOperation(colorspaceName));
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

				state.StrokingColor = newColor;

				operations.Append(new SetStrokingPatternOperation(patternName, values));
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

				state.NonStrokingColor = newColor;

				operations.Append(new SetNonStrokingPatternOperation(patternName, values));
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

				operations.Append(new SetStrokingGrayOperation(gray));
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

				operations.Append(new SetNonStrokingGrayOperation(gray));
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

				operations.Append(new SetStrokingRGBOperation(r, g, b));
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

				operations.Append(new SetNonStrokingRGBOperation(r, g, b));
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

				operations.Append(new SetStrokingCMYKOperation(c, m, y, k));
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

				operations.Append(new SetNonStrokingCMYKOperation(c, m, y, k));
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

				operations.Append(new SetStrokingColorOperation(color));
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

				operations.Append(new SetNonStrokingColorOperation(color));
			}

			return this;
		}

		public GraphicsStream SetStrokingAlphaConstant(float alpha) {
			PdfGraphicsStateParameterDictionary paramDict = new PdfGraphicsStateParameterDictionary(strokingAlphaConstant: float.Clamp(alpha, 0f, 1f));
			return SetGraphicsState(paramDict);
		}

		public GraphicsStream SetNonStrokingAlphaConstant(float alpha) {
			PdfGraphicsStateParameterDictionary paramDict = new PdfGraphicsStateParameterDictionary(nonStrokingAlphaConstant: float.Clamp(alpha, 0f, 1f));
			return SetGraphicsState(paramDict);
		}

		#endregion

		#region Shading Patterns

		public GraphicsStream PaintShading(PdfShadingDictionary shading) {
			if (streamLevel != GraphicsStreamState.PageDescription) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.PageDescription);
			}
			resources.AddShading(shading, out PdfName shadingName);
			operations.Append(new PaintShadingOperation(shadingName));
			return this;
		}

		#endregion

		#region External Objects 

		public GraphicsStream PaintXObject(PdfXObject xObject) {
			if (streamLevel != GraphicsStreamState.PageDescription) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.PageDescription);
			}
			resources.AddXObject(xObject, out PdfName xObjectName);
			operations.Append(new PaintXObjectOperation(xObjectName));
			return this;
		}

		#endregion

		#region Marked Content

		public GraphicsStream BeginMarkedContent(PdfName tag) {
			if (!(streamLevel == GraphicsStreamState.PageDescription || streamLevel == GraphicsStreamState.Text)) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.PageDescription | GraphicsStreamState.Text);
			}

			markedContentSequences.Push(tag);

			operations.Append(new BeginMarkedContentOperation(tag));

			return this;
		}

		public GraphicsStream EndMarkedContent() {
			if (!(streamLevel == GraphicsStreamState.PageDescription || streamLevel == GraphicsStreamState.Text)) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.PageDescription | GraphicsStreamState.Text);
			}

			// null value indicates that the last thing opened was a text object, and therefore we cannot close the sequence (no interleaving allowed)
			if (markedContentSequences.Count > 0 && markedContentSequences.Peek() == null) {
				throw new PdfInvalidGraphicsOperationException("Cannot end current marked content sequence before text object is closed.");
			}
			if (markedContentSequences.Count < 1) {
				throw new PdfInvalidGraphicsOperationException("There is no marked content sequence to end at this point in the stream.");
			}

			operations.Append(new EndMarkedContentOperation());

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

			return new PdfTextString(Encoding.ASCII.GetString(defaultAppearanceStream.GetStream().ToArray()).Trim());
		}

		public static PdfString GetTextFieldDefaultAppearance(PdfResourcesDictionary resources, PdfDeviceColor color) {
			GraphicsStream defaultAppearanceStream = new GraphicsStream(resources, false) {
				streamLevel = GraphicsStreamState.Text
			};

			defaultAppearanceStream.SetNonStrokingColor(color);

			return new PdfTextString(Encoding.ASCII.GetString(defaultAppearanceStream.GetStream().ToArray()).Trim());
		}

		#endregion
	}

	public class PdfGraphicsStreamWriter : PdfStreamWriter {
		private readonly bool useEOL;

		public PdfGraphicsStreamWriter(Stream stream, bool useEOL) : base(stream) {
			this.useEOL = useEOL;
		}

		public void WriteOperator(string operatorStr) {
			WriteASCII(operatorStr);

			if (useEOL) {
				WriteEOL();
			}
			else {
				WriteSpace();
			}
		}

		public void WriteFloats(params float[] values) {
			for (int i = 0; i < values.Length; i++) {
				if (i > 0) {
					WriteSpace();
				}
				WriteFloat(values[i]);
			}
		}

		public void WriteColorFloats(params float[] values) {
			for (int i = 0; i < values.Length; i++) {
				if (i > 0) {
					WriteSpace();
				}
				WriteFloat(ClampColor(values[i]));
			}
		}

		public void WriteFloatArray(float[] array) {
			WriteASCII("[");
			WriteFloats(array);
			WriteASCII("]");
		}

		public void WriteText(byte[] textBytes) {
			WriteASCII("<");
			WriteASCII(HexWriter.ToString(textBytes));
			WriteASCII(">");
		}

		private static float ClampColor(float value) {
			return Math.Max(0f, Math.Min(1f, value));
		}

	}

	public abstract class GraphicsOperation {

		public abstract bool StateChange { get; }
		public abstract bool HasGraphicsWrite { get; }

		public abstract void WriteTo(PdfGraphicsStreamWriter writer);

	}

	public class GraphicsOperationsSet : GraphicsOperation {

		public override bool StateChange => ops.Any(o => o.StateChange);
		public override bool HasGraphicsWrite => ops.Any(o => o.HasGraphicsWrite);

		protected readonly List<GraphicsOperation> ops;

		public GraphicsOperationsSet() {
			ops = new List<GraphicsOperation>();
		}

		public void Append(GraphicsOperation op) {
			ops.Add(op);
		}

		public override void WriteTo(PdfGraphicsStreamWriter writer) {
			foreach (GraphicsOperation op in ops) {
				op.WriteTo(writer);
			}
		}

	}

	public class GraphicsStateOperationsSet : GraphicsOperationsSet {

		public GraphicsStateOperationsSet() : base() { }

		protected void WriteTo(PdfGraphicsStreamWriter writer, bool isLast) {
			if (!HasGraphicsWrite) { return; }
			bool needsStateStorage = StateChange && !isLast;
			if (needsStateStorage) { writer.WriteOperator("q"); }

			foreach (GraphicsOperation op in ops[..^1]) {
				op.WriteTo(writer);
			}
			if (ops.Count > 0) {
				if (ops[^1] is GraphicsStateOperationsSet lastStatefulSet) {
					lastStatefulSet.WriteTo(writer, true);
				}
				else {
					ops[^1].WriteTo(writer);
				}
			}

			if (needsStateStorage) { writer.WriteOperator("Q"); }
		}

		public override void WriteTo(PdfGraphicsStreamWriter writer) {
			WriteTo(writer, false);
		}

	}

	public abstract class GraphicsStateChangeOperation : GraphicsOperation {

		public sealed override bool StateChange { get; } = true;
		public sealed override bool HasGraphicsWrite { get; } = false;

	}

	public abstract class GraphicsStatelessOperation : GraphicsOperation {

		public sealed override bool StateChange { get; } = false;

	}

	public abstract class GraphicsNumericStateChangeOperation : GraphicsStateChangeOperation {

		public readonly float Value;
		public readonly string Operator;

		protected GraphicsNumericStateChangeOperation(float value, string opCode) {
			Value = value;
			Operator = opCode;
		}

		public sealed override void WriteTo(PdfGraphicsStreamWriter writer) {
			writer.WriteFloat(Value);
			writer.WriteSpace();
			writer.WriteOperator(Operator);
		}

	}

	public class ConcatMatrixOperation : GraphicsStateChangeOperation {

		public readonly Transform Matrix;

		public ConcatMatrixOperation(Transform matrix) {
			Matrix = matrix;
		}

		public override void WriteTo(PdfGraphicsStreamWriter writer) {
			writer.WriteFloats(Matrix.a, Matrix.b, Matrix.c, Matrix.d, Matrix.e, Matrix.f);
			writer.WriteSpace();
			writer.WriteOperator("cm");
		}
	}

	public class LineWidthOperation : GraphicsStateChangeOperation {

		public readonly float LineWidth;

		public LineWidthOperation(float lineWidth) {
			LineWidth = lineWidth;
		}

		public override void WriteTo(PdfGraphicsStreamWriter writer) {
			writer.WriteFloat(LineWidth);
			writer.WriteSpace();
			writer.WriteOperator("w");
		}

	}

	public class LineCapOperation : GraphicsStateChangeOperation {

		public readonly LineCapStyle LineCap;

		public LineCapOperation(LineCapStyle lineCap) {
			LineCap = lineCap;
		}

		public override void WriteTo(PdfGraphicsStreamWriter writer) {
			writer.WriteInt((int)LineCap);
			writer.WriteSpace();
			writer.WriteOperator("J");
		}

	}

	public class LineJoinOperation : GraphicsStateChangeOperation {

		public readonly LineJoinStyle LineJoin;

		public LineJoinOperation(LineJoinStyle lineJoin) {
			LineJoin = lineJoin;
		}

		public override void WriteTo(PdfGraphicsStreamWriter writer) {
			writer.WriteInt((int)LineJoin);
			writer.WriteSpace();
			writer.WriteOperator("j");
		}

	}

	public class MitreLimitOperation : GraphicsStateChangeOperation {

		public readonly float MitreLimit;

		public MitreLimitOperation(float mitreLimit) {
			MitreLimit = mitreLimit;
		}

		public override void WriteTo(PdfGraphicsStreamWriter writer) {
			writer.WriteFloat(MitreLimit);
			writer.WriteSpace();
			writer.WriteOperator("M");
		}

	}

	public class LineDashPatternOperation : GraphicsStateChangeOperation {

		public readonly float[]? DashArray;
		public readonly float DashPhase;

		public LineDashPatternOperation(float[]? dashArray, float dashPhase) {
			DashArray = dashArray;
			DashPhase = dashPhase;
		}

		public override void WriteTo(PdfGraphicsStreamWriter writer) {
			writer.WriteFloatArray(DashArray ?? Array.Empty<float>()); // TODO Is this fallback correct?
			writer.WriteSpace();
			writer.WriteFloat(DashPhase);
			writer.WriteSpace();
			writer.WriteOperator("d");
		}

	}

	public class SetGraphicsStateOperation : GraphicsStateChangeOperation {

		public readonly PdfName StateName;

		public SetGraphicsStateOperation(PdfName stateName) {
			StateName = stateName;
		}

		public override void WriteTo(PdfGraphicsStreamWriter writer) {
			writer.WriteName(StateName);
			writer.WriteSpace();
			writer.WriteOperator("gs");
		}

	}

	public class MoveOperation : GraphicsStatelessOperation {

		public override bool HasGraphicsWrite { get; } = false;

		public readonly float X, Y;

		public MoveOperation(float x, float y) {
			X = x;
			Y = y;
		}

		public override void WriteTo(PdfGraphicsStreamWriter writer) {
			writer.WriteFloats(X, Y);
			writer.WriteSpace();
			writer.WriteOperator("m");
		}

	}

	public class LineOperation : GraphicsStatelessOperation {

		public override bool HasGraphicsWrite { get; } = false;

		public readonly float X, Y;

		public LineOperation(float x, float y) {
			X = x;
			Y = y;
		}

		public override void WriteTo(PdfGraphicsStreamWriter writer) {
			writer.WriteFloats(X, Y);
			writer.WriteSpace();
			writer.WriteOperator("l");
		}

	}

	public class CubicOperation : GraphicsStatelessOperation {

		public override bool HasGraphicsWrite { get; } = false;

		public readonly float X1, Y1, X2, Y2, X3, Y3;

		public CubicOperation(float x1, float y1, float x2, float y2, float x3, float y3) {
			X1 = x1;
			Y1 = y1;
			X2 = x2;
			Y2 = y2;
			X3 = x3;
			Y3 = y3;
		}

		public override void WriteTo(PdfGraphicsStreamWriter writer) {
			writer.WriteFloats(X1, Y1, X2, Y2, X3, Y3);
			writer.WriteSpace();
			writer.WriteOperator("c");
		}

	}

	public class QuadraticOperation : GraphicsStatelessOperation {

		public override bool HasGraphicsWrite { get; } = false;

		public readonly float X2, Y2, X3, Y3;

		public QuadraticOperation(float x2, float y2, float x3, float y3) {
			X2 = x2;
			Y2 = y2;
			X3 = x3;
			Y3 = y3;
		}

		public override void WriteTo(PdfGraphicsStreamWriter writer) {
			writer.WriteFloats(X2, Y2, X3, Y3);
			writer.WriteSpace();
			writer.WriteOperator("v"); // v or y?
		}

	}

	public class CloseOperation : GraphicsStatelessOperation {

		public override bool HasGraphicsWrite { get; } = false;

		public override void WriteTo(PdfGraphicsStreamWriter writer) {
			writer.WriteOperator("h");
		}

	}

	public class RectangleOperation : GraphicsStatelessOperation {

		public override bool HasGraphicsWrite { get; } = false;

		public readonly float X, Y, Width, Height;

		public RectangleOperation(float x, float y, float width, float height) {
			X = x;
			Y = y;
			Width = width;
			Height = height;
		}

		public override void WriteTo(PdfGraphicsStreamWriter writer) {
			writer.WriteFloats(X, Y, Width, Height);
			writer.WriteSpace();
			writer.WriteOperator("re");
		}

	}

	public class StrokeOperation : GraphicsStatelessOperation {

		public override bool HasGraphicsWrite { get; } = true;

		public override void WriteTo(PdfGraphicsStreamWriter writer) {
			writer.WriteOperator("S");
		}

	}

	public class FillNonZeroOperation : GraphicsStatelessOperation {

		public override bool HasGraphicsWrite { get; } = true;

		public override void WriteTo(PdfGraphicsStreamWriter writer) {
			writer.WriteOperator("f");
		}

	}

	public class FillEvenOddOperation : GraphicsStatelessOperation {

		public override bool HasGraphicsWrite { get; } = true;

		public override void WriteTo(PdfGraphicsStreamWriter writer) {
			writer.WriteOperator("f*");
		}

	}

	public class FillStrokeNonZeroOperation : GraphicsStatelessOperation {

		public override bool HasGraphicsWrite { get; } = true;

		public override void WriteTo(PdfGraphicsStreamWriter writer) {
			writer.WriteOperator("B");
		}

	}

	public class FillStrokeEvenOddOperation : GraphicsStatelessOperation {

		public override bool HasGraphicsWrite { get; } = true;

		public override void WriteTo(PdfGraphicsStreamWriter writer) {
			writer.WriteOperator("B*");
		}

	}

	public class EndPathOperation : GraphicsStatelessOperation {

		public override bool HasGraphicsWrite { get; } = false;

		public override void WriteTo(PdfGraphicsStreamWriter writer) {
			writer.WriteOperator("n");
		}

	}

	public abstract class ClippingGraphicsOperation : GraphicsStateChangeOperation { }

	public class ClipNonZeroOperation : ClippingGraphicsOperation {

		public override void WriteTo(PdfGraphicsStreamWriter writer) {
			writer.WriteOperator("W");
		}

	}

	public class ClipEvenOddOperation : ClippingGraphicsOperation {

		public override void WriteTo(PdfGraphicsStreamWriter writer) {
			writer.WriteOperator("W*");
		}

	}

	public class BeginTextOperation : GraphicsStatelessOperation {

		public override bool HasGraphicsWrite { get; } = false;

		public override void WriteTo(PdfGraphicsStreamWriter writer) {
			writer.WriteOperator("BT");
		}

	}

	public class EndTextOperation : GraphicsStatelessOperation {

		public override bool HasGraphicsWrite { get; } = false;

		public override void WriteTo(PdfGraphicsStreamWriter writer) {
			writer.WriteOperator("ET");
		}

	}

	public class CharacterSpacingOperation : GraphicsStateChangeOperation {

		public readonly float CharacterSpacing;

		public CharacterSpacingOperation(float charSpace) {
			CharacterSpacing = charSpace;
		}

		public override void WriteTo(PdfGraphicsStreamWriter writer) {
			writer.WriteFloat(CharacterSpacing);
			writer.WriteSpace();
			writer.WriteOperator("Tc");
		}

	}

	public class WordSpacingOperation : GraphicsStateChangeOperation {

		public readonly float WordSpacing;

		public WordSpacingOperation(float wordSpace) {
			WordSpacing = wordSpace;
		}

		public override void WriteTo(PdfGraphicsStreamWriter writer) {
			writer.WriteFloat(WordSpacing);
			writer.WriteSpace();
			writer.WriteOperator("Tw");
		}

	}

	public class TextHorizontalScaleOperation : GraphicsStateChangeOperation {

		public readonly float TextHorizontalScale;

		public TextHorizontalScaleOperation(float scale) {
			TextHorizontalScale = scale;
		}

		public override void WriteTo(PdfGraphicsStreamWriter writer) {
			writer.WriteFloat(TextHorizontalScale);
			writer.WriteSpace();
			writer.WriteOperator("Tz");
		}

	}

	public class TextLeadingOperation : GraphicsStateChangeOperation {

		public readonly float TextLeading;

		public TextLeadingOperation(float leading) {
			TextLeading = leading;
		}

		public override void WriteTo(PdfGraphicsStreamWriter writer) {
			writer.WriteFloat(TextLeading);
			writer.WriteSpace();
			writer.WriteOperator("TL");
		}

	}

	public class FontAndSizeOperation : GraphicsStateChangeOperation {

		public readonly PdfName FontName;
		public readonly float FontSize;

		public FontAndSizeOperation(PdfName fontName, float fontSize) {
			FontName = fontName;
			FontSize = fontSize;
		}

		public override void WriteTo(PdfGraphicsStreamWriter writer) {
			writer.WriteName(FontName);
			writer.WriteSpace();
			writer.WriteFloat(FontSize);
			writer.WriteSpace();
			writer.WriteOperator("Tf");
		}

	}

	public class TextRenderingModeOperation : GraphicsStateChangeOperation {

		public readonly TextRenderingMode TextRenderingMode;

		public TextRenderingModeOperation(TextRenderingMode render) {
			TextRenderingMode = render;
		}

		public override void WriteTo(PdfGraphicsStreamWriter writer) {
			writer.WriteInt((int)TextRenderingMode);
			writer.WriteSpace();
			writer.WriteOperator("Tr");
		}

	}

	public class TextRiseOperation : GraphicsStateChangeOperation {

		public readonly float TextRise;

		public TextRiseOperation(float rise) {
			TextRise = rise;
		}

		public override void WriteTo(PdfGraphicsStreamWriter writer) {
			writer.WriteFloat(TextRise);
			writer.WriteSpace();
			writer.WriteOperator("Ts");
		}

	}

	public abstract class TextMoveToStartOperation : GraphicsStatelessOperation {

		public override bool HasGraphicsWrite { get; } = false;

		public static TextMoveToStartOperation Create(float tx, float ty) {
			return new TextMoveToStartCoordinatesOperation(tx, ty);
		}

		public static TextMoveToStartOperation Create() {
			return new TextMoveToStartNoCoordinatesOperation();
		}

		public class TextMoveToStartCoordinatesOperation : TextMoveToStartOperation {
			
			public readonly float tx, ty;

			public TextMoveToStartCoordinatesOperation(float tx, float ty) {
				this.tx = tx;
				this.ty = ty;
			}

			public override void WriteTo(PdfGraphicsStreamWriter writer) {
				writer.WriteFloats(tx, ty);
				writer.WriteSpace();
				writer.WriteOperator("Td");
			}

		}

		public class TextMoveToStartNoCoordinatesOperation : TextMoveToStartOperation {

			public override void WriteTo(PdfGraphicsStreamWriter writer) {
				writer.WriteOperator("T*");
			}

		}

	}

	public class TextMoveToStartSetLeadingOperation : GraphicsStateChangeOperation {

		public readonly float tx, ty;

		public TextMoveToStartSetLeadingOperation(float tx, float ty) {
			this.tx = tx;
			this.ty = ty;
		}

		public override void WriteTo(PdfGraphicsStreamWriter writer) {
			writer.WriteFloats(tx, ty);
			writer.WriteSpace();
			writer.WriteOperator("TD");
		}

	}

	public class SetTextMatrixOperation : GraphicsStateChangeOperation {

		public readonly Transform Matrix;

		public SetTextMatrixOperation(Transform matrix) {
			Matrix = matrix;
		}

		public override void WriteTo(PdfGraphicsStreamWriter writer) {
			writer.WriteFloats(Matrix.a, Matrix.b, Matrix.c, Matrix.d, Matrix.e, Matrix.f);
			writer.WriteSpace();
			writer.WriteOperator("tm");
		}

	}

	public class ShowTextOperation : GraphicsStatelessOperation {

		public override bool HasGraphicsWrite { get; } = true;

		private readonly byte[] textBytes;

		public ShowTextOperation(byte[] textBytes) {
			this.textBytes = textBytes;
		}

		public override void WriteTo(PdfGraphicsStreamWriter writer) {
			writer.WriteText(textBytes);
			writer.WriteSpace();
			writer.WriteOperator("Tj");
		}

	}

	public class MoveNextLineShowTextOperation : GraphicsStatelessOperation {

		public override bool HasGraphicsWrite { get; } = true;

		private readonly byte[] textBytes;

		public MoveNextLineShowTextOperation(byte[] textBytes) {
			this.textBytes = textBytes;
		}

		public override void WriteTo(PdfGraphicsStreamWriter writer) {
			writer.WriteText(textBytes);
			writer.WriteSpace();
			writer.WriteOperator("'");
		}

	}

	public class SetSpacingMoveNextLineShowTextOperation : GraphicsOperation {

		public override bool StateChange { get; } = true;
		public override bool HasGraphicsWrite { get; } = true;

		private readonly float wordSpacing, charSpacing;
		private readonly byte[] textBytes;

		public SetSpacingMoveNextLineShowTextOperation(float aw, float ac, byte[] textBytes) {
			this.wordSpacing = aw;
			this.charSpacing = ac;
			this.textBytes = textBytes;
		}

		public override void WriteTo(PdfGraphicsStreamWriter writer) {
			writer.WriteFloats(wordSpacing, charSpacing);
			writer.WriteSpace();
			writer.WriteText(textBytes);
			writer.WriteSpace();
			writer.WriteOperator("\"");
		}

	}

	public class ShowTextWithPositioningOperation : GraphicsStatelessOperation {

		public override bool HasGraphicsWrite { get; } = true;

		private readonly (byte[] textBytes, float? offset)[] array;

		public ShowTextWithPositioningOperation((byte[] textBytes, float? offset)[] array) {
			this.array = array;
		}

		public override void WriteTo(PdfGraphicsStreamWriter writer) {

			if (array.Length == 1 && !array[0].offset.HasValue) {
				writer.WriteText(array[0].textBytes);
				writer.WriteSpace();
				writer.WriteOperator("Tj");
				return;
			}
			else {
				writer.WriteASCII("[");
				writer.WriteSpace();

				for (int i = 0; i < array.Length; i++) {
					if (array[i].textBytes.Length > 0) {
						writer.WriteText(array[i].textBytes);
						writer.WriteSpace();
					}
					if (array[i].offset.HasValue) {
						writer.WriteFloat(array[i].offset!.Value);
						writer.WriteSpace();
					}
				}

				writer.WriteASCII("]");
				writer.WriteSpace();
				writer.WriteOperator("TJ");
			}
		}

	}

	public class SetStrokingColorSpaceOperation : GraphicsStateChangeOperation {

		public readonly PdfName ColorspaceName;

		public SetStrokingColorSpaceOperation(PdfName colorspaceName) {
			ColorspaceName = colorspaceName;
		}

		public override void WriteTo(PdfGraphicsStreamWriter writer) {
			writer.WriteName(ColorspaceName);
			writer.WriteSpace();
			writer.WriteOperator("CS");
		}

	}

	public class SetNonStrokingColorSpaceOperation : GraphicsStateChangeOperation {

		public readonly PdfName ColorspaceName;

		public SetNonStrokingColorSpaceOperation(PdfName colorspaceName) {
			ColorspaceName = colorspaceName;
		}

		public override void WriteTo(PdfGraphicsStreamWriter writer) {
			writer.WriteName(ColorspaceName);
			writer.WriteSpace();
			writer.WriteOperator("cs");
		}

	}

	public class SetStrokingPatternOperation : GraphicsStateChangeOperation {

		public readonly PdfName PatternName;
		public readonly float[]? Values;

		public SetStrokingPatternOperation(PdfName patternName, float[]? values) {
			PatternName = patternName;
			Values = values;
		}

		public override void WriteTo(PdfGraphicsStreamWriter writer) {
			if (Values is not null && Values.Length > 0) {
				writer.WriteColorFloats(Values);
				writer.WriteSpace();
			}

			writer.WriteName(PatternName);
			writer.WriteSpace();
			writer.WriteOperator("SCN");
		}

	}

	public class SetNonStrokingPatternOperation : GraphicsStateChangeOperation {

		public readonly PdfName PatternName;
		public readonly float[]? Values;

		public SetNonStrokingPatternOperation(PdfName patternName, float[]? values) {
			PatternName = patternName;
			Values = values;
		}

		public override void WriteTo(PdfGraphicsStreamWriter writer) {
			if (Values is not null && Values.Length > 0) {
				writer.WriteColorFloats(Values);
				writer.WriteSpace();
			}

			writer.WriteName(PatternName);
			writer.WriteSpace();
			writer.WriteOperator("scn");
		}

	}

	public class SetStrokingGrayOperation : GraphicsStateChangeOperation {

		public readonly float Gray;

		public SetStrokingGrayOperation(float gray) {
			Gray = gray;
		}

		public override void WriteTo(PdfGraphicsStreamWriter writer) {
			writer.WriteFloat(float.Clamp(Gray, 0f, 1f));
			writer.WriteSpace();
			writer.WriteOperator("G");
		}

	}

	public class SetNonStrokingGrayOperation : GraphicsStateChangeOperation {

		public readonly float Gray;

		public SetNonStrokingGrayOperation(float gray) {
			Gray = gray;
		}

		public override void WriteTo(PdfGraphicsStreamWriter writer) {
			writer.WriteFloat(float.Clamp(Gray, 0f, 1f));
			writer.WriteSpace();
			writer.WriteOperator("g");
		}

	}

	public class SetStrokingRGBOperation : GraphicsStateChangeOperation {

		public readonly float R, G, B;

		public SetStrokingRGBOperation(float r, float g, float b) {
			R = r;
			G = g;
			B = b;
		}

		public override void WriteTo(PdfGraphicsStreamWriter writer) {
			writer.WriteFloats(float.Clamp(R, 0f, 1f), float.Clamp(G, 0f, 1f), float.Clamp(B, 0f, 1f));
			writer.WriteSpace();
			writer.WriteOperator("RG");
		}

	}

	public class SetNonStrokingRGBOperation : GraphicsStateChangeOperation {

		public readonly float R, G, B;

		public SetNonStrokingRGBOperation(float r, float g, float b) {
			R = r;
			G = g;
			B = b;
		}

		public override void WriteTo(PdfGraphicsStreamWriter writer) {
			writer.WriteFloats(float.Clamp(R, 0f, 1f), float.Clamp(G, 0f, 1f), float.Clamp(B, 0f, 1f));
			writer.WriteSpace();
			writer.WriteOperator("rg");
		}

	}

	public class SetStrokingCMYKOperation : GraphicsStateChangeOperation {

		public readonly float C, M, Y, K;

		public SetStrokingCMYKOperation(float c, float m, float y, float k) {
			C = c;
			M = m;
			Y = y;
			K = k;
		}

		public override void WriteTo(PdfGraphicsStreamWriter writer) {
			writer.WriteFloats(float.Clamp(C, 0f, 1f), float.Clamp(M, 0f, 1f), float.Clamp(Y, 0f, 1f), float.Clamp(K, 0f, 1f));
			writer.WriteSpace();
			writer.WriteOperator("K");
		}

	}

	public class SetNonStrokingCMYKOperation : GraphicsStateChangeOperation {

		public readonly float C, M, Y, K;

		public SetNonStrokingCMYKOperation(float c, float m, float y, float k) {
			C = c;
			M = m;
			Y = y;
			K = k;
		}

		public override void WriteTo(PdfGraphicsStreamWriter writer) {
			writer.WriteFloats(float.Clamp(C, 0f, 1f), float.Clamp(M, 0f, 1f), float.Clamp(Y, 0f, 1f), float.Clamp(K, 0f, 1f));
			writer.WriteSpace();
			writer.WriteOperator("k");
		}

	}

	public class SetStrokingColorOperation : GraphicsStateChangeOperation {

		public readonly PdfDeviceColor Color;

		public SetStrokingColorOperation(PdfDeviceColor color) {
			Color = color;
		}

		public override void WriteTo(PdfGraphicsStreamWriter writer) {
			writer.WriteFloats(Color.Values);
			writer.WriteSpace();

			if (Color is PdfGrayColor) {
				writer.WriteOperator("G");
			}
			else if (Color is PdfRGBColor) {
				writer.WriteOperator("RG");
			}
			else if (Color is PdfCMYKColor) {
				writer.WriteOperator("K");
			}
			else {
				throw new NotImplementedException($"Unrecognised PdfDeviceColor of type {Color.GetType().Name}.");
			}
		}

	}

	public class SetNonStrokingColorOperation : GraphicsStateChangeOperation {

		public readonly PdfDeviceColor Color;

		public SetNonStrokingColorOperation(PdfDeviceColor color) {
			Color = color;
		}

		public override void WriteTo(PdfGraphicsStreamWriter writer) {
			writer.WriteFloats(Color.Values);
			writer.WriteSpace();

			if (Color is PdfGrayColor) {
				writer.WriteOperator("g");
			}
			else if (Color is PdfRGBColor) {
				writer.WriteOperator("rg");
			}
			else if (Color is PdfCMYKColor) {
				writer.WriteOperator("k");
			}
			else {
				throw new NotImplementedException($"Unrecognised PdfDeviceColor of type {Color.GetType().Name}.");
			}
		}

	}

	public class PaintShadingOperation : GraphicsStatelessOperation {

		public override bool HasGraphicsWrite { get; } = true;

		public readonly PdfName ShadingName;

		public PaintShadingOperation(PdfName shadingName) {
			ShadingName = shadingName;
		}

		public override void WriteTo(PdfGraphicsStreamWriter writer) {
			writer.WriteName(ShadingName);
			writer.WriteSpace();
			writer.WriteOperator("sh");
		}

	}

	public class PaintXObjectOperation : GraphicsStatelessOperation {

		public override bool HasGraphicsWrite { get; } = true;

		public readonly PdfName XObjectName;

		public PaintXObjectOperation(PdfName xObjectName) {
			XObjectName = xObjectName;
		}

		public override void WriteTo(PdfGraphicsStreamWriter writer) {
			writer.WriteName(XObjectName);
			writer.WriteSpace();
			writer.WriteOperator("Do");
		}

	}

	public class BeginMarkedContentOperation : GraphicsStatelessOperation {

		public override bool HasGraphicsWrite { get; } = true;

		public readonly PdfName Tag;

		public BeginMarkedContentOperation(PdfName tag) {
			Tag = tag;
		}

		public override void WriteTo(PdfGraphicsStreamWriter writer) {
			writer.WriteName(Tag);
			writer.WriteSpace();
			writer.WriteOperator("BMC");
		}

	}

	public class EndMarkedContentOperation : GraphicsStatelessOperation {

		public override bool HasGraphicsWrite { get; } = true;

		public override void WriteTo(PdfGraphicsStreamWriter writer) {
			writer.WriteOperator("EMC");
		}

	}

}
