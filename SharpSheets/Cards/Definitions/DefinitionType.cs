using SharpSheets.Evaluations;
using SharpSheets.Evaluations.Nodes;
using SharpSheets.Parsing;
using SharpSheets.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace SharpSheets.Cards.Definitions {

	public abstract class DefinitionType {

		public abstract EvaluationType ReturnType { get; }

		public DefinitionType() { }

		public abstract EvaluationNode Validation(EvaluationNode node);

		public static DefinitionType Simple(EvaluationType elementType, int rank) {
			return new SimpleTypeDefinition(rank > 0 ? elementType.MakeArray(rank) : elementType);
		}

		public static DefinitionType Regex(Regex regex, int rank) {
			return new RegexType(regex, rank);
		}

		public static DefinitionType IntegerRange(int start, int end, int rank) {
			return new IntegerRange(start, end, rank);
		}

		public static DefinitionType FloatRange(float start, float end, int rank) {
			return new FloatRange(start, end, rank);
		}

		public static DefinitionType Categorical(string[] categories) {
			return new CategoricalType(categories);
		}

		public static DefinitionType Multicategory(string[] categories) {
			return new MulticategoryType(categories);
		}

		

		public static implicit operator DefinitionType(EvaluationType type) {
			return Simple(type, 0);
		}

		public static string ValueToString(object value) {
			// TODO This method needs updating!

			if(value == null) {
				return "";
			}
			else if(value is Array array) {
				return string.Join(", ", array.OfType<object>().Select(v => ValueToString(v)));
			}
			else {
				return value.ToString() ?? "";
			}
		}

	}

	public class SimpleTypeDefinition : DefinitionType {
		public override EvaluationType ReturnType { get; }

		/// <summary></summary>
		/// <exception cref="ArgumentNullException"></exception>
		internal SimpleTypeDefinition(EvaluationType returnType) {
			this.ReturnType = returnType ?? throw new ArgumentNullException(nameof(returnType));
		}

		public override EvaluationNode Validation(EvaluationNode node) {
			return new SimpleTypeValidationNode(node, ReturnType);
		}

		private class SimpleTypeValidationNode : EvaluationNode {

			public EvaluationNode Subject { get; }

			private readonly EvaluationType castType;
			public override EvaluationType ReturnType {
				get {
					if (EvaluationTypes.IsCompatibleType(castType, Subject.ReturnType)) {
						return castType;
					}
					else {
						throw new EvaluationTypeException($"Cannot cast {Subject.ReturnType} to {castType}.");
					}
				}
			}

			public override bool IsConstant => Subject.IsConstant;

			public SimpleTypeValidationNode(EvaluationNode subject, EvaluationType returnType) {
				Subject = subject;
				this.castType = returnType;
			}

			public override object Evaluate(IEnvironment environment) {
				object? arg = Subject.Evaluate(environment);

				return EvaluationTypes.GetCompatibleValue(castType, arg);
			}

			public override EvaluationNode Clone() => new SimpleTypeValidationNode(Subject.Clone(), ReturnType);
			public override IEnumerable<EvaluationName> GetVariables() => Subject.GetVariables();

			public override EvaluationNode Simplify() {
				if (IsConstant) {
					return new ConstantNode(Evaluate(Environments.Empty));
				}
				else {
					return new SimpleTypeValidationNode(Subject.Simplify(), ReturnType);
				}
			}

			protected override string GetRepresentation() => Subject.ToString(); // TODO Is this right?
		}
	}

	public class RegexType : DefinitionType {
		public override EvaluationType ReturnType { get; }

		public readonly Regex Pattern;

		/// <summary></summary>
		/// <exception cref="ArgumentNullException"></exception>
		internal RegexType(Regex pattern, int rank) {
			this.Pattern = pattern ?? throw new ArgumentNullException(nameof(pattern), "Must provide a regex.");
			//this.rank = rank;
			this.ReturnType = rank > 0 ? EvaluationType.STRING.MakeArray(rank) : EvaluationType.STRING;
		}

		public override EvaluationNode Validation(EvaluationNode node) {
			return new RegexValidationNode(node, Pattern, ReturnType);
		}

		private class RegexValidationNode : EvaluationNode {

			public EvaluationNode Subject { get; }
			private readonly Regex pattern;

			public override bool IsConstant => Subject.IsConstant;
			public override EvaluationType ReturnType { get; }

			public RegexValidationNode(EvaluationNode subject, Regex pattern, EvaluationType returnType) {
				Subject = subject;
				this.pattern = pattern;
				this.ReturnType = returnType;
			}

			private void ValidateValue(object value) {
				if (value is string rawText) {
					string text = StringParsing.Parse(rawText);
					Match match = pattern.Match(text);
					if (!(match.Success && match.Length == text.Length && match.Index == 0)) {
						throw new EvaluationCalculationException("Value does not match expected pattern: " + pattern.ToString());
					}
				}
				else if (value is Array array) {
					foreach (object nestedValue in array) {
						ValidateValue(nestedValue);
					}
				}
				else {
					throw new EvaluationTypeException("Subject of regex match must be a string, or array of strings.");
				}
			}

			public override object Evaluate(IEnvironment environment) {
				object? result = Subject.Evaluate(environment);

				if (result is not null && ReturnType.ValidDataType(result.GetType())) { // result.GetType() == ReturnType.SystemType
					ValidateValue(result);

					return result;
				}
				else {
					throw new EvaluationTypeException($"Value must be a string, or array of strings, not {EvaluationUtils.GetDataTypeName(result)}.");
				}
			}

			public override EvaluationNode Clone() => new RegexValidationNode(Subject.Clone(), pattern, ReturnType);
			public override IEnumerable<EvaluationName> GetVariables() => Subject.GetVariables();

			public override EvaluationNode Simplify() {
				if (IsConstant) {
					return new ConstantNode(Evaluate(Environments.Empty));
				}
				else {
					return new RegexValidationNode(Subject.Simplify(), pattern, ReturnType);
				}
			}

			protected override string GetRepresentation() => Subject.ToString();
		}
	}

	public class CategoricalType : DefinitionType {
		public override EvaluationType ReturnType => EvaluationType.STRING;

		public IReadOnlyList<string> Categories => categories;
		private readonly string[] categories;

		/// <summary></summary>
		/// <exception cref="ArgumentNullException"></exception>
		internal CategoricalType(string[] categories) {
			if (categories == null) { throw new ArgumentNullException(nameof(categories), "Must provide a list of categories."); }
			this.categories = categories.OrderBy(s => s).ToArray();
		}

		public override EvaluationNode Validation(EvaluationNode node) {
			return new CategoryValidationNode(node, categories);
		}

		private class CategoryValidationNode : EvaluationNode {

			public EvaluationNode Subject { get; }
			private readonly string[] categories;

			public override bool IsConstant => Subject.IsConstant;
			public override EvaluationType ReturnType { get; } = EvaluationType.STRING;

			public CategoryValidationNode(EvaluationNode subject, string[] categories) {
				Subject = subject;
				this.categories = categories;
			}

			public override object Evaluate(IEnvironment environment) {
				object? result = Subject.Evaluate(environment);

				if (result is string text) {
					string simpleText = text.Replace(" ", "");
					string? matching = categories.FirstOrDefault(c => c.Replace(" ", "").StartsWith(simpleText, StringComparison.InvariantCultureIgnoreCase));
					if (matching != null) {
						return matching;
					}
					else {
						throw new EvaluationCalculationException($"Value must match one of the following: " + string.Join(", ", categories));
					}
				}
				else {
					throw new EvaluationTypeException("A categorical value must be a string.");
				}
			}

			public override EvaluationNode Clone() => new CategoryValidationNode(Subject.Clone(), categories);
			public override IEnumerable<EvaluationName> GetVariables() => Subject.GetVariables();

			public override EvaluationNode Simplify() {
				if (IsConstant) {
					return new ConstantNode(Evaluate(Environments.Empty));
				}
				else {
					return new CategoryValidationNode(Subject.Simplify(), categories);
				}
			}

			protected override string GetRepresentation() => Subject.ToString();
		}
	}

	public class MulticategoryType : DefinitionType {
		public override EvaluationType ReturnType { get; } = EvaluationType.STRING.MakeArray();

		public IReadOnlyList<string> Categories => categories;
		private readonly string[] categories;

		/// <summary></summary>
		/// <exception cref="ArgumentNullException"></exception>
		internal MulticategoryType(string[] categories) {
			if (categories == null) { throw new ArgumentNullException(nameof(categories), "Must provide a list of categories."); }
			this.categories = categories.ToArray();
		}

		public override EvaluationNode Validation(EvaluationNode node) {
			return new MulticategoryValidationNode(node, categories);
		}

		private class MulticategoryValidationNode : EvaluationNode {

			public EvaluationNode Subject { get; }
			private readonly string[] categories;

			public override bool IsConstant => Subject.IsConstant;
			public override EvaluationType ReturnType { get; } = EvaluationType.STRING.MakeArray();

			public MulticategoryValidationNode(EvaluationNode subject, string[] categories) {
				Subject = subject;
				this.categories = categories;
			}

			public override object Evaluate(IEnvironment environment) {
				object? data = Subject.Evaluate(environment);

				if (data is Array array) {

					string[] result = new string[categories.Length];

					foreach (object value in (IEnumerable)array) {
						if (value is string text) {
							string simpleText = text.Replace(" ", "");
							int index = -1;
							string? matching = null;
							for (int i = 0; i < categories.Length; i++) {
								if (categories[i].Replace(" ", "").StartsWith(simpleText, StringComparison.InvariantCultureIgnoreCase)) {
									index = i;
									matching = categories[i];
									break;
								}
							}
							if (matching != null) {
								result[index] = matching;
							}
							else {
								throw new EvaluationCalculationException($"Values must match one of the following: " + string.Join(", ", categories));
							}
						}
						else {
							throw new EvaluationCalculationException("Multicategory value must be strings.");
						}
					}

					return EvaluationTypes.MakeArray(typeof(string), result.WhereNotEmpty().ToArray());
				}
				else {
					throw new EvaluationTypeException("A multicategory value must be an array of strings.");
				}
			}

			public override EvaluationNode Clone() => new MulticategoryValidationNode(Subject.Clone(), categories);
			public override IEnumerable<EvaluationName> GetVariables() => Subject.GetVariables();

			public override EvaluationNode Simplify() {
				if (IsConstant) {
					return new ConstantNode(Evaluate(Environments.Empty));
				}
				else {
					return new MulticategoryValidationNode(Subject.Simplify(), categories);
				}
			}

			protected override string GetRepresentation() => Subject.ToString();
		}
	}

	public class IntegerRange : DefinitionType {
		public override EvaluationType ReturnType { get; }

		public readonly int Start;
		public readonly int End;

		/// <summary></summary>
		/// <exception cref="ArgumentNullException"></exception>
		internal IntegerRange(int start, int end, int rank) {
			Start = start;
			End = end;
			this.ReturnType = rank > 0 ? EvaluationType.INT.MakeArray(rank) : EvaluationType.INT;
		}

		public override EvaluationNode Validation(EvaluationNode node) {
			return new IntegerRangeValidationNode(node, Start, End, ReturnType);
		}

		private class IntegerRangeValidationNode : EvaluationNode {

			public EvaluationNode Subject { get; }
			private readonly int start;
			private readonly int end;

			public override bool IsConstant => Subject.IsConstant;
			public override EvaluationType ReturnType { get; }

			private bool ReturnsCollection => ReturnType.IsArray || ReturnType.IsTuple;

			public IntegerRangeValidationNode(EvaluationNode subject, int start, int end, EvaluationType returnType) {
				Subject = subject;
				this.start = start;
				this.end = end;
				this.ReturnType = returnType;
			}

			private void ValidateValue(object value) {
				if (EvaluationTypes.TryGetIntegral(value, out int intVal)) {
					if (intVal < start || intVal > end) {
						throw new EvaluationCalculationException($"Value{(ReturnsCollection ? "s" : "")} must be in the range {start} to {end} (inclusive).");
					}
				}
				else if (value is Array array) {
					foreach (object nestedValue in array) {
						ValidateValue(nestedValue);
					}
				}
				else {
					throw new EvaluationTypeException("Subject of integer range must be an integer, or array of integers.");
				}
			}

			public override object Evaluate(IEnvironment environment) {
				object? result = Subject.Evaluate(environment);

				if (result is not null && ReturnType.ValidDataType(result.GetType())) { // result.GetType() == ReturnType.SystemType
					ValidateValue(result);

					return result;
				}
				else if (ReturnsCollection) {
					throw new EvaluationTypeException($"Value must be an array of integers, not {EvaluationUtils.GetDataTypeName(result)}.");
				}
				else {
					throw new EvaluationTypeException($"Value must be an integer, not {EvaluationUtils.GetDataTypeName(result)}.");
				}
			}

			public override EvaluationNode Clone() => new IntegerRangeValidationNode(Subject.Clone(), start, end, ReturnType);
			public override IEnumerable<EvaluationName> GetVariables() => Subject.GetVariables();

			public override EvaluationNode Simplify() {
				if (IsConstant) {
					return new ConstantNode(Evaluate(Environments.Empty));
				}
				else {
					return new IntegerRangeValidationNode(Subject.Simplify(), start, end, ReturnType);
				}
			}

			protected override string GetRepresentation() => Subject.ToString();
		}
	}

	public class FloatRange : DefinitionType {
		public override EvaluationType ReturnType { get; }

		public readonly float Start;
		public readonly float End;

		/// <summary></summary>
		/// <exception cref="ArgumentNullException"></exception>
		internal FloatRange(float start, float end, int rank) {
			Start = start;
			End = end;
			this.ReturnType = rank > 0 ? EvaluationType.FLOAT.MakeArray(rank) : EvaluationType.FLOAT;
		}

		public override EvaluationNode Validation(EvaluationNode node) {
			return new FloatRangeValidationNode(node, Start, End, ReturnType);
		}

		private class FloatRangeValidationNode : EvaluationNode {

			public EvaluationNode Subject { get; }
			private readonly float start;
			private readonly float end;

			public override bool IsConstant => Subject.IsConstant;
			public override EvaluationType ReturnType { get; }

			private bool ReturnsCollection => ReturnType.IsArray || ReturnType.IsTuple;

			public FloatRangeValidationNode(EvaluationNode subject, float start, float end, EvaluationType returnType) {
				Subject = subject;
				this.start = start;
				this.end = end;
				this.ReturnType = returnType;
			}

			private void ValidateValue(object value) {
				if (EvaluationTypes.TryGetReal(value, out float realVal)) {
					if (realVal < start || realVal > end) {
						throw new EvaluationCalculationException($"Value{(ReturnsCollection ? "s" : "")} must be in the range {start} to {end} (inclusive).");
					}
				}
				else if (value is Array array) {
					foreach (object nestedValue in array) {
						ValidateValue(nestedValue);
					}
				}
				else {
					throw new EvaluationTypeException("Subject of float range must be a float, or array of floats.");
				}
			}

			public override object Evaluate(IEnvironment environment) {
				object? result = Subject.Evaluate(environment);

				if (result is not null && ReturnType.ValidDataType(result.GetType())) { // result.GetType() == ReturnType.SystemType
					ValidateValue(result);

					return result;
				}
				else if (ReturnsCollection) {
					throw new EvaluationTypeException($"Value must be an array of floats, not {EvaluationUtils.GetDataTypeName(result)}.");
				}
				else {
					throw new EvaluationTypeException($"Value must be a float, not {EvaluationUtils.GetDataTypeName(result)}.");
				}
			}

			public override EvaluationNode Clone() => new FloatRangeValidationNode(Subject.Clone(), start, end, ReturnType);
			public override IEnumerable<EvaluationName> GetVariables() => Subject.GetVariables();

			public override EvaluationNode Simplify() {
				if (IsConstant) {
					return new ConstantNode(Evaluate(Environments.Empty));
				}
				else {
					return new FloatRangeValidationNode(Subject.Simplify(), start, end, ReturnType);
				}
			}

			protected override string GetRepresentation() => Subject.ToString();
		}
	}

}
