using System;

namespace SharpSheets.Evaluations {

	/// <summary>
	/// Indicates that an error has been encountered relating to evaluations (parsing and calculating).
	/// </summary>
	public abstract class EvaluationException : Exception {
		public EvaluationException(string message) : base(message) { }
		public EvaluationException(string message, Exception innerException) : base(message, innerException) { }
	}

	/// <summary>
	/// Indicates an error has occured whilst processing an evaluation during parsing.
	/// </summary>
	public class EvaluationProcessingException : EvaluationException {
		public EvaluationProcessingException(string message) : base(message) { }
		public EvaluationProcessingException(string message, Exception innerException) : base(message, innerException) { }
	}

	/// <summary>
	/// Indicates that a syntax error has been encountered in an evaluation string during parsing.
	/// </summary>
	public class EvaluationSyntaxException : EvaluationProcessingException {
		public EvaluationSyntaxException(string message) : base(message) { }
		public EvaluationSyntaxException(string message, Exception innerException) : base(message, innerException) { }
	}

	/// <summary>
	/// Indicates that an error has occured while calculating the result of an evaluation.
	/// </summary>
	public class EvaluationCalculationException : EvaluationException {
		public EvaluationCalculationException(string message) : base(message) { }
		public EvaluationCalculationException(string message, Exception innerException) : base(message, innerException) { }
	}

	/// <summary>
	/// Indicates that an invalid or unexpected evaluation type has been encountered, either during parsing or calculation.
	/// </summary>
	public class EvaluationTypeException : EvaluationException {
		public EvaluationTypeException(string message) : base(message) { }
		public EvaluationTypeException(string message, Exception innerException) : base(message, innerException) { }
	}

	/// <summary>
	/// Indicates that there is an undefined expression in an evaluation.
	/// </summary>
	public abstract class UndefinedException : EvaluationException {
		public UndefinedException(string message) : base(message) { }
	}

	/// <summary>
	/// Indicates than an undefined variable has been encountered in an evaluation.
	/// </summary>
	public class UndefinedVariableException : UndefinedException {
		public EvaluationName Key { get; }
		public UndefinedVariableException(EvaluationName key) : base($"Variable \"{key}\" not currently defined.") {
			this.Key = key;
		}
		public UndefinedVariableException(EvaluationName key, string message) : base(message) {
			this.Key = key;
		}
	}

	/// <summary>
	/// Indicates than an undefined function has been encountered in an evaluation.
	/// </summary>
	public class UndefinedFunctionException : UndefinedException {
		public EvaluationName Name { get; }
		public UndefinedFunctionException(EvaluationName name) : base($"Function \"{name}\" not currently defined.") {
			this.Name = name;
		}
		public UndefinedFunctionException(EvaluationName name, string message) : base(message) {
			this.Name = name;
		}
	}

}
