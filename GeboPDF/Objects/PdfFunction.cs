using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeboPdf.Objects {

	public abstract class PdfFunction : AbstractPdfDictionary {

		public int InputDim { get { return domain.Length / 2; } }
		//public int OutputDim { get { return range.Length / 2; } }
		public int OutputDim { get; }

		private readonly PdfFunctionType functionType;
		private readonly PdfArray domain;
		private readonly PdfArray? range;

		protected PdfFunction(PdfFunctionType functionType, PdfArray domain, PdfArray? range, int outputDim) {
			this.functionType = functionType;
			this.domain = domain;
			this.range = range;
			this.OutputDim = outputDim;
		}

		public override int Count {
			get {
				int count = 2 + FunctionParamsCount;
				if (range != null) {
					count += 1;
				}
				return count;
			}
		}

		public override IEnumerator<KeyValuePair<PdfName, PdfObject>> GetEnumerator() {
			yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.FunctionType, new PdfInt((int)functionType));
			yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.Domain, domain);
			if (range != null) {
				yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.Range, range);
			}
			foreach(KeyValuePair<PdfName, PdfObject> funcParam in GetFunctionParams()) {
				yield return funcParam;
			}
		}

		protected abstract int FunctionParamsCount { get; }
		protected abstract IEnumerable<KeyValuePair<PdfName, PdfObject>> GetFunctionParams();

		protected static PdfArray MakeDomain((float start, float end) domain) {
			return new PdfArray(new PdfFloat(domain.start), new PdfFloat(domain.end));
		}
		protected static PdfArray? MakeRange((float start, float end)[]? range) {
			if (range != null) {
				PdfArray array = new PdfArray();
				for (int i = 0; i < range.Length; i++) {
					array.Add(new PdfFloat(range[i].start));
					array.Add(new PdfFloat(range[i].end));
				}
				return array;
			}
			else {
				return null;
			}
		}

	}

	public enum PdfFunctionType : int {
		Sampled = 0,
		ExponentialInterpolation = 2,
		Stitching = 3,
		PostScriptCalculator = 4
	}

	public class PdfExponentialInterpolationFunction : PdfFunction {

		private readonly PdfArray valuesAt0;
		private readonly PdfArray valuesAt1;
		private readonly PdfObject interpExponent;

		private PdfExponentialInterpolationFunction(PdfArray domain, PdfArray? range,
			PdfArray valuesAt0, PdfArray valuesAt1, PdfObject interpExponent
			) : base(PdfFunctionType.ExponentialInterpolation, domain, range, valuesAt0.Length) {

			this.valuesAt0 = valuesAt0;
			this.valuesAt1 = valuesAt1;
			this.interpExponent = interpExponent;
		}

		protected override int FunctionParamsCount {
			get {
				return 3;
			}
		}

		protected override IEnumerable<KeyValuePair<PdfName, PdfObject>> GetFunctionParams() {
			yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.ValuesAt0, valuesAt0);
			yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.ValuesAt1, valuesAt1);
			yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.InterpolationExponent, interpExponent);
		}

		private static bool DomainIncludeZero((float start, float end) domain) {
			return domain.start == 0 || domain.end == 0 || (domain.start < 0 && domain.end > 0);
		}

		private static PdfExponentialInterpolationFunction MakeFunction(
			(float start, float end) domain, (float start, float end)[]? range,
			float[] valuesAt0, float[] valuesAt1, PdfObject interpExponent) {

			if (valuesAt0 == null) { throw new ArgumentNullException(nameof(valuesAt0)); }
			if (valuesAt1 == null) { throw new ArgumentNullException(nameof(valuesAt1)); }

			if (valuesAt0.Length != valuesAt1.Length) {
				throw new ArgumentException("The number of output values must be the same at 0 and 1.");
			}

			if(valuesAt0.Length < 1) {
				throw new ArgumentException("The number of output values must be greater than zero.");
			}

			if (range != null && range.Length != valuesAt0.Length) {
				throw new ArgumentException("If present, the range must contain n*2 values, where n is the number of values.");
			}

			return new PdfExponentialInterpolationFunction(
				MakeDomain(domain), MakeRange(range),
				PdfArray.MakeArray(valuesAt0), PdfArray.MakeArray(valuesAt1), interpExponent
				);
		}

		public static PdfExponentialInterpolationFunction MakeFunction(
			(float start, float end) domain, (float start, float end)[]? range,
			float[] valuesAt0, float[] valuesAt1, float interpExponent) {

			if (domain.start < 0) {
				throw new ArgumentException("For non-integer interpolation exponents, the domain must all be non-negative.");
			}
			if (interpExponent < 0 && DomainIncludeZero(domain)) {
				throw new ArgumentException("Domain cannot include 0 for negative interpolation exponents.");
			}

			return MakeFunction(domain, range, valuesAt0, valuesAt1, new PdfFloat(interpExponent));
		}

		public static PdfExponentialInterpolationFunction MakeFunction(
			(float start, float end) domain, (float start, float end)[] range,
			float[] valuesAt0, float[] valuesAt1, int interpExponent) {

			if (interpExponent < 0 && DomainIncludeZero(domain)) {
				throw new ArgumentException("Domain cannot include 0 for negative interpolation exponents.");
			}

			return MakeFunction(domain, range, valuesAt0, valuesAt1, new PdfInt(interpExponent));
		}

	}

	public class PdfStitchingFunction : PdfFunction {

		private readonly PdfFunction[] functions;
		private readonly PdfArray bounds;
		private readonly PdfArray encode;

		private PdfStitchingFunction(PdfArray domain, PdfArray? range, int outputDim,
			PdfFunction[] functions, PdfArray bounds, PdfArray encode
			) : base(PdfFunctionType.Stitching, domain, range, outputDim) {

			this.functions = functions;
			this.bounds = bounds;
			this.encode = encode;
		}

		protected override int FunctionParamsCount {
			get {
				return 3;
			}
		}

		protected override IEnumerable<KeyValuePair<PdfName, PdfObject>> GetFunctionParams() {
			yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.Functions, new PdfArray(functions));
			yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.Bounds, bounds);
			yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.Encode, encode);
		}

		public static PdfStitchingFunction MakeFunction(
			(float start, float end) domain, (float start, float end)[]? range,
			PdfFunction[] functions, float[] bounds, float[] encode
			) {

			if (functions == null) { throw new ArgumentNullException(nameof(functions)); }
			if (bounds == null) { throw new ArgumentNullException(nameof(bounds)); }
			if (encode == null) { throw new ArgumentNullException(nameof(encode)); }

			if (functions.Length == 0) {
				throw new ArgumentException("Stitching functions require a non-zero number of component functions.");
			}

			int outputDims = functions[0].OutputDim;
			for(int i=1; i<functions.Length; i++) {
				if (outputDims != functions[i].OutputDim) {
					throw new ArgumentException("All functions must have the same number of output dimensions.");
				}
			}

			if (range != null && range.Length != outputDims) {
				throw new ArgumentException("If present, the range must contain n*2 values, where n is the number of output dimensions.");
			}

			if(bounds.Length != functions.Length - 1) {
				throw new ArgumentException("bounds must have a length of k-1, where k is the number of component functions.");
			}

			for(int i=1; i<bounds.Length; i++) {
				if(bounds[i-1] >= bounds[i]) {
					throw new ArgumentException("bounds values must be in strictly increasing order.");
				}
			}
			if(bounds.Length > 0 && (bounds[0] <= domain.start || bounds[^1] >= domain.end)) {
				throw new ArgumentException("bounds values must all be strictly inside the domain.");
			}

			if (encode.Length != functions.Length * 2) {
				throw new ArgumentException("encode must contain k*2 values, where k is the number of component functions.");
			}

			return new PdfStitchingFunction(
				MakeDomain(domain), MakeRange(range), outputDims,
				functions, PdfArray.MakeArray(bounds), PdfArray.MakeArray(encode)
				);
		}

	}

}
