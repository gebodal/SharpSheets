using SharpSheets.Evaluations;
using SharpSheets.Parsing;
using SharpSheets.Cards.Definitions;
using SharpSheets.Cards.CardSubjects;
using SharpSheets.Exceptions;
using System.Diagnostics.CodeAnalysis;

namespace SharpSheets.Cards.CardConfigs {
	public class LazyInterpolatedContext : IContext {

		public IContext OriginalContext { get; }
		public IEnvironment Environment { get; }

		/// <summary></summary>
		/// <exception cref="ArgumentNullException"></exception>
		public LazyInterpolatedContext(IContext originalContext, IEnvironment environment) {
			this.OriginalContext = originalContext ?? throw new ArgumentNullException(nameof(originalContext));
			//this.Environment = environment ?? throw new ArgumentNullException(nameof(environment));
			this.Environment = GetFullEnvironment(originalContext, environment ?? throw new ArgumentNullException(nameof(environment)));
		}

		/// <summary></summary>
		/// <exception cref="SharpParsingException"></exception>
		[return: NotNullIfNotNull(nameof(original))]
		private string? Replace(string? original, DocumentSpan? location) {
			if (original == null) { return null; }
			// Find any variables/expressions ($-prefixed or ${}-wrapped) and replace
			try {
				TextExpression expr = Interpolation.Parse(original, Environment, true);
				string result = expr.Evaluate(Environment); // Environment.Interpolate(original, true);
				return result;
			}
			catch (EvaluationException e) {
				throw new SharpParsingException(location, e.Message, e);
			}
		}

		public string SimpleName => OriginalContext.SimpleName;
		public string DetailedName => OriginalContext.DetailedName;
		public string FullName => OriginalContext.FullName;

		public DocumentSpan Location => OriginalContext.Location;
		public int Depth => OriginalContext.Depth;

		public IContext? Parent => OriginalContext.Parent; // TODO This is still not right...
		public IEnumerable<IContext> Children {
			get {
				return OriginalContext.Children
					.Select(c => new LazyInterpolatedContext(c, Environment));
			}
		}
		public IEnumerable<KeyValuePair<string, IContext>> NamedChildren {
			get {
				return OriginalContext.NamedChildren
					.Select(kv => new KeyValuePair<string, IContext>(kv.Key, new LazyInterpolatedContext(kv.Value, Environment)));
			}
		}

		IDocumentEntity? IDocumentEntity.Parent => Parent;
		IEnumerable<IDocumentEntity> IDocumentEntity.Children => Children;

		private static IEnvironment GetFullEnvironment(IContext context, IEnvironment initial) {
			string? foreachStr = context.GetProperty("foreach", true, null, null, out _);
			if (foreachStr is not null) {
				try {
					ContextForEach forEach = ContextForEach.Parse(foreachStr, initial);
					return initial.AppendEnvironment(new DryRunEnvironment(VariableBoxes.Single(forEach.LoopVariable, CardEnvironments.Context), VariableDefinitionBox.Empty));
				}
				catch (EvaluationException) { }
				catch (FormatException) { }
			}
			return initial;
		}

		public IEnumerable<ContextProperty<string>> GetLocalProperties(IContext? origin) {
			return OriginalContext.GetLocalProperties(origin)
				.Select(p => new ContextProperty<string>(p.Location, p.Name, p.ValueLocation, Replace(p.Value, p.ValueLocation)));
		}

		public IEnumerable<ContextProperty<bool>> GetLocalFlags(IContext? origin) {
			return OriginalContext.GetLocalFlags(origin);
		}

		public IEnumerable<ContextValue<string>> GetEntries(IContext? origin) {
			return OriginalContext.GetEntries(origin).Select(e => new ContextValue<string>(e.Location, Replace(e.Value, e.Location)));
		}

		public IEnumerable<ContextValue<string>> GetDefinitions(IContext? origin) {
			return OriginalContext.GetDefinitions(origin); // TODO Is this right?
		}

		public bool TryGetLocalProperty(string key, IContext? origin, bool isLocalRequest, [MaybeNullWhen(false)] out string property, out DocumentSpan? location) {
			if(OriginalContext.TryGetLocalProperty(key, origin, isLocalRequest, out string? propertyRaw, out location)) {
				property = Replace(propertyRaw, location);
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
			if (OriginalContext.TryGetLocalNamedChild(name, origin, isLocalRequest, out IContext? namedChildRaw)) {
				namedChild = new LazyInterpolatedContext(namedChildRaw, Environment);
				return true;
			}
			else {
				namedChild = null;
				return false;
			}
		}
	}

}
