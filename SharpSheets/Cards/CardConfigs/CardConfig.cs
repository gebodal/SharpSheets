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

	public interface ICardSectionParent : IHasVariableDefinitionBox {
		DirectoryPath Source { get; }
	}

	public enum LayoutStrategy { CARD, SCROLL }

	public class CardSetConfig : ICardSectionParent {
		
		public readonly string name;
		public readonly FilePath origin;
		public DirectoryPath Source { get; }

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

		public readonly ConditionalCollection<IContext> outlines;
		public readonly ConditionalCollection<IContext> headers;
		public readonly ConditionalCollection<AbstractCardSectionConfig> cardSections;

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

			this.outlines = new ConditionalCollection<IContext>();
			this.headers = new ConditionalCollection<IContext>();
			this.cardSections = new ConditionalCollection<AbstractCardSectionConfig>();

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

	public class CardConfig : ICardSectionParent {

		public readonly CardSetConfig cardSetConfig;
		public DirectoryPath Source => cardSetConfig.Source;

		public readonly FontPathGrouping? fonts;
		public readonly ParagraphSpecification paragraphSpec;
		public readonly FontSizeSearchParams fontParams;

		public readonly float gutter;
		public readonly IDetail? gutterStyle;
		public readonly bool cropOnFinalCard;
		public readonly bool joinSplitCards;
		
		public readonly DefinitionGroup definitions;
		public readonly ConditionalCollection<IContext> outlines;
		public readonly ConditionalCollection<IContext> headers;
		public readonly ConditionalCollection<AbstractCardSectionConfig> cardSections;

		private readonly bool singles;
		private readonly int? _maxCards;
		public int MaxCards { get { return singles ? 1 : (_maxCards ?? cardSetConfig.columns); } } // TODO This should really be left up to the layout strategy

		private readonly VariableDefinitionBox variableBox;
		public IVariableDefinitionBox Variables => variableBox;

		/// <summary>
		/// 
		/// </summary>
		/// /// <param name="cardSetConfig" exclude="true"></param>
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
			FontPathGrouping? font = null,
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
			this.cardSetConfig = cardSetConfig;

			this.fonts = font;
			this.paragraphSpec = new ParagraphSpecification(lineSpacing, paragraphSpacing, 0f, 0f);
			this.fontParams = new FontSizeSearchParams(minFontSize, maxFontSize, fontEpsilon);

			this._maxCards = maxCards;
			this.singles = singles;

			this.gutter = gutter;
			this.gutterStyle = gutter_;
			this.cropOnFinalCard = cropOnFinalCard;
			this.joinSplitCards = joinSplitCards;

			this.definitions = new DefinitionGroup(cardSetConfig.definitions); // Combined with card set definitions here
			this.outlines = new ConditionalCollection<IContext>();
			this.headers = new ConditionalCollection<IContext>();
			this.cardSections = new ConditionalCollection<AbstractCardSectionConfig>();

			this.variableBox = new VariableDefinitionBox(CardConfigEnvironments.BaseDefinitions, this.definitions, cardSetConfig.Variables);
		}

	}

	public abstract class AbstractCardSectionConfig : IHasVariableDefinitionBox {

		//public readonly CardConfig cardConfig;
		public readonly ICardSectionParent parent;

		public readonly bool splittable;
		public readonly bool acceptRemaining;
		public readonly DefinitionGroup definitions;
		public readonly ConditionalCollection<IContext> outlines;
		public readonly RegexFormats regexFormats;

		public readonly int[]? atPosition;

		private readonly VariableDefinitionBox variableBox;
		public IVariableDefinitionBox Variables => variableBox;

		/// <summary>
		/// 
		/// </summary>
		/// <param name="parent" exclude="true"></param>
		/// <param name="_splittable"></param>
		/// <param name="_acceptRemaining"></param>
		/// <param name="_atPosition"></param>
		/// <param name="format"></param>
		public AbstractCardSectionConfig(
			//CardConfig cardConfig,
			ICardSectionParent parent,
			bool _splittable = false,
			bool _acceptRemaining = false,
			int[]? _atPosition = null,
			RegexFormats? format = null
		) {
			this.parent = parent;
			this.splittable = _splittable;
			this.acceptRemaining = _acceptRemaining;

			this.atPosition = (_atPosition != null && _atPosition.Length > 0) ? _atPosition : null;

			this.regexFormats = format ?? new RegexFormats(null, null, null, null);

			this.definitions = new DefinitionGroup();
			this.outlines = new ConditionalCollection<IContext>();

			this.variableBox = new VariableDefinitionBox(CardSectionEnvironments.BaseDefinitions, this.definitions, parent.Variables);
		}

	}

	public class DynamicCardSectionConfig : AbstractCardSectionConfig {

		public readonly bool equalSizeFeatures;
		public readonly bool spaceFeatures;
		public readonly float gutter;

		public readonly ConditionalCollection<CardFeatureConfig> cardFeatures;

		public bool AlwaysInclude { get; }

		/// <summary>
		/// 
		/// </summary>
		/// <param name="parent" exclude="true"></param>
		/// <param name="_splittable"></param>
		/// <param name="_acceptRemaining"></param>
		/// <param name="_equalSizeFeatures"></param>
		/// <param name="_spaceFeatures"></param>
		/// <param name="gutter"></param>
		/// <param name="_alwaysInclude"></param>
		/// <param name="_atPosition"></param>
		/// <param name="format"></param>
		public DynamicCardSectionConfig(
			//CardConfig cardConfig,
			ICardSectionParent parent,
			bool _splittable = false,
			bool _acceptRemaining = false,
			bool _equalSizeFeatures = false,
			bool _spaceFeatures = false,
			float gutter = 5f,
			bool _alwaysInclude = false,
			int[]? _atPosition = null,
			RegexFormats? format = null
		) : base(parent, _splittable, _acceptRemaining, _atPosition, format) {

			this.equalSizeFeatures = _equalSizeFeatures;
			this.spaceFeatures = _spaceFeatures;
			this.gutter = gutter;

			this.cardFeatures = new ConditionalCollection<CardFeatureConfig>();

			this.AlwaysInclude = _alwaysInclude;
		}

	}

	public class TextCardSectionConfig : AbstractCardSectionConfig {

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
		/// <param name="_splittable"></param>
		/// <param name="_acceptRemaining"></param>
		/// <param name="paragraph"></param>
		/// <param name="justification"></param>
		/// <param name="alignment"></param>
		/// <param name="heightStrategy"></param>
		/// <param name="_atPosition"></param>
		/// <param name="format"></param>
		public TextCardSectionConfig(
			//CardConfig cardConfig,
			ICardSectionParent parent,
			IExpression<string> _content,
			IExpression<string> _delimiter,
			IExpression<string> _prefix,
			IExpression<string> _tail,
			bool _splittable = false,
			bool _acceptRemaining = false,
			ParagraphIndentArg? paragraph = null,
			Justification justification = Justification.LEFT,
			Alignment alignment = Alignment.TOP,
			TextHeightStrategy heightStrategy = TextHeightStrategy.LineHeightDescent,
			int[]? _atPosition = null,
			RegexFormats? format = null
		) : base(parent, _splittable, _acceptRemaining, _atPosition, format) {

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

	public class ParagraphCardSectionConfig : AbstractCardSectionConfig {

		public readonly IExpression<string> content;

		public readonly ParagraphIndent paragraphIndent;
		public readonly ParagraphIndent listIndent;

		public readonly Justification justification;
		public readonly Alignment alignment;
		public readonly TextHeightStrategy heightStrategy;

		public readonly FontPath? dingbatsPath;
		public readonly string bullet;
		public readonly (float x,float y) bulletOffset;

		/// <summary>
		/// 
		/// </summary>
		/// <param name="parent" exclude="true"></param>
		/// <param name="_content" default="default"></param>
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
		public ParagraphCardSectionConfig(
			//CardConfig cardConfig,
			ICardSectionParent parent,
			IExpression<string> _content,
			bool _splittable = false,
			bool _acceptRemaining = false,
			ParagraphIndentArg? paragraph = null,
			Justification justification = Justification.LEFT,
			Alignment alignment = Alignment.TOP,
			TextHeightStrategy heightStrategy = TextHeightStrategy.LineHeightDescent,
			ParagraphIndentArg? list = null,
			FontPath? dingbats = null,
			string bullet = "\u2022",
			(float x,float y) bulletOffset = default,
			int[]? _atPosition = null,
			RegexFormats? format = null
		) : base(parent, _splittable, _acceptRemaining, _atPosition, format) {

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

	public class TableCardSectionConfig : AbstractCardSectionConfig {

		public readonly bool equalSizeFeatures;
		public readonly bool spaceFeatures;
		public readonly (float column, float row) tableSpacing;
		public readonly float edgeOffset;

		public readonly Color[] tableColors;

		/// <summary>
		/// 
		/// </summary>
		/// <param name="parent" exclude="true"></param>
		/// <param name="_splittable"></param>
		/// <param name="_acceptRemaining"></param>
		/// <param name="_equalSizeFeatures"></param>
		/// <param name="_spaceFeatures"></param>
		/// <param name="tableSpacing"></param>
		/// <param name="edgeOffset"></param>
		/// <param name="tableColors"></param>
		/// <param name="_atPosition"></param>
		/// <param name="format"></param>
		public TableCardSectionConfig(
			//CardConfig cardConfig,
			ICardSectionParent parent,
			bool _splittable = false,
			bool _acceptRemaining = false,
			bool _equalSizeFeatures = false,
			bool _spaceFeatures = false,
			(float column, float row) tableSpacing = default,
			float edgeOffset = 0f,
			Color[]? tableColors = null,
			int[]? _atPosition = null,
			RegexFormats? format = null
		) : base(parent, _splittable, _acceptRemaining, _atPosition, format) {

			this.equalSizeFeatures = _equalSizeFeatures;
			this.spaceFeatures = _spaceFeatures;
			this.tableSpacing = tableSpacing;
			this.edgeOffset = edgeOffset;

			this.tableColors = tableColors ?? new Color[] { Color.Transparent, ColorUtils.FromGrayscale(0.6f) };
		}

	}

	public class CardFeatureConfig : IHasVariableDefinitionBox {

		public readonly AbstractCardSectionConfig cardSectionConfig;

		public readonly DefinitionGroup definitions;
		public readonly IContext layout;
		private readonly RegexFormats? regexFormats;

		public RegexFormats RegexFormats { get { return regexFormats ?? cardSectionConfig.regexFormats; } }

		private readonly VariableDefinitionBox variableBox;
		public IVariableDefinitionBox Variables => variableBox;

		/// <summary>
		/// 
		/// </summary>
		/// <param name="cardSection" exclude="true"></param>
		/// <param name="layout" exclude="true"></param>
		/// <param name="format"></param>
		public CardFeatureConfig(AbstractCardSectionConfig cardSection, IContext layout, RegexFormats? format = null) {
			this.cardSectionConfig = cardSection;
			this.layout = layout;
			this.regexFormats = format;

			this.definitions = new DefinitionGroup();

			this.variableBox = new VariableDefinitionBox(CardFeatureEnvironments.BaseDefinitions, this.definitions, cardSectionConfig.Variables);
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

		public bool TryGetReturnType(EvaluationName key, [MaybeNullWhen(false)] out EvaluationType returnType) {
			if (baseDefinitions.TryGetReturnType(key, out returnType)) { return true; }
			else if (definitions.TryGetReturnType(key, out returnType)) { return true; }
			else if (fallback != null) { return fallback.TryGetReturnType(key, out returnType); }
			else {
				returnType = null;
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

		public bool TryGetFunctionInfo(EvaluationName name, [MaybeNullWhen(false)] out EnvironmentFunctionInfo functionInfo) {
			if (baseDefinitions.TryGetFunctionInfo(name, out functionInfo)) { return true; }
			else if (definitions.TryGetFunctionInfo(name, out functionInfo)) { return true; }
			else if (fallback != null) { return fallback.TryGetFunctionInfo(name, out functionInfo); }
			else {
				functionInfo = null;
				return false;
			}
		}

		public IEnumerable<EvaluationName> GetVariables() {
			return baseDefinitions.GetVariables().Concat(definitions.GetVariables()).ConcatOrNothing(fallback?.GetVariables());
		}

		public IEnumerable<EnvironmentFunctionInfo> GetFunctionInfos() {
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