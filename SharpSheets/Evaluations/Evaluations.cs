using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.RegularExpressions;
using SharpSheets.Evaluations.Nodes;
using SharpSheets.Parsing;
using SharpSheets.Utilities;

namespace SharpSheets.Evaluations {

	public static class Evaluation {

		/// <summary></summary>
		/// <exception cref="EvaluationSyntaxException"></exception>
		private static OperatorNode GetOperator(string operatorStr) {
			// * / % + - <= >= < > == != & ^ |
			if (operatorStr == "**") { return new ExponentNode(); }
			else if (operatorStr == "*") { return new MultiplicationNode(); }
			else if(operatorStr == "/") { return new DivisionNode(); }
			else if (operatorStr == "%") { return new RemainderNode(); }
			else if (operatorStr == "+") { return new AdditionNode(); }
			else if (operatorStr == "-") { return new SubtractNode(); }
			else if (operatorStr == "<") { return new LessThanNode(); }
			else if (operatorStr == ">") { return new GreaterThanNode(); }
			else if (operatorStr == "<=") { return new LessThanEqualNode(); }
			else if (operatorStr == ">=") { return new GreaterThanEqualNode(); }
			else if (operatorStr == "==") { return new EqualityNode(); }
			else if (operatorStr == "!=") { return new InequalityNode(); }
			else if (operatorStr == "&" || operatorStr == "&&" || operatorStr.ToLowerInvariant() == "and") { return new ANDNode(); }
			else if (operatorStr == "^") { return new XORNode(); }
			else if (operatorStr == "|" || operatorStr == "||" || operatorStr.ToLowerInvariant() == "or") { return new ORNode(); }
			else if (operatorStr == "!") { return new NegateOperator(); }
			else if (operatorStr == "??") { return new NullCoalescingNode(); }
			else if (operatorStr == "?") { return new ConditionalOperatorNode.ConditionalOpenNode(); }
			else if (operatorStr == ":") { return new ConditionalOperatorNode(); }
			else { throw new EvaluationSyntaxException($"Unrecognized operator: {operatorStr}"); }
		}

		private static bool TryGetUnaryOperator(string operatorStr, [MaybeNullWhen(false)] out UnaryOperatorNode node) {
			node = null;
			if (operatorStr == "-") { node = new MinusOperator(); }
			else if (operatorStr == "+") { node = new PlusOperator(); }
			return node is not null;
		}

		private static readonly Regex indexerRegex = new Regex(@"^(?<start>\-?[0-9]+)?\:(?<end>\-?[0-9]+)?|(?<index>\-?[0-9]+)$");

		/*
		private static IndexerNode GetIndexer(string indexer) {
			if(indexer == ":") {
				return new IndexerNode(null, null, true);
			}

			Match match = indexerRegex.Match(indexer);
			if (match.Groups["index"].Success) {
				int index = int.Parse(match.Groups["index"].Value);
				return new IndexerNode(index, null, false);
			}
			else if (match.Success) {
				int? start = match.Groups["start"].Success ? int.Parse(match.Groups["start"].Value) : (int?)null;
				int? end = match.Groups["end"].Success ? int.Parse(match.Groups["end"].Value) : (int?)null;
				return new IndexerNode(start, end, true);
			}
			else {
				throw new EvaluationSyntaxException("Badly formatted indexer.");
			}
		}
		*/

		/// <summary></summary>
		/// <exception cref="UndefinedFunctionException"></exception>
		private static EnvironmentFunctionNode GetFunction(EvaluationName name, IVariableBox variables) {
			if (variables.TryGetFunctionInfo(name, out IEnvironmentFunctionInfo? functionInfo)) {
				return new EnvironmentFunctionNode(functionInfo);
			}
			else {
				throw new UndefinedFunctionException(name);
			}

			/*
			if (name == "len") { return new LengthNode(); }
			else if (name == "exists") { return new ExistsNode(); }
			else if (name == "try") { return new TryNode(); }
			else if (name == "floor") { return new FloorNode(); }
			else if (name == "ceil") { return new CeilingNode(); }
			else if (name == "lower") { return new LowerNode(); }
			else if (name == "upper") { return new UpperNode(); }
			else if (name == "titlecase") { return new TitleCaseNode(); }
			else if (name == "join") { return new StringJoinNode(); }
			else if (name == "split") { return new StringSplitNode(); }
			else if (name == "format") { return new StringFormatNode(); }
			else if (name == "min") { return new MinVarNode(); }
			else if (name == "max") { return new MaxVarNode(); }
			else if (name == "sum") { return new SumNode(); }
			else if (name == "int") { return new IntCastNode(); }
			else if (name == "float") { return new FloatCastNode(); }
			else if (name == "bool") { return new BoolCastNode(); }
			else if (name == "str" || name == "string") { return new StringCastNode(); }
			else if (name == "color") { return new ColorCreateNode(); }
			else if (name == "array") { return new ArrayCreateNode(); }
			else if (name == "range") { return new RangeNode(); }
			else if (name == "contains") { return new ArrayContainsNode(); }
			else if (name == "all") { return new ArrayAllNode(); }
			else if (name == "any") { return new ArrayAnyNode(); }
			else if (name == "sort") { return new ArraySortNode(); }
			else if (name == "reverse") { return new ArrayReverseNode(); }
			else if (name == "sin") { return new SinNode(); }
			else if (name == "cos") { return new CosNode(); }
			else if (name == "tan") { return new TanNode(); }
			else if (name == "asin") { return new AsinNode(); }
			else if (name == "acos") { return new AcosNode(); }
			else if (name == "atan") { return new AtanNode(); }
			else if (name == "atan2") { return new Atan2Node(); }
			else if (name == "sinh") { return new SinhNode(); }
			else if (name == "cosh") { return new CoshNode(); }
			else if (name == "tanh") { return new TanhNode(); }
			else if (name == "lerp") { return new LerpNode(); }
			else if (name == "sqrt") { return new SquareRootNode(); }
			else if (name == "random") { return new RandomNode(); }
			else if (variables.TryGetFunctionInfo(name, out EnvironmentFunctionInfo? functionInfo)) {
				return new EnvironmentFunctionNode(functionInfo);
			}
			else { throw new UndefinedFunctionException($"Unrecognized function: {name}"); }
			*/
		}

		/// <summary></summary>
		/// <exception cref="EvaluationSyntaxException"></exception>
		private static object ParseFloat(string token) {
			try {
				float value = float.Parse(token);
				if (value >= 0f) { return new UFloat(value); }
				else { return value; }
			}
			catch(FormatException e) {
				throw new EvaluationSyntaxException($"\"{token}\" is not a valid float value.", e);
			}
		}
		/// <summary></summary>
		/// <exception cref="EvaluationSyntaxException"></exception>
		private static object ParseInt(string token) {
			try {
				int value = int.Parse(token);
				if (value >= 0) { return (uint)value; }
				else { return value; }
			}
			catch (FormatException e) {
				throw new EvaluationSyntaxException($"\"{token}\" is not a valid int value.", e);
			}
		}
		/// <summary></summary>
		/// <exception cref="EvaluationSyntaxException"></exception>
		private static bool ParseBool(string token) {
			try {
				return bool.Parse(token);
			}
			catch (FormatException e) {
				throw new EvaluationSyntaxException($"\"{token}\" is not a valid bool value.", e);
			}
		}
		/// <summary></summary>
		/// <exception cref="EvaluationSyntaxException"></exception>
		private static string ParseString(string token) {
			try {
				return StringParsing.Parse(token);
			}
			catch (FormatException e) {
				throw new EvaluationSyntaxException($"\"{token}\" is not a valid string value.", e);
			}
		}

		private static readonly RegexChunker tokenRegex = new RegexChunker(new Regex(@"
			(?<comprehension>for \s+ \$?(?<compvar>[a-z][a-z0-9]*) \s+ in)
			|
			(?<if>(?<=\s|^|\b)if(?=\s|$|\b)) # For use in comprehensions
			|
			(?<andor>(?<=\s|^|\b)(?:and|or)(?=\s|$|\b)) # Text versions of Boolean operators
			|
			\.(?<accessor>[a-z_][a-z0-9_]*)
			|
			(?<value>
				(?<float>[0-9]+\.[0-9]*) # (?<float>((?<![0-9\)])\-\s*)?[0-9]+\.[0-9]*)
				|
				(?<int>[0-9]+) # (?<int>((?<![0-9\)])\-\s*)?[0-9]+)
				|
				(?<bool>true|false)
				|
				\""(?<string>(?:[^\""\\]|\\.)*)\""
				|
				\$(?<variable>[a-z][a-z0-9]*)
			)
			#|
			#\[(?<indexer>\-?[0-9]+\:\-?[0-9]+|\:\-?[0-9]+|\-?[0-9]+\:|\-?[0-9]+|\:)\]
			|
			(?<function>[a-z][a-z0-9]*)(?=\s*\()
			|
			(?<rawvariable>[a-z][a-z0-9]*)
			|
			(?<operator>\*\*|\*|\/|\%|\+|\-|\<\=|\>\=|\<|\>|\=\=|\!\=|\&\&|\&|\^|\|\||\||\!|\?\?|\?|\:)
			|
			(?<openbrace>\()
			|
			(?<closebrace>\))
			|
			(?<openindexer>\[)
			|
			(?<closeindexer>\])
			|
			(?<comma>\,)
			",
			RegexOptions.IgnorePatternWhitespace | RegexOptions.IgnoreCase), true);

		private enum ParseExpressionState { START, OPERATOR, FUNCTION, VALUE }
		private enum ParseState { OUTER, FUNCTION, INDEXER }

		/// <summary></summary>
		/// <exception cref="EvaluationSyntaxException"></exception>
		/// <exception cref="EvaluationProcessingException"></exception>
		/// <exception cref="EvaluationTypeException"></exception>
		/// <exception cref="UndefinedVariableException"></exception>
		/// <exception cref="UndefinedFunctionException"></exception>
		public static EvaluationNode Parse(string expression, IVariableBox variables) {
			// With help from: https://blog.kallisti.net.nz/2008/02/extension-to-the-shunting-yard-algorithm-to-allow-variable-numbers-of-arguments-to-functions/

			List<EvaluationNode> output = new List<EvaluationNode>();
			Stack<OperatorNode> operators = new Stack<OperatorNode>();

			//expression = expression.Replace(" ", "");

			Stack<bool> wereValues = new Stack<bool>();
			Stack<int> argCount = new Stack<int>();

			ParseExpressionState previousExpression = ParseExpressionState.START;
			Stack<ParseState> state = new Stack<ParseState>();
			state.Push(ParseState.OUTER);

			/*
			Console.WriteLine(expression + "\n");
			string GetString(EvaluationNode node) {
				try {
					if (node is OpenBraceNode) { return "("; }
					else if (node is OpenIndexerNode) { return "["; }
					else if (node is IndexerNode) { return "[]"; }
					else if (node is IndexerSliceNode) { return "[:]"; }
					else if (node is BinaryOperatorNode binary) { return binary.Symbol; }
					else if (node is UnaryOperatorNode unary) { return unary.Symbol + "()"; }
					else if (node is VariablePlaceholderNode placeholderNode) { return "$" + placeholderNode.Key; }
					else if (node is ValueNode) { return node.ToString(); }
				}
				catch (Exception) { }
				return node.GetType().Name.Split('+').Last();
			}
			void PrintState(string token) {
				Console.WriteLine("Token: " + token);
				Console.WriteLine("Output:    " + string.Join(" ", output.Select(GetString)) + $" ({output.Count})");
				Console.WriteLine("Operators: " + string.Join(" ", operators.Select(GetString)) + $" ({operators.Count})");
				Console.WriteLine("Were Vals: " + string.Join(" ", wereValues) + $" ({wereValues.Count})");
				Console.WriteLine("Arg Count: " + string.Join(" ", argCount) + $" ({argCount.Count})");
				Console.WriteLine("State: " + state.Peek());
				Console.WriteLine("Expression type: " + previousExpression);
				Console.WriteLine();
			}
			*/

			Match[] tokenMatches;
			try {
				tokenMatches = tokenRegex.Matches(expression.Trim()).ToArray();
			}
			catch(FormatException e) {
				throw new EvaluationSyntaxException("Could not parse expression into tokens.", e);
			}

			try {
				foreach (Match match in tokenMatches) {
					//string token = tokens[i];

					if (match.Groups["value"].Success || match.Groups["rawvariable"].Success) {
						if (match.Groups["float"].Success) {
							output.Add(new ConstantNode(ParseFloat(match.Value)));
						}
						else if (match.Groups["int"].Success) {
							output.Add(new ConstantNode(ParseInt(match.Value)));
						}
						else if (match.Groups["bool"].Success) {
							output.Add(new ConstantNode(ParseBool(match.Value)));
						}
						else if (match.Groups["string"].Success) {
							output.Add(new ConstantNode(ParseString(match.Groups["string"].Value)));
						}
						else if (match.Groups["variable"].Success || match.Groups["rawvariable"].Success) {
							string key;
							if (match.Groups["variable"].Success) {
								key = match.Groups["variable"].Value;
							}
							else {
								key = match.Groups["rawvariable"].Value;
							}
							//output.Add(new VariableNode(key, variables.GetReturnType(key)));
							//output.Add(new WrapperNode(variables.GetNode(key)));
							output.Add(new VariablePlaceholderNode(key));
						}

						if (wereValues.Count > 0) {
							wereValues.Pop();
							wereValues.Push(true);
						}

						previousExpression = ParseExpressionState.VALUE;
					}
					else if (match.Groups["function"].Success) {
						operators.Push(GetFunction(match.Groups["function"].Value, variables));

						argCount.Push(0);
						if (wereValues.Count > 0) {
							wereValues.Pop();
							wereValues.Push(true);
						}
						wereValues.Push(false);

						previousExpression = ParseExpressionState.FUNCTION;
						state.Push(ParseState.FUNCTION);
					}
					else if (state.Count > 0 && operators.Count > 0 && state.Peek() == ParseState.INDEXER && match.Value == ":" && operators.Peek().GetType() != typeof(ConditionalOperatorNode.ConditionalOpenNode)) {
						// This is for array slicing ":" operators

						while (operators.Count > 0 && operators.Peek().GetType() != typeof(OpenIndexerNode)) {
							output.Add(operators.Pop());
						}
						if (operators.Count == 0 || operators.Peek().GetType() != typeof(OpenIndexerNode)) {
							throw new EvaluationSyntaxException("Invalid slicing indexer.");
						}

						// Replace OpenIndexerNode with IndexerSlicePlaceholder
						operators.Pop();
						operators.Push(new IndexerSlicePlaceholder());

						previousExpression = ParseExpressionState.START;
					}
					else if (match.Groups["operator"].Success || match.Groups["accessor"].Success || match.Groups["comprehension"].Success || match.Groups["if"].Success || match.Groups["andor"].Success) {
						// Except for the null-coalescing operator, all allowed binary operators are left-associative

						ParseExpressionState nextState = ParseExpressionState.OPERATOR;

						OperatorNode operatorNode;
						if (match.Groups["accessor"].Success) {
							string fieldName = match.Groups["accessor"].Value;
							operatorNode = new FieldAccessNode(fieldName);
							nextState = ParseExpressionState.VALUE;
						}
						else if (match.Groups["comprehension"].Success) {
							string loopVariable = match.Groups["compvar"].Value;
							operatorNode = new ComprehensionNode(loopVariable);
						}
						else if (match.Groups["if"].Success) {
							operatorNode = new ComprehensionIfNode();
						}
						else if (TryGetUnaryOperator(match.Value, out UnaryOperatorNode? opNode) && (previousExpression == ParseExpressionState.START || previousExpression == ParseExpressionState.OPERATOR)) {
							operatorNode = opNode;
						}
						else { // This will cover "andor" group as well
							operatorNode = GetOperator(match.Value);
						}

						if (operatorNode.Associativity == Associativity.LEFT) {
							while (operators.Count > 0 && operators.Peek().Precedence <= operatorNode.Precedence && operators.Peek().GetType() != typeof(OpenBraceNode) && operators.Peek().GetType() != typeof(OpenIndexerNode)) {
								output.Add(operators.Pop());
							}
						}
						else {
							while (operators.Count > 0 && operators.Peek().Precedence < operatorNode.Precedence && operators.Peek().GetType() != typeof(OpenBraceNode) && operators.Peek().GetType() != typeof(OpenIndexerNode)) {
								output.Add(operators.Pop());
							}
						}

						if (operatorNode is TernaryOperatorNode ternary) {
							if (operators.Count > 0 && ternary.OpeningType.IsAssignableFrom(operators.Peek().GetType())) {
								OperatorNode openingNode = operators.Pop();
								ternary.AssignOpening(openingNode);
							}
							// Should there be an error here otherwise?
						}

						if (operatorNode is FieldAccessNode) {
							output.Add(operatorNode); // There must be a more general rule at play here?
						}
						else {
							operators.Push(operatorNode);
						}

						previousExpression = nextState;
					}
					else if (match.Groups["openbrace"].Success) {
						operators.Push(new OpenBraceNode());

						previousExpression = ParseExpressionState.START;
					}
					else if (match.Groups["closebrace"].Success) {
						while (operators.Count > 0 && operators.Peek().GetType() != typeof(OpenBraceNode)) {
							output.Add(operators.Pop());
						}

						if (operators.Count > 0 && operators.Peek().GetType() == typeof(OpenBraceNode)) {
							operators.Pop();
						}
						else {
							throw new EvaluationSyntaxException("Unbalanced brackets.");
						}

						if (operators.Count > 0 && typeof(EnvironmentFunctionNode).IsAssignableFrom(operators.Peek().GetType())) {
							EnvironmentFunctionNode func = (EnvironmentFunctionNode)operators.Pop();
							int a = argCount.Pop();
							bool w = wereValues.Pop();
							if (w) { a++; }
							func.SetArgumentCount(a);
							output.Add(func);
							state.Pop();
						}

						previousExpression = ParseExpressionState.VALUE;
					}
					else if (match.Groups["openindexer"].Success) {
						OperatorNode openIndexNode = new OpenIndexerNode();

						/*
						while (operators.Count > 0 && operators.Peek().Precedence <= openIndexNode.Precedence && operators.Peek().GetType() != typeof(OpenBraceNode) && !(operators.Peek().GetType() == typeof(IndexerNode) || operators.Peek().GetType() == typeof(IndexerSliceNode))) {
							// This feels very hacky and weird... is this right?
							output.Add(operators.Pop());
						}
						*/

						operators.Push(openIndexNode);

						previousExpression = ParseExpressionState.START;
						state.Push(ParseState.INDEXER);
					}
					else if (match.Groups["closeindexer"].Success) {
						/*
						while (operators.Count > 0 && operators.Peek().Precedence <= OpenIndexerNode.IndexerClosePrecedence && operators.Peek().GetType() != typeof(OpenBraceNode) && operators.Peek().GetType() != typeof(OpenIndexerNode) && operators.Peek().GetType() != typeof(IndexerSlicePlaceholder)) {
							output.Add(operators.Pop());
						}
						*/
						while (operators.Count > 0 && operators.Peek().GetType() != typeof(OpenIndexerNode) && operators.Peek().GetType() != typeof(IndexerSlicePlaceholder)) {
							output.Add(operators.Pop());
						}

						if (operators.Count > 0 && operators.Peek().GetType() == typeof(IndexerSlicePlaceholder)) {
							operators.Pop();
							output.Add(new IndexerSliceNode());
						}
						else if (operators.Count > 0 && operators.Peek().GetType() == typeof(OpenIndexerNode)) {
							operators.Pop();
							output.Add(new IndexerNode());
						}
						else {
							throw new EvaluationSyntaxException("Unbalanced square brackets.");
						}

						previousExpression = ParseExpressionState.VALUE;
						state.Pop();
					}
					else if (match.Groups["comma"].Success) {
						try {
							while (operators.Count > 0 && operators.Peek().GetType() != typeof(OpenBraceNode)) {
								output.Add(operators.Pop());
							}
							bool w = wereValues.Pop();
							if (w) {
								int a = argCount.Pop();
								a++;
								argCount.Push(a);
							}
							wereValues.Push(false);

							previousExpression = ParseExpressionState.START;
						}
						catch (InvalidOperationException) {
							throw new EvaluationSyntaxException("Unexpected comma in expression.");
						}
					}
					//PrintState(match.Value);
				}
				while (operators.Count > 0) {
					output.Add(operators.Pop());
					//PrintState("EMPTY");
				}
			}
			catch (InvalidOperationException e) {
				throw new EvaluationSyntaxException("Invalid expression.", e);
			}

			EvaluationNode resultNode;
			try {
				Stack<EvaluationNode> nodeStack = new Stack<EvaluationNode>();
				for (int i = 0; i < output.Count; i++) {
					EvaluationNode node = output[i];
					if (node is UnaryOperatorNode unaryNode) {
						unaryNode.Operand = nodeStack.Pop();
						nodeStack.Push(unaryNode);
					}
					else if (node is BinaryOperatorNode binaryNode) {
						binaryNode.Second = nodeStack.Pop();
						binaryNode.First = nodeStack.Pop();
						nodeStack.Push(binaryNode);
					}
					else if (node is TernaryOperatorNode ternary) {
						ternary.Third = nodeStack.Pop();
						ternary.Second = nodeStack.Pop();
						ternary.First = nodeStack.Pop();
						nodeStack.Push(ternary);
					}
					else if (node is EnvironmentFunctionNode funcNode) {
						for (int arg = funcNode.Operands - 1; arg >= 0; arg--) {
							funcNode.Arguments[arg] = nodeStack.Pop();
						}
						nodeStack.Push(funcNode);
					}
					else if (node is ValueNode valueNode) {
						nodeStack.Push(valueNode);
					}
					else {
						throw new EvaluationSyntaxException($"Unexpected node type: {node.GetType().Name}");
					}
				}
				resultNode = nodeStack.Pop();

				if (nodeStack.Count > 0) {
					throw new EvaluationSyntaxException("Invalid expression parse.");
				}
			}
			catch(InvalidOperationException e) {
				throw new EvaluationSyntaxException("Invalid expression.", e);
			}

			EvaluationNode ReplaceVariableNodes(EvaluationNode node, List<IVariableProvider> providers) {
				if(node is IVariableProvider provider) {
					providers = new List<IVariableProvider>(providers) {
						provider
					};
				}

				if (node is VariablePlaceholderNode placeholderNode) {
					Dictionary<EvaluationName, EvaluationType> definedVariables = providers.SelectMany(p => p.ProvidedVariables()).ToDictionary();
					
					if(definedVariables.TryGetValue(placeholderNode.Key, out EvaluationType? returnType)) {
						return new VariableNode(placeholderNode.Key, returnType);
					}
					else {
						return variables.GetNode(placeholderNode.Key);
					}
					
					//IVariableBox nodeVariables = variables.AppendVariables(definedVariables); // TODO Unnecessary if definedVariables.Count==0
					//return nodeVariables.GetNode(placeholderNode.Key);
				}
				else {
					if (node is UnaryOperatorNode unaryNode) {
						unaryNode.Operand = ReplaceVariableNodes(unaryNode.Operand, providers);
					}
					else if (node is BinaryOperatorNode binaryNode) {
						for(int i=0; i<2; i++) {
							binaryNode[binaryNode.CalculationOrder[i]] = ReplaceVariableNodes(binaryNode[binaryNode.CalculationOrder[i]], providers);
						}
					}
					else if (node is TernaryOperatorNode ternary) {
						for (int i = 0; i < 3; i++) {
							ternary[ternary.CalculationOrder[i]] = ReplaceVariableNodes(ternary[ternary.CalculationOrder[i]], providers);
						}
					}
					else if (node is EnvironmentFunctionNode funcNode) {
						for (int arg = funcNode.Operands - 1; arg >= 0; arg--) {
							funcNode.Arguments[arg] = ReplaceVariableNodes(funcNode.Arguments[arg], providers);
						}
					}

					return node;
				}
			}

			//resultNode.Print(0, SimpleEnvironment.Empty);

			resultNode = ReplaceVariableNodes(resultNode, new List<IVariableProvider>());

			_ = resultNode.ReturnType; // Run this to ensure that no errors are thrown from badly formed expressions later

			try {
				resultNode = resultNode.Simplify();
			}
			catch(EvaluationCalculationException e) {
				throw new EvaluationProcessingException("Invalid expression.", e); // Better error message?
			}

			//Console.WriteLine(resultNode);

			return resultNode;
		}

		private class OpenBraceNode : OperatorNode {
			public override bool IsConstant => true;
			public override EvaluationType ReturnType => throw new NotImplementedException();
			public sealed override int Operands { get { return 0; } }
			public sealed override int Precedence => -2;
			public sealed override Associativity Associativity => throw new NotImplementedException();
			public override object Evaluate(IEnvironment environment) { throw new NotImplementedException(); }
			public override EvaluationNode Simplify() { throw new NotImplementedException(); }
			public override EvaluationNode Clone() { return new OpenBraceNode(); }
			public override IEnumerable<EvaluationName> GetVariables() { throw new NotImplementedException(); }
			//public override void Print(int indent, IEnvironment environment) { throw new NotImplementedException(); }
			protected override string GetRepresentation() { throw new NotImplementedException(); }
		}

		private class OpenIndexerNode : OperatorNode {
			public override bool IsConstant => true;
			public override EvaluationType ReturnType => throw new NotImplementedException();
			public sealed override int Operands { get { return 0; } }
			public sealed override int Precedence => -2;
			public sealed override Associativity Associativity => throw new NotImplementedException();
			public override object Evaluate(IEnvironment environment) { throw new NotImplementedException(); }
			public override EvaluationNode Simplify() { throw new NotImplementedException(); }
			public override EvaluationNode Clone() { return new OpenIndexerNode(); }
			public override IEnumerable<EvaluationName> GetVariables() { throw new NotImplementedException(); }
			//public override void Print(int indent, IEnvironment environment) { throw new NotImplementedException(); }
			protected override string GetRepresentation() { throw new NotImplementedException(); }
		}

		private class IndexerSlicePlaceholder : OperatorNode {
			public override bool IsConstant => true;
			public override EvaluationType ReturnType => throw new NotImplementedException();
			public sealed override int Operands { get { return 2; } }
			public sealed override int Precedence => 13;
			public sealed override Associativity Associativity => throw new NotImplementedException();
			public override object Evaluate(IEnvironment environment) { throw new NotImplementedException(); }
			public override EvaluationNode Simplify() { throw new NotImplementedException(); }
			public override EvaluationNode Clone() { return new IndexerSlicePlaceholder(); }
			public override IEnumerable<EvaluationName> GetVariables() { throw new NotImplementedException(); }
			//public override void Print(int indent, IEnvironment environment) { throw new NotImplementedException(); }
			protected override string GetRepresentation() { throw new NotImplementedException(); }
		}

		private class VariablePlaceholderNode : ValueNode {
			#pragma warning disable GJT0001 // Unhandled thrown exception from statement
			public override bool IsConstant => throw new UndefinedVariableException(Key);
			public override EvaluationType ReturnType => throw new UndefinedVariableException(Key);
			#pragma warning restore GJT0001 // Unhandled thrown exception from statement

			public EvaluationName Key { get; }

			public VariablePlaceholderNode(EvaluationName key) {
				this.Key = key;
			}

			public override EvaluationNode Clone() { return new VariablePlaceholderNode(Key); }

			public override object Evaluate(IEnvironment environment) => throw new NotImplementedException();
			public override IEnumerable<EvaluationName> GetVariables() => throw new NotImplementedException();

			protected override string GetRepresentation() {
				return "$" + Key;
			}

			/*
			public override void Print(int indent, IEnvironment environment) {
				Console.WriteLine(new string(' ', indent * 4) + $"Variable: {Key}");
			}
			*/
		}
	}
}
