using SharpSheets.Utilities;
using System;

namespace SharpSheets.Evaluations.Nodes {

	public delegate object? EnvironmentFunction(object?[] arguments);

	public class EnvironmentFunctionInfo {

		public EvaluationName Name { get; }
		public EvaluationType ReturnType { get; }
		public EvaluationType[] Args { get; }
		public int ArgCount { get { return Args.Length; } }

		public EnvironmentFunctionInfo(EvaluationName name, EvaluationType returnType, EvaluationType[] args) {
			this.Name = name;
			this.ReturnType = returnType;
			this.Args = args;
		}

	}

	public class EnvironmentFunctionDefinition : EnvironmentFunctionInfo { // TODO Needs better name

		public EnvironmentFunction Evaluator { get; }

		public EnvironmentFunctionDefinition(EvaluationName name, EvaluationType returnType, EvaluationType[] args, EnvironmentFunction evaluator) : base(name, returnType, args) {
			this.Evaluator = evaluator;
		}

	}

	public class EnvironmentFunctionNode : VariableArgsFunctionNode {
		private readonly EnvironmentFunctionInfo functionInfo;
		public override string Name => functionInfo.Name.ToString();

		public override bool IsConstant { get; } = false;

		public override EvaluationType ReturnType {
			get {
				for (int i = 0; i < functionInfo.Args.Length; i++) {
					if (functionInfo.Args[i].IsEnum && !(functionInfo.Args[i].DisplayType?.IsEnum ?? false)) {
						throw new EvaluationTypeException("Enum types for environment functions must be based on system types.");
					}

					// TODO Check this works as expected
					EvaluationType argType = Arguments[i].ReturnType;
					if (functionInfo.Args[i] != argType && !(functionInfo.Args[i].IsReal() && argType.IsIntegral()) && !(functionInfo.Args[i].IsEnum && argType == EvaluationType.STRING)) {
						throw new EvaluationTypeException($"Invalid argument type for function {Name}: {argType} (expected {functionInfo.Args[i]})");
					}
				}
				return functionInfo.ReturnType;
			}
		}

		public EnvironmentFunctionNode(EnvironmentFunctionInfo functionInfo) {
			this.functionInfo = functionInfo;
		}

		public override void SetArgumentCount(int count) {
			if (count == functionInfo.ArgCount) {
				base.SetArgumentCount(count);
			}
			else {
				throw new EvaluationProcessingException($"Invalid number of arguments for function {Name}: {count} (expected {functionInfo.ArgCount})");
			}
		}

		public override object? Evaluate(IEnvironment environment) {
			EnvironmentFunctionDefinition func = environment.GetFunction(functionInfo.Name);

			//object[] args = Arguments.Select(a => a.Evaluate(environment)).ToArray();

			object?[] args = new object[Arguments.Length];
			for(int i=0; i<Arguments.Length; i++) {
				object? arg = Arguments[i].Evaluate(environment);

				if (functionInfo.Args[i].IsEnum) {
					arg = ParseEnumArg(functionInfo.Args[i], arg);
				}

				args[i] = arg;
			}

			object? result = func.Evaluator(args);

			return result;
			/*
			if (result.GetType() == ReturnType.DisplayType) {
				return result;
			}
			else {
				throw new EvaluationCalculationException($"Invalid return type for evaluation function {Name}: {result.GetType()} (expected {ReturnType})");
			}
			*/
		}

		private static object ParseEnumArg(EvaluationType type, object? arg) {
			// TODO What to do if the type does not have a SystemType?

			if(arg is null) {
				throw new EvaluationCalculationException("Cannot parse null value to enum.");
			}
			else if (arg.GetType() == type.DisplayType) { // Should this be DataType...?
				return arg;
			}
			else if (arg is string stringVal) {
				if (type.IsEnumValueDefined(stringVal)) {
					try {
						return EnumUtils.ParseEnum(type.DisplayType, stringVal);
					}
					catch (FormatException e) {
						throw new EvaluationCalculationException("Error parsing enum value.", e);
					}
				}
				else {
					throw new EvaluationCalculationException($"Invalid value for enum argument of type {type}: {stringVal} (must be one of {string.Join(", ", type.EnumNames ?? Enumerable.Empty<string>())})");
				}
			}
			else {
				throw new EvaluationCalculationException($"Invalid type for enum argument, must be string or {type.Name}, got {arg.GetType().Name}.");
			}
		}

		protected override VariableArgsFunctionNode MakeEmptyBase() {
			return new EnvironmentFunctionNode(functionInfo);
		}
	}

}
