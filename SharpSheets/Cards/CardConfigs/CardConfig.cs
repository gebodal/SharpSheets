using SharpSheets.Evaluations;
using SharpSheets.Shapes;
using SharpSheets.Utilities;
using SharpSheets.Layouts;
using System;
using System.Collections.Generic;
using System.Linq;
using SharpSheets.Parsing;
using SharpSheets.Cards.Definitions;
using SharpSheets.Canvas.Text;
using SharpSheets.Fonts;
using SharpSheets.Evaluations.Nodes;
using SharpSheets.Cards.Layouts;
using SharpSheets.Cards.CardSubjects;
using SharpSheets.Colors;
using System.Diagnostics.CodeAnalysis;
using SharpSheets.Widgets;

namespace SharpSheets.Cards.CardConfigs {

	public interface IVariableDefinitionBox : IVariableBox {
		/// <summary>
		/// Return the <see cref="Definition"/> specified by the provided alias. Return <see langword="false"/> if no such <see cref="Definition"/> exists.
		/// </summary>
		/// <param name="key">Alias of the definition to find.</param>
		/// <param name="definition"></param>
		/// <returns></returns>
		bool TryGetDefinition(EvaluationName key, [MaybeNullWhen(false)] out Definition definition);
	}

	public interface IHasVariableDefinitionBox {
		IVariableDefinitionBox Variables { get; }
	}

	public interface ICardSegmentParent : IHasVariableDefinitionBox {
		DirectoryPath Source { get; }
	}

	public enum LayoutStrategy { CARD, SCROLL }

	public class CardSetConfig : ICardSegmentParent {
		
		public readonly string name;
		public readonly FilePath origin;
		public DirectoryPath Source { get; }

		public readonly string? description;

		public readonly AbstractLayoutStrategy layoutStrategy;
		public readonly PageSize paper;
		public readonly Margins pageMargins;
		public readonly float cardGutter;
		public readonly int rows;
		public readonly int columns;

		public readonly bool allowFeatureFollowOn;
		public readonly bool requireFormalSetupEnd;
		public readonly bool allowSingleLineFeatures;

		public readonly DefinitionGroup definitions;
		private readonly VariableDefinitionBox variableBox;
		public IVariableDefinitionBox Variables => variableBox;

		public readonly ConditionalCollection<CardConfig> cardConfigs;

		public readonly ConditionalCollection<InterpolatedContext> backgrounds;
		public readonly ConditionalCollection<InterpolatedContext> outlines;
		public readonly ConditionalCollection<AbstractCardSegmentConfig> cardSetSegments;

		public readonly List<FilePath> archivePaths;
		private readonly Dictionary<string, CardSubject> archive;
		public IReadOnlyDictionary<string, CardSubject> Archive => archive;
		private readonly List<CardSubject> examples;
		public IReadOnlyList<CardSubject> Examples => examples;

		/// <summary>
		/// 
		/// </summary>
		/// <param name="name" exclude="true"></param>
		/// <param name="origin" exclude="true"></param>
		/// <param name="source" exclude="true"></param>
		/// <param name="_description"></param>
		/// <param name="_layoutStrategy"></param>
		/// <param name="_paper"></param>
		/// <param name="_pageMargins" default="(20,20,20,20)"></param>
		/// <param name="_cardGutter"></param>
		/// <param name="_rows"></param>
		/// <param name="_columns"></param>
		/// <param name="_allowFeatureFollowOn"></param>
		/// <param name="_requireFormalSetupEnd"></param>
		/// <param name="_allowSingleLineFeatures"></param>
		/// <exception cref="ArgumentNullException"></exception>
		public CardSetConfig(
			string name,
			FilePath origin,
			DirectoryPath source,
			List<string>? _description = null,
			LayoutStrategy _layoutStrategy = LayoutStrategy.CARD,
			PageSize? _paper = null,
			Margins? _pageMargins = null,
			float _cardGutter = 20f,
			int _rows = 1, // TODO Grid?
			int _columns = 1, // TODO Grid?
			bool _allowFeatureFollowOn = false,
			bool _requireFormalSetupEnd = false,
			bool _allowSingleLineFeatures = false
		) {
			this.name = name ?? throw new ArgumentNullException(nameof(name));
			this.origin = origin;
			this.Source = source;

			this.description = _description is not null ? string.Join(" ", _description.Select(s=>s.Trim())) : null;

			this.layoutStrategy = _layoutStrategy == LayoutStrategy.CARD ? AbstractLayoutStrategy.Card : AbstractLayoutStrategy.Scroll;

			this.paper = _paper ?? PageSize.A4;
			this.pageMargins = _pageMargins ?? new Margins(20f);
			this.cardGutter = _cardGutter;
			this.rows = _rows;
			this.columns = _columns;

			this.allowFeatureFollowOn = _allowFeatureFollowOn;
			this.requireFormalSetupEnd = _requireFormalSetupEnd;
			this.allowSingleLineFeatures = _allowSingleLineFeatures;

			this.definitions = new DefinitionGroup();

			this.cardConfigs = new ConditionalCollection<CardConfig>();

			this.backgrounds = new ConditionalCollection<InterpolatedContext>();
			this.outlines = new ConditionalCollection<InterpolatedContext>();
			this.cardSetSegments = new ConditionalCollection<AbstractCardSegmentConfig>();

			this.archivePaths = new List<FilePath>();
			this.archive = new Dictionary<string, CardSubject>(StringComparer.InvariantCultureIgnoreCase); // TODO This is not the right StringComparer
			this.examples = new List<CardSubject>();

			this.variableBox = new VariableDefinitionBox(CardConfigEnvironments.BaseDefinitions, this.definitions, null);
		}

		public bool TryGetArchived(string name, [MaybeNullWhen(false)] out CardSubject archived) {
			return archive.TryGetValue(name, out archived);
		}

		public void AddToArchive(CardSubject subject) {
			archive.Add(subject.Name.Value, subject);
		}
		public void AddRangeToArchive(IEnumerable<CardSubject> subjects) {
			foreach (CardSubject subject in subjects) {
				AddToArchive(subject);
			}
		}

		public void AddExample(CardSubject example) {
			examples.Add(example);
		}

	}

	public class CardConfig : ICardSegmentParent {

		public readonly string? name;
		public readonly string? description;

		public readonly CardSetConfig cardSetConfig;
		public DirectoryPath Source => cardSetConfig.Source;

		public readonly FontSettingGrouping? fonts;
		public readonly ParagraphSpecification paragraphSpec;
		public readonly FontSizeSearchParams fontParams;

		public readonly float gutter;
		public readonly IDetail? gutterStyle;
		public readonly bool cropOnFinalCard;
		public readonly bool joinSplitCards;
		
		public readonly DefinitionGroup definitions;
		public readonly ConditionalCollection<InterpolatedContext> backgrounds;
		public readonly ConditionalCollection<InterpolatedContext> outlines;

		public readonly ConditionalCollection<AbstractCardSegmentConfig> cardSegments;
		public IEnumerable<Conditional<AbstractCardSegmentConfig>> AllCardSegments => cardSegments.Concat(cardSetConfig.cardSetSegments);

		private readonly bool singles;
		private readonly int? _maxCards;
		public int MaxCards { get { return singles ? 1 : (_maxCards ?? cardSetConfig.columns); } } // TODO This should really be left up to the layout strategy

		private readonly VariableDefinitionBox variableBox;
		public IVariableDefinitionBox Variables => variableBox;

		/// <summary>
		/// 
		/// </summary>
		/// /// <param name="cardSetConfig" exclude="true"></param>
		/// <param name="_name"></param>
		/// <param name="_description"></param>
		/// <param name="font"></param>
		/// <param name="minFontSize"></param>
		/// <param name="maxFontSize"></param>
		/// <param name="fontEpsilon"></param>
		/// <param name="lineSpacing"></param>
		/// <param name="paragraphSpacing"></param>
		/// <param name="maxCards"></param>
		/// <param name="singles"></param>
		/// <param name="gutter"></param>
		/// <param name="gutter_"></param>
		/// <param name="cropOnFinalCard"></param>
		/// <param name="joinSplitCards"></param>
		public CardConfig(
			CardSetConfig cardSetConfig,
			string? _name = null,
			List<string>? _description = null,
			FontArgument? font = null,
			//FontArguments.FontGrouping? font = null,
			//FontArguments.FontSettingCollection? font_ = null,
			float minFontSize = 7.5f,
			float maxFontSize = 8.5f,
			float fontEpsilon = 0.5f,
			float lineSpacing = 1.35f,
			float paragraphSpacing = 3f,
			int? maxCards = null,
			bool singles = false,
			float gutter = 5f,
			IDetail? gutter_ = null, // gutter_
			bool cropOnFinalCard = false,
			bool joinSplitCards = false
		) {

			this.name = _name;
			this.description = _description is not null ? string.Join(" ", _description.Select(s => s.Trim())) : null;

			this.cardSetConfig = cardSetConfig;

			//this.fonts = FontArguments.FinalFonts(font, font_);
			this.fonts = font?.Fonts ?? new FontSettingGrouping();

			this.paragraphSpec = new ParagraphSpecification(lineSpacing, paragraphSpacing, 0f, 0f);
			this.fontParams = new FontSizeSearchParams(minFontSize, maxFontSize, fontEpsilon);

			this._maxCards = maxCards;
			this.singles = singles;

			this.gutter = gutter;
			this.gutterStyle = gutter_;
			this.cropOnFinalCard = cropOnFinalCard;
			this.joinSplitCards = joinSplitCards;

			this.definitions = new DefinitionGroup(cardSetConfig.definitions); // Combined with card set definitions here
			this.backgrounds = new ConditionalCollection<InterpolatedContext>();
			this.outlines = new ConditionalCollection<InterpolatedContext>();
			this.cardSegments = new ConditionalCollection<AbstractCardSegmentConfig>();

			this.variableBox = new VariableDefinitionBox(CardConfigEnvironments.BaseDefinitions, this.definitions, cardSetConfig.Variables);
		}

		public void AddSegment(Conditional<AbstractCardSegmentConfig> segmentConfig) {
			cardSegments.Add(segmentConfig);
		}

	}

	public abstract class AbstractCardSegmentConfig : IHasVariableDefinitionBox {

		//public readonly CardConfig cardConfig;
		public readonly ICardSegmentParent parent;

		public readonly string? name;
		public readonly string? description;

		public readonly bool splittable;
		public readonly bool acceptRemaining;
		public readonly DefinitionGroup definitions;
		public readonly ConditionalCollection<InterpolatedContext> outlines;
		public readonly RegexFormats regexFormats;

		public readonly int[]? atPosition;

		private readonly VariableDefinitionBox variableBox;
		public IVariableDefinitionBox Variables => variableBox;

		/// <summary>
		/// 
		/// </summary>
		/// <param name="parent" exclude="true"></param>
		/// <param name="_name"></param>
		/// <param name="_description"></param>
		/// <param name="_splittable"></param>
		/// <param name="_acceptRemaining"></param>
		/// <param name="_atPosition"></param>
		/// <param name="format"></param>
		public AbstractCardSegmentConfig(
			//CardConfig cardConfig,
			ICardSegmentParent parent,
			string? _name = null,
			List<string>? _description = null,
			bool _splittable = false,
			bool _acceptRemaining = false,
			int[]? _atPosition = null,
			RegexFormats? format = null
		) {
			this.parent = parent;

			this.name = _name;
			this.description = _description is not null ? string.Join(" ", _description.Select(s => s.Trim())) : null;

			this.splittable = _splittable;
			this.acceptRemaining = _acceptRemaining;

			this.atPosition = (_atPosition != null && _atPosition.Length > 0) ? _atPosition : null;

			this.regexFormats = format ?? new RegexFormats(null, null, null, null);

			this.definitions = new DefinitionGroup();
			this.outlines = new ConditionalCollection<InterpolatedContext>();

			this.variableBox = new VariableDefinitionBox(CardSegmentEnvironments.BaseDefinitions, this.definitions, parent.Variables);
		}

	}

	public class DynamicCardSegmentConfig : AbstractCardSegmentConfig {

		public readonly bool equalSizeFeatures;
		public readonly bool spaceFeatures;
		public readonly float gutter;

		public readonly ConditionalCollection<CardFeatureConfig> cardFeatures;

		public bool AlwaysInclude { get; }

		/// <summary>
		/// 
		/// </summary>
		/// <param name="parent" exclude="true"></param>
		/// <param name="_name"></param>
		/// <param name="_description"></param>
		/// <param name="_splittable"></param>
		/// <param name="_acceptRemaining"></param>
		/// <param name="_equalSizeFeatures"></param>
		/// <param name="_spaceFeatures"></param>
		/// <param name="gutter"></param>
		/// <param name="_alwaysInclude"></param>
		/// <param name="_atPosition"></param>
		/// <param name="format"></param>
		public DynamicCardSegmentConfig(
			//CardConfig cardConfig,
			ICardSegmentParent parent,
			string? _name = null,
			List<string>? _description = null,
			bool _splittable = false,
			bool _acceptRemaining = false,
			bool _equalSizeFeatures = false,
			bool _spaceFeatures = false,
			float gutter = 5f,
			bool _alwaysInclude = false,
			int[]? _atPosition = null,
			RegexFormats? format = null
		) : base(parent, _name, _description, _splittable, _acceptRemaining, _atPosition, format) {

			this.equalSizeFeatures = _equalSizeFeatures;
			this.spaceFeatures = _spaceFeatures;
			this.gutter = gutter;

			this.cardFeatures = new ConditionalCollection<CardFeatureConfig>();

			this.AlwaysInclude = _alwaysInclude;
		}

	}

	public class TextCardSegmentConfig : AbstractCardSegmentConfig {

		public readonly IExpression<string> content;
		public readonly IExpression<string> delimiter;
		public readonly IExpression<string> prefix;
		public readonly IExpression<string> tail;

		public readonly ParagraphIndent paragraphIndent;

		public readonly Justification justification;
		public readonly Alignment alignment;
		public readonly TextHeightStrategy heightStrategy;

		/// <summary>
		/// 
		/// </summary>
		/// <param name="parent" exclude="true"></param>
		/// <param name="_content" default="default"></param>
		/// <param name="_delimiter" default="\n"></param>
		/// <param name="_prefix" default="null"></param>
		/// <param name="_tail" default="null"></param>
		/// <param name="_name"></param>
		/// <param name="_description"></param>
		/// <param name="_splittable"></param>
		/// <param name="_acceptRemaining"></param>
		/// <param name="paragraph"></param>
		/// <param name="justification"></param>
		/// <param name="alignment"></param>
		/// <param name="heightStrategy"></param>
		/// <param name="_atPosition"></param>
		/// <param name="format"></param>
		public TextCardSegmentConfig(
			//CardConfig cardConfig,
			ICardSegmentParent parent,
			IExpression<string> _content,
			IExpression<string> _delimiter,
			IExpression<string> _prefix,
			IExpression<string> _tail,
			string? _name = null,
			List<string>? _description = null,
			bool _splittable = false,
			bool _acceptRemaining = false,
			ParagraphIndentArg? paragraph = null,
			Justification justification = Justification.LEFT,
			Alignment alignment = Alignment.TOP,
			TextHeightStrategy heightStrategy = TextHeightStrategy.LineHeightDescent,
			int[]? _atPosition = null,
			RegexFormats? format = null
		) : base(parent, _name, _description, _splittable, _acceptRemaining, _atPosition, format) {

			this.content = _content ?? CardFeatureEnvironments.TextExpression;
			this.delimiter = _delimiter ?? new StringExpression("\n");
			this.prefix = _prefix ?? new StringExpression("");
			this.tail = _tail ?? new StringExpression("");

			this.paragraphIndent = (paragraph ?? new ParagraphIndentArg()).Indent;

			this.justification = justification;
			this.alignment = alignment;
			this.heightStrategy = heightStrategy;
		}

	}

	public class ParagraphCardSegmentConfig : AbstractCardSegmentConfig {

		public readonly IExpression<string> content;

		public readonly ParagraphIndent paragraphIndent;
		public readonly ParagraphIndent listIndent;

		public readonly Justification justification;
		public readonly Alignment alignment;
		public readonly TextHeightStrategy heightStrategy;

		public readonly FontSetting? dingbatsPath;
		public readonly string bullet;
		public readonly (float x,float y) bulletOffset;

		/// <summary>
		/// 
		/// </summary>
		/// <param name="parent" exclude="true"></param>
		/// <param name="_content" default="default"></param>
		/// <param name="_name"></param>
		/// <param name="_description"></param>
		/// <param name="_splittable"></param>
		/// <param name="_acceptRemaining"></param>
		/// <param name="paragraph"></param>
		/// <param name="justification"></param>
		/// <param name="alignment"></param>
		/// <param name="heightStrategy"></param>
		/// <param name="list"></param>
		/// <param name="dingbats"></param>
		/// <param name="bullet"></param>
		/// <param name="bulletOffset"></param>
		/// <param name="_atPosition"></param>
		/// <param name="format"></param>
		public ParagraphCardSegmentConfig(
			//CardConfig cardConfig,
			ICardSegmentParent parent,
			IExpression<string> _content,
			string? _name = null,
			List<string>? _description = null,
			bool _splittable = false,
			bool _acceptRemaining = false,
			ParagraphIndentArg? paragraph = null,
			Justification justification = Justification.LEFT,
			Alignment alignment = Alignment.TOP,
			TextHeightStrategy heightStrategy = TextHeightStrategy.LineHeightDescent,
			ParagraphIndentArg? list = null,
			FontSetting? dingbats = null,
			string bullet = "\u2022",
			(float x,float y) bulletOffset = default,
			int[]? _atPosition = null,
			RegexFormats? format = null
		) : base(parent, _name, _description, _splittable, _acceptRemaining, _atPosition, format) {

			this.content = _content ?? CardFeatureEnvironments.TextExpression;

			this.paragraphIndent = (paragraph ?? new ParagraphIndentArg()).Indent;

			this.justification = justification;
			this.alignment = alignment;
			this.heightStrategy = heightStrategy;

			this.listIndent = (list ?? new ParagraphIndentArg()).Indent;

			this.dingbatsPath = dingbats;
			this.bullet = bullet;
			this.bulletOffset = bulletOffset;
		}

	}

	public class TableCardSegmentConfig : AbstractCardSegmentConfig {

		public readonly bool equalSizeFeatures;
		public readonly bool spaceFeatures;
		public readonly (float column, float row) tableSpacing;
		public readonly float edgeOffset;

		public readonly Color[] tableColors;

		/// <summary>
		/// 
		/// </summary>
		/// <param name="parent" exclude="true"></param>
		/// <param name="_name"></param>
		/// <param name="_description"></param>
		/// <param name="_splittable"></param>
		/// <param name="_acceptRemaining"></param>
		/// <param name="_equalSizeFeatures"></param>
		/// <param name="_spaceFeatures"></param>
		/// <param name="tableSpacing"></param>
		/// <param name="edgeOffset"></param>
		/// <param name="tableColors"></param>
		/// <param name="_atPosition"></param>
		/// <param name="format"></param>
		public TableCardSegmentConfig(
			//CardConfig cardConfig,
			ICardSegmentParent parent,
			string? _name = null,
			List<string>? _description = null,
			bool _splittable = false,
			bool _acceptRemaining = false,
			bool _equalSizeFeatures = false,
			bool _spaceFeatures = false,
			(float column, float row) tableSpacing = default,
			float edgeOffset = 0f,
			Color[]? tableColors = null,
			int[]? _atPosition = null,
			RegexFormats? format = null
		) : base(parent, _name, _description, _splittable, _acceptRemaining, _atPosition, format) {

			this.equalSizeFeatures = _equalSizeFeatures;
			this.spaceFeatures = _spaceFeatures;
			this.tableSpacing = tableSpacing;
			this.edgeOffset = edgeOffset;

			this.tableColors = tableColors ?? new Color[] { Color.Transparent, ColorUtils.FromGrayscale(0.6f) };
		}

	}

	public class CardFeatureConfig : IHasVariableDefinitionBox {

		public readonly AbstractCardSegmentConfig cardSegmentConfig;

		public readonly string? name;
		public readonly string? description;

		public readonly DefinitionGroup definitions;
		private InterpolatedContext? layout;
		private readonly RegexFormats? regexFormats;

		public InterpolatedContext Layout {
			get {
				return layout ?? InterpolatedContext.Empty;
			}
			internal set {
				layout = value;
			}
		}

		public RegexFormats RegexFormats { get { return regexFormats ?? cardSegmentConfig.regexFormats; } }

		private readonly VariableDefinitionBox variableBox;
		public IVariableDefinitionBox Variables => variableBox;

		/// <summary>
		/// 
		/// </summary>
		/// <param name="cardSegment" exclude="true"></param>
		/// <param name="_name"></param>
		/// <param name="_description"></param>
		/// <param name="format"></param>
		public CardFeatureConfig(
			AbstractCardSegmentConfig cardSegment,
			string? _name = null,
			List<string>? _description = null,
			RegexFormats? format = null
		) {

			this.cardSegmentConfig = cardSegment;

			this.name = _name;
			this.description = _description is not null ? string.Join(" ", _description.Select(s => s.Trim())) : null;

			this.layout = null;
			this.regexFormats = format;

			this.definitions = new DefinitionGroup();

			this.variableBox = new VariableDefinitionBox(CardFeatureEnvironments.BaseDefinitions, this.definitions, cardSegmentConfig.Variables);
		}

	}


	public class VariableDefinitionBox : IVariableDefinitionBox {

		public readonly DefinitionGroup baseDefinitions;
		public readonly DefinitionGroup definitions;
		public readonly IVariableDefinitionBox? fallback;

		public VariableDefinitionBox(DefinitionGroup baseDefinitions, DefinitionGroup definitions, IVariableDefinitionBox? fallback) {
			this.baseDefinitions = baseDefinitions;
			this.definitions = definitions;
			this.fallback = fallback;
		}

		public bool TryGetVariableInfo(EvaluationName key, [MaybeNullWhen(false)] out EnvironmentVariableInfo variableInfo) {
			if (baseDefinitions.TryGetVariableInfo(key, out variableInfo)) { return true; }
			else if (definitions.TryGetVariableInfo(key, out variableInfo)) { return true; }
			else if (fallback != null) { return fallback.TryGetVariableInfo(key, out variableInfo); }
			else {
				variableInfo = null;
				return false;
			}
		}

		public bool TryGetNode(EvaluationName key, [MaybeNullWhen(false)] out EvaluationNode node) {
			if (baseDefinitions.TryGetNode(key, out node)) { return true; }
			else if (definitions.TryGetNode(key, out node)) { return true; }
			else if (fallback != null) { return fallback.TryGetNode(key, out node); }
			else {
				node = null;
				return false;
			}
		}

		public bool TryGetFunctionInfo(EvaluationName name, [MaybeNullWhen(false)] out IEnvironmentFunctionInfo functionInfo) {
			if (baseDefinitions.TryGetFunctionInfo(name, out functionInfo)) { return true; }
			else if (definitions.TryGetFunctionInfo(name, out functionInfo)) { return true; }
			else if (fallback != null) { return fallback.TryGetFunctionInfo(name, out functionInfo); }
			else {
				functionInfo = null;
				return false;
			}
		}

		public IEnumerable<EnvironmentVariableInfo> GetVariables() {
			return baseDefinitions.GetVariables().Concat(definitions.GetVariables()).ConcatOrNothing(fallback?.GetVariables());
		}

		public IEnumerable<IEnvironmentFunctionInfo> GetFunctionInfos() {
			return baseDefinitions.GetFunctionInfos().Concat(definitions.GetFunctionInfos()).ConcatOrNothing(fallback?.GetFunctionInfos());
		}

		public bool TryGetDefinition(EvaluationName key, [MaybeNullWhen(false)] out Definition definition) {
			if (baseDefinitions.TryGetDefinition(key, out definition)) {
				return true;
			}
			else if (definitions.TryGetDefinition(key, out definition)) {
				return true;
			}
			else if (fallback != null) {
				return fallback.TryGetDefinition(key, out definition);
			}
			else {
				definition = null;
				return false;
			}
		}

	}

}