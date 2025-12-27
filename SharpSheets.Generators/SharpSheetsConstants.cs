using System;
using System.Collections.Generic;
using System.Text;

namespace SharpSheets.Generators {

	internal static class SharpSheetsConstants {

		public static readonly string FactoryAttribute = "SharpSheets.Parsing.FactoryAttribute";
		public static readonly string FactoryBuilderAttribute = "SharpSheets.Parsing.FactoryBuilderAttribute";
		public static readonly string ParameterParserAttribute = "SharpSheets.Parsing.ParameterParserAttribute";
		public static readonly string GenerateParameterParserAttribute = "SharpSheets.Parsing.GenerateParameterParserAttribute";
		public static readonly string GroupedArgumentBuilderAttribute = "SharpSheets.Parsing.GroupedArgumentBuilderAttribute";
		public static readonly string SupplementedArgumentBuilderAttribute = "SharpSheets.Parsing.SupplementedArgumentBuilderAttribute";
		public static readonly string ExpandedArgumentBuilderAttribute = "SharpSheets.Parsing.ExpandedArgumentBuilderAttribute";
		public static readonly string PropertyAttribute = "SharpSheets.Parsing.PropertyAttribute";
		public static readonly string LocalPropertyAttribute = "SharpSheets.Parsing.LocalPropertyAttribute";
		public static readonly string LocalPropertyAttribute_Name = "LocalPropertyAttribute";
		public static readonly string BuildErrorsAttribute = "SharpSheets.Parsing.BuildErrorsAttribute";
		public static readonly string SourceDirectoryAttribute = "SharpSheets.Parsing.SourceDirectoryAttribute";

		public static readonly string ParameterParsers = "SharpSheets.Parsing.ParameterParsers";

		public static readonly string UFloat = "SharpSheets.Utilities.UFloat";
		public static readonly string UnitInterval = "SharpSheets.Utilities.UnitInterval";
		public static readonly string Margins = "SharpSheets.Layouts.Margins";
		public static readonly string DirectoryPath = "SharpSheets.Utilities.DirectoryPath";
		public static readonly string ChildHolder = "SharpSheets.Parsing.ChildHolder";

		public static readonly string Dimension = "SharpSheets.Layouts.Dimension";

		public static readonly string Dimension_Automatic = $"{Dimension}.Automatic";
		public static string Dimension_FromPercent(float value) => $"{Dimension}.FromPercent({value}f)";
		public static string Dimension_FromPoints(float value) => $"{Dimension}.FromPoints({value}f)";
		public static string Dimension_FromInches(float value) => $"{Dimension}.FromInches({value}f)";
		public static string Dimension_FromCentimetres(float value) => $"{Dimension}.FromCentimetres({value}f)";
		public static string Dimension_FromMillimetres(float value) => $"{Dimension}.FromMillimetres({value}f)";
		public static string Dimension_FromRelative(float value) => $"{Dimension}.FromRelative({value}f)";

		public static readonly string ShapeFactory = "SharpSheets.Shapes.ShapeFactory";
		public static readonly string ShapeFactory_GetDefaultShape= $"{ShapeFactory}.GetDefaultShape";
		
		public static readonly string IShape = "SharpSheets.Shapes.IShape";
		public static readonly string IAreaShape = "SharpSheets.Shapes.IAreaShape";
		//public static readonly string IContainerShape = "SharpSheets.Shapes.IContainerShape";
		public static readonly string IBox = "SharpSheets.Shapes.IBox";
		public static readonly string ILabelledBox = "SharpSheets.Shapes.ILabelledBox";
		public static readonly string ITitledBox = "SharpSheets.Shapes.ITitledBox";
		public static readonly string ITitleStyle = "SharpSheets.Shapes.ITitleStyle";
		public static readonly string IEntriedShape = "SharpSheets.Shapes.IEntriedShape";
		public static readonly string IBar = "SharpSheets.Shapes.IBar";
		public static readonly string IUsageBar = "SharpSheets.Shapes.IUsageBar";
		public static readonly string IDetail = "SharpSheets.Shapes.IDetail";

		public static readonly string IWidget = "SharpSheets.Widgets.IWidget";
		public static readonly string WidgetSetup = "SharpSheets.Widgets.WidgetSetup";

		public static readonly string Param_Name = "name";


		public static readonly string ArgumentType = "SharpSheets.Documentation.ArgumentType";
		public static readonly string DisplayType = "SharpSheets.Documentation.DisplayType";
		public static readonly string DisplayTypeStructure = "SharpSheets.Documentation.DisplayTypeStructure";

		public static string ArgumentType_Simple(string typeName) {
			return $"{ArgumentType}.Simple<{typeName}>()";
		}

		public static string ArgumentType_Structured(string typeName, string elemType, string structure) {
			return $"new {ArgumentType}({DisplayType_Structured(elemType, structure)}, typeof({typeName}))";
		}

		public static string DisplayType_Simple(string typeName) {
			return $"{DisplayType}.FromSystem<{typeName}>()";
		}

		public static string DisplayType_Structured(string elemType, string structure) {
			return $"{DisplayType}.FromSystem<{elemType}>({DisplayTypeStructure}.{structure})";
		}

	}

}
