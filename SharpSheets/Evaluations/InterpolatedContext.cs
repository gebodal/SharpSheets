using SharpSheets.Parsing;
using SharpSheets.Exceptions;
using SharpSheets.Utilities;
using SharpSheets.Evaluations.Nodes;
using System.Text.RegularExpressions;
using System.Diagnostics.CodeAnalysis;

namespace SharpSheets.Evaluations {

	public class InterpolatedContext {

		public static readonly InterpolatedContext Empty = new InterpolatedContext(Context.Empty, true, null, true, Enumerable.Empty<ContextProperty<string>>(), Enumerable.Empty<ContextProperty<TextExpression>>(), Array.Empty<ContextValue<TextExpression>>());

		public static InterpolatedContext Parse(IContext originalContext, IVariableBox variables, bool pruneChildren, out SharpParsingException[] contextErrors, out IReadOnlyLineOwnership knownLineOwners) {
			LineOwnership lineOwners = new LineOwnership();
			InterpolatedContext parsed = Parse(originalContext, variables, pruneChildren, "For-each not allowed here.", lineOwners, out contextErrors);
			knownLineOwners = lineOwners;
			return parsed;
		}

		private static InterpolatedContext Parse(IContext originalContext, IVariableBox variables, bool pruneChildren, string? forEachErrorMessage, LineOwnership lineOwners, out SharpParsingException[] contextErrors) {

			List<SharpParsingException> errors = new List<SharpParsingException>();

			string? forEachStr = originalContext.GetProperty("foreach", true, originalContext, null, out DocumentSpan? forEachLocation);
			ContextForEach? forEach;
			if (forEachStr is not null) {
				if (forEachLocation.HasValue) { lineOwners.Add(forEachLocation.Value.Line, originalContext.Location.Line); }
				if (forEachErrorMessage is null) {
					try {
						forEach = ContextForEach.Parse(forEachStr, variables);
					}
					catch (EvaluationException e) {
						errors.Add(new SharpParsingException(forEachLocation, "Error parsing for-each: " + e.Message, e));
						forEach = null;
					}
					catch (FormatException e) {
						errors.Add(new SharpParsingException(forEachLocation, "Invalid for-each: " + e.Message, e));
						forEach = null;
					}
				}
				else {
					errors.Add(new SharpParsingException(forEachLocation, forEachErrorMessage));
					forEach = null;
				}
			}
			else {
				forEach = null;
			}

			IVariableBox fullContextVariables = forEach is not null ? forEach.GetVariables(variables) : variables;

			string? conditionStr = originalContext.GetProperty("condition", true, originalContext, null, out DocumentSpan? conditionLocation);
			BoolExpression condition;
			if (conditionStr is not null) {
				if (conditionLocation.HasValue) { lineOwners.Add(conditionLocation.Value.Line, originalContext.Location.Line); }
				try {
					condition = BoolExpression.Parse(conditionStr, fullContextVariables);
				}
				catch (EvaluationException e) {
					errors.Add(new SharpParsingException(conditionLocation, "Error parsing condition: " + e.Message, e));
					condition = true;
				}
			}
			else {
				condition = true;
			}



			List<ContextProperty<string>> simpleProperties = new List<ContextProperty<string>>();
			List<ContextProperty<TextExpression>> exprProperties = new List<ContextProperty<TextExpression>>();

			foreach (ContextProperty<string> property in originalContext.GetLocalProperties(null).Where(p => !SharpDocuments.StringComparer.Equals("condition", p.Name))) {
				
				try {
					TextExpression expr = Interpolation.Parse(property.Value, fullContextVariables, true);
					if (expr.IsConstant) {
						simpleProperties.Add(new ContextProperty<string>(property.Location, property.Name, property.ValueLocation, expr.Evaluate(Environments.Empty)));
					}
					else {
						exprProperties.Add(new ContextProperty<TextExpression>(property.Location, property.Name, property.ValueLocation, expr));
					}
				}
				catch (EvaluationException e) {
					errors.Add(new SharpParsingException(property.ValueLocation, e.Message, e));
				}
			}

			List<ContextValue<TextExpression>> entries = new List<ContextValue<TextExpression>>();

			foreach (ContextValue<string> entry in originalContext.GetEntries(null)) {
				try {
					TextExpression expr = Interpolation.Parse(entry.Value, fullContextVariables, true);
					entries.Add(new ContextValue<TextExpression>(entry.Location, expr));
				}
				catch (EvaluationException e) {
					errors.Add(new SharpParsingException(entry.Location, e.Message, e));
				}
			}

			InterpolatedContext context = new InterpolatedContext(originalContext, pruneChildren, forEach, condition, simpleProperties, exprProperties, entries);

			foreach (IContext child in originalContext.Children) {
				context.children.Add(Parse(child, fullContextVariables, pruneChildren, null, lineOwners, out SharpParsingException[] childErrors));
				errors.AddRange(childErrors);
			}
			foreach (KeyValuePair<string, IContext> namedChild in originalContext.NamedChildren) {
				context.namedChildren.Add(namedChild.Key, Parse(namedChild.Value, fullContextVariables, pruneChildren, "Named children cannot use for-each.", lineOwners, out SharpParsingException[] namedChildErrors));
				errors.AddRange(namedChildErrors);
			}

			contextErrors = errors.ToArray();
			return context;
		}

		public string SimpleName => OriginalContext.SimpleName;
		public string DetailedName => OriginalContext.DetailedName;
		public string FullName => OriginalContext.FullName;
		public DocumentSpan Location => OriginalContext.Location;
		public int Depth => OriginalContext.Depth;
		public IContext? Parent => OriginalContext.Parent;

		public IContext OriginalContext { get; }
		//public IVariableBox Variables { get; }
		public bool PruneChildren { get; }

		public ContextForEach? ForEach { get; }
		public BoolExpression Condition { get; }
		private readonly Dictionary<string, ContextProperty<string>> simpleProperties;
		private readonly Dictionary<string, ContextProperty<TextExpression>> exprProperties;
		private readonly List<ContextValue<TextExpression>> entries;

		private readonly List<InterpolatedContext> children;
		private readonly Dictionary<string, InterpolatedContext> namedChildren;

		private InterpolatedContext(IContext originalContext, bool pruneChildren, ContextForEach? forEach, BoolExpression condition, IEnumerable<ContextProperty<string>> simpleProperties, IEnumerable<ContextProperty<TextExpression>> exprProperties, IList<ContextValue<TextExpression>> exprEntries) {
			OriginalContext = originalContext;
			//Variables = variables;
			PruneChildren = pruneChildren;

			ForEach = forEach;
			Condition = condition;
			this.simpleProperties = new Dictionary<string, ContextProperty<string>>(simpleProperties.Select(p => new KeyValuePair<string, ContextProperty<string>>(p.Name, p)), SharpDocuments.StringComparer);
			this.exprProperties = new Dictionary<string, ContextProperty<TextExpression>>(exprProperties.Select(p => new KeyValuePair<string, ContextProperty<TextExpression>>(p.Name, p)), SharpDocuments.StringComparer);
			entries = exprEntries.ToList();

			children = new List<InterpolatedContext>();
			namedChildren = new Dictionary<string, InterpolatedContext>(SharpDocuments.StringComparer);
		}

		public bool IsConstant {
			get {
				return exprProperties.Count == 0 && entries.All(e => e.Value.IsConstant) && children.All(c => c.IsConstant) && namedChildren.Values.All(c => c.IsConstant);
			}
		}

		public IContext Evaluate(IEnvironment environment, out SharpParsingException[] contextErrors) {
			List<SharpParsingException> errors = new List<SharpParsingException>();
			EvaluatedContext result = EvaluateContext(environment, errors, null);
			contextErrors = errors.ToArray();
			return result;
		}

		public IEnumerable<EvaluationName> GetVariables() {
			return exprProperties.Values.SelectMany(p => p.Value.GetVariables())
				.Concat(
					entries.SelectMany(e => e.Value.GetVariables()),
					children.SelectMany(c => c.GetVariables()),
					namedChildren.Values.SelectMany(c => c.GetVariables())
					).Distinct();
		}

		private EvaluatedContext EvaluateContext(IEnvironment environment, List<SharpParsingException> errors, string? nameSuffix) {
			Dictionary<string, ContextProperty<string>> properties = new Dictionary<string, ContextProperty<string>>(simpleProperties, SharpDocuments.StringComparer);

			foreach (KeyValuePair<string, ContextProperty<TextExpression>> property in exprProperties) {
				try {
					string result = property.Value.Value.Evaluate(environment);
					properties.Add(property.Key, new ContextProperty<string>(property.Value.Location, property.Value.Name, property.Value.ValueLocation, result));
				}
				catch (EvaluationException e) {
					errors.Add(new SharpParsingException(property.Value.ValueLocation, e.Message, e));
				}
			}

			List<ContextValue<string>> entries = new List<ContextValue<string>>();

			foreach (ContextValue<TextExpression> entry in this.entries) {
				try {
					string result = entry.Value.Evaluate(environment);
					entries.Add(new ContextValue<string>(entry.Location, result));
				}
				catch (EvaluationException e) {
					errors.Add(new SharpParsingException(entry.Location, e.Message, e));
				}
			}

			EvaluatedContext context = new EvaluatedContext(OriginalContext, properties, entries, nameSuffix);

			foreach (InterpolatedContext child in children) {
				foreach ((int childIdx, IEnvironment childEnv) in (child.ForEach?.GetEnvironments(environment) ?? environment.Yield()).Enumerate()) {
					if (!PruneChildren || child.Condition.Evaluate(childEnv)) {
						context.children.Add(child.EvaluateContext(childEnv, errors, childIdx > 0 ? $"##{childIdx}" : null));
					}
				}
			}
			foreach (KeyValuePair<string, InterpolatedContext> namedChild in namedChildren) {
				if (!PruneChildren || namedChild.Value.Condition.Evaluate(environment)) {
					context.namedChildren.Add(namedChild.Key, namedChild.Value.EvaluateContext(environment, errors, null));
				}
			}

			return context;
		}

		private class EvaluatedContext : IContext {

			public IContext OriginalContext { get; }
			private readonly string? nameSuffix;

			private readonly Dictionary<string, ContextProperty<string>> properties;
			private readonly List<ContextValue<string>> entries;

			public readonly List<EvaluatedContext> children;
			public readonly Dictionary<string, EvaluatedContext> namedChildren;

			public EvaluatedContext(IContext originalContext, Dictionary<string, ContextProperty<string>> properties, List<ContextValue<string>> entries, string? nameSuffix) {
				OriginalContext = originalContext;
				this.properties = properties;
				this.entries = entries;

				children = new List<EvaluatedContext>();
				namedChildren = new Dictionary<string, EvaluatedContext>(SharpDocuments.StringComparer);

				this.nameSuffix = nameSuffix;
			}

			public string SimpleName => OriginalContext.SimpleName;
			public string DetailedName => OriginalContext.DetailedName; // + (nameSuffix ?? "");
			public string FullName => OriginalContext.FullName; // + (nameSuffix ?? "");
			public DocumentSpan Location => OriginalContext.Location;
			public int Depth => OriginalContext.Depth;

			public IContext? Parent => OriginalContext.Parent;
			public IEnumerable<IContext> Children => children;
			public IEnumerable<KeyValuePair<string, IContext>> NamedChildren => namedChildren.Select(kv => new KeyValuePair<string, IContext>(kv.Key, kv.Value));

			IDocumentEntity? IDocumentEntity.Parent => Parent;
			IEnumerable<IDocumentEntity> IDocumentEntity.Children => Children;

			public IEnumerable<ContextProperty<string>> GetLocalProperties(IContext? origin) => properties.Values;
			public IEnumerable<ContextProperty<bool>> GetLocalFlags(IContext? origin) => OriginalContext.GetLocalFlags(origin);
			public IEnumerable<ContextValue<string>> GetEntries(IContext? origin) => entries;
			public IEnumerable<ContextValue<string>> GetDefinitions(IContext? origin) => OriginalContext.GetDefinitions(origin); // TODO Is this right?

			public bool TryGetLocalProperty(string key, IContext? origin, bool isLocalRequest, [MaybeNullWhen(false)] out string property, out DocumentSpan? location) {
				if (OriginalContext.TryGetLocalProperty(key, origin, isLocalRequest, out _, out _) && properties.TryGetValue(key, out ContextProperty<string> value)) {
					property = value.Value;
					location = value.Location;
					return true;
				}
				else {
					property = null;
					location = null;
					return false;
				}
			}

			public bool TryGetLocalFlag(string key, IContext? origin, bool isLocalRequest, out bool flag, out DocumentSpan? location) {
				return OriginalContext.TryGetLocalFlag(key, origin, isLocalRequest, out flag, out location);
			}

			public bool TryGetLocalNamedChild(string name, IContext? origin, bool isLocalRequest, [MaybeNullWhen(false)] out IContext namedChild) {
				if (OriginalContext.TryGetLocalNamedChild(name, origin, isLocalRequest, out _) && namedChildren.TryGetValue(name, out EvaluatedContext? evaluatedNamedChild)) {
					namedChild = evaluatedNamedChild;
					return true;
				}
				else {
					namedChild = null;
					return false;
				}
			}
		}

	}

	public class ContextForEach {

		public readonly EnvironmentVariableInfo LoopVariable;
		private readonly EvaluationNode array;

		public ContextForEach(EvaluationName loopVariable, EvaluationNode array) {
			if (!(array.ReturnType.IsArray || array.ReturnType.IsTuple)) {
				throw new EvaluationTypeException("For-each expression must produce an array or tuple.");
			}

			LoopVariable = new EnvironmentVariableInfo(loopVariable, array.ReturnType.ElementType, null);
			this.array = array;
		}

		public IVariableBox GetVariables(IVariableBox variables) {
			return VariableBoxes.Concat(
				variables,
				SimpleVariableBoxes.Single(LoopVariable)
				);
		}

		public IEnumerable<IEnvironment> GetEnvironments(IEnvironment environment) {
			object? value = array.Evaluate(environment);
			if (EvaluationTypes.TryGetArray(value, out Array? arrayValue)) {
				foreach (object entry in arrayValue) {
					IEnvironment loopVarEnv = SimpleEnvironments.Single(LoopVariable, entry);
					yield return loopVarEnv.AppendEnvironment(environment);
				}
			}
		}

		private static readonly Regex forEachRegex = new Regex(@"
				^ \s*
				(?<loopVar>[a-z][a-z0-9]*)
				\s+ in \s+
				(?<expr>.+)
				$
				", RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);

		/// <summary>
		/// 
		/// </summary>
		/// <param name="text"></param>
		/// <param name="variables"></param>
		/// <returns></returns>
		/// <exception cref="FormatException"></exception>
		/// <exception cref="EvaluationException"></exception>
		public static ContextForEach Parse(string text, IVariableBox variables) {
			Match match = forEachRegex.Match(text);
			if (!match.Success) {
				throw new FormatException("Could not parse for-each.");
			}

			EvaluationName loopVar = new EvaluationName(match.Groups["loopVar"].Value);

			string exprText = match.Groups["expr"].Value.Trim();
			EvaluationNode expr = Evaluation.Parse(exprText, variables);

			return new ContextForEach(loopVar, expr);
		}

	}

}
