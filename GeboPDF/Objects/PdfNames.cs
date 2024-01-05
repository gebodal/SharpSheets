namespace GeboPdf.Objects {
	public static class PdfNames {

		#region Document Structure
		public static readonly PdfName Type = new PdfName("Type");
		public static readonly PdfName Subtype = new PdfName("Subtype");
		public static readonly PdfName Parent = new PdfName("Parent");
		public static readonly PdfName Kids = new PdfName("Kids");
		public static readonly PdfName Root = new PdfName("Root");
		public static readonly PdfName Catalogue = new PdfName("Catalogue");
		public static readonly PdfName Count = new PdfName("Count");
		public static readonly PdfName Pages = new PdfName("Pages");
		public static readonly PdfName Page = new PdfName("Page");
		public static readonly PdfName AcroForm = new PdfName("AcroForm");
		#region Marked Info
		public static readonly PdfName MarkInfo = new PdfName("MarkInfo");
		public static readonly PdfName Marked = new PdfName("Marked");
		public static readonly PdfName UserProperties = new PdfName("UserProperties");
		public static readonly PdfName Suspects = new PdfName("Suspects");
		#endregion
		public static readonly PdfName MediaBox = new PdfName("MediaBox");
		public static readonly PdfName CropBox = new PdfName("CropBox");
		public static readonly PdfName TrimBox = new PdfName("TrimBox");
		public static readonly PdfName Resources = new PdfName("Resources");
		public static readonly PdfName Contents = new PdfName("Contents");
		public static readonly PdfName Annots = new PdfName("Annots");
		public static readonly PdfName Size = new PdfName("Size");
		public static readonly PdfName ExtGState = new PdfName("ExtGState");
		#endregion Document Structure

		#region Cross Reference Streams
		/// <summary> Field sizes ("W") name for Cross Reference Stream dictionary. </summary>
		public static readonly PdfName FieldSizes = new PdfName("W");
		public static readonly PdfName Index = new PdfName("Index");
		/// <summary> Previous ("Prev") name for xref trailer and Cross Reference Stream dictionaries. </summary>
		public static readonly PdfName Previous = new PdfName("Prev");
		#endregion

		#region Object Streams
		/// <summary> Number of Objects ("N") name for Object Stream dictionary. </summary>
		public static readonly PdfName NumObjects = new PdfName("N");
		public static readonly PdfName First = new PdfName("First");
		#endregion

		#region Resources
		//public static readonly PdfName ColorSpace = new PdfName("ColorSpace");
		public static readonly PdfName Pattern = new PdfName("Pattern");
		#endregion

		#region AcroForms

		#region Interactive Form Dictionary
		public static readonly PdfName Fields = new PdfName("Fields");
		public static readonly PdfName NeedAppearances = new PdfName("NeedAppearances");
		/// <summary> Calculation Order ("CO") name for AcroForms dictionary. </summary>
		public static readonly PdfName CalculationOrder = new PdfName("CO");
		/// <summary> Default Resources ("DR") name for AcroForms dictionary. </summary>
		public static readonly PdfName DefaultResources = new PdfName("DR");
		#endregion Interactive Form Dictionary

		#region Widget Annotation Dictionary
		public static readonly PdfName Widget = new PdfName("Widget");
		public static readonly PdfName Rect = new PdfName("Rect");
		/// <summary> Field Page ("P") name for Widget Annotation dictionary. </summary>
		public static readonly PdfName FieldPage = new PdfName("P");
		/// <summary> Annotation Flags ("F") name for Widget Annotation dictionary. </summary>
		public static readonly PdfName AnnotationFlags = new PdfName("F");
		/// <summary> Appearance Dictionary ("AP") name for Widget Annotation dictionary. </summary>
		public static readonly PdfName AppearanceDictionary = new PdfName("AP");
		#region AppearanceDictionary
		/// <summary> Normal Appearance ("N") name for Annotation Appearance dictionary. </summary>
		public static readonly PdfName NormalAppearance = new PdfName("N");
		/// <summary> Rollover Appearance ("R") name for Annotation Appearance dictionary. </summary>
		public static readonly PdfName RolloverAppearance = new PdfName("R");
		/// <summary> Down Appearance ("D") name for Annotation Appearance dictionary. </summary>
		public static readonly PdfName DownAppearance = new PdfName("D");
		#endregion
		/// <summary> Appearance State ("AS") name for Widget Annotation dictionary. </summary>
		public static readonly PdfName AppearanceState = new PdfName("AS");
		/// <summary> Appearance Characteristics ("MK") name for Widget Annotation dictionary. </summary>
		public static readonly PdfName AppearanceCharacteristics = new PdfName("MK");
		/// <summary> Widget rotation ("R") name for Appearance Characteristics dictionary. </summary>
		public static readonly PdfName WidgetRotation = new PdfName("R");
		/// <summary> Normal Caption ("CA") name for Appearance Characteristics dictionary. </summary>
		public static readonly PdfName NormalCaption = new PdfName("CA");
		/// <summary> Normal Caption Position ("TP") name for Appearance Characteristics dictionary. </summary>
		public static readonly PdfName NormalCaptionPosition = new PdfName("TP");
		/// <summary> Activate Action ("A") name for Appearance Widget Annotation dictionary. </summary>
		public static readonly PdfName ActivateAction = new PdfName("A");
		/// <summary> Action ("A") name for Appearance Widget Annotation dictionary. </summary>
		public static readonly PdfName BorderStyle = new PdfName("BS");
		#region BorderStyle
		/// <summary> Border Width ("W") name for Appearance Border Style dictionary. </summary>
		public static readonly PdfName BorderWidth = new PdfName("W");
		/// <summary> Border Line Style ("S") name for Appearance Border Style dictionary. </summary>
		public static readonly PdfName BorderLineStyle = new PdfName("S");
		/// <summary> Border Dash Array ("D") name for Appearance Border Style dictionary. </summary>
		public static readonly PdfName BorderDashArray = new PdfName("D");
		#endregion
		#endregion Widget Annotation Dictionary

		#region Actions
		public static readonly PdfName Action = new PdfName("Action");
		/// <summary> Action Type ("S") name for Action dictionary. </summary>
		public static readonly PdfName ActionType = new PdfName("S");
		public static readonly PdfName Next = new PdfName("Next");
		public static readonly PdfName JavaScript = new PdfName("JavaScript");
		/// <summary> Java Script Code ("JS") name for JavaScript Action dictionary. </summary>
		public static readonly PdfName JavaScriptCode = new PdfName("JS");
		#endregion

		#region Field Dictionary

		/// <summary> Field type ("FT") name for AcroForms field dictionary. </summary>
		public static readonly PdfName FieldType = new PdfName("FT");
		#region Field Types
		/// <summary> Button field type ("Btn") name for AcroForms field dictionary. </summary>
		public static readonly PdfName ButtonField = new PdfName("Btn");
		/// <summary> Text field type ("Tx") name for AcroForms field dictionary. </summary>
		public static readonly PdfName TextField = new PdfName("Tx");
		#endregion

		/// <summary> Partial Field Name ("T") name for AcroForms field dictionary. </summary>
		public static readonly PdfName PartialFieldName = new PdfName("T");
		/// <summary> Alternate Field Name ("TU") name for AcroForms field dictionary. </summary>
		public static readonly PdfName AlternateFieldName = new PdfName("TU");
		/// <summary> Field Flags ("Ff") name for AcroForms field dictionary. </summary>
		public static readonly PdfName FieldFlags = new PdfName("Ff");
		/// <summary> Field Value ("V") name for AcroForms field dictionary. </summary>
		public static readonly PdfName FieldValue = new PdfName("V");
		/// <summary> Default Field Value ("DV") name for AcroForms field dictionary. </summary>
		public static readonly PdfName FieldDefaultValue = new PdfName("DV");
		/// <summary> Additional Actions ("AA") name for AcroForms field dictionary. </summary>
		public static readonly PdfName AdditionalActions = new PdfName("AA");

		#region Variable Length Text
		/// <summary> Default Appearance ("DA") name for AcroForms variable text dictionary. </summary>
		public static readonly PdfName DefaultAppearance = new PdfName("DA");
		/// <summary> Quadding ("Q") name for AcroForms variable text dictionary. </summary>
		public static readonly PdfName Quadding = new PdfName("Q");
		#endregion

		#region Text Fields
		public static readonly PdfName MaxLen = new PdfName("MaxLen");
		#endregion

		#region CheckBox Fields
		public static readonly PdfName Yes = new PdfName("Yes");
		public static readonly PdfName Off = new PdfName("Off");
		#endregion

		#endregion Field Dictionary

		#endregion AcroForms

		#region Structure Hierarchy
		public static readonly PdfName StructTreeRoot = new PdfName("StructTreeRoot");
		public static readonly PdfName StructElem = new PdfName("StructElem");
		public static readonly PdfName ParentTree = new PdfName("ParentTree");
		/// <summary> Kids ("K") name for structure tree dictionaries. </summary>
		public static readonly PdfName StructTreeKids = new PdfName("K");
		/// <summary> Parent ("P") name for structure tree dictionaries. </summary>
		public static readonly PdfName StructTreeParent = new PdfName("P");
		/// <summary> Page ("Pg") name for structure element dictionary. </summary>
		public static readonly PdfName StructTreePage = new PdfName("Pg");
		/// <summary> Structure Type ("S") name for structure element dictionary. </summary>
		public static readonly PdfName StructureType = new PdfName("S");
		/// <summary> Marked Content Reference dictionary type ("MCR") name for structure element dictionaries. </summary>
		public static readonly PdfName MarkedContentReference = new PdfName("MCR");
		public static readonly PdfName MarkedContentIdentifier = new PdfName("MCR");
		#endregion Structure Heirarchy

		#region Streams
		public static readonly PdfName Length = new PdfName("Length");
		public static readonly PdfName Filter = new PdfName("Filter");
		public static readonly PdfName DecodeParms = new PdfName("DecodeParms");
		public static readonly PdfName FlateDecode = new PdfName("FlateDecode");
		public static readonly PdfName Predictor = new PdfName("Predictor");
		public static readonly PdfName Columns = new PdfName("Columns");
		#endregion

		#region XObjects
		public static readonly PdfName XObject = new PdfName("XObject");
		public static readonly PdfName Image = new PdfName("Image");
		public static readonly PdfName Width = new PdfName("Width");
		public static readonly PdfName Height = new PdfName("Height");
		public static readonly PdfName BitsPerComponent = new PdfName("BitsPerComponent");
		public static readonly PdfName Interpolate = new PdfName("Interpolate");
		public static readonly PdfName Form = new PdfName("Form");
		public static readonly PdfName BBox = new PdfName("BBox");
		public static readonly PdfName Matrix = new PdfName("Matrix");
		#endregion

		#region Fonts

		public static readonly PdfName Font = new PdfName("Font");
		public static readonly PdfName BaseFont = new PdfName("BaseFont");
		public static readonly PdfName Type0 = new PdfName("Type0");
		public static readonly PdfName Type1 = new PdfName("Type1");
		public static readonly PdfName Encoding = new PdfName("Encoding");
		public static readonly PdfName DescendantFonts = new PdfName("DescendantFonts");
		public static readonly PdfName ToUnicode = new PdfName("ToUnicode");
		public static readonly PdfName CMap = new PdfName("CMap");
		public static readonly PdfName CMapName = new PdfName("CMapName");

		public static readonly PdfName CIDFontType2 = new PdfName("CIDFontType2");
		public static readonly PdfName FontDescriptor = new PdfName("FontDescriptor");
		/// <summary> Default Width ("DW") name for font dictionary. </summary>
		public static readonly PdfName DefaultWidth = new PdfName("DW");
		/// <summary> Widths ("D") name for font dictionary. </summary>
		public static readonly PdfName Widths = new PdfName("W");

		public static readonly PdfName CIDSystemInfo = new PdfName("CIDSystemInfo");
		public static readonly PdfName Registry = new PdfName("Registry");
		public static readonly PdfName Ordering = new PdfName("Ordering");
		public static readonly PdfName Supplement = new PdfName("Supplement");
		
		public static readonly PdfName FontName = new PdfName("FontName");
		public static readonly PdfName Flags = new PdfName("Flags");
		public static readonly PdfName FontBBox = new PdfName("FontBBox");
		public static readonly PdfName ItalicAngle = new PdfName("ItalicAngle");
		public static readonly PdfName Ascent = new PdfName("Ascent");
		public static readonly PdfName Descent = new PdfName("Descent");
		public static readonly PdfName CapHeight = new PdfName("CapHeight");
		public static readonly PdfName StemV = new PdfName("StemV");
		public static readonly PdfName FontFile2 = new PdfName("FontFile2");

		public static readonly PdfName Length1 = new PdfName("Length1");
		public static readonly PdfName OpenType = new PdfName("OpenType");

		#endregion Fonts

		#region Metadata
		public static readonly PdfName Info = new PdfName("Info");
		public static readonly PdfName Producer = new PdfName("Producer");
		public static readonly PdfName Creator = new PdfName("Creator");
		public static readonly PdfName Title = new PdfName("Title");
		public static readonly PdfName Author = new PdfName("Author");
		public static readonly PdfName Subject = new PdfName("Subject");
		public static readonly PdfName Keywords = new PdfName("Keywords");
		public static readonly PdfName CreationDate = new PdfName("CreationDate");
		public static readonly PdfName ModDate = new PdfName("ModDate");
		public static readonly PdfName ID = new PdfName("ID");
		#endregion Metadata

		#region Functions
		public static readonly PdfName FunctionType = new PdfName("FunctionType");
		public static readonly PdfName Domain = new PdfName("Domain");
		public static readonly PdfName Range = new PdfName("Range");

		public static readonly PdfName Encode = new PdfName("Encode");

		#region Exponential Interpolation Functions
		/// <summary> Values at 0 ("C0") name for exponential interpolation function dictionary. </summary>
		public static readonly PdfName ValuesAt0 = new PdfName("C0");
		/// <summary> Values at 1 ("C1") name for exponential interpolation function dictionary. </summary>
		public static readonly PdfName ValuesAt1 = new PdfName("C1");
		/// <summary> Interpolation Exponent ("N") name for exponential interpolation function dictionary. </summary>
		public static readonly PdfName InterpolationExponent = new PdfName("N");
		#endregion
		#region Stitching Functions
		public static readonly PdfName Functions = new PdfName("Functions");
		public static readonly PdfName Bounds = new PdfName("Bounds");
		#endregion
		#endregion Functions

		#region Graphics State Parameters
		/// <summary> Line Width ("LW") name for graphics state parameters dictionary. </summary>
		public static readonly PdfName LineWidth = new PdfName("LW");
		/// <summary> Line Cap Style ("LC") name for graphics state parameters dictionary. </summary>
		public static readonly PdfName LineCapStyle = new PdfName("LC");
		/// <summary> Line Join Style ("LJ") name for graphics state parameters dictionary. </summary>
		public static readonly PdfName LineJoinStyle = new PdfName("LJ");
		/// <summary> Miter Limit ("ML") name for graphics state parameters dictionary. </summary>
		public static readonly PdfName MiterLimit = new PdfName("ML");
		/// <summary> Line Dash Pattern ("D") name for graphics state parameters dictionary. </summary>
		public static readonly PdfName LineDashPattern = new PdfName("D");
		/// <summary> Font ("Font") name for graphics state parameters dictionary. </summary>
		public static readonly PdfName GraphicsStateFont = new PdfName("Font");
		/// <summary> Stroking Alpha constant ("CA") name for graphics state parameters dictionary. </summary>
		public static readonly PdfName StrokingAlpha = new PdfName("CA");
		/// <summary> Non-Stroking Alpha constant ("CA") name for graphics state parameters dictionary. </summary>
		public static readonly PdfName NonStrokingAlpha = new PdfName("ca");
		#endregion

		#region Patterns
		public static readonly PdfName PatternType = new PdfName("PatternType");

		#region Tiling Pattern
		public static readonly PdfName PaintType = new PdfName("PaintType");
		public static readonly PdfName TilingType = new PdfName("TilingType");
		public static readonly PdfName XStep = new PdfName("XStep");
		public static readonly PdfName YStep = new PdfName("YStep");
		#endregion

		#region Shading Pattern
		public static readonly PdfName Shading = new PdfName("Shading");
		#region Shading Dictionary
		public static readonly PdfName ShadingType = new PdfName("ShadingType");
		public static readonly PdfName ColorSpace = new PdfName("ColorSpace");
		public static readonly PdfName Background = new PdfName("Background");
		public static readonly PdfName AntiAlias = new PdfName("AntiAlias");

		public static readonly PdfName Coords = new PdfName("Coords");
		public static readonly PdfName Function = new PdfName("Function");
		public static readonly PdfName Extend = new PdfName("Extend");
		#endregion
		#endregion Shading Pattern

		#endregion Patterns
	}
}
