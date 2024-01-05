using System;
using System.Globalization;
using System.Reflection;

namespace SharpSheets.Evaluations {

	/*
	public class EvaluationTupleType : Type {

		public override string Name => elementType.Name + "[" + ElementCount + "]";

		public override Guid GUID { get; }

		public override Type BaseType => typeof(Array);
		public override Type UnderlyingSystemType { get; }

		private readonly Type elementType;
		public int ElementCount { get; }

		public EvaluationTupleType(Type elementType, int count) {
			this.GUID = default;

			this.UnderlyingSystemType = elementType.MakeArrayType();

			this.elementType = elementType;
			this.ElementCount = count;
		}

		public override Type GetElementType() { return elementType; }
		public override int GetArrayRank() { return 1; }

		protected override bool HasElementTypeImpl() => true;
		protected override bool IsArrayImpl() => true;
		protected override bool IsByRefImpl() => true;

		public override bool Equals(object o) => base.Equals(o) && o is EvaluationTupleType tuple && tuple.ElementCount == ElementCount;
		public override bool Equals(Type o) => base.Equals(o) && o is EvaluationTupleType tuple && tuple.ElementCount == ElementCount;

		public override int GetHashCode() {
			int hashCode = -796334646;
			hashCode = hashCode * -1521134295 + base.GetHashCode();
			hashCode = hashCode * -1521134295 + ElementCount.GetHashCode();
			return hashCode;
		}

		#region Necessary Abstract Implementation

		private static readonly Type typeBasis = typeof(EvaluationTupleType);

		public override Module Module => typeBasis.Module;
		public override Assembly Assembly => typeBasis.Assembly;
		public override string Namespace => typeBasis.Namespace;
		public override string AssemblyQualifiedName => FullName + ", " + Assembly.FullName;
		public override string FullName => Namespace + "." + Name;

		public override ConstructorInfo[] GetConstructors(BindingFlags bindingAttr) { return new ConstructorInfo[0]; }
		public override object[] GetCustomAttributes(bool inherit) { return new object[0]; }
		public override object[] GetCustomAttributes(Type attributeType, bool inherit) { return new object[0]; }
		public override EventInfo GetEvent(string name, BindingFlags bindingAttr) { return null; }
		public override EventInfo[] GetEvents(BindingFlags bindingAttr) { return new EventInfo[0]; }
		public override FieldInfo GetField(string name, BindingFlags bindingAttr) { return null; }
		public override FieldInfo[] GetFields(BindingFlags bindingAttr) { return new FieldInfo[0]; }
		public override Type GetInterface(string name, bool ignoreCase) { return null; }
		public override Type[] GetInterfaces() { return new Type[0]; }
		public override MemberInfo[] GetMembers(BindingFlags bindingAttr) { return new MemberInfo[0]; }
		public override MethodInfo[] GetMethods(BindingFlags bindingAttr) { return new MethodInfo[0]; }
		public override Type GetNestedType(string name, BindingFlags bindingAttr) { return null; }
		public override Type[] GetNestedTypes(BindingFlags bindingAttr) { return new Type[0]; }
		public override PropertyInfo[] GetProperties(BindingFlags bindingAttr) { return new PropertyInfo[0]; }
		public override object InvokeMember(string name, BindingFlags invokeAttr, Binder binder, object target, object[] args, ParameterModifier[] modifiers, CultureInfo culture, string[] namedParameters) {
			throw new MissingMethodException();
		}
		public override bool IsDefined(Type attributeType, bool inherit) => false;
		protected override TypeAttributes GetAttributeFlagsImpl() { return default; }
		protected override ConstructorInfo GetConstructorImpl(BindingFlags bindingAttr, Binder binder, CallingConventions callConvention, Type[] types, ParameterModifier[] modifiers) {
			return null;
		}
		protected override MethodInfo GetMethodImpl(string name, BindingFlags bindingAttr, Binder binder, CallingConventions callConvention, Type[] types, ParameterModifier[] modifiers) {
			return null;
		}
		protected override PropertyInfo GetPropertyImpl(string name, BindingFlags bindingAttr, Binder binder, Type returnType, Type[] types, ParameterModifier[] modifiers) {
			return null;
		}
		protected override bool IsCOMObjectImpl() => false;
		protected override bool IsPointerImpl() => true; // ???
		protected override bool IsPrimitiveImpl() => false;

		public override bool IsEnum => false;
		public override bool IsEnumDefined(object value) => false;
		public override string GetEnumName(object value) => null;
		public override string[] GetEnumNames() => throw new ArgumentException(Name + " is not an enumeration type.");
		public override Type GetEnumUnderlyingType() => throw new ArgumentException(Name + " is not an enumeration type.");
		public override Array GetEnumValues() => throw new ArgumentException(Name + " is not an enumeration type.");

		public override Type[] GetGenericArguments() {
			return new Type[0];
		}

		#endregion
	}
	*/

}
