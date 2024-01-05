using GeboPdf.Graphics;
using GeboPdf.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeboPdf.Patterns {

	public class PdfShadingPattern : IPdfPattern {

		public PdfPatternType PatternType { get; } = PdfPatternType.Shading;
		public PdfColorSpace ColorSpace { get; } = PdfColorSpace.PatternNoParams;

		public PdfIndirectReference Reference { get { return PdfIndirectReference.Create(shadingPatternDictionary); } }

		//private readonly PdfShadingDictionary shading;
		//private readonly PdfMatrix matrix;
		private readonly PdfGraphicsStateParameterDictionary? extGState;

		private readonly PdfDictionary shadingPatternDictionary;

		public PdfShadingPattern(PdfShadingDictionary shading, PdfMatrix matrix, PdfGraphicsStateParameterDictionary? extGState) {
			//this.shading = shading;
			//this.matrix = matrix;
			this.extGState = extGState;

			this.shadingPatternDictionary = new PdfDictionary() {
				{ PdfNames.PatternType, new PdfInt((int)this.PatternType) },
				{ PdfNames.Shading, shading }
			};
			if (matrix != null) {
				this.shadingPatternDictionary.Add(PdfNames.Matrix, matrix);
			}
			if (extGState != null) {
				this.shadingPatternDictionary.Add(PdfNames.ExtGState, PdfIndirectReference.Create(extGState));
			}
		}

		public IEnumerable<PdfObject> CollectObjects() {
			yield return shadingPatternDictionary;
			if (extGState != null) {
				yield return extGState;
			}
		}

		public override int GetHashCode() {
			unchecked {
				int hash = 17;
				hash = hash * 23 + shadingPatternDictionary.GetHashCode();
				if (extGState is not null) {
					hash = hash * 23 + extGState.GetHashCode();
				}
				return hash;
			}
		}
		public override bool Equals(object? obj) {
			if (obj is PdfShadingPattern other) {
				return shadingPatternDictionary.Equals(other.shadingPatternDictionary) && AbstractPdfDictionary.Equals(extGState, other.extGState);
			}
			return false;
		}

	}

	public enum PdfShadingType : int {
		FunctionBased = 1,
		Axial = 2,
		Radial = 3,
		FreeFormGouraud = 4,
		LatticeFormGouraud = 5,
		CoonsPatch = 6,
		TensorProductPatch = 7
	}

	public abstract class PdfShadingDictionary : AbstractPdfDictionary {

		private readonly PdfShadingType shadingType;
		protected readonly PdfColorSpace colorSpace;
		private readonly PdfColor? background;
		private readonly PdfRectangle? bBox;
		private readonly bool? antiAlias;

		protected PdfShadingDictionary(PdfShadingType shadingType, PdfColorSpace colorSpace, PdfColor? background, PdfRectangle? bBox, bool? antiAlias) {
			this.shadingType = shadingType;
			this.colorSpace = colorSpace ?? throw new ArgumentNullException(nameof(colorSpace));

			if (background != null && background.ColorSpace != this.colorSpace) {
				throw new ArgumentException("Any background provided to a shading dictionary must match the provided color space.");
			}
			this.background = background;
			
			this.bBox = bBox;
			this.antiAlias = antiAlias;
		}

		public override int Count {
			get {
				int count = 2 + ShadingParametersCount;
				if (background != null) {
					count += 1;
				}
				if (bBox != null) {
					count += 1;
				}
				if (antiAlias.HasValue) {
					count += 1;
				}
				return count;
			}
		}

		public override IEnumerator<KeyValuePair<PdfName, PdfObject>> GetEnumerator() {
			yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.ShadingType, new PdfInt((int)shadingType));
			yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.ColorSpace, colorSpace);
			if (background != null) {
				yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.Background, PdfArray.MakeArray(background.Values));
			}
			if (bBox != null) {
				yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.BBox, bBox);
			}
			if (antiAlias.HasValue) {
				yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.AntiAlias, new PdfBoolean(antiAlias.Value));
			}
			foreach(KeyValuePair<PdfName, PdfObject> shadingParam in GetShadingParameters()) {
				yield return shadingParam;
			}
		}

		protected abstract int ShadingParametersCount { get; }
		protected abstract IEnumerable<KeyValuePair<PdfName, PdfObject>> GetShadingParameters();

	}

	public abstract class PdfGeometricShadingDictionary : PdfShadingDictionary {

		private readonly PdfArray coords;
		private readonly PdfArray? domain;
		private readonly PdfFunction function;
		private readonly PdfArray? extend;

		protected PdfGeometricShadingDictionary(
			PdfShadingType shadingType, PdfColorSpace colorSpace, PdfColor? background, PdfRectangle? bBox, bool? antiAlias,
			PdfArray coords, PdfArray? domain, PdfFunction function, PdfArray? extend
			) : base(shadingType, colorSpace, background, bBox, antiAlias) {

			this.coords = coords;
			this.domain = domain;
			this.function = function;
			this.extend = extend;
		}

		protected override sealed int ShadingParametersCount {
			get {
				int count = 2;
				if (domain != null) {
					count += 1;
				}
				if (extend != null) {
					count += 1;
				}
				return count;
			}
		}
		protected override sealed IEnumerable<KeyValuePair<PdfName, PdfObject>> GetShadingParameters() {
			yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.Coords, coords);
			if (domain != null) {
				yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.Domain, domain);
			}
			yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.Function, function);
			if (extend != null) {
				yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.Extend, extend);
			}
		}

		protected static void ValidateFunction(PdfColorSpace colorSpace, PdfFunction function) {
			if (function is null) { throw new ArgumentNullException(nameof(function)); }

			if (function.InputDim != 1 || function.OutputDim != colorSpace.NumComponents) {
				throw new ArgumentException($"Provided function must be 1-to-n (got {function.InputDim}-to-{function.OutputDim}), where n is the number of components in the provided color space ({colorSpace.NumComponents} provided).");
			}
		}

		protected static PdfArray? MakeDomain((float start, float end)? domain) {
			return domain.HasValue ? PdfArray.MakeArray(domain.Value.start, domain.Value.end) : null;
		}

		protected static PdfArray? MakeExtend((bool start, bool end)? extend) {
			return extend.HasValue ? PdfArray.MakeArray(extend.Value.start, extend.Value.end) : null;
		}

	}

	public class PdfAxialShadingDictionary : PdfGeometricShadingDictionary {

		protected PdfAxialShadingDictionary(PdfColorSpace colorSpace, PdfColor? background, PdfRectangle? bBox, bool? antiAlias,
			PdfArray coords, PdfArray? domain, PdfFunction function, PdfArray? extend
			) : base(PdfShadingType.Axial, colorSpace, background, bBox, antiAlias, coords, domain, function, extend) { }

		public static PdfAxialShadingDictionary Create(
			PdfColorSpace colorSpace, PdfColor? background, PdfRectangle? bBox, bool? antiAlias,
			float x0, float y0, float x1, float y1, (float start, float end)? domain, PdfFunction function, (bool start, bool end)? extend
			) {

			ValidateFunction(colorSpace, function);

			PdfArray coordsArray = PdfArray.MakeArray(x0, y0, x1, y1);

			return new PdfAxialShadingDictionary(colorSpace, background, bBox, antiAlias, coordsArray, MakeDomain(domain), function, MakeExtend(extend));
		}

	}

	public class PdfRadialShadingDictionary : PdfGeometricShadingDictionary {

		protected PdfRadialShadingDictionary(PdfColorSpace colorSpace, PdfColor? background, PdfRectangle? bBox, bool? antiAlias,
			PdfArray coords, PdfArray? domain, PdfFunction function, PdfArray? extend
			) : base(PdfShadingType.Radial, colorSpace, background, bBox, antiAlias, coords, domain, function, extend) { }

		public static PdfRadialShadingDictionary Create(
			PdfColorSpace colorSpace, PdfColor? background, PdfRectangle? bBox, bool? antiAlias,
			float x0, float y0, float r0, float x1, float y1, float r1, (float start, float end)? domain, PdfFunction function, (bool start, bool end)? extend
			) {

			ValidateFunction(colorSpace, function);

			if (r0 < 0 || r1 < 0) {
				throw new ArgumentException($"Radii for circles must be greater than or equal to zero (got {r0} and {r1}).");
			}

			PdfArray coordsArray = PdfArray.MakeArray(x0, y0, r0, x1, y1, r1);

			return new PdfRadialShadingDictionary(colorSpace, background, bBox, antiAlias, coordsArray, MakeDomain(domain), function, MakeExtend(extend));
		}

	}

}
