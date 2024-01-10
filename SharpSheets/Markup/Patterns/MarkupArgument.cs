using SharpSheets.Evaluations;
using SharpSheets.Markup.Elements;
using System.Collections.Generic;
using System.Linq;

namespace SharpSheets.Markup.Patterns {

	public interface IMarkupArgument {
		EvaluationName ArgumentName { get; }
		EvaluationName VariableName { get; }
		EvaluationType Type { get; }

		bool FromEntries { get; }

		string? Description { get; }
	}

	/// <summary>
	/// Indicates the format that the Markup argument should take in the configuration file.
	/// </summary>
	public enum MarkupArgumentFormat {
		/// <summary>
		/// Indicates that the argument should be a single-line key-value pair or boolean flag.
		/// </summary>
		DEFAULT,
		/// <summary>
		/// Indicates that the argument should take a list of values from the entries of an entity in the configuration file.
		/// </summary>
		ENTRIES,
		/// <summary>
		/// Indicates that the argument should take a sequence of numbered properties from the configuration file.
		/// </summary>
		NUMBERED
	}

	/// <summary>
	/// This element represents a single Markup argument.
	/// </summary>
	public class MarkupSingleArgument : IMarkupArgument, IMarkupElement {

		public virtual EvaluationName ArgumentName { get; }
		private readonly EvaluationName? variableName;
		public virtual EvaluationName VariableName => variableName ?? ArgumentName;
		public EvaluationType Type { get; }
		public string? Description { get; }
		public bool IsOptional { get; }
		public object? DefaultValue { get; }
		public object? ExampleValue { get; }
		public BoolExpression? Validation { get; }
		public string? ValidationMessage { get; }
		public bool UseLocal { get; }
		public MarkupArgumentFormat ArgumentFormat { get; }

		public bool FromEntries => ArgumentFormat == MarkupArgumentFormat.ENTRIES;
		public bool IsNumbered => ArgumentFormat == MarkupArgumentFormat.NUMBERED;

		/// <summary>
		/// Constructor for MarkupSingleArgument.
		/// </summary>
		/// <param name="_name">The name for this argument. This name will be visible to
		/// the user, and by default will also be the variable name for this argument
		/// in the Markup, unless the <paramref name="_variable"/> attribute is specified.</param>
		/// <param name="_type">The type for this argument. This determines how the data
		/// provided to this argument will be processed and made available in the Markup.</param>
		/// <param name="_variable">An optional variable name, which (if provided) will be used
		/// in place of the <paramref name="_name"/> as the variable handle for this argument
		/// in the Markup.</param>
		/// <param name="_desc">A description for this argument, which will be presented to
		/// the user.</param>
		/// <param name="_optional">A flag to indicate that this argument is optional. If this
		/// attribute is true and the argument does not have a <paramref name="_default"/> value,
		/// it is important to check that the value is present before using it.</param>
		/// <param name="_default">An optional default value for this argument, which must be
		/// valid data of the specified <paramref name="_type"/>.</param>
		/// <param name="_example">An optional example value for this argument, which will be used
		/// when displaying the pattern in the designer and in documentation, but will not be used
		/// as a default when the pattern is utilised by a user.</param>
		/// <param name="_validate">A validation test for this argument, which may only use the
		/// current variable. If this evaluates to false, then the default value will be used instead,
		/// and an error message will be displayed to the user.</param>
		/// <param name="_validate_message">An error message to display to the user when the
		/// <paramref name="_validate"/> test evaluates to false.</param>
		/// <param name="_local">A flag to indicate that argument must be explicitly specified for the
		/// entry in the configuration file, and may not be inherited.</param>
		/// <param name="_format">The format for this argument, allowing for arguments which
		/// utilise entry data from the configuration file, or similar.</param>
		public MarkupSingleArgument(EvaluationName _name, EvaluationType _type,
				EvaluationName? _variable = null,
				string? _desc = null,
				bool _optional = false,
				object? _default = null,
				object? _example = null,
				BoolExpression? _validate = null,
				string? _validate_message = null,
				bool _local = false,
				MarkupArgumentFormat _format = MarkupArgumentFormat.DEFAULT
			) {

			ArgumentName = _name;
			variableName = _variable;
			Type = _type;
			Description = _desc;
			IsOptional = _optional;
			DefaultValue = _default;
			ExampleValue = _example;
			Validation = _validate;
			ValidationMessage = _validate_message;
			UseLocal = _local;
			ArgumentFormat = _format;
		}
	}

	/// <summary>
	/// This element represents a group of Markup arguments.
	/// </summary>
	public class MarkupGroupArgument : IMarkupArgument, IMarkupElement {
		public EvaluationName ArgumentName { get; }
		private readonly EvaluationName? variableName;
		public EvaluationName VariableName => variableName ?? ArgumentName;
		public IMarkupArgument[] Args { get; }

		public EvaluationType Type { get; }

		public bool FromEntries { get; } = false;

		public string? Description { get; }

		/// <summary>
		/// Constructor for MarkupGroupArgument.
		/// </summary>
		/// <param name="_name">The name for this argument group. This name will be visible to
		/// the user, and by default will also be the variable name for this argument group
		/// in the Markup, unless the <paramref name="_variable"/> attribute is specified.</param>
		/// <param name="_variable">An optional variable name, which (if provided) will be used
		/// in place of the <paramref name="_name"/> as the variable handle for this argument group
		/// in the Markup.</param>
		/// <param name="_desc">A description for this argument, which will be presented to
		/// the user.</param>
		/// <param name="args">The arguments inside this grouping.</param>
		public MarkupGroupArgument(EvaluationName _name, EvaluationName? _variable = null, string? _desc = null, IEnumerable<IMarkupArgument>? args = null) {
			ArgumentName = _name;
			variableName = _variable;
			Description = _desc;
			Args = (args ?? Enumerable.Empty<IMarkupArgument>()).ToArray(); ;
			Type = MakeGroupType(VariableName.ToString(), Args);
		}

		public static EvaluationType MakeGroupType(string name, IEnumerable<IMarkupArgument> args) {
			List<TypeField> fields = new List<TypeField>();

			foreach(IMarkupArgument arg in args) {
				TypeField field = new TypeField(arg.VariableName, arg.Type, obj => GroupFieldAccessor(obj, arg.VariableName));
				fields.Add(field);
			}

			return EvaluationType.CustomType(name, fields, typeof(Dictionary<EvaluationName, object>));
		}

		/// <summary></summary>
		/// <exception cref="UndefinedVariableException"></exception>
		/// <exception cref="EvaluationTypeException"></exception>
		private static object GroupFieldAccessor(object obj, EvaluationName name) {
			// TODO Should this be "Dictionary<EvaluationName, object?>"? (Cf. With argument parsing)
			if (obj is Dictionary<EvaluationName, object> values) {
				if(values.TryGetValue(name, out object? result)) {
					return result;
				}
				else {
					throw new UndefinedVariableException("Cannot find field value.");
				}
			}
			else {
				throw new EvaluationTypeException("Cannot access group field from invalid object.");
			}
		}
	}

	/// <summary>
	/// This element represents a validation test for a Markup pattern's arguments.
	/// If the provided test returns false, then an error message will be displayed
	/// to the user.
	/// </summary>
	public class MarkupValidation : IMarkupElement {

		public BoolExpression Test { get; }
		public string? Message { get; }

		/// <summary>
		/// Constructor for MarkupValidation.
		/// </summary>
		/// <param name="_test">A test to be run on one or more of the pattern argument values.
		/// An error message will be displayed if this expression evaluates to false.</param>
		/// <param name="_message">A message to be displayed to the user if the test evaluates to
		/// false.</param>
		public MarkupValidation(
				BoolExpression _test,
				string? _message = null
			) {

			Test = _test;
			Message = _message;
		}

		public bool Evaluate(IEnvironment environment) {
			return Test.Evaluate(environment);
		}

	}

	/*
	public interface IMarkupVariable {
		EvaluationName Name { get; }
		EvaluationType Type { get; }
	}
	*/

	/*
	public class PrefixedMarkupArgument : MarkupArgument {
		public MarkupArgument Basis { get; }

		private readonly string prefix;

		public override EvaluationName Name => $"{prefix}.{Basis.Name}";

		public PrefixedMarkupArgument(MarkupArgument basis, string prefix) : base(basis.Name, basis.Type, basis.Description, basis.DefaultValue, basis.IsOptional, basis.ExampleValue, basis.Validation, basis.ValidationMessage, basis.UseLocal, basis.FromEntries) {
			this.Basis = basis;
			this.prefix = prefix;
		}
	}
	*/

	/*
	public class MarkupArgumentOption : IMarkupElement {
		
		public virtual EvaluationName Name { get; }
		public object Value { get; }
		public string Description { get; }

		public MarkupArgumentOption(EvaluationName _name, object _value, string _description) {
			Name = _name;
			Value = _value;
			Description = _description;
		}

	}
	*/

}
