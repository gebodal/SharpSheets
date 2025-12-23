using SharpSheets.Evaluations;
using SharpSheets.Evaluations.Nodes;
using SharpSheets.Evaluations.Types;
using SharpSheets.Parsing;
using SharpSheets.Utilities;
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

		public static string ValueToString(object? value) {
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

			public override bool IsConstant => Subject.IsConstant;

			public SimpleTypeValidationNode(EvaluationNode subject, EvaluationType returnType) : base(subject.Context) {
				Subject = subject;
				this.castType = returnType;
			}

			public override EvaluationType GetReturnType() {
				EvaluationType subjectType = Subject.GetReturnType();
				if (castType.CanImplicitCastFrom(subjectType)) {
					return castType;
				}
				else {
					throw new EvaluationTypeException($"Cannot cast {subjectType} to {castType}.");
				}
			}

			public override EvaluationValue Evaluate(IEnvironment environment) {
				EvaluationValue arg = Subject.Evaluate(environment);

				return castType.Cast(arg) ?? throw new EvaluationTypeException($"Cannot convert {arg.Type} value to {castType} value.");
			}

			public override EvaluationNode Clone() => new SimpleTypeValidationNode(Subject.Clone(), castType);
			public override IEnumerable<EvaluationName> GetVariables() => Subject.GetVariables();

			public override EvaluationNode Simplify() {
				if (IsConstant) {
					return new ConstantNode(Evaluate(CardEnvironments.Basis));
				}
				else {
					return new SimpleTypeValidationNode(Subject.Simplify(), castType);
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
			this.ReturnType = rank > 0 ? CardEnvironments.STRING.MakeArray(rank) : CardEnvironments.STRING;
		}

		public override EvaluationNode Validation(EvaluationNode node) {
			return new RegexValidationNode(node, Pattern, ReturnType);
		}

		private class RegexValidationNode : EvaluationNode {

			public EvaluationNode Subject { get; }
			private readonly Regex pattern;

			public override bool IsConstant => Subject.IsConstant;
			private readonly EvaluationType returnType;
			public override EvaluationType GetReturnType() { return returnType; }

			public RegexValidationNode(EvaluationNode subject, Regex pattern, EvaluationType returnType) : base(subject.Context) {
				Subject = subject;
				this.pattern = pattern;
				this.returnType = returnType;
			}

			private void ValidateValue(EvaluationValue value) {
				if (StringEvaluationType.TryGetString(value, out string? rawText)) {
					string text = StringParsing.Parse(rawText);
					Match match = pattern.Match(text);
					if (!(match.Success && match.Length == text.Length && match.Index == 0)) {
						throw new EvaluationCalculationException("Value does not match expected pattern: " + pattern.ToString());
					}
				}
				else if (value.Type.Iteration(value) is IEnumerable<EvaluationValue> iterations) {
					foreach (EvaluationValue nestedValue in iterations) {
						ValidateValue(nestedValue);
					}
				}
				else {
					//throw new EvaluationTypeException("Subject of regex match must be a string, or array of strings.");
					throw new EvaluationTypeException($"Value must be a string, or array of strings, not {value.Type}.");
				}
			}

			public override EvaluationValue Evaluate(IEnvironment environment) {
				EvaluationValue result = Subject.Evaluate(environment);

				ValidateValue(result);

				return result;
			}

			public override EvaluationNode Clone() => new RegexValidationNode(Subject.Clone(), pattern, returnType);
			public override IEnumerable<EvaluationName> GetVariables() => Subject.GetVariables();

			public override EvaluationNode Simplify() {
				if (IsConstant) {
					return new ConstantNode(Evaluate(CardEnvironments.Basis));
				}
				else {
					return new RegexValidationNode(Subject.Simplify(), pattern, returnType);
				}
			}

			protected override string GetRepresentation() => Subject.ToString();
		}
	}

	public class CategoricalType : DefinitionType {
		public override EvaluationType ReturnType => CardEnvironments.STRING;

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
			private readonly EvaluationType returnType = CardEnvironments.STRING;
			public override EvaluationType GetReturnType() => returnType;

			public CategoryValidationNode(EvaluationNode subject, string[] categories) : base(subject.Context) {
				Subject = subject;
				this.categories = categories;
			}

			public override EvaluationValue Evaluate(IEnvironment environment) {
				EvaluationValue result = Subject.Evaluate(environment);

				if (StringEvaluationType.TryGetString(result, out string? text)) {
					string simpleText = text.Replace(" ", "");
					string? matching = categories.FirstOrDefault(c => c.Replace(" ", "").StartsWith(simpleText, StringComparison.InvariantCultureIgnoreCase));
					if (matching != null) {
						return new EvaluationValue(matching, returnType);
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
					return new ConstantNode(Evaluate(CardEnvironments.Basis));
				}
				else {
					return new CategoryValidationNode(Subject.Simplify(), categories);
				}
			}

			protected override string GetRepresentation() => Subject.ToString();
		}
	}

	public class MulticategoryType : DefinitionType {
		public override EvaluationType ReturnType { get; } = CardEnvironments.STRING.MakeArray();

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
			private readonly EvaluationType returnType = CardEnvironments.STRING.MakeArray();
			public override EvaluationType GetReturnType() => returnType;

			public MulticategoryValidationNode(EvaluationNode subject, string[] categories) : base(subject.Context) {
				Subject = subject;
				this.categories = categories;
			}

			public override EvaluationValue Evaluate(IEnvironment environment) {
				EvaluationValue data = Subject.Evaluate(environment);

				if (data.Type.Iteration(data) is IEnumerable<EvaluationValue> iterations) {

					string[] result = new string[categories.Length];

					foreach (EvaluationValue value in iterations) {
						if (StringEvaluationType.TryGetString(value, out string? text)) {
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

					return ArrayEvaluationType.MakeArray(CardEnvironments.STRING, result.WhereNotEmpty().Select(s => new EvaluationValue(s, CardEnvironments.STRING)).ToArray());
				}
				else {
					throw new EvaluationTypeException("A multicategory value must be an array of strings.");
				}
			}

			public override EvaluationNode Clone() => new MulticategoryValidationNode(Subject.Clone(), categories);
			public override IEnumerable<EvaluationName> GetVariables() => Subject.GetVariables();

			public override EvaluationNode Simplify() {
				if (IsConstant) {
					return new ConstantNode(Evaluate(CardEnvironments.Basis));
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
			this.ReturnType = rank > 0 ? CardEnvironments.INT.MakeArray(rank) : CardEnvironments.INT;
		}

		public override EvaluationNode Validation(EvaluationNode node) {
			return new IntegerRangeValidationNode(node, Start, End, ReturnType);
		}

		private class IntegerRangeValidationNode : EvaluationNode {

			public EvaluationNode Subject { get; }
			private readonly int start;
			private readonly int end;

			public override bool IsConstant => Subject.IsConstant;
			public EvaluationType ReturnType { get; }
			public override EvaluationType GetReturnType() => ReturnType;

			public IntegerRangeValidationNode(EvaluationNode subject, int start, int end, EvaluationType returnType) : base(subject.Context) {
				Subject = subject;
				this.start = start;
				this.end = end;
				this.ReturnType = returnType;
			}

			private void ValidateValue(EvaluationValue value) {
				if (IntEvaluationType.TryGetInt(value, out int intVal)) {
					if (intVal < start || intVal > end) {
						throw new EvaluationCalculationException($"Values must be in the range {start} to {end} (inclusive).");
					}
				}
				else if (value.Type.Iteration(value) is IEnumerable<EvaluationValue> iterations) {
					foreach (EvaluationValue nestedValue in iterations) {
						ValidateValue(nestedValue);
					}
				}
				else {
					throw new EvaluationTypeException("Value of integer range must be a int, or array of ints.");
				}
			}

			public override EvaluationValue Evaluate(IEnvironment environment) {
				EvaluationValue result = Subject.Evaluate(environment);

				ValidateValue(result);

				return result;
			}

			public override EvaluationNode Clone() => new IntegerRangeValidationNode(Subject.Clone(), start, end, ReturnType);
			public override IEnumerable<EvaluationName> GetVariables() => Subject.GetVariables();

			public override EvaluationNode Simplify() {
				if (IsConstant) {
					return new ConstantNode(Evaluate(CardEnvironments.Basis));
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
			this.ReturnType = rank > 0 ? CardEnvironments.FLOAT.MakeArray(rank) : CardEnvironments.FLOAT;
		}

		public override EvaluationNode Validation(EvaluationNode node) {
			return new FloatRangeValidationNode(node, Start, End, ReturnType);
		}

		private class FloatRangeValidationNode : EvaluationNode {

			public EvaluationNode Subject { get; }
			private readonly float start;
			private readonly float end;

			public override bool IsConstant => Subject.IsConstant;
			public EvaluationType ReturnType { get; }
			public override EvaluationType GetReturnType() => ReturnType;

			public FloatRangeValidationNode(EvaluationNode subject, float start, float end, EvaluationType returnType) : base(subject.Context) {
				Subject = subject;
				this.start = start;
				this.end = end;
				this.ReturnType = returnType;
			}

			private void ValidateValue(EvaluationValue value) {
				if (FloatEvaluationType.TryGetFloat(value, out float realVal)) {
					if (realVal < start || realVal > end) {
						throw new EvaluationCalculationException($"Values must be in the range {start} to {end} (inclusive).");
					}
				}
				else if (value.Type.Iteration(value) is IEnumerable<EvaluationValue> iterations) {
					foreach (EvaluationValue nestedValue in iterations) {
						ValidateValue(nestedValue);
					}
				}
				else {
					throw new EvaluationTypeException("Value of float range must be a float, or array of floats.");
				}
			}

			public override EvaluationValue Evaluate(IEnvironment environment) {
				EvaluationValue result = Subject.Evaluate(environment);

				ValidateValue(result);

				return result;
			}

			public override EvaluationNode Clone() => new FloatRangeValidationNode(Subject.Clone(), start, end, ReturnType);
			public override IEnumerable<EvaluationName> GetVariables() => Subject.GetVariables();

			public override EvaluationNode Simplify() {
				if (IsConstant) {
					return new ConstantNode(Evaluate(CardEnvironments.Basis));
				}
				else {
					return new FloatRangeValidationNode(Subject.Simplify(), start, end, ReturnType);
				}
			}

			protected override string GetRepresentation() => Subject.ToString();
		}
	}

}
