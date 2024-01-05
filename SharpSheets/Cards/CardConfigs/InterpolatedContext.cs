using SharpSheets.Evaluations;
using SharpSheets.Parsing;
using SharpSheets.Exceptions;
using SharpSheets.Utilities;

namespace SharpSheets.Cards.CardConfigs {

	public class InterpolatedContext : IExpression<IContext> {

		public static InterpolatedContext Parse(IContext originalContext, IVariableBox variables, bool pruneChildren) {

			string? conditionStr = originalContext.GetProperty("condition", true, null, null, out DocumentSpan? conditionLocation);

			BoolExpression condition;
			if(conditionStr is not null) {
				try {
					condition = BoolExpression.Parse(conditionStr, variables);
				}
				catch (EvaluationException) { }
				throw new SharpParsingException(conditionLocation, "Invalid condition.");
			}
			else {
				condition = true;
			}

			List<ContextProperty<string>> simpleProperties = new List<ContextProperty<string>>();
			List<ContextProperty<TextExpression>> exprProperties = new List<ContextProperty<TextExpression>>();

			foreach (ContextProperty<string> property in originalContext.GetLocalProperties(null).Where(p => !SharpDocuments.StringComparer.Equals("condition", p.Name))) {
				try {
					TextExpression expr = Interpolation.Parse(property.Value, variables, true);
					if (expr.IsConstant) {
						simpleProperties.Add(new ContextProperty<string>(property.Location, property.Name, property.ValueLocation, expr.Evaluate(Environments.Empty)));
					}
					else {
						exprProperties.Add(new ContextProperty<TextExpression>(property.Location, property.Name, property.ValueLocation, expr));
					}
				}
				catch (EvaluationException e) {
					throw new SharpParsingException(property.ValueLocation, e.Message, e);
				}
			}
			
			List<ContextValue<TextExpression>> entries = new List<ContextValue<TextExpression>>();

			foreach (ContextValue<string> entry in originalContext.GetEntries(null)) {
				try {
					TextExpression expr = Interpolation.Parse(entry.Value, variables, true);
					entries.Add(new ContextValue<TextExpression>(entry.Location, expr));
				}
				catch (EvaluationException e) {
					throw new SharpParsingException(entry.Location, e.Message, e);
				}
			}

			InterpolatedContext context = new InterpolatedContext(originalContext, variables, pruneChildren, condition, simpleProperties, exprProperties, entries);

			foreach(IContext child in originalContext.Children) {
				context.children.Add(Parse(child, variables, pruneChildren));
			}
			foreach(KeyValuePair<string, IContext> namedChild in originalContext.NamedChildren) {
				context.namedChildren.Add(namedChild.Key, Parse(namedChild.Value, variables, pruneChildren));
			}

			return context;
		}

		public IContext OriginalContext { get; }
		public IVariableBox Variables { get; }
		public bool PruneChildren { get; }

		public BoolExpression Condition { get; }
		private readonly Dictionary<string, ContextProperty<string>> simpleProperties;
		private readonly Dictionary<string, ContextProperty<TextExpression>> exprProperties;
		private readonly List<ContextValue<TextExpression>> entries;

		private readonly List<InterpolatedContext> children;
		private readonly Dictionary<string, InterpolatedContext> namedChildren;

		public InterpolatedContext(IContext originalContext, IVariableBox variables, bool pruneChildren, BoolExpression condition, IEnumerable<ContextProperty<string>> simpleProperties, IEnumerable<ContextProperty<TextExpression>> exprProperties, IList<ContextValue<TextExpression>> exprEntries) {
			OriginalContext = originalContext;
			Variables = variables;
			PruneChildren = pruneChildren;

			this.Condition = condition;
			this.simpleProperties = new Dictionary<string, ContextProperty<string>>(simpleProperties.Select(p => new KeyValuePair<string, ContextProperty<string>>(p.Name, p)), SharpDocuments.StringComparer);
			this.exprProperties = new Dictionary<string, ContextProperty<TextExpression>>(exprProperties.Select(p => new KeyValuePair<string, ContextProperty<TextExpression>>(p.Name, p)), SharpDocuments.StringComparer);
			this.entries = exprEntries.ToList();

			this.children = new List<InterpolatedContext>();
			this.namedChildren = new Dictionary<string, InterpolatedContext>(SharpDocuments.StringComparer);
		}

		public bool IsConstant {
			get {
				return exprProperties.Count == 0 && children.All(c => c.IsConstant) && namedChildren.Values.All(c => c.IsConstant);
			}
		}

		public IContext Evaluate(IEnvironment environment) {
			return EvaluateContext(environment);
		}

		public IEnumerable<EvaluationName> GetVariables() {
			return exprProperties.Values.SelectMany(p => p.Value.GetVariables())
				.Concat(
					children.SelectMany(c => c.GetVariables()),
					namedChildren.Values.SelectMany(c => c.GetVariables())
					).Distinct();
		}

		private EvaluatedContext EvaluateContext(IEnvironment environment) {
			Dictionary<string, ContextProperty<string>> properties = new Dictionary<string, ContextProperty<string>>(simpleProperties, SharpDocuments.StringComparer);

			foreach(KeyValuePair<string, ContextProperty<TextExpression>> property in exprProperties) {
				try {
					string result = property.Value.Value.Evaluate(environment);
					properties.Add(property.Key, new ContextProperty<string>(property.Value.Location, property.Value.Name, property.Value.ValueLocation, result));
				}
				catch (EvaluationException e) {
					throw new SharpParsingException(property.Value.ValueLocation, e.Message, e);
				}
			}

			List<ContextValue<string>> entries = new List<ContextValue<string>>();

			foreach (ContextValue<TextExpression> entry in this.entries) {
				try {
					string result = entry.Value.Evaluate(environment);
					entries.Add(new ContextValue<string>(entry.Location, result));
				}
				catch (EvaluationException e) {
					throw new SharpParsingException(entry.Location, e.Message, e);
				}
			}

			EvaluatedContext context = new EvaluatedContext(OriginalContext, properties, entries);

			foreach(InterpolatedContext child in children) {
				if (!PruneChildren || child.Condition.Evaluate(environment)) {
					context.children.Add(child.EvaluateContext(environment));
				}
			}
			foreach(KeyValuePair<string, InterpolatedContext> namedChild in namedChildren) {
				if (!PruneChildren || namedChild.Value.Condition.Evaluate(environment)) {
					context.namedChildren.Add(namedChild.Key, namedChild.Value.EvaluateContext(environment));
				}
			}

			return context;
		}

		private class EvaluatedContext : IContext {

			public IContext OriginalContext { get; }

			private readonly Dictionary<string, ContextProperty<string>> properties;
			private readonly List<ContextValue<string>> entries;

			public readonly List<EvaluatedContext> children;
			public readonly Dictionary<string, EvaluatedContext> namedChildren;

			public EvaluatedContext(IContext originalContext, Dictionary<string, ContextProperty<string>> properties, List<ContextValue<string>> entries) {
				this.OriginalContext = originalContext;
				this.properties = properties;
				this.entries = entries;

				this.children = new List<EvaluatedContext>();
				this.namedChildren = new Dictionary<string, EvaluatedContext>(SharpDocuments.StringComparer);
			}

			public string SimpleName => OriginalContext.SimpleName;
			public string DetailedName => OriginalContext.DetailedName;
			public string FullName => OriginalContext.FullName;
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

			public bool GetFlag(string flag, bool local, IContext? origin, out DocumentSpan? location) {
				return OriginalContext.GetFlag(flag, local, origin, out location);
			}

			public IContext? GetNamedChild(string name) {
				return namedChildren.GetValueOrFallback(name, null);
			}

			public string? GetProperty(string key, bool local, IContext? origin, string? defaultValue, out DocumentSpan? location) {
				if (properties.TryGetValue(key, out ContextProperty<string> property)) {
					location = property.Location;
					return property.Value;
				}
				else {
					return OriginalContext.GetProperty(key, local, origin, defaultValue, out location);
				}
			}

			public bool HasFlag(string flag, bool local, IContext? origin, out DocumentSpan? location) {
				return OriginalContext.HasFlag(flag, local, origin, out location);
			}

			public bool HasProperty(string key, bool local, IContext? origin, out DocumentSpan? location) {
				return OriginalContext.HasProperty(key, local, origin, out location);
			}

		}

	}

}
