using System.Collections.Generic;
using System;
using System.Linq;
using SharpSheets.Layouts;
using SharpSheets.Colors;
using SharpSheets.Shapes;
using System.Reflection;
using System.Collections;
using System.Text.RegularExpressions;
using SharpSheets.Utilities;
using SharpSheets.Fonts;
using SharpSheets.Exceptions;
using SixLabors.ImageSharp;
using System.Diagnostics.CodeAnalysis;

namespace SharpSheets.Parsing {

	[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
	public class FactoryBuilderAttribute : Attribute {

		public string? Name { get; set; } = null;

		public Type BuildType { get; }

		public FactoryBuilderAttribute(Type buildType) {
			this.BuildType = buildType;
		}

		public static Type GetBuilderType(MethodInfo method) {
			if (Nullable.GetUnderlyingType(method.ReturnType) is Type underlying) {
				return underlying;
			}
			else {
				return method.ReturnType;
			}
		}

	}

	[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
	public class GroupedArgumentBuilderAttribute : Attribute { }

	[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
	public class SupplementedArgumentBuilderAttribute : Attribute { }

	[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
	public class ExpandedArgumentBuilderAttribute : Attribute {
		public bool Defer { get; set; } = false;
		public string? PrefixSep { get; set; } = null;
	}

	[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
	public class PropertyAttribute : Attribute {

		public bool Local { get; }
		public bool Exclude { get; set; } = false;
		public string? Default { get; set; } = null;
		public string? Example { get; set; } = null;

		protected PropertyAttribute(bool local) {
			Local = local;
		}
		
		public PropertyAttribute() : this(false) { }

	}

	[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
	public class LocalPropertyAttribute : PropertyAttribute {

		public LocalPropertyAttribute() : base(true) { }

	}

	[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
	public class BuildErrorsAttribute : Attribute { }

	[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
	public class SourceDirectoryAttribute : Attribute { }

	[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
	public class ParameterParserAttribute : Attribute { }

	[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true, Inherited = false)]
	public class GenerateParameterParserAttribute : Attribute {
		public Type ParserType { get; }

		public GenerateParameterParserAttribute(Type parserType) {
			ParserType = parserType;
		}
	}

	[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
	public class FactoryAttribute : Attribute {

		public Type FactoryType { get; }
		public Type[] RequiredParamaters { get; }
		public string[] RequiredParamaterNames { get; }
		public bool[] ExcludeRequiredParamaters { get; }
		public Type? Default { get; }

		public bool IncludeDocs { get; set; } = true;

		public FactoryAttribute(Type factoryType, Type[] requiredParams, string[] requiredParamaterNames, bool[] excludeRequiredParamaters, Type? @default) {
			this.FactoryType = factoryType;
			this.RequiredParamaters = requiredParams;
			this.RequiredParamaterNames = requiredParamaterNames;
			this.ExcludeRequiredParamaters = excludeRequiredParamaters;
			Default = @default;
		}

	}

}