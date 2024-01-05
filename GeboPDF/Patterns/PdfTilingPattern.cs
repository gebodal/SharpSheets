using GeboPdf.Documents;
using GeboPdf.Graphics;
using GeboPdf.Objects;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeboPdf.Patterns {

	public enum PdfTilingPaintType : int {
		Colored = 1,
		Uncolored = 2
	}

	public enum PdfTilingType : int {
		ConstantSpacing = 1,
		NoDistortion = 2,
		ConstantSpacingFasterTiling = 3
	}

	public class PdfTilingPattern : AbstractPdfStream, IPdfPattern {

		public PdfPatternType PatternType { get; } = PdfPatternType.Tiling;
		PdfIndirectReference IPdfPattern.Reference => this.Reference;

		public PdfTilingPaintType PaintType { get; }

		//public PdfIndirectReference Reference { get { return new PdfIndirectReference(this); } }

		private readonly PdfTilingType tilingType;
		private readonly PdfRectangle bBox;
		private readonly float xStep; // Non-zero
		private readonly float yStep; // Non-zero
		private readonly PdfResourcesDictionary resources;
		private readonly PdfMatrix matrix;

		public readonly GraphicsStream graphics;

		public PdfTilingPattern(
			PdfTilingPaintType paintType, PdfTilingType tilingType,
			PdfRectangle bBox, float xStep, float yStep,
			PdfMatrix matrix) : base() {

			if(xStep==0 || yStep == 0) {
				throw new ArgumentException($"XStep and YStep must be non-zero (got {xStep} and {yStep}).");
			}

			this.PaintType = paintType;
			this.tilingType = tilingType;
			this.bBox = bBox;
			this.xStep = xStep;
			this.yStep = yStep;
			this.matrix = matrix;


			this.resources = new PdfResourcesDictionary(true);
			this.graphics = new GraphicsStream(this.resources, true);
		}

		public override bool AllowEncoding => graphics.AllowEncoding;
		public override MemoryStream GetStream() => graphics.GetStream();

		public override int Count => throw new NotImplementedException();
		public override IEnumerator<KeyValuePair<PdfName, PdfObject>> GetEnumerator() {
			yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.PatternType, new PdfInt((int)PdfPatternType.Tiling));
			yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.PaintType, new PdfInt((int)this.PaintType));
			yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.TilingType, new PdfInt((int)tilingType));
			yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.BBox, bBox);
			yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.XStep, new PdfFloat(xStep));
			yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.YStep, new PdfFloat(yStep));
			yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.Resources, PdfIndirectReference.Create(resources));

			if (matrix != null) {
				yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.Matrix, matrix);
			}
		}

		public IEnumerable<PdfObject> CollectObjects() {
			yield return this;
			foreach(PdfObject resourceObj in resources.CollectObjects()) {
				yield return resourceObj;
			}
		}

	}

}
