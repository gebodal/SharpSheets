using SharpSheets.Documentation;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

namespace SharpSheets.Markup.Parsing {

	public class MarkupEnumType : Type {

		public override string Name { get; }

		public override Guid GUID { get; }

		public override bool IsEnum { get; } = true;

		public override Type BaseType => typeof(Enum);
		public override Type UnderlyingSystemType => typeof(int);

		private readonly string[] enumNames;
		private readonly int[] enumValues;
		private readonly Dictionary<string, string> enumNamesSet;

		public EnumDoc Documentation { get; }

		public MarkupEnumType(string name, string? description, IEnumerable<EnumValDoc> enumValues) {
			this.Name = name;
			this.GUID = default;

			this.Documentation = new EnumDoc(this.Name, enumValues.ToArray(), !string.IsNullOrWhiteSpace(description) ? new DocumentationString(description) : null);

			this.enumNames = Documentation.values.Select(v => v.name).ToArray();
			this.enumNamesSet = new Dictionary<string, string>(this.enumNames.ToDictionary(s => s), StringComparer.InvariantCultureIgnoreCase);

			this.enumValues = new int[this.enumNames.Length];
			for(int i=0; i<this.enumValues.Length; i++) {
				this.enumValues[i] = i;
			}
		}

		public override bool IsEnumDefined(object value) {
			if(value is int index) {
				return index >= 0 && index < enumNames.Length;
			}
			else if(value is string name) {
				return enumNamesSet.ContainsKey(name);
			}
			else {
				return false;
			}
		}

		public override string? GetEnumName(object value) {
			if (value is int index && index >= 0 && index < enumNames.Length) {
				return enumNames[index];
			}
			else if (value is string name && enumNamesSet.TryGetValue(name, out string? enumName)) {
				return enumName;
			}
			else {
				return null;
			}
		}

		public override string[] GetEnumNames() {
			return enumNames;
		}

		public override Type GetEnumUnderlyingType() {
			return typeof(int);
		}

		public override Array GetEnumValues() {
			//throw new NotImplementedException("Cannot get enum values for custom enum type.");
			return enumValues;
		}

		public override Type MakeArrayType() {
			return new CustomArrayType(this);
		}

		public override Type MakeArrayType(int rank) {
			if(rank != 1) {
				throw new NotSupportedException("MarkupEnumType arrays may only ever be rank 1.");
			}
			return MakeArrayType();
		}

		#region Necessary Abstract Implementation

		private static readonly Type typeBasis = typeof(MarkupEnumType);

		public override Module Module => typeBasis.Module;
		public override Assembly Assembly => typeBasis.Assembly;
		public override string? Namespace => typeBasis.Namespace;
		public override string AssemblyQualifiedName => FullName + ", " + Assembly.FullName;
		public override string FullName => Namespace + "." + Name;

		public override ConstructorInfo[] GetConstructors(BindingFlags bindingAttr) { return Array.Empty<ConstructorInfo>(); }
		public override object[] GetCustomAttributes(bool inherit) { return Array.Empty<object>(); }
		public override object[] GetCustomAttributes(Type attributeType, bool inherit) { return Array.Empty<object>(); }
		public override Type? GetElementType() { return null; }
		public override EventInfo? GetEvent(string name, BindingFlags bindingAttr) { return null; }
		public override EventInfo[] GetEvents(BindingFlags bindingAttr) { return Array.Empty<EventInfo>(); }
		public override FieldInfo? GetField(string name, BindingFlags bindingAttr) { return null; }
		public override FieldInfo[] GetFields(BindingFlags bindingAttr) { return Array.Empty<FieldInfo>(); }
		public override Type? GetInterface(string name, bool ignoreCase) { return null; }
		public override Type[] GetInterfaces() { return Array.Empty<Type>(); }
		public override MemberInfo[] GetMembers(BindingFlags bindingAttr) { return Array.Empty<MemberInfo>(); }
		public override MethodInfo[] GetMethods(BindingFlags bindingAttr) { return Array.Empty<MethodInfo>(); }
		public override Type? GetNestedType(string name, BindingFlags bindingAttr) { return null; }
		public override Type[] GetNestedTypes(BindingFlags bindingAttr) { return Array.Empty<Type>(); }
		public override PropertyInfo[] GetProperties(BindingFlags bindingAttr) { return Array.Empty<PropertyInfo>(); }
		public override object InvokeMember(string name, BindingFlags invokeAttr, Binder? binder, object? target, object?[]? args, ParameterModifier[]? modifiers, CultureInfo? culture, string[]? namedParameters) {
			throw new MissingMethodException();
		}
		public override bool IsDefined(Type attributeType, bool inherit) => false;
		protected override TypeAttributes GetAttributeFlagsImpl() { return default; }
		protected override ConstructorInfo? GetConstructorImpl(BindingFlags bindingAttr, Binder? binder, CallingConventions callConvention, Type[] types, ParameterModifier[]? modifiers) {
			return null;
		}
		protected override MethodInfo? GetMethodImpl(string name, BindingFlags bindingAttr, Binder? binder, CallingConventions callConvention, Type[]? types, ParameterModifier[]? modifiers) {
			return null;
		}
		protected override PropertyInfo? GetPropertyImpl(string name, BindingFlags bindingAttr, Binder? binder, Type? returnType, Type[]? types, ParameterModifier[]? modifiers) {
			return null;
		}
		protected override bool HasElementTypeImpl() => false;
		protected override bool IsArrayImpl() => false;
		protected override bool IsByRefImpl() => false;
		protected override bool IsCOMObjectImpl() => false;
		protected override bool IsPointerImpl() => true; // ???
		protected override bool IsPrimitiveImpl() => false;

		public override Type[] GetGenericArguments() {
			return Array.Empty<Type>();
		}

		#endregion
	}

	public class CustomArrayType : Type {

		public override string Name => elementType.Name + "[]";

		public override Guid GUID { get; }

		public override Type BaseType => typeof(Array);
		public override Type UnderlyingSystemType => typeof(Array); // Is this right?

		private readonly Type elementType;

		public CustomArrayType(Type elementType) {
			this.GUID = default;

			this.elementType = elementType;
		}

		public override Type GetElementType() { return elementType; }
		public override int GetArrayRank() { return 1; }

		protected override bool HasElementTypeImpl() => true;
		protected override bool IsArrayImpl() => true;
		protected override bool IsByRefImpl() => true;

		public override Type MakeArrayType() {
			return new CustomArrayType(this);
		}

		public override Type MakeArrayType(int rank) {
			if (rank != 1) {
				throw new NotSupportedException("MarkipEnumType arrays may only ever be rank 1.");
			}
			return MakeArrayType();
		}

		#region Necessary Abstract Implementation

		private static readonly Type typeBasis = typeof(CustomArrayType);

		public override Module Module => typeBasis.Module;
		public override Assembly Assembly => typeBasis.Assembly;
		public override string? Namespace => typeBasis.Namespace;
		public override string AssemblyQualifiedName => FullName + ", " + Assembly.FullName;
		public override string FullName => Namespace + "." + Name;

		public override ConstructorInfo[] GetConstructors(BindingFlags bindingAttr) { return Array.Empty<ConstructorInfo>(); }
		public override object[] GetCustomAttributes(bool inherit) { return Array.Empty<object>(); }
		public override object[] GetCustomAttributes(Type attributeType, bool inherit) { return Array.Empty<object>(); }
		public override EventInfo? GetEvent(string name, BindingFlags bindingAttr) { return null; }
		public override EventInfo[] GetEvents(BindingFlags bindingAttr) { return Array.Empty<EventInfo>(); }
		public override FieldInfo? GetField(string name, BindingFlags bindingAttr) { return null; }
		public override FieldInfo[] GetFields(BindingFlags bindingAttr) { return Array.Empty<FieldInfo>(); }
		public override Type? GetInterface(string name, bool ignoreCase) { return null; }
		public override Type[] GetInterfaces() { return Array.Empty<Type>(); }
		public override MemberInfo[] GetMembers(BindingFlags bindingAttr) { return Array.Empty<MemberInfo>(); }
		public override MethodInfo[] GetMethods(BindingFlags bindingAttr) { return Array.Empty<MethodInfo>(); }
		public override Type? GetNestedType(string name, BindingFlags bindingAttr) { return null; }
		public override Type[] GetNestedTypes(BindingFlags bindingAttr) { return Array.Empty<Type>(); }
		public override PropertyInfo[] GetProperties(BindingFlags bindingAttr) { return Array.Empty<PropertyInfo>(); }
		public override object? InvokeMember(string name, BindingFlags invokeAttr, Binder? binder, object? target, object?[]? args, ParameterModifier[]? modifiers, CultureInfo? culture, string[]? namedParameters) {
			throw new MissingMethodException();
		}
		public override bool IsDefined(Type attributeType, bool inherit) => false;
		protected override TypeAttributes GetAttributeFlagsImpl() { return default; }
		protected override ConstructorInfo? GetConstructorImpl(BindingFlags bindingAttr, Binder? binder, CallingConventions callConvention, Type[] types, ParameterModifier[]? modifiers) {
			return null;
		}
		protected override MethodInfo? GetMethodImpl(string name, BindingFlags bindingAttr, Binder? binder, CallingConventions callConvention, Type[]? types, ParameterModifier[]? modifiers) {
			return null;
		}
		protected override PropertyInfo? GetPropertyImpl(string name, BindingFlags bindingAttr, Binder? binder, Type? returnType, Type[]? types, ParameterModifier[]? modifiers) {
			return null;
		}
		protected override bool IsCOMObjectImpl() => false;
		protected override bool IsPointerImpl() => true; // ???
		protected override bool IsPrimitiveImpl() => false;

		public override bool IsEnum => false;
		public override bool IsEnumDefined(object value) => false;
		public override string? GetEnumName(object value) => null;
		public override string[] GetEnumNames() => throw new ArgumentException(Name + " is not an enumeration type.");
		public override Type GetEnumUnderlyingType() => throw new ArgumentException(Name + " is not an enumeration type.");
		public override Array GetEnumValues() => throw new ArgumentException(Name + " is not an enumeration type.");

		public override Type[] GetGenericArguments() {
			return Array.Empty<Type>();
		}

		#endregion
	}

}
