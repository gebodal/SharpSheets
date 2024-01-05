using SharpSheets.Evaluations;
using SharpSheets.Evaluations.Nodes;
using System.Collections.Generic;
using System.Linq;

namespace SharpSheets.Markup.Elements {

	/*
	public interface IMarkupVariable {
		EvaluationName Name { get; }
		EvaluationType Type { get; }
	}
	*/

	/// <summary>
	/// This element represents a Markup variable.
	/// </summary>
	[System.Diagnostics.DebuggerDisplay("{Name} ({Type}) = {Evaluation}")]
	public class MarkupVariable : IMarkupElement {
		public EvaluationName Name { get; }
		public EvaluationNode Evaluation { get; }
		public EvaluationType Type { get; }

		/// <summary>
		/// Constructor for MarkupVariable.
		/// </summary>
		/// <param name="name">The name for this variable, which will be used as its variable
		/// handle in the Markup.</param>
		/// <param name="value">An expression for the value of this variable. The type of the
		/// variable will be inferred from the type of this expression.</param>
		/// <exception cref="EvaluationTypeException">Thrown when <paramref name="value"/> does not have a valid <see cref="EvaluationNode.ReturnType"/></exception>
		public MarkupVariable(EvaluationName name, EvaluationNode value) {
			Name = name;
			Evaluation = value;
			Type = Evaluation.ReturnType;
		}

		public static IVariableBox MakeVariableBox(IEnumerable<MarkupVariable> variables) {
			return VariableBoxes.Simple(variables.ToDictionary(v => v.Name, v => v.Evaluation));
		}
	}

}
