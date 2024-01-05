using System;
using System.Collections.Generic;

namespace SharpSheets.Evaluations.Nodes {

	public abstract class ValueNode : EvaluationNode {
		public override EvaluationNode Simplify() {
			return Clone();
		}
	}

	public class ConstantNode : ValueNode {

		public override bool IsConstant { get; } = true;
		public override EvaluationType ReturnType { get; }
		public object? Value { get; }

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

		public ConstantNode(object value) {
			this.Value = value;
			this.ReturnType = EvaluationType.FromSystemType(value.GetType());
		}

		public ConstantNode(object? value, EvaluationType returnType) {
			this.Value = value;
			this.ReturnType = returnType;
		}

		public override object? Evaluate(IEnvironment environment) {
			return Value;
		}

		public override EvaluationNode Clone() {
			return new ConstantNode(Value, ReturnType);
		}

		public override IEnumerable<EvaluationName> GetVariables() {
			yield break;
		}

		/*
		public override void Print(int indent, IEnvironment environment) {
			Console.WriteLine(new string(' ', indent * 4) + Value.ToString());
		}
		*/

		private static string ValueString(object? value) {
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

		protected override string GetRepresentation() {
			return ValueString(Value);
		}
	}

	public class VariableNode : ValueNode {

		public override bool IsConstant { get; } = false;
		public override EvaluationType ReturnType { get; }
		public EvaluationName key;

		public VariableNode(EvaluationName key, EvaluationType type) {
			this.key = key;
			this.ReturnType = type;
		}

		public override object? Evaluate(IEnvironment environment) {
			return environment.GetVariable(key);
		}

		public override EvaluationNode Clone() {
			return new VariableNode(key, ReturnType);
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

}
