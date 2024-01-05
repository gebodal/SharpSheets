using SharpSheets.Cards.Definitions;
using SharpSheets.Evaluations;
using SharpSheets.Evaluations.Nodes;
using SharpSheets.Exceptions;
using SharpSheets.Parsing;
using SharpSheets.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SharpSheets.Cards.CardSubjects {

	public static class SubjectValueParsing {

		public static DefinitionEnvironment? ParseValues(ContextValue<string>? details, IVariableBox variables, DefinitionGroup definitions, out List<SharpParsingException> errors) {
			errors = new List<SharpParsingException>();

			if(definitions.Count == 0) {
				return DefinitionEnvironment.Empty;
			}

			if (!details.HasValue && definitions.Any(d => d is ConstantDefinition)) {
				return null;
			}

			IList<ContextProperty<string>>? values;
			DocumentSpan location;
			if (details.HasValue) {
				values = SplitEvaluationParsing.SplitDictionaryValues(details, errors);
				location = details.Value.Location;
			}
			else {
				values = new List<ContextProperty<string>>();
				location = DocumentSpan.Imaginary;
			}

			if (values is null) {
				return null;
			}

			try {
				DefinitionEnvironment? result = ParseValues(location, values, variables, definitions, out List<SharpParsingException> parseErrors);
				errors.AddRange(parseErrors);
				return result;
			}
			catch (Exception e) {
				errors.Add(new SharpParsingException(location, e.Message, e));
				return null;
			}
		}

		public static DefinitionEnvironment? ParseValues(DocumentSpan errorLocation, IList<ContextProperty<string>> values, IVariableBox variables, DefinitionGroup definitions, out List<SharpParsingException> errors) {
			errors = new List<SharpParsingException>();
			List<SharpParsingException> missingValueErrors = new List<SharpParsingException>();

			Dictionary<EvaluationName, ContextProperty<string>> valuesDict = values.ToDictionary<ContextProperty<string>, EvaluationName>(p => p.Name);
			Dictionary<EvaluationName, ContextProperty<string>> unusedValues = values.ToDictionary<ContextProperty<string>, EvaluationName>(p => p.Name);
			ContextProperty<string>? GetValue(Definition definition) {
				foreach (EvaluationName name in definition.AllNames) {
					if (valuesDict.TryGetValue(name, out ContextProperty<string> value)) {
						unusedValues.Remove(value.Name);
						return value;
					}
				}
				return null;
			}

			IVariableBox allVariables = variables.AppendVariables(definitions);
			Dictionary<Definition, ContextProperty<object>> definitionValues = new Dictionary<Definition, ContextProperty<object>>();
			Dictionary<Definition, ContextProperty<EvaluationNode>> definitionNodes = new Dictionary<Definition, ContextProperty<EvaluationNode>>();
			foreach (Definition definition in definitions) {
				if (definition is ConstantDefinition constant) {
					ContextProperty<string>? value = GetValue(constant);
					if (value.HasValue) {
						EvaluationNode? node = null;
						try {
							node = constant.MakeNode(value.Value.Value, allVariables);
							if (node.IsConstant) {
								object constantValue = node.Evaluate(Environments.Empty) ?? throw new EvaluationTypeException("Definition constant value must not be null.");
								////results.AddValue(constant, new ContextProperty<object>(value.Value.Location, value.Value.Name, value.Value.ValueLocation, constantValue));
								//definitionValues.Add(constant, constantValue);
								definitionValues.Add(constant, new ContextProperty<object>(value.Value.Location, value.Value.Name, value.Value.ValueLocation, constantValue));
							}
							else {
								////results.AddNode(constant, node);
								//definitionNodes.Add(constant, node);
								// TODO No check here to ensure contents is as expected?
								definitionNodes.Add(constant, new ContextProperty<EvaluationNode>(value.Value.Location, value.Value.Name, value.Value.ValueLocation, node));
							}
						}
						catch (EvaluationException e) {
							errors.Add(new SharpParsingException(value.Value.ValueLocation, e.Message, e));
						}
						catch (FormatException e) {
							errors.Add(new SharpParsingException(value.Value.ValueLocation, e.Message, e));
						}

						if (node is null) {
							missingValueErrors.Add(new MissingDefinitionException(errorLocation, definition));
						}
					}
					else {
						/* If a non-optional definition is missing, then the provided set of definitions cannot apply to this set of details,
						 * and we should gracefully exit.
						 * We will record the error, and then if any such errors have been found, return them at the end, in case
						 * there is no recovery possible (e.g. for card subjects).
						 */
						missingValueErrors.Add(new MissingDefinitionException(errorLocation, definition));
					}
				}
				else if (definition is FallbackDefinition fallback) {
					ContextProperty<string>? value = GetValue(fallback);
					if (value.HasValue) {
						try {
							EvaluationNode node = fallback.MakeNode(value.Value.Value, allVariables);
							if (node.IsConstant) {
								object fallbackValue = node.Evaluate(Environments.Empty) ?? throw new EvaluationTypeException("Definition fallback value must not be null.");
								////results.AddValue(fallback, new ContextProperty<object>(value.Value.Location, value.Value.Name, value.Value.ValueLocation, fallbackValue));
								//definitionValues.Add(fallback, fallbackValue);
								definitionValues.Add(fallback, new ContextProperty<object>(value.Value.Location, value.Value.Name, value.Value.ValueLocation, fallbackValue));
							}
							else {
								////results.AddNode(fallback, node);
								//definitionNodes.Add(fallback, node);
								definitionNodes.Add(fallback, new ContextProperty<EvaluationNode>(value.Value.Location, value.Value.Name, value.Value.ValueLocation, node));
							}
						}
						catch (EvaluationException e) {
							errors.Add(new SharpParsingException(value.Value.ValueLocation, e.Message, e));
							//results.AddNode(fallback, fallback.Evaluation);
						}
						catch (FormatException e) {
							errors.Add(new SharpParsingException(value.Value.ValueLocation, e.Message, e));
							//results.AddNode(fallback, fallback.Evaluation);
						}
					}
					/*
					else {
						results.AddNode(fallback, fallback.Evaluation);
					}
					*/
				}
				/*
				else if (definition is CalculatedDefinition calculated) {
					results.AddNode(calculated, calculated.Evaluation);
				}
				*/
			}

			foreach (KeyValuePair<EvaluationName, ContextProperty<string>> unused in unusedValues) {
				errors.Add(new SharpParsingException(unused.Value.Location, $"Unrecognized value \"{unused.Key}\"."));
			}

			if (missingValueErrors.Count > 0) {
				// Non-optional definition is missing, and so we gracefully exit here.
				//errors = missingValueErrors;
				errors.AddRange(missingValueErrors);
				return null;
			}

			return DefinitionEnvironment.Create(definitions, definitionValues, definitionNodes);
		}
	}

	public class MissingDefinitionException : SharpParsingException {

		public readonly Definition Missing;

		public MissingDefinitionException(DocumentSpan? location, Definition missing) : base(location, $"No value provided for {missing.name}.") {
			Missing = missing;
		}


		public override object Clone() {
			return new MissingDefinitionException(Location, Missing);
		}

	}

}