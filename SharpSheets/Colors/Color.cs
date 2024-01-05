using SharpSheets.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpSheets.Colors {

	public readonly struct Color : IEquatable<Color> {

		[MemberNotNullWhen(true, nameof(Name))]
		public bool IsNamedColor { get { return ColorNames.ContainsKey(this); } }
		public string? Name { get { return ColorNames.TryGetValue(this, out string? name) ? name : null; } }

		public readonly bool Initialised;

		public readonly byte R;
		public readonly byte G;
		public readonly byte B;
		public readonly byte A;

		private Color(bool initialised) {
			R = 0;
			G = 0;
			B = 0;
			A = 0;
			Initialised = initialised;
		}

		public Color(byte r, byte g, byte b, byte a) {
			R = r;
			G = g;
			B = b;
			A = a;
			Initialised = true;
		}

		public Color(byte r, byte g, byte b) : this(r, g, b, 0xFF) { }

		public override bool Equals(object? obj) {
			return obj is Color color && Equals(color);
		}

		public bool Equals(Color other) {
			return R == other.R && G == other.G && B == other.B && A == other.A;
		}

		public static bool operator ==(Color c1, Color c2) {
			return c1.Equals(c2);
		}

		public static bool operator !=(Color c1, Color c2) {
			return !c1.Equals(c2);
		}

		public override int GetHashCode() {
			return HashCode.Combine(R, G, B, A);
		}

		#region HSL

		public float GetHue() {
			float rf = R / 255f;
			float gf = G / 255f;
			float bf = B / 255f;

			float cmax = MathUtils.Max(rf, gf, bf);
			float cmin = MathUtils.Min(rf, gf, bf);

			byte bmax = MathUtils.Max(R, G, B);

			float delta = cmax - cmin;

			float hue;
			if (delta == 0f) {
				hue = 0f;
			}
			else if (bmax == R) {
				hue = 60f * (((gf - bf) / delta) % 6f);
			}
			else if (bmax == G) {
				hue = 60f * (((bf - rf) / delta) + 2f);
			}
			else { // bmax == B
				hue = 60f * (((rf - gf) / delta) + 4f);
			}

			return hue;
		}

		public float GetSaturation() {
			byte bmax = MathUtils.Max(R, G, B);
			byte bmin = MathUtils.Min(R, G, B);

			float saturation;
			if (bmax == 0 || bmin == 0xFF) {
				saturation = 0f;
			}
			else {
				float rf = R / 255f;
				float gf = G / 255f;
				float bf = B / 255f;

				float cmax = MathUtils.Max(rf, gf, bf);
				float cmin = MathUtils.Min(rf, gf, bf);
				float delta = cmax - cmin;

				float lightness = (cmax + cmin) / 2f;

				saturation = delta / (1 - Math.Abs(2 * lightness - 1));
			}

			return saturation;
		}

		public float GetLightness() {
			float rf = R / 255f;
			float gf = G / 255f;
			float bf = B / 255f;

			float cmax = MathUtils.Max(rf, gf, bf);
			float cmin = MathUtils.Min(rf, gf, bf);

			return (cmax + cmin) / 2f;
		}

		#endregion HSL

		#region Named Colours

		public static Color FromName(string name) {
			return NamedColors.TryGetValue(name, out Color defined) ? defined : new Color(false);
		}

		public static readonly Color None = new Color(0, 0, 0, 0);
		public static readonly Color Transparent = new Color(0, 0, 0, 0);

		public static readonly Color AliceBlue = new Color(240, 248, 255);
		public static readonly Color AntiqueWhite = new Color(250, 235, 215);
		public static readonly Color Aqua = new Color(0, 255, 255);
		public static readonly Color Aquamarine = new Color(127, 255, 212);
		public static readonly Color Azure = new Color(240, 255, 255);
		public static readonly Color Beige = new Color(245, 245, 220);
		public static readonly Color Bisque = new Color(255, 228, 196);
		public static readonly Color Black = new Color(0, 0, 0);
		public static readonly Color BlanchedAlmond = new Color(255, 235, 205);
		public static readonly Color Blue = new Color(0, 0, 255);
		public static readonly Color BlueViolet = new Color(138, 43, 226);
		public static readonly Color Brown = new Color(165, 42, 42);
		public static readonly Color BurlyWood = new Color(222, 184, 135);
		public static readonly Color CadetBlue = new Color(95, 158, 160);
		public static readonly Color Chartreuse = new Color(127, 255, 0);
		public static readonly Color Chocolate = new Color(210, 105, 30);
		public static readonly Color Coral = new Color(255, 127, 80);
		public static readonly Color CornflowerBlue = new Color(100, 149, 237);
		public static readonly Color Cornsilk = new Color(255, 248, 220);
		public static readonly Color Crimson = new Color(220, 20, 60);
		public static readonly Color Cyan = new Color(0, 255, 255);
		public static readonly Color DarkBlue = new Color(0, 0, 139);
		public static readonly Color DarkCyan = new Color(0, 139, 139);
		public static readonly Color DarkGoldenrod = new Color(184, 134, 11);
		public static readonly Color DarkGray = new Color(169, 169, 169);
		public static readonly Color DarkGreen = new Color(0, 100, 0);
		public static readonly Color DarkKhaki = new Color(189, 183, 107);
		public static readonly Color DarkMagenta = new Color(139, 0, 139);
		public static readonly Color DarkOliveGreen = new Color(85, 107, 47);
		public static readonly Color DarkOrange = new Color(255, 140, 0);
		public static readonly Color DarkOrchid = new Color(153, 50, 204);
		public static readonly Color DarkRed = new Color(139, 0, 0);
		public static readonly Color DarkSalmon = new Color(233, 150, 122);
		public static readonly Color DarkSeaGreen = new Color(143, 188, 139);
		public static readonly Color DarkSlateBlue = new Color(72, 61, 139);
		public static readonly Color DarkSlateGray = new Color(47, 79, 79);
		public static readonly Color DarkTurquoise = new Color(0, 206, 209);
		public static readonly Color DarkViolet = new Color(148, 0, 211);
		public static readonly Color DeepPink = new Color(255, 20, 147);
		public static readonly Color DeepSkyBlue = new Color(0, 191, 255);
		public static readonly Color DimGray = new Color(105, 105, 105);
		public static readonly Color DodgerBlue = new Color(30, 144, 255);
		public static readonly Color Firebrick = new Color(178, 34, 34);
		public static readonly Color FloralWhite = new Color(255, 250, 240);
		public static readonly Color ForestGreen = new Color(34, 139, 34);
		public static readonly Color Fuchsia = new Color(255, 0, 255);
		public static readonly Color Gainsboro = new Color(220, 220, 220);
		public static readonly Color GhostWhite = new Color(248, 248, 255);
		public static readonly Color Gold = new Color(255, 215, 0);
		public static readonly Color Goldenrod = new Color(218, 165, 32);
		public static readonly Color Gray = new Color(128, 128, 128);
		public static readonly Color Green = new Color(0, 128, 0);
		public static readonly Color GreenYellow = new Color(173, 255, 47);
		public static readonly Color Honeydew = new Color(240, 255, 240);
		public static readonly Color HotPink = new Color(255, 105, 180);
		public static readonly Color IndianRed = new Color(205, 92, 92);
		public static readonly Color Indigo = new Color(75, 0, 130);
		public static readonly Color Ivory = new Color(255, 255, 240);
		public static readonly Color Khaki = new Color(240, 230, 140);
		public static readonly Color Lavender = new Color(230, 230, 250);
		public static readonly Color LavenderBlush = new Color(255, 240, 245);
		public static readonly Color LawnGreen = new Color(124, 252, 0);
		public static readonly Color LemonChiffon = new Color(255, 250, 205);
		public static readonly Color LightBlue = new Color(173, 216, 230);
		public static readonly Color LightCoral = new Color(240, 128, 128);
		public static readonly Color LightCyan = new Color(224, 255, 255);
		public static readonly Color LightGoldenrodYellow = new Color(250, 250, 210);
		public static readonly Color LightGray = new Color(211, 211, 211);
		public static readonly Color LightGreen = new Color(144, 238, 144);
		public static readonly Color LightPink = new Color(255, 182, 193);
		public static readonly Color LightSalmon = new Color(255, 160, 122);
		public static readonly Color LightSeaGreen = new Color(32, 178, 170);
		public static readonly Color LightSkyBlue = new Color(135, 206, 250);
		public static readonly Color LightSlateGray = new Color(119, 136, 153);
		public static readonly Color LightSteelBlue = new Color(176, 196, 222);
		public static readonly Color LightYellow = new Color(255, 255, 224);
		public static readonly Color Lime = new Color(0, 255, 0);
		public static readonly Color LimeGreen = new Color(50, 205, 50);
		public static readonly Color Linen = new Color(250, 240, 230);
		public static readonly Color Magenta = new Color(255, 0, 255);
		public static readonly Color Maroon = new Color(128, 0, 0);
		public static readonly Color MediumAquamarine = new Color(102, 205, 170);
		public static readonly Color MediumBlue = new Color(0, 0, 205);
		public static readonly Color MediumOrchid = new Color(186, 85, 211);
		public static readonly Color MediumPurple = new Color(147, 112, 219);
		public static readonly Color MediumSeaGreen = new Color(60, 179, 113);
		public static readonly Color MediumSlateBlue = new Color(123, 104, 238);
		public static readonly Color MediumSpringGreen = new Color(0, 250, 154);
		public static readonly Color MediumTurquoise = new Color(72, 209, 204);
		public static readonly Color MediumVioletRed = new Color(199, 21, 133);
		public static readonly Color MidnightBlue = new Color(25, 25, 112);
		public static readonly Color MintCream = new Color(245, 255, 250);
		public static readonly Color MistyRose = new Color(255, 228, 225);
		public static readonly Color Moccasin = new Color(255, 228, 181);
		public static readonly Color NavajoWhite = new Color(255, 222, 173);
		public static readonly Color Navy = new Color(0, 0, 128);
		public static readonly Color OldLace = new Color(253, 245, 230);
		public static readonly Color Olive = new Color(128, 128, 0);
		public static readonly Color OliveDrab = new Color(107, 142, 35);
		public static readonly Color Orange = new Color(255, 165, 0);
		public static readonly Color OrangeRed = new Color(255, 69, 0);
		public static readonly Color Orchid = new Color(218, 112, 214);
		public static readonly Color PaleGoldenrod = new Color(238, 232, 170);
		public static readonly Color PaleGreen = new Color(152, 251, 152);
		public static readonly Color PaleTurquoise = new Color(175, 238, 238);
		public static readonly Color PaleVioletRed = new Color(219, 112, 147);
		public static readonly Color PapayaWhip = new Color(255, 239, 213);
		public static readonly Color PeachPuff = new Color(255, 218, 185);
		public static readonly Color Peru = new Color(205, 133, 63);
		public static readonly Color Pink = new Color(255, 192, 203);
		public static readonly Color Plum = new Color(221, 160, 221);
		public static readonly Color PowderBlue = new Color(176, 224, 230);
		public static readonly Color Purple = new Color(128, 0, 128);
		public static readonly Color RebeccaPurple = new Color(102, 51, 153); // In memory of Rebecca Alison Meyer
		public static readonly Color Red = new Color(255, 0, 0);
		public static readonly Color RosyBrown = new Color(188, 143, 143);
		public static readonly Color RoyalBlue = new Color(65, 105, 225);
		public static readonly Color SaddleBrown = new Color(139, 69, 19);
		public static readonly Color Salmon = new Color(250, 128, 114);
		public static readonly Color SandyBrown = new Color(244, 164, 96);
		public static readonly Color SeaGreen = new Color(46, 139, 87);
		public static readonly Color SeaShell = new Color(255, 245, 238);
		public static readonly Color Sienna = new Color(160, 82, 45);
		public static readonly Color Silver = new Color(192, 192, 192);
		public static readonly Color SkyBlue = new Color(135, 206, 235);
		public static readonly Color SlateBlue = new Color(106, 90, 205);
		public static readonly Color SlateGray = new Color(112, 128, 144);
		public static readonly Color Snow = new Color(255, 250, 250);
		public static readonly Color SpringGreen = new Color(0, 255, 127);
		public static readonly Color SteelBlue = new Color(70, 130, 180);
		public static readonly Color Tan = new Color(210, 180, 140);
		public static readonly Color Teal = new Color(0, 128, 128);
		public static readonly Color Thistle = new Color(216, 191, 216);
		public static readonly Color Tomato = new Color(255, 99, 71);
		public static readonly Color Turquoise = new Color(64, 224, 208);
		public static readonly Color Violet = new Color(238, 130, 238);
		public static readonly Color Wheat = new Color(245, 222, 179);
		public static readonly Color White = new Color(255, 255, 255);
		public static readonly Color WhiteSmoke = new Color(245, 245, 245);
		public static readonly Color Yellow = new Color(255, 255, 0);
		public static readonly Color YellowGreen = new Color(154, 205, 50);

		public static readonly IReadOnlyDictionary<string, Color> NamedColors = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase) {
			{ "Null", Color.None },
			{ "None", Color.None },
			{ "Transparent", Color.Transparent },
			{ "AliceBlue", Color.AliceBlue },
			{ "AntiqueWhite", Color.AntiqueWhite },
			{ "Aqua", Color.Aqua },
			{ "Aquamarine", Color.Aquamarine },
			{ "Azure", Color.Azure },
			{ "Beige", Color.Beige },
			{ "Bisque", Color.Bisque },
			{ "Black", Color.Black },
			{ "BlanchedAlmond", Color.BlanchedAlmond },
			{ "Blue", Color.Blue },
			{ "BlueViolet", Color.BlueViolet },
			{ "Brown", Color.Brown },
			{ "BurlyWood", Color.BurlyWood },
			{ "CadetBlue", Color.CadetBlue },
			{ "Chartreuse", Color.Chartreuse },
			{ "Chocolate", Color.Chocolate },
			{ "Coral", Color.Coral },
			{ "CornflowerBlue", Color.CornflowerBlue },
			{ "Cornsilk", Color.Cornsilk },
			{ "Crimson", Color.Crimson },
			{ "Cyan", Color.Cyan },
			{ "DarkBlue", Color.DarkBlue },
			{ "DarkCyan", Color.DarkCyan },
			{ "DarkGoldenrod", Color.DarkGoldenrod },
			{ "DarkGray", Color.DarkGray },
			{ "DarkGreen", Color.DarkGreen },
			{ "DarkKhaki", Color.DarkKhaki },
			{ "DarkMagenta", Color.DarkMagenta },
			{ "DarkOliveGreen", Color.DarkOliveGreen },
			{ "DarkOrange", Color.DarkOrange },
			{ "DarkOrchid", Color.DarkOrchid },
			{ "DarkRed", Color.DarkRed },
			{ "DarkSalmon", Color.DarkSalmon },
			{ "DarkSeaGreen", Color.DarkSeaGreen },
			{ "DarkSlateBlue", Color.DarkSlateBlue },
			{ "DarkSlateGray", Color.DarkSlateGray },
			{ "DarkTurquoise", Color.DarkTurquoise },
			{ "DarkViolet", Color.DarkViolet },
			{ "DeepPink", Color.DeepPink },
			{ "DeepSkyBlue", Color.DeepSkyBlue },
			{ "DimGray", Color.DimGray },
			{ "DodgerBlue", Color.DodgerBlue },
			{ "Firebrick", Color.Firebrick },
			{ "FloralWhite", Color.FloralWhite },
			{ "ForestGreen", Color.ForestGreen },
			{ "Fuchsia", Color.Fuchsia },
			{ "Gainsboro", Color.Gainsboro },
			{ "GhostWhite", Color.GhostWhite },
			{ "Gold", Color.Gold },
			{ "Goldenrod", Color.Goldenrod },
			{ "Gray", Color.Gray },
			{ "Green", Color.Green },
			{ "GreenYellow", Color.GreenYellow },
			{ "Honeydew", Color.Honeydew },
			{ "HotPink", Color.HotPink },
			{ "IndianRed", Color.IndianRed },
			{ "Indigo", Color.Indigo },
			{ "Ivory", Color.Ivory },
			{ "Khaki", Color.Khaki },
			{ "Lavender", Color.Lavender },
			{ "LavenderBlush", Color.LavenderBlush },
			{ "LawnGreen", Color.LawnGreen },
			{ "LemonChiffon", Color.LemonChiffon },
			{ "LightBlue", Color.LightBlue },
			{ "LightCoral", Color.LightCoral },
			{ "LightCyan", Color.LightCyan },
			{ "LightGoldenrodYellow", Color.LightGoldenrodYellow },
			{ "LightGray", Color.LightGray },
			{ "LightGreen", Color.LightGreen },
			{ "LightPink", Color.LightPink },
			{ "LightSalmon", Color.LightSalmon },
			{ "LightSeaGreen", Color.LightSeaGreen },
			{ "LightSkyBlue", Color.LightSkyBlue },
			{ "LightSlateGray", Color.LightSlateGray },
			{ "LightSteelBlue", Color.LightSteelBlue },
			{ "LightYellow", Color.LightYellow },
			{ "Lime", Color.Lime },
			{ "LimeGreen", Color.LimeGreen },
			{ "Linen", Color.Linen },
			{ "Magenta", Color.Magenta },
			{ "Maroon", Color.Maroon },
			{ "MediumAquamarine", Color.MediumAquamarine },
			{ "MediumBlue", Color.MediumBlue },
			{ "MediumOrchid", Color.MediumOrchid },
			{ "MediumPurple", Color.MediumPurple },
			{ "MediumSeaGreen", Color.MediumSeaGreen },
			{ "MediumSlateBlue", Color.MediumSlateBlue },
			{ "MediumSpringGreen", Color.MediumSpringGreen },
			{ "MediumTurquoise", Color.MediumTurquoise },
			{ "MediumVioletRed", Color.MediumVioletRed },
			{ "MidnightBlue", Color.MidnightBlue },
			{ "MintCream", Color.MintCream },
			{ "MistyRose", Color.MistyRose },
			{ "Moccasin", Color.Moccasin },
			{ "NavajoWhite", Color.NavajoWhite },
			{ "Navy", Color.Navy },
			{ "OldLace", Color.OldLace },
			{ "Olive", Color.Olive },
			{ "OliveDrab", Color.OliveDrab },
			{ "Orange", Color.Orange },
			{ "OrangeRed", Color.OrangeRed },
			{ "Orchid", Color.Orchid },
			{ "PaleGoldenrod", Color.PaleGoldenrod },
			{ "PaleGreen", Color.PaleGreen },
			{ "PaleTurquoise", Color.PaleTurquoise },
			{ "PaleVioletRed", Color.PaleVioletRed },
			{ "PapayaWhip", Color.PapayaWhip },
			{ "PeachPuff", Color.PeachPuff },
			{ "Peru", Color.Peru },
			{ "Pink", Color.Pink },
			{ "Plum", Color.Plum },
			{ "PowderBlue", Color.PowderBlue },
			{ "Purple", Color.Purple },
			{ "RebeccaPurple", Color.RebeccaPurple },
			{ "Red", Color.Red },
			{ "RosyBrown", Color.RosyBrown },
			{ "RoyalBlue", Color.RoyalBlue },
			{ "SaddleBrown", Color.SaddleBrown },
			{ "Salmon", Color.Salmon },
			{ "SandyBrown", Color.SandyBrown },
			{ "SeaGreen", Color.SeaGreen },
			{ "SeaShell", Color.SeaShell },
			{ "Sienna", Color.Sienna },
			{ "Silver", Color.Silver },
			{ "SkyBlue", Color.SkyBlue },
			{ "SlateBlue", Color.SlateBlue },
			{ "SlateGray", Color.SlateGray },
			{ "Snow", Color.Snow },
			{ "SpringGreen", Color.SpringGreen },
			{ "SteelBlue", Color.SteelBlue },
			{ "Tan", Color.Tan },
			{ "Teal", Color.Teal },
			{ "Thistle", Color.Thistle },
			{ "Tomato", Color.Tomato },
			{ "Turquoise", Color.Turquoise },
			{ "Violet", Color.Violet },
			{ "Wheat", Color.Wheat },
			{ "White", Color.White },
			{ "WhiteSmoke", Color.WhiteSmoke },
			{ "Yellow", Color.Yellow },
			{ "YellowGreen", Color.YellowGreen }
		};

		private static readonly IReadOnlyDictionary<Color, string> ColorNames = NamedColors.ToDictionaryAllowRepeats(kv => kv.Value, kv => kv.Key, false);

		#endregion Named Colors
	}

}
