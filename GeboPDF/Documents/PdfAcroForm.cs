using GeboPdf.Fonts;
using GeboPdf.Graphics;
using GeboPdf.Objects;
using GeboPdf.XObjects;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace GeboPdf.Documents {

	public static class PdfAcroFormManager {

		public static PdfAcroField AddTextField(PdfAcroForm acroForm,
			PdfPage page, PdfRectangle rect,
			PdfAnnotationFlags annotationFlags,
			string partialFieldName, string? alternateFieldName,
			PdfFieldFlags fieldFlags, PdfTextFieldFlags textFieldFlags,
			int? maxLen,
			PdfString? value, PdfString? defaultValue,
			PdfFont font, float fontsize, PdfDeviceColor color, PdfVariableTextQuadding quadding,
			PdfWidgetRotation rotation
			) {

			PdfRectangle bBox = PdfRectangle.FromDimensions(0, 0, rect.Width, rect.Height);

			PdfGraphicsXObject normalAppearance = new PdfGraphicsXObject(bBox, PdfMatrix.Identity);

			PdfResourcesDictionary acroFormDefaultResources = acroForm.defaultResources;
			acroFormDefaultResources.AddFont(font, out PdfName fontName);
			acroFormDefaultResources.RegisterFontUsageAll(font);
			normalAppearance.resources.AddFont(fontName, font);
			PdfString defaultAppearanceString = GraphicsStream.GetTextFieldDefaultAppearance(normalAppearance.resources, font, fontsize, color);

			normalAppearance.graphics.BeginMarkedContent(PdfNames.TextField);
			normalAppearance.graphics.SaveState();
			if (value != null && value.Value.Length > 0) {
				// TODO Add text content here
			}
			normalAppearance.graphics.RestoreState();
			normalAppearance.graphics.EndMarkedContent();

			PdfAppearanceDictionary appearanceDict = new PdfAppearanceDictionary() {
				Normal = normalAppearance
			};

			PdfAppearanceCharacteristicsDictionary appearanceCharacteristics = new PdfAppearanceCharacteristicsDictionary() {
				Rotation = rotation
			};

			PdfWidgetAnnotationDictionary widgetAnnotationDict = new PdfWidgetAnnotationDictionary(
				page, rect, annotationFlags, appearanceDict, null, appearanceCharacteristics, null, null
				);
			PdfTextFieldDictionary textFieldDict = new PdfTextFieldDictionary(
				partialFieldName, alternateFieldName,
				fieldFlags, textFieldFlags,
				defaultAppearanceString, quadding, maxLen,
				value, defaultValue
				);

			PdfAcroFieldDictionary acroFieldDict = new PdfAcroFieldDictionary(widgetAnnotationDict, textFieldDict);

			PdfAcroField acroField = new PdfAcroField(acroFieldDict, appearanceDict);

			page.pageAnnotations.Add(acroField);
			acroForm.AddField(acroField);

			return acroField;
		}

		private static readonly float CheckMarkSizeFactor = 0.75f;

		public static PdfCheckBoxAcroField AddCustomCheckBoxField(PdfAcroForm acroForm,
			PdfPage page, PdfRectangle rect,
			PdfAnnotationFlags annotationFlags,
			string partialFieldName, string? alternateFieldName,
			PdfFieldFlags fieldFlags,
			bool value, bool? defaultValue,
			char normalCaption, PdfDeviceColor color,
			PdfAnnotationBorderStyleDictionary? borderStyle
			) {

			if (!PdfStandardFont.standardFontMetrics[PdfStandardFonts.ZapfDingbats].metrics.ContainsKey((uint)((int)normalCaption))) {
				throw new ArgumentException($"Invalid normalCaption character ({(int)normalCaption} is not defined for ZapfDingbats).");
			}

			PdfRectangle bBox = PdfRectangle.FromDimensions(0, 0, rect.Width, rect.Height);

			PdfGraphicsXObject normalOnAppearance = new PdfGraphicsXObject(bBox, PdfMatrix.Identity);
			PdfGraphicsXObject normalOffAppearance = new PdfGraphicsXObject(bBox, PdfMatrix.Identity);

			PdfFont zapfFont = PdfStandardFont.ZapfDingbats;
			PdfResourcesDictionary acroFormDefaultResources = acroForm.defaultResources;
			acroFormDefaultResources.AddFont(zapfFont, out PdfName fontName);
			normalOnAppearance.resources.AddFont(fontName, zapfFont);
			PdfString defaultAppearanceString = GraphicsStream.GetTextFieldDefaultAppearance(normalOnAppearance.resources, zapfFont, rect.Height * CheckMarkSizeFactor, color);

			PdfAppearanceDictionary appearanceDict = new PdfAppearanceDictionary() {
				Normal = new PdfDictionary() { { PdfNames.Yes, normalOnAppearance }, { PdfNames.Off, normalOffAppearance } }
			};

			PdfAppearanceCharacteristicsDictionary appearanceCharacteristics = new PdfAppearanceCharacteristicsDictionary() {
				NormalCaption = normalCaption.ToString()
			};

			PdfWidgetAnnotationDictionary widgetAnnotationDict = new PdfWidgetAnnotationDictionary(
				page, rect,
				annotationFlags,
				appearanceDict, PdfCheckBoxFieldDictionary.StateName(value),
				appearanceCharacteristics, null, borderStyle
				);
			PdfCheckBoxFieldDictionary checkBoxFieldDict = new PdfCheckBoxFieldDictionary(
				partialFieldName, alternateFieldName,
				fieldFlags,
				value, defaultValue,
				defaultAppearanceString
				);

			PdfAcroFieldDictionary acroFieldDict = new PdfAcroFieldDictionary(widgetAnnotationDict, checkBoxFieldDict);

			PdfCheckBoxAcroField acroField = new PdfCheckBoxAcroField(acroFieldDict, appearanceDict, normalOnAppearance, normalOffAppearance);

			page.pageAnnotations.Add(acroField);
			acroForm.AddField(acroField);

			return acroField;
		}

		public static PdfAcroField AddStandardCheckBoxField(PdfAcroForm acroForm,
			PdfPage page, PdfRectangle rect,
			PdfAnnotationFlags annotationFlags,
			string partialFieldName, string? alternateFieldName,
			PdfFieldFlags fieldFlags,
			bool value, bool? defaultValue,
			char normalCaption, PdfDeviceColor color,
			PdfAnnotationBorderStyleDictionary? borderStyle
			) {

			PdfCheckBoxAcroField field = AddCustomCheckBoxField(
				acroForm,
				page, rect,
				annotationFlags,
				partialFieldName, alternateFieldName,
				fieldFlags,
				value, defaultValue,
				normalCaption, color,
				borderStyle);

			Fonts.CharMetric charMetric = PdfStandardFont.standardFontMetrics[PdfStandardFonts.ZapfDingbats].metrics[(uint)((int)normalCaption)];

			GraphicsStream onCanvas = field.OnAppearance.graphics;

			if (normalCaption == '8') {
				float x, y, size;
				float initialAspect = rect.Width / rect.Height;
				if (initialAspect > 1f) { // Rectangle is too wide
					size = rect.Height;
					y = 0;
					x = (rect.Width - size) / 2f;
				}
				else { // Rectangle is too tall
					size = rect.Width;
					x = 0;
					y = (rect.Height - size) / 2f;
				}

				onCanvas.SaveState();
				onCanvas.SetStrokingColor(color);
				onCanvas.Move(x + 2, y + 2).Line(x + size - 2, y + size - 2).Move(x + 2, y + size - 2).Line(x + size - 2, y + 2);
				onCanvas.Stroke();
				onCanvas.RestoreState();
			}
			else {
				float fontSize = rect.Height * CheckMarkSizeFactor;

				float charWidth = fontSize * charMetric.width / 1000f;
				float charOffset = fontSize * charMetric.bBox.lly / 1000f;
				float charHeight = fontSize * (charMetric.bBox.ury - charMetric.bBox.lly) / 1000f;

				float x = (rect.Width - charWidth) / 2f;
				float y = 0.5f * rect.Height - charOffset - 0.5f * charHeight;

				onCanvas.SaveState();
				onCanvas.FontAndSize(PdfStandardFont.ZapfDingbats, fontSize);
				onCanvas.BeginText()
					.MoveToStart(x, y)
					.ShowText(normalCaption.ToString())
					.EndText();
				onCanvas.RestoreState();
			}

			return field;
		}

		public static bool IsValidCheckBoxNormalCaption(char c) {
			uint cValue = (uint)((int)c);
			return PdfStandardFont.standardFontMetrics[PdfStandardFonts.ZapfDingbats].metrics.ContainsKey(cValue);
		}

		public static PdfPushButtonAcroField AddPushButtonField(PdfAcroForm acroForm,
			PdfPage page, PdfRectangle rect,
			PdfAnnotationFlags annotationFlags,
			string partialFieldName, string? alternateFieldName,
			PdfFieldFlags fieldFlags,
			PdfAction? action,
			string? normalCaption, PdfPushButtonCaptionPosition captionPosition, PdfFont font, float size, PdfDeviceColor color,
			PdfAnnotationBorderStyleDictionary? borderStyle
			) {

			PdfRectangle bBox = PdfRectangle.FromDimensions(0, 0, rect.Width, rect.Height);

			PdfGraphicsXObject normalAppearance = new PdfGraphicsXObject(bBox, PdfMatrix.Identity);

			PdfAppearanceCharacteristicsDictionary appearanceCharacteristics = new PdfAppearanceCharacteristicsDictionary() {
				CaptionPosition = captionPosition
			};

			PdfString? defaultAppearanceString = null;
			if (captionPosition != PdfPushButtonCaptionPosition.NoCaptionIconOnly && !string.IsNullOrEmpty(normalCaption)) {
				if(font is null) {
					throw new ArgumentException("Must provide a font if the push button has a caption.");
				}
				PdfResourcesDictionary acroFormDefaultResources = acroForm.defaultResources;
				acroFormDefaultResources.AddFont(font, out PdfName fontName);
				acroFormDefaultResources.RegisterFontUsageAll(font); // TODO Is this necessary?
				normalAppearance.resources.AddFont(fontName, font);
				defaultAppearanceString = GraphicsStream.GetTextFieldDefaultAppearance(normalAppearance.resources, font, size, color);

				appearanceCharacteristics.NormalCaption = normalCaption;
			}

			PdfAppearanceDictionary appearanceDict = new PdfAppearanceDictionary() {
				Normal = normalAppearance
			};

			

			PdfWidgetAnnotationDictionary widgetAnnotationDict = new PdfWidgetAnnotationDictionary(
				page, rect,
				annotationFlags,
				appearanceDict, null,
				appearanceCharacteristics,
				action,
				borderStyle
				);
			PdfPushButtonFieldDictionary pushButtonFieldDict = new PdfPushButtonFieldDictionary(
				partialFieldName, alternateFieldName,
				fieldFlags,
				defaultAppearanceString
				);

			PdfAcroFieldDictionary acroFieldDict = new PdfAcroFieldDictionary(widgetAnnotationDict, pushButtonFieldDict);

			PdfPushButtonAcroField acroField = new PdfPushButtonAcroField(acroFieldDict, appearanceDict, normalAppearance);

			page.pageAnnotations.Add(acroField);
			acroForm.AddField(acroField);

			return acroField;
		}

		public static PdfPushButtonAcroField AddPushButtonField(PdfAcroForm acroForm,
			PdfPage page, PdfRectangle rect,
			PdfAnnotationFlags annotationFlags,
			string partialFieldName, string? alternateFieldName,
			PdfFieldFlags fieldFlags,
			PdfAction? action,
			PdfAnnotationBorderStyleDictionary? borderStyle
			) {

			return AddPushButtonField(acroForm,
				page, rect,
				annotationFlags,
				partialFieldName, alternateFieldName,
				fieldFlags,
				action,
				null, PdfPushButtonCaptionPosition.NoCaptionIconOnly, new PdfStandardFont(PdfStandardFonts.Helvetica, null), 0f, PdfGrayColor.Black,
				borderStyle
				);
		}

	}

	public class PdfAcroForm : IPdfDocumentContents {

		public readonly PdfIndirectReference Reference;

		public bool HasFields { get { return fields.Length > 0; } }

		public bool NeedsAppearances {
			get { return dict.NeedsAppearances; }
			set { dict.NeedsAppearances = value; }
		}

		private readonly PdfAcroFormDictionary dict;

		private readonly PdfArray fields;
		public readonly PdfResourcesDictionary defaultResources;

		private readonly List<PdfAcroField> fieldList; // TODO This should probably hold some kind of explanatory object, which can be checked for existing field names, etc.

		public PdfAcroForm() {
			this.fields = new PdfArray();
			this.fieldList = new List<PdfAcroField>();

			this.defaultResources = new PdfResourcesDictionary(false);

			this.dict = new PdfAcroFormDictionary(this.fields, this.defaultResources);

			this.Reference = PdfIndirectReference.Create(this.dict);
		}

		public IEnumerable<PdfObject> CollectObjects() {
			yield return dict;
			foreach(PdfAcroField acroField in fieldList) {
				foreach(PdfObject acroFieldObj in acroField.CollectObjects()) {
					yield return acroFieldObj;
				}
			}
			foreach (PdfObject resourceObj in defaultResources.CollectObjects()) {
				yield return resourceObj;
			}
		}

		public void AddField(PdfAcroField acroField) {
			fields.Add(acroField.DictionaryReference);
			fieldList.Add(acroField);
		}
	}

	public class PdfAcroFormDictionary : AbstractPdfDictionary {

		private readonly AbstractPdfArray fields;
		public bool NeedsAppearances { get; set; }
		// We won't be interacting with signatures, so no SigFlags (defaults to zero, so we're good)
		// TODO CO? (Calculation order)
		private readonly PdfResourcesDictionary defaultResources;

		public PdfAcroFormDictionary(AbstractPdfArray fields, PdfResourcesDictionary defaultResources) : base() {
			this.fields = fields;
			this.defaultResources = defaultResources;
			this.NeedsAppearances = false;
		}

		public override int Count {
			get {
				return 2 + (NeedsAppearances ? 1 : 0);
			}
		}

		public override IEnumerator<KeyValuePair<PdfName, PdfObject>> GetEnumerator() {
			yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.Fields, fields);
			yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.DefaultResources, defaultResources);

			if (NeedsAppearances) {
				yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.NeedAppearances, new PdfBoolean(NeedsAppearances));
			}
		}

	}

	public class PdfAcroField : IPdfDocumentContents {

		public PdfIndirectReference DictionaryReference { get; }

		private readonly PdfAcroFieldDictionary fieldDict;
		private readonly PdfAppearanceDictionary appearanceDict;

		public PdfObject? Value { get { return fieldDict.Value; } set { fieldDict.Value = value; } }

		public PdfAcroField(PdfAcroFieldDictionary fieldDict, PdfAppearanceDictionary appearanceDict) {
			this.fieldDict = fieldDict;
			this.appearanceDict = appearanceDict;

			this.DictionaryReference = PdfIndirectReference.Create(this.fieldDict);
		}

		public IEnumerable<PdfObject> CollectObjects() {
			yield return fieldDict;
			foreach(PdfXObject appearance in appearanceDict.CollectAppearances()) {
				foreach(PdfObject appObj in appearance.CollectObjects()) {
					yield return appObj;
				}
			}
		}

	}

	public class PdfCheckBoxAcroField : PdfAcroField {

		public readonly PdfGraphicsXObject OnAppearance;
		public readonly PdfGraphicsXObject OffAppearance;

		public PdfCheckBoxAcroField(PdfAcroFieldDictionary fieldDict, PdfAppearanceDictionary appearanceDict, PdfGraphicsXObject onAppearance, PdfGraphicsXObject offAppearance) : base(fieldDict, appearanceDict) {
			this.OnAppearance = onAppearance;
			this.OffAppearance = offAppearance;
		}

	}

	public class PdfPushButtonAcroField : PdfAcroField {

		public readonly PdfGraphicsXObject Appearance;

		public PdfPushButtonAcroField(PdfAcroFieldDictionary fieldDict, PdfAppearanceDictionary appearanceDict, PdfGraphicsXObject appearance) : base(fieldDict, appearanceDict) {
			this.Appearance = appearance;
		}

	}

	public class PdfAppearanceDictionary : AbstractPdfDictionary {

		// Would love some better type checking here (this should really be "Union<PdfXObject,PdfDictionary>", but what can you do?)

		private PdfObject? normal;
		private PdfObject? rollover;
		private PdfObject? down;

		public PdfObject? Normal {
			get {
				return normal;
			}
			set {
				if (!(value is PdfXObject || (value is AbstractPdfDictionary dict && dict.All(kv => kv.Value is PdfXObject)))) {
					throw new ArgumentException("Appearance dictionary can only contain XObjects or Dictionaries of XOBjects");
				}
				normal = value;
			}
		}
		public PdfObject? Rollover {
			get {
				return rollover;
			}
			set {
				if ((value is AbstractPdfDictionary dict && !dict.All(kv => kv.Value is PdfXObject)) || value is not PdfXObject) {
					throw new ArgumentException("Appearance dictionary can only contain XObjects or Dictionaries of XOBjects");
				}
				rollover = value;
			}
		}
		public PdfObject? Down {
			get {
				return down;
			}
			set {
				if ((value is AbstractPdfDictionary dict && !dict.All(kv => kv.Value is PdfXObject)) || value is not PdfXObject) {
					throw new ArgumentException("Appearance dictionary can only contain XObjects or Dictionaries of XOBjects");
				}
				down = value;
			}
		}

		public override int Count {
			get {
				int count = 0;
				if (normal != null) count += 1;
				if (rollover != null) count += 1;
				if (down != null) count += 1;
				return count;
			}
		}

		public override IEnumerator<KeyValuePair<PdfName, PdfObject>> GetEnumerator() {
			if (normal != null) {
				yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.NormalAppearance, GetDictObject(normal));
			}
			if (rollover != null) {
				yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.RolloverAppearance, GetDictObject(rollover));
			}
			if (down != null) {
				yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.DownAppearance, GetDictObject(down));
			}
		}

		private static PdfObject GetDictObject(PdfObject appearance) {
			if (appearance is PdfXObject xObject) {
				return PdfIndirectReference.Create(xObject);
			}
			else if (appearance is AbstractPdfDictionary dict) {
				return GetDict(dict);
			}
			else {
				return appearance;
			}
		}

		private static AbstractPdfDictionary GetDict(AbstractPdfDictionary dict) {
			PdfDictionary referencesDict = new PdfDictionary();
			foreach(KeyValuePair<PdfName, PdfObject> entry in dict) {
				referencesDict.Add(entry.Key, PdfIndirectReference.Create(entry.Value));
			}
			return referencesDict;
		}

		public IEnumerable<PdfXObject> CollectAppearances() {
			foreach(PdfXObject normalApp in GetAppearanceXOBjects(normal)) {
				yield return normalApp;
			}
			foreach (PdfXObject rolloverApp in GetAppearanceXOBjects(rollover)) {
				yield return rolloverApp;
			}
			foreach (PdfXObject downApp in GetAppearanceXOBjects(down)) {
				yield return downApp;
			}
		}

		private static IEnumerable<PdfXObject> GetAppearanceXOBjects(PdfObject? appearance) {
			if (appearance == null) {
				yield break;
			}

			if (appearance is PdfXObject xObject) {
				yield return xObject;
			}
			else if (appearance is AbstractPdfDictionary dictionary) {
				foreach (KeyValuePair<PdfName, PdfObject> entry in dictionary) {
					yield return (PdfXObject)entry.Value;
				}
			}
		}

	}

	public enum PdfFieldType { Button, Text }

	public static class FieldTypeUtils {

		public static PdfName GetFieldName(this PdfFieldType fieldType) {
			if (fieldType == PdfFieldType.Button) {
				return PdfNames.ButtonField;
			}
			else {
				return PdfNames.TextField;
			}
		}

	}

	[Flags]
	public enum PdfFieldFlags : int {
		None = 0,
		/// <summary>
		/// If set, the user may not change the value of the field.
		/// Any associated widget annotations will not interact with the user;
		/// that is, they will not respond to mouse clicks or change their appearance in response to mouse motions.
		/// This flag is useful for fields whose values are computed or imported from a database.
		/// </summary>
		ReadOnly = 1 << 0,
		/// <summary>
		/// If set, the field must have a value at the time it is exported by a submit-form action.
		/// </summary>
		Required = 1 << 2,
		/// <summary>
		/// If set, the field must not be exported.
		/// </summary>
		NoExport = 1 << 3
	}

	[Flags]
	public enum PdfTextFieldFlags : int {
		None = 0,
		/// <summary>
		/// (Bit 13) If set, the field can contain multiple lines of text; if clear,
		/// the field's text is restricted to a single line.
		/// </summary>
		Multiline = 1 << 12,
		/// <summary>
		/// (Bit 14) If set, the field is intended for entering a secure password that
		/// should not be echoed visibly to the screen. Characters typed from the keyboard
		/// should instead be echoed in some unreadable form, such as asterisks or
		/// bullet characters.
		/// <br/>
		/// To protect password confidentiality, viewer applications should never store
		/// the value of the text field in the PDF file if this flag is set.
		/// </summary>
		Password = 1 << 13,
		/// <summary>
		/// (Bit 21) If set, the text entered in the field represents the pathname of a file
		/// whose contents are to be submitted as the value of the field.
		/// </summary>
		FileSelect = 1 << 20,
		/// <summary>
		/// (Bit 23) If set, text entered in the field is not spell-checked.
		/// </summary>
		DoNotSpellCheck = 1 << 22,
		/// <summary>
		/// (Bit 24) If set, the field does not scroll (horizontally for single-line fields,
		/// vertically for multiple-line fields) to accommodate more text than fits within
		/// its annotation rectangle. Once the field is full, no further text is accepted.
		/// </summary>
		DoNotScroll = 1 << 23,
		/// <summary>
		/// (Bit 25) Meaningful only if the MaxLen entry is present in the text field dictionary
		/// and if the Multiline, Password, and FileSelect flags are clear. If set, the field is
		/// automatically divided into as many equally spaced positions, or combs, as the value
		/// of MaxLen, and the text is laid out into those combs.
		/// </summary>
		Comb = 1 << 24,
		/// <summary>
		/// (Bit 26) If set, the value of this field should be represented as a rich text string.
		/// If the field has a value, the RVentry of the field dictionary specifies the rich
		/// text string.
		/// </summary>
		RichText = 1 << 25
	}

	[Flags]
	public enum PdfButtonFieldFlags : int {
		None = 0,
		/// <summary>
		/// (Bit 15) (Radio buttons only) If set, exactly one radio button must be selected at all
		/// times; clicking the currently selected button has no effect. If clear, clicking the
		/// selected button deselects it, leaving no button selected.
		/// </summary>
		NoToggleToOff = 1 << 14,
		/// <summary>
		/// (Bit 16) If set, the field is a set of radio buttons; if clear, the field is a check box.
		/// This flag is meaningful only if the Pushbutton flag is clear.
		/// </summary>
		Radio = 1<<15,
		/// <summary>
		/// (Bit 17) If set, the field is a pushbutton that does not retain a permanent value.
		/// </summary>
		Pushbutton = 1 << 16,
		/// <summary>
		/// (Bit 26) If set, a group of radio buttons within a radio button field that use the same
		/// value for the on state will turn on and off in unison; that is if one is checked, they
		/// are all checked. If clear, the buttons are mutually exclusive (the same behavior as HTML
		/// radio buttons).
		/// </summary>
		RadiosInUnison = 1 << 25
	}

	public enum PdfVariableTextQuadding : int {
		LeftJustified = 0,
		Centered = 1,
		RightJustified = 2
	}

	[Flags]
	public enum PdfAnnotationFlags : int {
		None = 0,
		/// <summary>
		/// (Bit 1) If set, do not display the annotation if it does not belong to one of the standard
		/// annotation types and no annotation handler is available. If clear, display such an unknown
		/// annotation using an appearance stream specified by its appearance dictionary, if any.
		/// </summary>
		Invisible = 1 << 0,
		/// <summary>
		/// (Bit 2) If set, do not display or print the annotation or allow it to interact with the user,
		/// regardless of its annotation type or whether an annotation handler is available. In cases
		/// where screen space is limited, the ability to hide and show annotations selectively can be
		/// used in combination with appearance streams to display auxiliary pop-up information similar
		/// in function to online help systems.
		/// </summary>
		Hidden = 1 << 1,
		/// <summary>
		/// (Bit 3) If set, print the annotation when the page is printed. If clear, never print the
		/// annotation, regardless of whether it is displayed on the screen. This can be useful, for
		/// example, for annotations representing interactive pushbuttons, which would serve no meaningful
		/// purpose on the printed page.
		/// </summary>
		Print = 1 << 2,
		/// <summary>
		/// (Bit 4) If set, do not scale the annotation's appearance to match the magnification of the
		/// page. The location of the annotation on the page (defined by the upper-left corner of its
		/// annotation rectangle) remains fixed, regardless of the page magnification.
		/// </summary>
		NoZoom = 1 << 3,
		/// <summary>
		/// (Bit 5) If set, do not rotate the annotation's appearance to match the rotation of the page.
		/// The upper-left corner of the annotation rectangle remains in a fixed location on the page,
		/// regardless of the page rotation.
		/// </summary>
		NoRotate = 1 << 4,
		/// <summary>
		/// (Bit 6) If set, do not display the annotation on the screen or allow it to interact with the
		/// user. The annotation may be printed (depending on the setting of the Print flag) but should
		/// be considered hidden for purposes of on-screen display and user interaction.
		/// </summary>
		NoView = 1 << 5,
		/// <summary>
		/// (Bit 7) If set, do not allow the annotation to interact with the user. The annotation may be
		/// displayed or printed (depending on the settings of the NoView and Print flags) but should not
		/// respond to mouse clicks or change its appearance in response to mouse motions.
		/// <br/>
		///	Note: This flag is ignored for widget annotations; its function is subsumed by the ReadOnly
		///	flag of the associated form field.
		/// </summary>
		ReadOnly = 1 << 6,
		/// <summary>
		/// (Bit 8) If set, do not allow the annotation to be deleted or its properties (including position
		/// and size) to be modified by the user. However, this flag does not restrict changes to the
		/// annotation's contents, such as the value of a form field.
		/// </summary>
		Locked = 1 << 7,
		/// <summary>
		/// (Bit 9) If set, invert the interpretation of the NoView flag for certain events. A typical
		/// use is to have an annotation that appears only when a mouse cursor is held over it.
		/// </summary>
		ToggleNoView = 1 << 8,
		/// <summary>
		/// (Bit 10) If set, do not allow the contents of the annotation to be modified by the user. This
		/// flag does not restrict deletion of the annotation or changes to other annotation properties,
		/// such as position and size.
		/// </summary>
		LockedContents = 1 << 9
	}

	public class PdfAcroFieldDictionary : AbstractPdfDictionary {

		// This is both a an Annotation (specifically Widget) dictionary, and a Field dictionary, which have no overlap in entry keys

		private readonly PdfWidgetAnnotationDictionary widgetAnnotationDict;
		private readonly PdfFieldDictionary fieldDict;

		public PdfObject? Value { get { return fieldDict.Value; } set { fieldDict.Value = value; } }

		public PdfAcroFieldDictionary(PdfWidgetAnnotationDictionary widgetAnnotationDict, PdfFieldDictionary fieldDict) {
			this.widgetAnnotationDict = widgetAnnotationDict;
			this.fieldDict = fieldDict;
		}

		public override int Count {
			get {
				return widgetAnnotationDict.Count + fieldDict.Count;
			}
		}

		public override IEnumerator<KeyValuePair<PdfName, PdfObject>> GetEnumerator() {
			foreach(KeyValuePair<PdfName, PdfObject> widgetAnnotEntry in widgetAnnotationDict) {
				yield return widgetAnnotEntry;
			}
			foreach (KeyValuePair<PdfName, PdfObject> fieldEntry in fieldDict) {
				yield return fieldEntry;
			}
		}

	}

	public class PdfWidgetAnnotationDictionary : AbstractPdfDictionary {

		private readonly PdfPage page;
		private readonly PdfRectangle rect;
		private readonly PdfAnnotationFlags flags;
		private readonly AbstractPdfDictionary appearanceDict;
		private readonly PdfName? appearanceState;
		private readonly PdfAppearanceCharacteristicsDictionary? appearanceCharacteristics;
		private readonly PdfAction? action;
		private readonly PdfAnnotationBorderStyleDictionary? borderStyle;

		public PdfWidgetAnnotationDictionary(PdfPage page, PdfRectangle rect, PdfAnnotationFlags flags, AbstractPdfDictionary appearanceDict, PdfName? appearanceState, PdfAppearanceCharacteristicsDictionary? appearanceCharacteristics, PdfAction? action, PdfAnnotationBorderStyleDictionary? borderStyle) {
			this.page = page;
			this.rect = rect;
			this.flags = flags;
			this.appearanceDict = appearanceDict;
			this.appearanceState = appearanceState;
			this.appearanceCharacteristics = appearanceCharacteristics;
			this.action = action;
			this.borderStyle = borderStyle;
		}

		public override int Count {
			get {
				int count = 5;
				if (appearanceState != null) { count += 1; }
				if (appearanceCharacteristics != null && appearanceCharacteristics.Count > 0) { count += 1; }
				if (action != null) { count += 1; }
				if (borderStyle != null) { count += 1; }
				return count;
			}
		}

		public override IEnumerator<KeyValuePair<PdfName, PdfObject>> GetEnumerator() {
			// Type not needed here (/Annot if present)
			yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.Subtype, PdfNames.Widget);
			yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.Rect, rect);
			// Contents? Helps with accessibility?
			yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.FieldPage, page.Reference);
			// Annotation name?
			yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.AnnotationFlags, new PdfInt((int)flags));
			yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.AppearanceDictionary, appearanceDict);
			if (appearanceState != null) {
				yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.AppearanceState, appearanceState);
			}
			if(appearanceCharacteristics != null && appearanceCharacteristics.Count > 0) {
				yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.AppearanceCharacteristics, appearanceCharacteristics);
			}
			if (action != null) {
				yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.ActivateAction, action);
			}
			if (borderStyle != null) {
				yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.BorderStyle, borderStyle);
			}
		}

	}

	public enum PdfWidgetRotation { R0 = 0, R90 = 1, R180 = 2, R270 = 3 }
	public static class WidgetRotationUtils {
		public static PdfInt GetRotation(this PdfWidgetRotation rotation) {
			return new PdfInt(((int)rotation) * 90);
		}
	}

	public class PdfAppearanceCharacteristicsDictionary : AbstractPdfDictionary {

		// TODO There are many other possible values for this dictionary

		public PdfWidgetRotation Rotation { get; set; }
		public string? NormalCaption { get; set; }
		public PdfPushButtonCaptionPosition CaptionPosition { get; set; }

		public PdfAppearanceCharacteristicsDictionary(
				PdfWidgetRotation rotation = PdfWidgetRotation.R0,
				string? normalCaption = null,
				PdfPushButtonCaptionPosition captionPosition = PdfPushButtonCaptionPosition.NoIconCaptionOnly
			) {
			
			this.Rotation = rotation;
			this.NormalCaption = normalCaption;
			this.CaptionPosition = captionPosition;
		}

		public override int Count {
			get {
				int count = 0;
				if (Rotation != PdfWidgetRotation.R0) { count += 1; }
				if (NormalCaption != null) { count += 1; }
				if (CaptionPosition != PdfPushButtonCaptionPosition.NoIconCaptionOnly) { count += 1; }
				return count;
			}
		}

		public override IEnumerator<KeyValuePair<PdfName, PdfObject>> GetEnumerator() {
			if (Rotation != PdfWidgetRotation.R0) {
				yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.WidgetRotation, Rotation.GetRotation());
			}
			if (NormalCaption != null) {
				yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.NormalCaption, new PdfTextString(NormalCaption));
			}
			if (CaptionPosition != PdfPushButtonCaptionPosition.NoIconCaptionOnly) {
				yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.NormalCaptionPosition, new PdfInt((int)CaptionPosition));
			}
		}
	}

	public class PdfAnnotationBorderStyleDictionary : AbstractPdfDictionary {

		private readonly float width;
		private readonly PdfBorderStyle style;
		private readonly float[]? dashArray;

		public PdfAnnotationBorderStyleDictionary(float width = 1f, PdfBorderStyle style = PdfBorderStyle.Solid, float[]? dashArray = null) {
			this.width = width;
			this.style = style;
			this.dashArray = dashArray;
		}

		public override int Count {
			get {
				int count = 0;
				if (width != 1f) { count += 1; }
				if (style != PdfBorderStyle.Solid) { count += 1; }
				if (dashArray != null && dashArray.Length > 0 && !(dashArray.Length == 1 && dashArray[0] == 3f)) { count += 1; }
				return count;
			}
		}

		public override IEnumerator<KeyValuePair<PdfName, PdfObject>> GetEnumerator() {
			if (width != 1f) {
				yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.BorderWidth, new PdfFloat(width));
			}
			if (style != PdfBorderStyle.Solid) {
				yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.BorderLineStyle, style.GetName());
			}
			if (dashArray != null && dashArray.Length > 0 && !(dashArray.Length == 1 && dashArray[0] == 3f)) {
				yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.BorderLineStyle, style.GetName());
			}
		}
	}

	public enum PdfBorderStyle : int {
		Solid = 0, Dashed = 1, Beveled = 2, Inset = 3, Underline = 4
	}
	public static class PdfBorderStyleUtils {
		private static readonly PdfName[] styleNames = new PdfName[] { new PdfName("S"), new PdfName("D"), new PdfName("B"), new PdfName("I"), new PdfName("U") };

		public static PdfName GetName(this PdfBorderStyle style) {
			return styleNames[(int)style];
		}
	}

	public abstract class PdfFieldDictionary : AbstractPdfDictionary {

		private readonly PdfFieldType fieldType;
		
		private readonly string partialFieldName;
		private readonly string? alternateFieldName;

		private readonly int fieldFlags;

		protected PdfObject? value;
		private readonly PdfObject? defaultValue;

		public abstract PdfObject? Value { get; set; }

		protected PdfFieldDictionary(PdfFieldType fieldType, string partialFieldName, string? alternateFieldName, int fieldFlags, PdfObject? value, PdfObject? defaultValue) {
			this.fieldType = fieldType;
			this.partialFieldName = partialFieldName;
			this.alternateFieldName = alternateFieldName;
			this.fieldFlags = fieldFlags;
			this.value = value;
			this.defaultValue = defaultValue;
		}

		public override int Count {
			get {
				int count = 3 + FieldEntryCount;
				if (!string.IsNullOrEmpty(alternateFieldName)) {
					count += 1;
				}
				if (value != null) {
					count += 1;
				}
				if (defaultValue != null) {
					count += 1;
				}
				return count;
			}
		}

		public override IEnumerator<KeyValuePair<PdfName, PdfObject>> GetEnumerator() {
			yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.FieldType, fieldType.GetFieldName());

			// We are only using flat field structure, so no Parent entry
			// Kids not needed, as we're using a flat field tree

			yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.PartialFieldName, new PdfTextString(partialFieldName));

			if (!string.IsNullOrEmpty(alternateFieldName)) {
				yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.AlternateFieldName, new PdfTextString(alternateFieldName));
			}

			// Mapping name (TM)?

			yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.FieldFlags, new PdfInt(fieldFlags));

			if (value != null) {
				yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.FieldValue, value);
			}
			if (defaultValue != null) {
				yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.FieldDefaultValue, defaultValue);
			}

			foreach(KeyValuePair<PdfName, PdfObject> fieldEntry in GetFieldEntries()) {
				yield return fieldEntry;
			}
		}

		protected abstract int FieldEntryCount { get; }
		protected abstract IEnumerable<KeyValuePair<PdfName, PdfObject>> GetFieldEntries();

	}

	public class PdfTextFieldDictionary : PdfFieldDictionary {

		private readonly PdfString defaultAppearanceString;
		private readonly PdfVariableTextQuadding quadding;
		private readonly int? maxLen;

		public override PdfObject? Value {
			get {
				return value;
			}
			set {
				if(value is PdfString) {
					this.value = value;
				}
				else {
					throw new ArgumentException("Text fields must have PdfStrings for values.");
				}
			}
		}

		public PdfTextFieldDictionary(string partialFieldName, string? alternateFieldName,
			PdfFieldFlags fieldFlags, PdfTextFieldFlags textFieldFlags,
			PdfString defaultAppearanceString, PdfVariableTextQuadding quadding, int? maxLen,
			PdfString? value, PdfString? defaultValue)
			: base(PdfFieldType.Text, partialFieldName, alternateFieldName, ((int)fieldFlags) | ((int)textFieldFlags), value, defaultValue) {

			this.defaultAppearanceString = defaultAppearanceString;
			this.quadding = quadding;
			this.maxLen = maxLen;
		}

		protected override int FieldEntryCount {
			get {
				int count = 0;
				if (maxLen.HasValue) {
					count += 1;
				}
				return count;
			}
		}

		protected override IEnumerable<KeyValuePair<PdfName, PdfObject>> GetFieldEntries() {
			yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.DefaultAppearance, defaultAppearanceString);
			yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.Quadding, new PdfInt((int)quadding));
			if (maxLen.HasValue) {
				yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.MaxLen, new PdfInt(maxLen.Value));
			}
		}

	}

	public abstract class PdfButtonFieldDictionary : PdfFieldDictionary {

		public PdfButtonFieldDictionary(string partialFieldName, string? alternateFieldName,
			PdfFieldFlags fieldFlags, PdfButtonFieldFlags buttonFieldFlags,
			PdfObject? value, PdfObject? defaultValue)
			: base(PdfFieldType.Button, partialFieldName, alternateFieldName, ((int)fieldFlags) | ((int)buttonFieldFlags), value, defaultValue) { }

	}

	public class PdfCheckBoxFieldDictionary : PdfButtonFieldDictionary {

		private readonly PdfString defaultAppearanceString;

		public override PdfObject? Value {
			get {
				return value;
			}
			set {
				if (value is PdfName nameValue && (PdfNames.Yes.Equals(nameValue) || PdfNames.Off.Equals(nameValue))) {
					this.value = nameValue;
				}
				else {
					throw new ArgumentException("Check box fields must have one of the following PdfNames for values: /Yes, /Off");
				}
			}
		}

		public PdfCheckBoxFieldDictionary(string partialFieldName, string? alternateFieldName,
			PdfFieldFlags fieldFlags,
			bool value, bool? defaultValue, PdfString defaultAppearanceString)
			: base(partialFieldName, alternateFieldName, fieldFlags, PdfButtonFieldFlags.None, StateName(value), StateName(defaultValue)) {

			this.defaultAppearanceString = defaultAppearanceString;
		}

		protected override int FieldEntryCount { get { return defaultAppearanceString != null ? 1 : 0; } } // { get; } = 0;
		protected override IEnumerable<KeyValuePair<PdfName, PdfObject>> GetFieldEntries() {// => Enumerable.Empty<KeyValuePair<PdfName, PdfObject>>();
			if (defaultAppearanceString != null) {
				yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.DefaultAppearance, defaultAppearanceString);
			}
		}

		[return: NotNullIfNotNull(nameof(state))]
		public static PdfName? StateName(bool? state) {
			return state.HasValue ? (state.Value ? PdfNames.Yes : PdfNames.Off) : null;
		}

		public static bool StateValue(PdfName state) {
			if(state is null) { throw new ArgumentNullException(nameof(state)); }
			return !state.Equals(PdfNames.Off); // "Off" is specified by the spec, but "Yes" is seems to be optional?
		}

	}

	public class PdfPushButtonFieldDictionary : PdfButtonFieldDictionary {

		private readonly PdfString? defaultAppearanceString;

		public override PdfObject? Value {
			get {
				return value;
			}
			set {
				throw new ArgumentException("Cannot set the value of a push button.");
			}
		}

		public PdfPushButtonFieldDictionary(string partialFieldName, string? alternateFieldName,
			PdfFieldFlags fieldFlags, PdfString? defaultAppearanceString)
			: base(partialFieldName, alternateFieldName, fieldFlags, PdfButtonFieldFlags.Pushbutton, null, null) {

			this.defaultAppearanceString = defaultAppearanceString;
		}

		protected override int FieldEntryCount { get { return defaultAppearanceString != null ? 1 : 0; } } // { get; } = 0;
		protected override IEnumerable<KeyValuePair<PdfName, PdfObject>> GetFieldEntries() {
			if (defaultAppearanceString != null) {
				yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.DefaultAppearance, defaultAppearanceString);
			}
		}
	}

	public enum PdfPushButtonCaptionPosition : int {
		NoIconCaptionOnly = 0,
		NoCaptionIconOnly = 1,
		CaptionBelowIcon = 2,
		CaptionAboveIcon = 3,
		CaptionRightOfIcon = 4,
		CaptionLeftOfIcon = 5,
		CaptionOverlaid = 6
	}

}