using SharpSheets.Evaluations;
using SharpSheets.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using SharpSheets.Parsing;
using SharpSheets.Evaluations.Nodes;
using SharpSheets.Exceptions;

namespace SharpSheets.Cards.Definitions {

	public abstract class Definition {
		public EvaluationName name;
		public EvaluationName[] aliases;
		public virtual DefinitionType Type { get; }
		public string? description;

		public IEnumerable<EvaluationName> AllNames { get { return name.Yield().Concat(aliases); } }

		public Definition(EvaluationName name, EvaluationName[] aliases, DefinitionType type, string? description) {
			this.name = name;
			this.aliases = aliases;
			this.Type = type;
			this.description = description;
		}

		protected static EvaluationNode MakeBaseNode(string text, EvaluationType returnType, IVariableBox variables) {
			// TODO This method needs some documentation... I'm not sure what it's doing
			if (returnType.IsArray) {
				EvaluationType elementType = returnType.ElementType;
				char splitChar;
				if (elementType.IsArray) {
					EvaluationType nestedElementType = elementType.ElementType;
					if (nestedElementType.IsArray) {
						throw new NotSupportedException("Array definitions with rank greater than 2 not allowed.");
					}
					splitChar = ';';
				}
				else {
					splitChar = ',';
				}

				ContextValue<string> contextedValue = new ContextValue<string>(new DocumentSpan(0, -1, 0, text.Length), text);
				List<SharpParsingException> splitErrors = new List<SharpParsingException>();
				IList<ContextValue<string>>? splitValues = SplitEvaluationParsing.SplitListValues(contextedValue, splitErrors, splitChar);

				if (splitErrors.Count > 0 || splitValues is null) {
					throw new FormatException("Could not split array values.");
				}
				else {
					EvaluationNode[] nodes = splitValues.Select(v => MakeBaseNode(v.Value, elementType, variables)).ToArray();
					return ArrayCreateNode.MakeArrayCreateNode(nodes);
				}
			}
			else if (returnType == EvaluationType.STRING) {
				return Interpolation.Parse(text, variables, false).Evaluation;
			}
			else {
				EvaluationNode eval = Evaluation.Parse(text, variables);
				EvaluationType evalType = eval.ReturnType;
				if (evalType == returnType) {
					return eval;
				}
				else if (returnType == EvaluationType.FLOAT && evalType == EvaluationType.INT) {
					return new FloatCastNode() { Argument = eval };
				}
				else {
					throw new EvaluationTypeException("Invalid expression type.");
				}
			}
		}

		private static readonly Regex defRegex = new Regex(@"
				^ # Must be start of line
				def \s+ (?<aliases>([a-z][a-z0-9]*) (\s*\|\s*[a-z][a-z0-9\s]*)*) # Declaration and aliases
				(
					\s*\:\s* # Colon separator
					(
						(?<type>
							(?<typename>int|float|bool|string (\s*\((?<regex>(.(?!\/\/\/))+)\))? ) # Main type name
							(\s*(?<array>\[\s*\](\s*\[\s*\])?))? # Optional array specification
						)
						|
						(?<multi>multi)?category \s* \( \s* (?<categories>[a-z0-9\ ]+ (\s* \, \s* [a-z0-9\ ]+)*) \)
					)
				)? # Optional type specifier
				(\s*\=\s*(?<expression>(.(?!\/\/\/))+))? # Optional expression (either default value or calculation expression)
				(\s*\/\/\/\s*(?<description>.*))? # Optional description
				$ # Must be end of line
				", RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);

		//private static readonly Regex defNameRegex = new Regex(@"^def\s+(?<name>[a-z][a-z0-9]*)\s*[\|\:\=]", RegexOptions.IgnoreCase);

		private static EvaluationName[] GetDefinitionNames(Match match) {
			return match.Groups["aliases"].Value.SplitAndTrim('|').Select(s => new EvaluationName(s)).Distinct().ToArray();
		}

		public static string? GetDefinitionName(string str) {
			Match match = defRegex.Match(str);
			if (match.Success) {
				return GetDefinitionNames(match).First().ToString();
			}
			return null;
		}

		public static Definition Parse(string str, IVariableBox variables) {
			Match match = defRegex.Match(str);
			if (!match.Success) {
				throw new FormatException("Cannot parse definition.");
			}

			EvaluationName[] allNames = GetDefinitionNames(match); // match.Groups["aliases"].Value.SplitAndTrim('|').Select(s => new EvaluationName(s)).Distinct().ToArray();
			EvaluationName name = allNames.First();
			EvaluationName[] aliases = allNames.Skip(1).ToArray();

			string? description = null;
			if (match.Groups["description"].Success && match.Groups["description"].Length > 0) {
				description = match.Groups["description"].Value;
			}

			EvaluationNode? expression = null;
			if (match.Groups["expression"].Success) {
				string expressionStr = match.Groups["expression"].Value;
				expression = Evaluation.Parse(expressionStr, variables);
			}

			DefinitionType? definitionType = null;

			if (match.Groups["type"].Success) {
				// TODO This seems restrictive. Can we allow more freedom of types?
				string typeNameStr = match.Groups["typename"].Value;
				int arrayRank = match.Groups["array"].Success ? match.Groups["array"].Value.Length / 2 : 0;
				if (typeNameStr.StartsWith("string", StringComparison.InvariantCultureIgnoreCase)) {
					if (match.Groups["regex"].Success) {
						try {
							definitionType = DefinitionType.Regex(new Regex(match.Groups["regex"].Value), arrayRank);
						}
						catch(ArgumentException e) {
							throw new FormatException("Invalid regex format in type.", e);
						}
					}
					else {
						definitionType = DefinitionType.Simple(EvaluationType.STRING, arrayRank);
					}
				}
				else if (typeNameStr == "int") { definitionType = DefinitionType.Simple(EvaluationType.INT, arrayRank); }
				else if (typeNameStr == "float") { definitionType = DefinitionType.Simple(EvaluationType.FLOAT, arrayRank); }
				else if (typeNameStr == "bool") { definitionType = DefinitionType.Simple(EvaluationType.BOOL, arrayRank); }
				else { throw new FormatException($"Unrecognized definition type: {match.Groups["type"].Value}"); }
			}
			else if (match.Groups["categories"].Success) {
				string[] categories = match.Groups["categories"].Value.SplitAndTrim(',');
				if (match.Groups["multi"].Success) {
					definitionType = DefinitionType.Multicategory(categories);
				}
				else {
					definitionType = DefinitionType.Categorical(categories);
				}
			}

			if (definitionType != null) {
				if (expression is null) {
					return new ConstantDefinition(name, aliases, description, definitionType);
				}
				else {
					return new FallbackDefinition(name, aliases, description, definitionType, expression);
				}
			}
			else if (expression is not null) {
				return new CalculatedDefinition(name, aliases, description, expression);
			}
			else {
				throw new FormatException("Invalid definition.");
			}
		}

		public override bool Equals(object? obj) {
			if(obj is Definition other) {
				return this.name == other.name &&
					Enumerable.SequenceEqual(this.aliases, other.aliases) &&
					this.description == other.description &&
					this.Type.ReturnType == other.Type.ReturnType;
			}
			else {
				return false;
			}
		}
		public override int GetHashCode() {
			return (name + string.Join("|", aliases) + description + Type.ReturnType.ToString()).GetHashCode();
		}

		public override string ToString() {
			return string.Join("|", AllNames);
		}
	}

	public class ConstantDefinition : Definition {

		public ConstantDefinition(EvaluationName name, EvaluationName[] aliases, string? description, DefinitionType type) : base(name, aliases, type, description) { }

		/*
		public object ParseValue(string value) {
			return type.Parse(value);
		}
		*/

		public EvaluationNode MakeNode(string text, IVariableBox variables) {
			return Type.Validation(MakeBaseNode(text, Type.ReturnType, variables));
		}
	}

	public class CalculatedDefinition : Definition {
		public EvaluationNode Evaluation { get; }

		public CalculatedDefinition(EvaluationName name, EvaluationName[] aliases, string? description, EvaluationNode evaluation) : base(name, aliases, DefinitionType.Simple(evaluation.ReturnType, 0), description) {
			this.Evaluation = evaluation;
		}

		/*
		public object Evaluate(IEnvironment environment) {
			return Evaluation.Evaluate(environment);
		}
		*/
	}

	public class FallbackDefinition : Definition {
		public EvaluationNode Evaluation { get; }

		public FallbackDefinition(EvaluationName name, EvaluationName[] aliases, string? description, DefinitionType type, EvaluationNode evaluation) : base(name, aliases, type, description) {
			if(this.Type.ReturnType != evaluation.ReturnType) {
				throw new EvaluationTypeException($"Invalid definition: fallback expression return type must must match the stated definition type.");
			}

			this.Evaluation = type.Validation(evaluation);
		}

		/*
		public object ParseValue(string value) {
			return type.Parse(value);
		}

		public object Evaluate(IEnvironment environment) {
			return Evaluation.Evaluate(environment);
		}
		*/

		public EvaluationNode MakeNode(string text, IVariableBox variables) {
			return Type.Validation(MakeBaseNode(text, Type.ReturnType, variables));
		}

		/*
		private class FallbackNode : EvaluationNode {

			private readonly EvaluationNode evaluation;
			private readonly EvaluationName name;

			public override bool IsConstant { get; } = false;

			public override EvaluationType ReturnType => throw new NotImplementedException();

			public FallbackNode() {

			}

			public override EvaluationNode Clone() {
				throw new NotImplementedException();
			}

			public override object Evaluate(IEnvironment environment) {
				throw new NotImplementedException();
			}

			public override IEnumerable<EvaluationName> GetVariables() {
				throw new NotImplementedException();
			}

			public override EvaluationNode Simplify() {
				throw new NotImplementedException();
			}
		}
		*/

	}

	public static class DefinitionUtils {

		public static IEnvironment ToEnvironment(this Dictionary<Definition, object> source) {
			return SimpleEnvironments.Create(source.SelectMany(kv => kv.Key.AllNames.Select(n => new KeyValuePair<EvaluationName, (object?, EvaluationType)>(n, (kv.Value, kv.Key.Type.ReturnType)))).ToDictionary());
		}

		public static IEnvironment AppendEnvironment(this IEnvironment source, Dictionary<Definition, object> values) {
			return source.AppendEnvironment(values.ToEnvironment());
		}

		public static IVariableBox ToVariableBox(this Dictionary<Definition, EvaluationNode> source) {
			return SimpleVariableBoxes.Create(source.SelectMany(kv => kv.Key.AllNames.Select(n => new KeyValuePair<EvaluationName, EvaluationNode>(n, kv.Value))));
		}

		public static IVariableBox AppendVariables(this IVariableBox source, Dictionary<Definition, EvaluationNode> nodes) {
			return source.AppendVariables(nodes.ToVariableBox());
		}

	}
}