using SharpSheets.Evaluations;
using SharpSheets.Evaluations.Types;
using SharpSheets.Markup.Elements;
using SharpSheets.Parsing;
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

		/// <param name="name">The name for this argument. This name will be visible to
		/// the user, and by default will also be the variable name for this argument
		/// in the Markup, unless the <paramref name="variable"/> attribute is specified.</param>
		/// <param name="type">The type for this argument. This determines how the data
		/// provided to this argument will be processed and made available in the Markup.</param>
		/// <param name="variable">An optional variable name, which (if provided) will be used
		/// in place of the <paramref name="name"/> as the variable handle for this argument
		/// in the Markup.</param>
		/// <param name="desc">A description for this argument, which will be presented to
		/// the user.</param>
		/// <param name="optional">A flag to indicate that this argument is optional. If this
		/// attribute is true and the argument does not have a <paramref name="default_"/> value,
		/// it is important to check that the value is present before using it.</param>
		/// <param name="default_">An optional default value for this argument, which must be
		/// valid data of the specified <paramref name="type"/>.</param>
		/// <param name="example">An optional example value for this argument, which will be used
		/// when displaying the pattern in the designer and in documentation, but will not be used
		/// as a default when the pattern is utilised by a user.</param>
		/// <param name="validate">A validation test for this argument, which may only use the
		/// current variable. If this evaluates to false, then the default value will be used instead,
		/// and an error message will be displayed to the user.</param>
		/// <param name="validate_message">An error message to display to the user when the
		/// <paramref name="validate"/> test evaluates to false.</param>
		/// <param name="local">A flag to indicate that argument must be explicitly specified for the
		/// entry in the configuration file, and may not be inherited.</param>
		/// <param name="format">The format for this argument, allowing for arguments which
		/// utilise entry data from the configuration file, or similar.</param>
		[FactoryBuilder(typeof(MarkupSingleArgument), Name = "arg")]
		public static MarkupSingleArgument Build(
				[LocalProperty] EvaluationName name, [LocalProperty] EvaluationType type,
				[LocalProperty] EvaluationName? variable = null,
				[LocalProperty] string? desc = null,
				[LocalProperty] bool optional = false,
				[LocalProperty] object? default_ = null,
				[LocalProperty] object? example = null,
				[LocalProperty] BoolExpression? validate = null,
				[LocalProperty] string? validate_message = null,
				[LocalProperty] bool local = false,
				[LocalProperty] MarkupArgumentFormat format = MarkupArgumentFormat.DEFAULT
			) {

			return new MarkupSingleArgument(name, type, variable, desc, optional, default_, example, validate, validate_message, local, format);
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
		/// <param name="type" exclude="True"></param>
		/// <param name="_variable">An optional variable name, which (if provided) will be used
		/// in place of the <paramref name="_name"/> as the variable handle for this argument group
		/// in the Markup.</param>
		/// <param name="_desc">A description for this argument, which will be presented to
		/// the user.</param>
		/// <param name="args">The arguments inside this grouping.</param>
		public MarkupGroupArgument(EvaluationName _name, EvaluationType type, EvaluationName? _variable = null, string? _desc = null, IEnumerable<IMarkupArgument>? args = null) {
			ArgumentName = _name;
			variableName = _variable;
			Description = _desc;
			Args = (args ?? Enumerable.Empty<IMarkupArgument>()).ToArray(); ;
			Type = type; // MakeGroupType(VariableName.ToString(), Args);
		}

		/// <param name="name">The name for this argument group. This name will be visible to
		/// the user, and by default will also be the variable name for this argument group
		/// in the Markup, unless the <paramref name="variable"/> attribute is specified.</param>
		/// <param name="type" exclude="True"></param>
		/// <param name="variable">An optional variable name, which (if provided) will be used
		/// in place of the <paramref name="name"/> as the variable handle for this argument group
		/// in the Markup.</param>
		/// <param name="desc">A description for this argument, which will be presented to
		/// the user.</param>
		/// <param name="args">The arguments inside this grouping.</param>
		[FactoryBuilder(typeof(MarkupGroupArgument), Name = "grouparg")]
		public static MarkupGroupArgument Build(
				[LocalProperty] EvaluationName name,
				[Property(Exclude = true)] EvaluationType type,
				[LocalProperty] EvaluationName? variable = null,
				[LocalProperty] string? desc = null,
				[Property(Exclude = true)] IEnumerable<IMarkupArgument>? args = null
			) {

			return new MarkupGroupArgument(name, type, variable, desc, args);
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

		/// <param name="test">A test to be run on one or more of the pattern argument values.
		/// An error message will be displayed if this expression evaluates to false.</param>
		/// <param name="message">A message to be displayed to the user if the test evaluates to
		/// false.</param>
		[FactoryBuilder(typeof(MarkupValidation), Name = "validation")]
		public static MarkupValidation Build(
				[LocalProperty] BoolExpression test,
				[LocalProperty] string? message = null
			) {

			return new MarkupValidation(test, message);
		}

		public bool Evaluate(IEnvironment environment) {
			return Test.Evaluate(environment);
		}

	}

}
