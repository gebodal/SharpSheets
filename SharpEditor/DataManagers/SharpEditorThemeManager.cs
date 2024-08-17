using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using SharpEditor.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpEditor.DataManagers {

	public static class SharpEditorThemeManager {

		private static ConfigName ConfigName => new ConfigName(SharpEditorData.GetEditorName() + "_Theme", ".json");

		private static IReadOnlyDictionary<string, Color>? defaultTheme = null;

		public static void LoadTheme(App appInstance) {
			IReadOnlyDictionary<string, Color>? themeConfig = SharpConfigManager.Load<Dictionary<string, Color>>(ConfigName, out bool latest);
			if (themeConfig is not null) {
				SetColors(appInstance, themeConfig);
			}
			else if (latest && themeConfig is null) {
				SharpConfigManager.SaveBackup(ConfigName);
				SaveTheme(appInstance);
			}
		}

		private static void SaveTheme(App appInstance) {
			SharpConfigManager.Save(GetColors(appInstance), ConfigName);
		}

		public static void SetColors(App appInstance, IReadOnlyDictionary<string, Color> colors) {
			if (defaultTheme is null) {
				defaultTheme = GetColors(appInstance);
			}

			bool changed = false;
			foreach ((string name, Color color) in colors) {
				if (resourceNames.Contains(name) && appInstance.Resources.TryGetResource(name, null, out object? value) && value is Color current) {
					if (current != color) {
						appInstance.Resources[name] = color;
						changed = true;
					}
				}
			}

			if (changed) {
				SaveTheme(appInstance);
			}
		}

		public static IReadOnlyDictionary<string, Color> GetColors(App appInstance) {
			Dictionary<string, Color> colors = new Dictionary<string, Color>();

			foreach (string name in resourceNames) {
				if (appInstance.Resources.TryGetResource(name, null, out object? value) && value is Color color) {
					colors[name] = color;
				}
			}

			return colors;
		}

		public static IReadOnlyDictionary<string, Color> GetDefaultThemeColors(App appInstance) {
			if (defaultTheme is null) {
				defaultTheme = GetColors(appInstance);
			}

			return defaultTheme;
		}

		public static readonly IReadOnlyList<string> resourceNames = new List<string>() {
			"ThemeForegroundColor",
			"ThemeForegroundLowColor",
			"ThemeBackgroundColor",
			"ThemeBorderLowColor",
			"ThemeBorderMidColor",
			"ThemeBorderHighColor",
			"ThemeControlLowColor",
			"ThemeControlMidColor",
			"ThemeControlMidHighColor",
			"ThemeControlHighColor",
			"ThemeControlVeryHighColor",
			"ThemeControlHighlightLowColor",
			"ThemeControlHighlightMidColor",
			"ThemeControlHighlightHighColor",
			"HighlightForegroundColor",
			"HighlightColor",
			"HighlightColor2",
			"ThemeAccentColor",
			"ThemeAccentColor2",
			"ThemeAccentColor3",
			//"ThemeAccentColor4", // Not used in editor so far
			"ErrorColor",
			"ErrorLowColor",
			"HyperlinkColor",
			//"HyperlinkVisitedColor", // Not used in editor
			"EditorDecorationColor",
			"EditorSelectionColor",
			"EditorSearchResultColor",
			"EditorCurrentLineBorderColor",
			"EditorCurrentLineColor",
			"EditorOwnerColor",
			"EditorChildColor",
			"EditorSameTokenColor",
			"EditorErrorColor1",
			"EditorErrorColor2",
			"DesignerOriginalAreaHighlightColor",
			"DesignerInnerAreaHighlightColor",
			"DesignerAdjustedAreaHighlightColor",
			"DesignerUnselectedAreaHighlightColor",
			"DesignerFieldColor",
		};

		public static readonly string ThemeForegroundBrush = "ThemeForegroundBrush";
		public static readonly string ThemeForegroundLowBrush = "ThemeForegroundLowBrush";
		public static readonly string ThemeForegroundLowPartialBrush = "ThemeForegroundLowPartialBrush";

		public static readonly string ThemeBackgroundBrush = "ThemeBackgroundBrush";

		public static readonly string ThemeBorderLowBrush = "ThemeBorderLowBrush";
		public static readonly string ThemeBorderMidBrush = "ThemeBorderMidBrush";
		public static readonly string ThemeBorderHighBrush = "ThemeBorderHighBrush";

		public static readonly string ThemeControlLowBrush = "ThemeControlLowBrush";
		public static readonly string ThemeControlMidBrush = "ThemeControlMidBrush";
		public static readonly string ThemeControlMidHighBrush = "ThemeControlMidHighBrush";
		public static readonly string ThemeControlHighBrush = "ThemeControlHighBrush";
		public static readonly string ThemeControlVeryHighBrush = "ThemeControlVeryHighBrush";

		public static readonly string ThemeControlHighlightLowBrush = "ThemeControlHighlightLowBrush";
		public static readonly string ThemeControlHighlightMidBrush = "ThemeControlHighlightMidBrush";
		public static readonly string ThemeControlHighlightHighBrush = "ThemeControlHighlightHighBrush";

		public static readonly string HighlightForegroundBrush = "HighlightForegroundBrush";
		public static readonly string HighlightBrush = "HighlightBrush";
		public static readonly string HighlightBrush2 = "HighlightBrush2";
		public static readonly string HighlightForegroundWeakBrush = "HighlightForegroundWeakBrush";
		public static readonly string HighlightForegroundWeakerBrush = "HighlightForegroundWeakerBrush";

		public static readonly string ThemeAccentBrush = "ThemeAccentBrush";
		public static readonly string ThemeAccentBrush2 = "ThemeAccentBrush2";
		public static readonly string ThemeAccentBrush3 = "ThemeAccentBrush3";
		public static readonly string ThemeAccentBrush4 = "ThemeAccentBrush4";

		public static readonly string ErrorBrush = "ErrorBrush";
		public static readonly string ErrorLowBrush = "ErrorLowBrush";

		public static readonly string HyperlinkBrush = "HyperlinkBrush";
		public static readonly string HyperlinkVisitedBrush = "HyperlinkVisitedBrush";

		public static readonly string EditorDecorationBrush = "EditorDecorationBrush";
		public static readonly string EditorSelectionBrush = "EditorSelectionBrush";
		public static readonly string EditorSearchResultBrush = "EditorSearchResultBrush";
		public static readonly string EditorCurrentLineBorderBrush = "EditorCurrentLineBorderBrush";
		public static readonly string EditorCurrentLineBrush = "EditorCurrentLineBrush";

		public static readonly string EditorOwnerBrush = "EditorOwnerBrush";
		public static readonly string EditorChildBrush = "EditorChildBrush";
		public static readonly string EditorSameTokenBrush = "EditorSameTokenBrush";

		public static readonly string EditorErrorBrush1 = "EditorErrorBrush1";
		public static readonly string EditorErrorBrush2 = "EditorErrorBrush2";

		public static readonly string DesignerOriginalAreaHighlightBrush = "DesignerOriginalAreaHighlightBrush";
		public static readonly string DesignerInnerAreaHighlightBrush = "DesignerInnerAreaHighlightBrush";
		public static readonly string DesignerAdjustedAreaHighlightBrush = "DesignerAdjustedAreaHighlightBrush";
		public static readonly string DesignerUnselectedAreaHighlightBrush = "DesignerUnselectedAreaHighlightBrush";

		public static readonly string DesignerFieldBrush = "DesignerFieldBrush";

	}

}
