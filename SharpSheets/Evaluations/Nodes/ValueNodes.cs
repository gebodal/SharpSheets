using SharpSheets.Evaluations.Types;
using System;
using System.Collections.Generic;

namespace SharpSheets.Evaluations.Nodes {

	public abstract class ValueNode : EvaluationNode {
		public ValueNode(EvaluationContext context) : base(context) { }

		public override EvaluationNode Simplify() {
			return Clone();
		}
	}

	public class ConstantNode : ValueNode {

		public override bool IsConstant { get; } = true;
		public override EvaluationType GetReturnType() => Value.Type;
		public EvaluationValue Value { get; }

		/*
		public ConstantNode(float value) {
			this.Value = value;
			this.ReturnType = EvaluationPrimitive.FLOAT;
		}
		public ConstantNode(int value) {
			this.Value = value;
			this.ReturnType = EvaluationPrimitive.INT;
		}
		public ConstantNode(bool value) {
			this.Value = value;
			this.ReturnType = EvaluationPrimitive.BOOL;
		}
		public ConstantNode(string value) {
			this.Value = value;
			this.ReturnType = EvaluationPrimitive.STRING;
		}
		*/

		/*
		public ConstantNode(object value) {
			this.Value = value;
			this.ReturnType = EvaluationType.FromSystemType(value.GetType());
		}
		*/

		public ConstantNode(EvaluationValue value) : base(value.Type.Context) {
			this.Value = value;
		}

		public ConstantNode(object? value, EvaluationType type) : base(type.Context) {
			this.Value = new EvaluationValue(value, type);
		}

		public override EvaluationValue Evaluate(IEnvironment environment) {
			return Value;
		}

		public override EvaluationNode Clone() {
			return new ConstantNode(Value);
		}

		public override IEnumerable<EvaluationName> GetVariables() => Enumerable.Empty<EvaluationName>();

		/*
		public override void Print(int indent, IEnvironment environment) {
			Console.WriteLine(new string(' ', indent * 4) + Value.ToString());
		}
		*/

		/*
		private static string ValueString(object? value) {
			// TODO This logic shouldn't be here
			if (value is string s) {
				return "\"" + s + "\"";
			}
			else if (value is Array a) {
				string result = "{";
				foreach (object i in a) {
					result += result.Length > 1 ? " , " : " ";
					result += ValueString(i);
				}
				result += " }";
				return result;
			}
			else {
				return value?.ToString() ?? "";
			}
		}
		*/

		protected override string GetRepresentation() {
			//return ValueString(Value); // TODO This probably should change
			return Value.ToEvaluationString();
		}
	}

	public class VariableNode : ValueNode {

		public override bool IsConstant { get; } = false;
		private readonly EvaluationType returnType;
		public override EvaluationType GetReturnType() => returnType;
		public EvaluationName key;

		public VariableNode(EvaluationName key, EvaluationType type) : base(type.Context) {
			this.key = key;
			this.returnType = type;
		}

		public override EvaluationValue Evaluate(IEnvironment environment) {
			return environment.GetValue(key);
		}

		public override EvaluationNode Clone() {
			return new VariableNode(key, returnType);
		}

		public override IEnumerable<EvaluationName> GetVariables() {
			yield return key;
		}

		/*
		public override void Print(int indent, IEnvironment environment) {
			Console.WriteLine(new string(' ', indent * 4) + $"${key} = {environment[key]}");
		}
		*/

		protected override string GetRepresentation() { return key.ToString(); }
	}

	public class TypeLiteralNode : ValueNode {

		public override bool IsConstant { get; } = true;
		private EvaluationType ReturnType => Context.GetType<MetaEvaluationType>();
		public override EvaluationType GetReturnType() => ReturnType;

		public EvaluationType TypeValue { get; }

		public TypeLiteralNode(EvaluationType typeValue) : base(typeValue.Context) {
			this.TypeValue = typeValue;
		}

		public override EvaluationNode Clone() {
			return new TypeLiteralNode(TypeValue);
		}

		public override EvaluationValue Evaluate(IEnvironment environment) {
			return new EvaluationValue(TypeValue, ReturnType);
		}

		public override IEnumerable<EvaluationName> GetVariables() => Enumerable.Empty<EvaluationName>();

		protected override string GetRepresentation() {
			return TypeValue.ToString(); // TODO This should be the type environment name. Is this the same?
		}
	}

}
