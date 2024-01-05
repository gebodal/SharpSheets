using GeboPdf.Objects;
using GeboPdf.Patterns;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeboPdf.Graphics {

	public enum PdfColorSpaces {
		DeviceGray,
		DeviceRGB,
		DeviceCMYK,
		Pattern
	}

	public static class PdfColorSpacesUtils {

		public static PdfName GetName(this PdfColorSpaces colorSpace) {
			if(colorSpace == PdfColorSpaces.DeviceGray) {
				return PdfColorSpace.DeviceGrayName;
			}
			else if (colorSpace == PdfColorSpaces.DeviceRGB) {
				return PdfColorSpace.DeviceRGBName;
			}
			else if (colorSpace == PdfColorSpaces.DeviceCMYK) {
				return PdfColorSpace.DeviceCMYKName;
			}
			else if (colorSpace == PdfColorSpaces.Pattern) {
				return PdfColorSpace.PatternName;
			}
			else {
				throw new ArgumentException("Unrecognised PdfColorSpaces value.");
			}
		}

	}

	/*
	public class PdfColorSpace : PdfName {

		// TODO This very much needs changing!! Some color spaces are actually arrays!

		public static readonly PdfColorSpace DeviceGray = new PdfColorSpace("DeviceGray", 1, true);
		public static readonly PdfColorSpace DeviceRGB = new PdfColorSpace("DeviceRGB", 3, true);
		public static readonly PdfColorSpace DeviceCMYK = new PdfColorSpace("DeviceCMYK", 4, true);

		public static readonly PdfColorSpace PatternNoParams = new PdfColorSpace("Pattern", 0, true);

		public int NumComponents { get; }
		public bool IsBuiltIn { get; }

		private PdfColorSpace(string name, int numComponents, bool isBuiltIn) : base(name) {
			this.NumComponents = numComponents;
			this.IsBuiltIn = isBuiltIn;
		}

		public PdfColorSpace(string name, int numComponents) : this(name, numComponents, false) { }

	}
	*/

	public class PdfColorSpace : PdfProxyObject {

		public static readonly PdfName DeviceGrayName = new PdfName("DeviceGray");
		public static readonly PdfName DeviceRGBName = new PdfName("DeviceRGB");
		public static readonly PdfName DeviceCMYKName = new PdfName("DeviceCMYK");
		public static readonly PdfName PatternName = new PdfName("Pattern");

		public static readonly PdfColorSpace DeviceGray = new PdfColorSpace(PdfColorSpaces.DeviceGray, null, 1, new PdfGrayColor(0f), true);
		public static readonly PdfColorSpace DeviceRGB = new PdfColorSpace(PdfColorSpaces.DeviceRGB, null, 3, new PdfRGBColor(0f, 0f, 0f), true);
		public static readonly PdfColorSpace DeviceCMYK = new PdfColorSpace(PdfColorSpaces.DeviceCMYK, null, 4, new PdfCMYKColor(0f, 0f, 0f, 1f), true);
		
		public static readonly PdfColorSpace PatternNoParams = new PdfColorSpace(PdfColorSpaces.Pattern, null, 0, Array.Empty<float>(), true);

		public override PdfObject Content {
			get {
				if(fallback != null) {
					return new PdfArray(name, fallback.name);
				}
				else {
					return name;
				}
			}
		}

		public PdfName BuiltInName {
			get {
				if (IsBuiltIn) {
					return name;
				}
				else {
					throw new InvalidOperationException("This is not a built in color space.");
				}
			}
		}

		public bool IsUncoloredPatternColorSpace {
			get {
				return name == PatternName && fallback != null && fallback.IsBuiltIn;
			}
		}

		private readonly PdfName name;
		private readonly PdfColorSpace? fallback;

		public int NumComponents { get; }
		public bool IsBuiltIn { get; }
		public PdfColor DefaultValues;

		private PdfColorSpace(PdfColorSpaces name, PdfColorSpaces? fallback, int numComponents, PdfColor defaultValues, bool isBuiltIn) : base() {
			this.name = name.GetName();
			this.fallback = fallback.HasValue ? GetBaseColorSpace(fallback.Value) : null;
			this.NumComponents = numComponents;
			this.IsBuiltIn = isBuiltIn;
			DefaultValues = defaultValues;
		}

		private PdfColorSpace(PdfColorSpaces name, PdfColorSpaces? fallback, int numComponents, float[] defaultValues, bool isBuiltIn) : base() {
			this.name = name.GetName();
			this.fallback = fallback.HasValue ? GetBaseColorSpace(fallback.Value) : null;
			this.NumComponents = numComponents;
			this.IsBuiltIn = isBuiltIn;
			DefaultValues = new PdfColorValues(this, defaultValues);
		}

		public override int GetHashCode() {
			unchecked {
				int hash = 17;
				hash = hash * 23 + name.GetHashCode();
				if (fallback != null) {
					hash = hash * 23 + fallback.GetHashCode();
				}
				hash = hash * 23 + NumComponents.GetHashCode();
				hash = hash * 23 + IsBuiltIn.GetHashCode();
				hash = hash * 23 + DefaultValues.Values.GetHashCode();
				return hash;
			}
		}

		public override bool Equals(object? obj) {
			if (obj is PdfColorSpace other) {
				if (!PdfName.Equals(name, other.name) || !PdfName.Equals(fallback, other.fallback)) {
					return false;
				}
				return NumComponents == other.NumComponents && IsBuiltIn == other.IsBuiltIn;
			}
			return false;
		}

		private static PdfColorSpace GetBaseColorSpace(PdfColorSpaces colorSpace) {
			if (colorSpace == PdfColorSpaces.DeviceGray) {
				return DeviceGray;
			}
			else if (colorSpace == PdfColorSpaces.DeviceRGB) {
				return DeviceRGB;
			}
			else if (colorSpace == PdfColorSpaces.DeviceCMYK) {
				return DeviceCMYK;
			}
			else if (colorSpace == PdfColorSpaces.Pattern) {
				throw new ArgumentException("Pattern is not a base color space.");
			}
			else {
				throw new ArgumentException("Unrecognised PdfColorSpaces value.");
			}
		}

		public static PdfColorSpace PatternColorSpace(PdfColorSpaces fallback) {
			if(fallback == PdfColorSpaces.Pattern) {
				throw new ArgumentException("A Pattern color space cannot use another Pattern color space as its basis.");
			}

			PdfColorSpace fallbackColorspace = GetBaseColorSpace(fallback);

			return new PdfColorSpace(PdfColorSpaces.Pattern, fallback, fallbackColorspace.NumComponents, fallbackColorspace.DefaultValues, false);
		}

	}

	public abstract class PdfColor {

		public abstract PdfColorSpace ColorSpace { get; }
		public abstract float[] Values { get; }

		protected PdfColor() { }

		protected static float Clamp(float value) {
			return Math.Max(0f, Math.Min(1f, value));
		}

		public override int GetHashCode() {
			return HashCode.Combine(Values, ColorSpace);
		}

		public override bool Equals(object? obj) {
			if (obj is PdfColor other) {
				if (ColorSpace.Equals(other.ColorSpace) && Values.Length == other.Values.Length) {
					for (int i = 0; i < Values.Length; i++) {
						if (Values[i] != other.Values[i]) {
							return false;
						}
					}
					return true;
				}
			}
			return false;
		}

		public static bool operator ==(PdfColor? a, PdfColor? b) {
			if (a is null) { return b is null; }
			else { return a.Equals(b); }
		}
		public static bool operator !=(PdfColor? a, PdfColor? b) {
			if (a is null) { return b is not null; }
			else { return !a.Equals(b); }
		}

	}

	public class PdfColorValues : PdfColor {

		public override PdfColorSpace ColorSpace { get; }
		public override float[] Values { get; }

		public PdfColorValues(PdfColorSpace colorSpace, float[] values) : base() {
			ColorSpace = colorSpace;
			Values = values;
		}

		public override int GetHashCode() => base.GetHashCode();
		public override bool Equals(object? obj) {
			if (base.Equals(obj)) {
				return obj is PdfColorValues;
			}
			return false;
		}

	}

	public class PdfPatternColor : PdfColor {

		public override PdfColorSpace ColorSpace { get; }
		public override float[] Values { get; }

		private readonly IPdfPattern pattern;

		public PdfPatternColor(IPdfPattern pattern, PdfColorSpace patternColorSpace, float[]? values) : base() {
			this.pattern = pattern;
			this.ColorSpace = patternColorSpace;
			this.Values = values ?? Array.Empty<float>();
		}

		public PdfPatternColor(PdfShadingPattern pattern) : base() {
			this.pattern = pattern;
			this.ColorSpace = PdfColorSpace.PatternNoParams;
			this.Values = Array.Empty<float>();
		}

		public override int GetHashCode() {
			return HashCode.Combine(base.GetHashCode(), pattern);
		}

		public override bool Equals(object? obj) {
			if (obj is PdfPatternColor other) {
				if (pattern.Equals(other.pattern)) {
					return base.Equals(obj);
				}
			}
			return false;
		}

	}

	public abstract class PdfDeviceColor : PdfColor {

		protected PdfDeviceColor() : base() { }

	}

	public class PdfGrayColor : PdfDeviceColor {

		public static readonly PdfGrayColor Black = new PdfGrayColor(0.0f);
		public static readonly PdfGrayColor White = new PdfGrayColor(1.0f);

		public override PdfColorSpace ColorSpace { get { return PdfColorSpace.DeviceGray; } }
		public override float[] Values { get; }

		public readonly float Gray;

		public PdfGrayColor(float gray) {
			Gray = Clamp(gray);

			Values = new float[] { Gray };
		}
	}

	public class PdfRGBColor : PdfDeviceColor {

		public override PdfColorSpace ColorSpace { get { return PdfColorSpace.DeviceRGB; } }
		public override float[] Values { get; }

		public readonly float Red;
		public readonly float Green;
		public readonly float Blue;

		public PdfRGBColor(float red, float green, float blue) {
			Red = Clamp(red);
			Green = Clamp(green);
			Blue = Clamp(blue);

			Values = new float[] { Red, Green, Blue };
		}
	}

	public class PdfCMYKColor : PdfDeviceColor {

		public override PdfColorSpace ColorSpace { get { return PdfColorSpace.DeviceCMYK; } }
		public override float[] Values { get; }

		public readonly float Cyan;
		public readonly float Magenta;
		public readonly float Yellow;
		public readonly float Key;

		public PdfCMYKColor(float cyan, float magenta, float yellow, float key) {
			Cyan = Clamp(cyan);
			Magenta = Clamp(magenta);
			Yellow = Clamp(yellow);
			Key = Clamp(key);

			Values = new float[] { Cyan, Magenta, Yellow, Key };
		}
	}

}
