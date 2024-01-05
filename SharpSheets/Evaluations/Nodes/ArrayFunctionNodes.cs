using SharpSheets.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SharpSheets.Evaluations.Nodes {

	public class ArrayCreateNode : VariableArgsFunctionNode {
		public override string Name { get; } = "array";

		public override EvaluationType ReturnType {
			get {
				
				EvaluationType[] returnTypes = Arguments.Select(a => a.ReturnType).Distinct().ToArray();

				if (returnTypes.Length == 0) {
					throw new EvaluationTypeException("Unknown return type for array create node (contains no elements).");
				}

				EvaluationType arrayType = returnTypes[0];
				bool badTypes = false;
				for (int i=1; i<returnTypes.Length; i++) {
					if (arrayType != returnTypes[i]) {
						if(arrayType.IsIntegral() && returnTypes[i].IsIntegral()) {
							arrayType = EvaluationType.INT;
						}
						else if(arrayType.IsReal() && returnTypes[i].IsReal()) {
							arrayType = EvaluationType.FLOAT;
						}
						else {
							badTypes = true;
							break;
						}
					}
				}

				if (badTypes) {
					string s = (returnTypes.Length > 1) ? "s" : "";
					throw new EvaluationTypeException($"Cannot create an array from arguments with type{s}: " + string.Join(", ", returnTypes.Select(t => t.ToString())));
				}

				if (arrayType.DataType == null) {
					throw new EvaluationTypeException($"Cannot create array of dynamic type {arrayType}.");
				}

				return arrayType.MakeArray(); // EvaluationType.Array(arrayType);
			}
		}

		public override object? Evaluate(IEnvironment environment) {
			Type? elementType = (ReturnType.ElementType?.DataType) ?? throw new EvaluationCalculationException($"Cannot construct array of type {ReturnType.ElementType}.");
			
			List<object?> results = new List<object?>();

			for (int i = 0; i < Arguments.Length; i++) {
				object? arg = Arguments[i].Evaluate(environment);
				if (elementType == typeof(int) && arg is not int && EvaluationTypes.TryGetIntegral(arg, out int intVal)) {
					arg = intVal;
				}
				else if (elementType == typeof(float) && arg is not float && EvaluationTypes.TryGetReal(arg, out float realVal)) {
					arg = realVal;
				}
				results.Add(arg);
			}

			return EvaluationTypes.MakeArray(elementType, results);
		}

		protected override VariableArgsFunctionNode MakeEmptyBase() {
			return new ArrayCreateNode();
		}

		/// <summary></summary>
		/// <exception cref="EvaluationSyntaxException"></exception>
		/// <exception cref="EvaluationProcessingException"></exception>
		public static ArrayCreateNode MakeArrayCreateNode(params EvaluationNode[] nodes) {
			ArrayCreateNode node = new ArrayCreateNode();
			node.SetArgumentCount(nodes.Length);
			for (int i = 0; i < nodes.Length; i++) {
				node.Arguments[i] = nodes[i];
			}
			return node;
		}
	}

	public class ArrayContainsNode : FunctionNode {
		public override string Name { get; } = "contains";
		public override EvaluationNode[] Arguments { get; } = new EvaluationNode[2];
		public override bool IsConstant { get { return Arguments[0].IsConstant && Arguments[1].IsConstant; } }

		public sealed override EvaluationType ReturnType {
			get {
				EvaluationType arg1Type = Arguments[0].ReturnType;
				EvaluationType arg2Type = Arguments[1].ReturnType;
				if ((arg1Type.IsArray || arg1Type.IsTuple) && arg1Type.ElementType == arg2Type) {
					return EvaluationType.BOOL;
				}
				else if(arg1Type == EvaluationType.STRING && arg2Type == EvaluationType.STRING) {
					return EvaluationType.BOOL;
				}
				else {
					throw new EvaluationTypeException($"Contains not defined for operands of type {arg1Type} and {arg2Type}.");
				}
			}
		}

		public override object Evaluate(IEnvironment environment) {
			object? a = Arguments[0].Evaluate(environment);
			object? b = Arguments[1].Evaluate(environment);

			if (a is not null && EvaluationTypes.TryGetArray(a, out Array? values)) {
				try {
					return Array.IndexOf(values, b) != -1;
				}
				catch (RankException e) {
					throw new EvaluationTypeException($"Contains only defined for arrays of rank 1 (got rank {values.Rank}).", e);
				}
			}
			else if(a is string text && b is string searchTerm) {
				return text.Contains(searchTerm);
			}
			else {
				throw new EvaluationTypeException($"Contains not defined for operands of type {GetDataTypeName(a)} and {GetDataTypeName(b)}.");
			}
		}

		protected override FunctionNode Empty() {
			return new ArrayContainsNode();
		}
	}

	public class ArrayAllNode : FunctionNode {
		public override string Name { get; } = "all";
		public override EvaluationNode[] Arguments { get; } = new EvaluationNode[1];
		public override bool IsConstant { get { return Arguments[0].IsConstant; } }

		public sealed override EvaluationType ReturnType {
			get {
				EvaluationType argType = Arguments[0].ReturnType;
				if ((argType.IsArray || argType.IsTuple) && argType.ElementType == EvaluationType.BOOL) {
					return EvaluationType.BOOL;
				}
				else {
					throw new EvaluationTypeException($"All not defined for operands of type {argType}.");
				}
			}
		}

		public override object Evaluate(IEnvironment environment) {
			object? a = Arguments[0].Evaluate(environment);

			if (a is not null && EvaluationTypes.TryGetArray(a, out Array? values)) {
				foreach(object i in values) {
					if(i is bool b) {
						if(!b) { return false; }
					}
					else {
						throw new EvaluationTypeException($"Invalid input entry of type {i?.GetType()?.Name ?? "null"}.");
					}
				}

				return true;
			}
			else {
				throw new EvaluationTypeException($"All not defined for operands of type {GetDataTypeName(a)}.");
			}
		}

		protected override FunctionNode Empty() {
			return new ArrayAllNode();
		}
	}

	public class ArrayAnyNode : FunctionNode {
		public override string Name { get; } = "any";
		public override EvaluationNode[] Arguments { get; } = new EvaluationNode[1];
		public override bool IsConstant { get { return Arguments[0].IsConstant; } }

		public sealed override EvaluationType ReturnType {
			get {
				EvaluationType argType = Arguments[0].ReturnType;
				if ((argType.IsArray || argType.IsTuple) && argType.ElementType == EvaluationType.BOOL) {
					return EvaluationType.BOOL;
				}
				else {
					throw new EvaluationTypeException($"Any not defined for operands of type {argType}.");
				}
			}
		}

		public override object Evaluate(IEnvironment environment) {
			object? a = Arguments[0].Evaluate(environment);

			if (a is not null && EvaluationTypes.TryGetArray(a, out Array? values)) {
				foreach (object i in values) {
					if (i is bool b) {
						if (b) { return true; }
					}
					else {
						throw new EvaluationTypeException($"Invalid input entry of type {i?.GetType()?.Name ?? "null"}.");
					}
				}

				return false;
			}
			else {
				throw new EvaluationTypeException($"\"{Name}\" not defined for operands of type {GetDataTypeName(a)}.");
			}
		}

		protected override FunctionNode Empty() {
			return new ArrayAnyNode();
		}
	}

	public class ArraySortNode : VariableArgsFunctionNode {
		public override string Name { get; } = "sort";

		public override EvaluationType ReturnType {
			get {
				if (!(Arguments.Length == 1 || Arguments.Length == 2)) {
					throw new EvaluationTypeException($"Sort must take one or two array/tuple arguments (values [, keys ])."); // Better exception type?
				}
				if (!Arguments.All(a => a.ReturnType.IsArray || a.ReturnType.IsTuple)) {
					throw new EvaluationTypeException($"Sort arguments must be arrays or tuples, not {string.Join(", ", Arguments.Select(a => a.ReturnType.ToString()))}.");
				}
				EvaluationType argType = Arguments[0].ReturnType;
				if (argType.IsTuple) {
					argType = argType.ElementType.MakeArray();
				}
				return argType;
			}
		}

		public override object Evaluate(IEnvironment environment) {
			object? arg1 = Arguments[0].Evaluate(environment);

			if(arg1 is not null && EvaluationTypes.TryGetArray(arg1, out  Array? array)) {
				try {
					if (Arguments.Length > 1) {
						object? arg2 = Arguments[1].Evaluate(environment);
						if (arg2 is not null && EvaluationTypes.TryGetArray(arg2, out Array? keys)) {
							if(keys.Length != array.Length) {
								throw new EvaluationCalculationException($"Length of values ({array.Length}) does not match length of keys ({keys.Length}).");
							}
							Array.Sort(keys, array);
						}
						else {
							throw new EvaluationTypeException($"Invalid keys entry of type {arg2?.GetType()?.Name ?? "null"} to sort.");
						}
					}
					else {
						Array.Sort(array);
					}

					return array;
				}
				catch(InvalidOperationException e) {
					EvaluationType? elemType = Arguments[0].ReturnType.ElementType;
					throw new EvaluationCalculationException($"Could not sort values (sorting not implemented for {elemType?.ToString() ?? "unknown type"}).", e);
				}
				catch(RankException e) {
					throw new EvaluationTypeException($"Sort only defined for arrays of rank 1 (got rank {array.Rank}).", e);
				}
			}
			else {
				throw new EvaluationTypeException($"Invalid values argument of type {arg1?.GetType()?.Name ?? "null"} to sort.");
			}
		}

		protected override VariableArgsFunctionNode MakeEmptyBase() {
			return new ArraySortNode();
		}
	}

	public class ArrayReverseNode : SingleArgFunctionNode {
		public override string Name { get; } = "reverse";

		public override EvaluationType ReturnType {
			get {
				if (Argument.ReturnType.IsTuple) {
					return Argument.ReturnType.ElementType.MakeArray();
				}
				else if (!Argument.ReturnType.IsArray) {
					throw new EvaluationTypeException($"Arguments for reverse must be an array or tuple, not {Argument.ReturnType.ToString()}.");
				}
				else {
					return Argument.ReturnType;
				}
			}
		}

		public override object Evaluate(IEnvironment environment) {
			object? arg = Argument.Evaluate(environment);

			if (arg is not null && EvaluationTypes.TryGetArray(arg, out Array? array)) {
				List<object?> results = new List<object?>();

				for (int i = array.Length - 1; i >= 0; i--) {
					results.Add(array.GetValue(i));
				}

				Type? arrayType = Argument.ReturnType.ElementType?.DataType;
				if(arrayType is null) {
					throw new EvaluationTypeException("Cannot resolve array system type.");
				}

				return EvaluationTypes.MakeArray(arrayType, results);
			}
			else {
				throw new EvaluationTypeException($"Invalid values argument of type {arg?.GetType()?.Name ?? "null"} to {Name}.");
			}
		}

		protected override FunctionNode Empty() {
			return new ArrayReverseNode();
		}

	}

}
