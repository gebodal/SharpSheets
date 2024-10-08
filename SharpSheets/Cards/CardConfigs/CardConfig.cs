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

	public interface ICardConfigComponent {
		public string? Name { get; }
		public string? Description { get; }
	}

	public enum LayoutStrategy { CARD, SCROLL }

	/// <summary>
	/// This element represents a set of card configurations, and defines any information
	/// shared by all such cards, along with default values for card configuration properties.
	/// This element also controls some aspects of how corresponding card subject files must
	/// be formatted. The page layout for the final card document is determined by the properties
	/// of this element.
	/// </summary>
	public class CardSetConfig : ICardSegmentParent, ICardConfigComponent {
		
		public string Name { get; }
		public readonly FilePath origin;
		public DirectoryPath Source { get; }

		public string? Description { get; }

		public readonly AbstractLayoutStrategy layoutStrategy;
		public readonly PageSize paper;
		public readonly Margins pageMargins;
		public readonly float cardGutter;
		public readonly (uint rows, uint columns) grid;

		public readonly bool allowFeatureFollowOn;
		public readonly bool requireFormalSetupEnd;

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
		/// Constructor for CardSetConfig.
		/// </summary>
		/// <param name="name" exclude="true"></param>
		/// <param name="origin" exclude="true"></param>
		/// <param name="source" exclude="true"></param>
		/// <param name="_description">A description for this set of card configurations, to be displayed
		/// in the documentation. Multiple entries will be combined with spaces.</param>
		/// <param name="_paper">The paper size to use for document pages when generating the card content.
		/// A variety of common paper size options are available, such as "A4" or "letter", or alternatively
		/// a size may be specified explicitly (as in "20 x 20 cm").</param>
		/// <param name="_pageMargins" default="20">Margins to use for the page area, separating the cards
		/// from the edge of the paper.</param>
		/// <param name="_cardGutter">The spacing used between cards in the card grid, both horizontally and
		/// vertically.</param>
		/// <param name="_grid" default="1,1">The number of rows and columns for the card grid on the page.</param>
		/// <param name="_allowFeatureFollowOn">Flag to indicate that inline features are allowed to be split
		/// over multiple lines in the subject file.</param>
		/// <param name="_requireFormalSetupEnd">Flag to indicate that the card subject file must use explicit
		/// segment headings to indicate the end of the subject setup data.</param>
		/// <exception cref="ArgumentNullException"></exception>
		public CardSetConfig(
			string name,
			FilePath origin,
			DirectoryPath source,
			List<string>? _description = null,
			//LayoutStrategy _layoutStrategy = LayoutStrategy.CARD,
			PageSize? _paper = null,
			Margins? _pageMargins = null,
			float _cardGutter = 20f,
			(uint rows, uint columns)? _grid = null,
			bool _allowFeatureFollowOn = false,
			bool _requireFormalSetupEnd = true
		) {
			this.Name = name ?? throw new ArgumentNullException(nameof(name));
			this.origin = origin;
			this.Source = source;

			this.Description = _description is not null ? string.Join(" ", _description.Select(s=>s.Trim())) : null;

			//this.layoutStrategy = _layoutStrategy == LayoutStrategy.CARD ? AbstractLayoutStrategy.Card : AbstractLayoutStrategy.Scroll;
			this.layoutStrategy = AbstractLayoutStrategy.Card;

			this.paper = _paper ?? PageSize.A4;
			this.pageMargins = _pageMargins ?? new Margins(20f);
			this.cardGutter = _cardGutter;
			this.grid = (_grid?.rows ?? 1, _grid?.columns ?? 1);

			this.allowFeatureFollowOn = _allowFeatureFollowOn;
			this.requireFormalSetupEnd = _requireFormalSetupEnd;

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

	/// <summary>
	/// This element represents an individual card configuration. Various aspects of the
	/// card layout can be controlled using this element's properties.
	/// </summary>
	public class CardConfig : ICardSegmentParent, ICardConfigComponent {

		public string? Name { get; }
		public string? Description { get; }

		public readonly CardSetConfig cardSetConfig;
		public DirectoryPath Source => cardSetConfig.Source;

		public readonly FontSettingGrouping? fonts;
		public readonly ParagraphSpecification paragraphSpec;
		public readonly FontSizeSearchParams fontParams;

		public readonly float gutter;
		public readonly IDetail? gutterStyle;
		public readonly bool cropOnFinalCard;
		public readonly bool joinSplitCards;
		public readonly RectangleAllowance multiCardLayout;
		public readonly bool allowMultipage;

		public readonly DefinitionGroup definitions;
		public readonly ConditionalCollection<InterpolatedContext> backgrounds;
		public readonly ConditionalCollection<InterpolatedContext> outlines;

		public readonly ConditionalCollection<AbstractCardSegmentConfig> cardSegments;
		public IEnumerable<Conditional<AbstractCardSegmentConfig>> AllCardSegments => cardSegments.Concat(cardSetConfig.cardSetSegments);

		private readonly uint? _maxCards;
		public uint MaxCards { get { return _maxCards ?? cardSetConfig.grid.columns; } } // TODO This should really be left up to the layout strategy

		private readonly VariableDefinitionBox variableBox;
		public IVariableDefinitionBox Variables => variableBox;

		/// <summary>
		/// Constructor for CardConfig.
		/// </summary>
		/// <param name="cardSetConfig" exclude="true"></param>
		/// <param name="_name">The name for this card configuration.</param>
		/// <param name="_description">A description for this card configuration, to be displayed
		/// in the documentation. Multiple entries will be combined with spaces.</param>
		/// <param name="minFontSize">The minimum font size to use for card text during layout.</param>
		/// <param name="maxFontSize">The maximum font size to use for card text during layout.</param>
		/// <param name="fontEpsilon">The minimum font difference to be considered when searching
		/// for the final layout font size.</param>
		/// <param name="lineSpacing">The line spacing to use for card text, which is the distance between
		/// successive text baselines, measured in multiples of the layout fontsize.</param>
		/// <param name="paragraphSpacing">The spacing to be used between paragraphs of text, measured in points.
		/// This spacing is in addition to any line spacing.</param>
		/// <param name="maxCards">The maximum number of card grid spaces that an individual card subject
		/// may occupy. Defaults to the number of columns in the grid.</param>
		/// <param name="gutter">Spacing between card segment blocks, measured in points.</param>
		/// <param name="gutter_">Gutter style for the card configuration.
		/// This style is used to draw detailing in the spaces between segment blocks.</param>
		/// <param name="cropOnFinalCard">Flag to indicate that the segments in the final card grid space
		/// should be cropped rather than sized to fill any leftover space.</param>
		/// <param name="joinSplitCards">Flag to indicate that separate card grid spaces for the same
		/// card subject should be joined using the card background.</param>
		/// <param name="multiCardLayout">The layout strategy to use when arranging mulitple cards grid spaces
		/// in the card grid.</param>
		/// <param name="allowMultipage">Flag to indicate that cards are allowed to draw sections on multiple
		/// pages.</param>
		/// <param name="font">The default font to use for the card text content.</param>
		public CardConfig(
			CardSetConfig cardSetConfig,
			string? _name = null,
			List<string>? _description = null,
			//FontArguments.FontGrouping? font = null,
			//FontArguments.FontSettingCollection? font_ = null,
			float minFontSize = 7.5f,
			float maxFontSize = 8.5f,
			float fontEpsilon = 0.5f,
			float lineSpacing = 1.35f,
			float paragraphSpacing = 3f,
			uint? maxCards = null,
			float gutter = 5f,
			IDetail? gutter_ = null, // gutter_
			bool cropOnFinalCard = false,
			bool joinSplitCards = false,
			RectangleAllowance multiCardLayout = RectangleAllowance.NONE,
			bool allowMultipage = true,
			FontArgument? font = null
		) {

			this.Name = _name;
			this.Description = _description is not null ? string.Join(" ", _description.Select(s => s.Trim())) : null;

			this.cardSetConfig = cardSetConfig;

			//this.fonts = FontArguments.FinalFonts(font, font_);
			this.fonts = font?.Fonts ?? new FontSettingGrouping();

			this.paragraphSpec = new ParagraphSpecification(lineSpacing, paragraphSpacing, 0f, 0f);
			this.fontParams = new FontSizeSearchParams(minFontSize, maxFontSize, fontEpsilon);

			this._maxCards = maxCards;

			this.gutter = gutter;
			this.gutterStyle = gutter_;
			this.cropOnFinalCard = cropOnFinalCard;
			this.joinSplitCards = joinSplitCards;
			this.multiCardLayout = multiCardLayout;
			this.allowMultipage = allowMultipage;

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

	public abstract class AbstractCardSegmentConfig : IHasVariableDefinitionBox, ICardConfigComponent {

		//public readonly CardConfig cardConfig;
		public readonly ICardSegmentParent parent;

		public string? Name { get; }
		public string? Description { get; }

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

			this.Name = _name;
			this.Description = _description is not null ? string.Join(" ", _description.Select(s => s.Trim())) : null;

			this.splittable = _splittable;
			this.acceptRemaining = _acceptRemaining;

			this.atPosition = (_atPosition != null && _atPosition.Length > 0) ? _atPosition : null;

			this.regexFormats = format ?? new RegexFormats(null, null, null, null);

			this.definitions = new DefinitionGroup();
			this.outlines = new ConditionalCollection<InterpolatedContext>();

			this.variableBox = new VariableDefinitionBox(CardSegmentEnvironments.BaseDefinitions, this.definitions, parent.Variables);
		}

	}

	/// <summary>
	/// This element represents a card segment with arbitrary content.
	/// Layout settings for the feature contents can be specified, along
	/// with outlines for the segment area.
	/// </summary>
	public class DynamicCardSegmentConfig : AbstractCardSegmentConfig {

		public readonly bool equalSizeFeatures;
		public readonly bool spaceFeatures;
		public readonly float gutter;

		public readonly ConditionalCollection<CardFeatureConfig> cardFeatures;

		public bool AlwaysInclude { get; }

		/// <summary>
		/// Constructor for <see cref="DynamicCardSegmentConfig"/>.
		/// </summary>
		/// <param name="parent" exclude="true">The parent element for this configuration.</param>
		/// <param name="_name">The name for this segment configuration.</param>
		/// <param name="_description">A description for this segment configuration, to be displayed
		/// in the documentation. Multiple entries will be combined with spaces.</param>
		/// <param name="_splittable">Flag to indicate that this segment may be split
		/// across multiple card faces.</param>
		/// <param name="_acceptRemaining">Flag to indicate that this segment accepts
		/// and remaining area on the card, which will be included when drawing the outline.</param>
		/// <param name="_equalSizeFeatures">Flag to indicate that all features should be given
		/// the same amount of space on the card.</param>
		/// <param name="_spaceFeatures">Flag to indicate that each feature should take an
		/// equal share of any remaining space in the segment area.</param>
		/// <param name="gutter">The spacing between feature areas.</param>
		/// <param name="_alwaysInclude">Flag to indicate that this segment should always
		/// be included in the card, rather than being prompted by data in the card subject.</param>
		/// <param name="_atPosition">Specifies an override position for this segment
		/// when drawing the card content.</param>
		/// <param name="format">Regular expressions controlling formatting to be applied to feature texts.</param>
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

	/// <summary>
	/// This element represents a card segment which displays features as
	/// a continuous string of text. A delimiter, and a prefix and tail
	/// can be specified for the text content, along with text layout
	/// parameters and outlines for the segment area.
	/// </summary>
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
		/// Constructor for <see cref="TextCardSegmentConfig"/>.
		/// </summary>
		/// <param name="parent" exclude="true">The parent element for this configuration.</param>
		/// <param name="_content" default="default">Text expression to use for converting each feature into text.</param>
		/// <param name="_delimiter" default="\n">A delimiter to use between each feature's textual representation.</param>
		/// <param name="_prefix" default="null">Prefix text to add before the delimited feature texts.</param>
		/// <param name="_tail" default="null">Suffix text to add after the delimited feature texts.</param>
		/// <param name="_name">The name for this segment configuration.</param>
		/// <param name="_description">A description for this segment configuration, to be displayed
		/// in the documentation. Multiple entries will be combined with spaces.</param>
		/// <param name="_splittable">Flag to indicate that this segment may be split
		/// across multiple card faces.</param>
		/// <param name="_acceptRemaining">Flag to indicate that this segment accepts
		/// and remaining area on the card, which will be included when drawing the outline.</param>
		/// <param name="paragraph">Paragraph indentation specification for this configuration.</param>
		/// <param name="justification">The horizontal justification to use for the text within the segment area.</param>
		/// <param name="alignment">The vertical alignment to use for the text within the segment area.</param>
		/// <param name="heightStrategy">The height calculation strategy to use when arranging the text within the segment area.</param>
		/// <param name="_atPosition">Specifies an override position for this segment
		/// when drawing the card content.</param>
		/// <param name="format">Regular expressions controlling formatting to be applied to feature texts.</param>
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

	/// <summary>
	/// This element represents a card segment which displays features as
	/// individual paragraphs of text. Text layout parameters can be specified
	/// for the paragraphs, along with outlines for the segment area. Parameters
	/// for layout of list features as bullet point entries can also be
	/// provided.
	/// </summary>
	public class ParagraphCardSegmentConfig : AbstractCardSegmentConfig {

		public class BulletArg : ISharpArgsGrouping {
			public readonly string Symbol;
			public readonly FontSetting? FontPath;
			public readonly float FontSizeMultiplier;
			public readonly float Indent;
			public readonly float Offset;

			/// <summary>
			/// Constructor for <see cref="BulletArg"/>.
			/// </summary>
			/// <param name="symbol">The symbol (which may be arbitrary text) to use for
			/// the bullet in list entries. If an empty string is provided, then
			/// list paragraphs will be drawn with no symbol or indentation.</param>
			/// <param name="font">The font to use for the bullet symbol.</param>
			/// <param name="size">The font size at which to draw the bullet symbol,
			/// as a multiplier of the current text size.</param>
			/// <param name="indent">The identation at which to draw the bullet symbol,
			/// expressed in points.</param>
			/// <param name="offset">The offset from the baseline at which to draw the
			/// bullet symbol, as a fraction of the current font size.</param>
			public BulletArg(string symbol = "\u2022", FontSetting? font = null, float size = 1f, float indent = 0f, float offset = 0f) {
				Symbol = symbol;
				FontPath = font;
				FontSizeMultiplier = Math.Max(0f, size);
				Indent = indent;
				Offset = offset;
			}
		}

		public readonly IExpression<string> content;

		public readonly ParagraphIndent paragraphIndent;
		public readonly ParagraphIndent listIndent;

		public readonly Justification justification;
		public readonly Alignment alignment;
		public readonly TextHeightStrategy heightStrategy;

		public readonly BulletArg bullet;

		/// <summary>
		/// Constructor for <see cref="ParagraphCardSegmentConfig"/>.
		/// </summary>
		/// <param name="parent" exclude="true">The parent element for this configuration.</param>
		/// <param name="_content" default="default">Text expression to use for converting each feature into text.</param>
		/// <param name="_name">The name for this segment configuration.</param>
		/// <param name="_description">A description for this segment configuration, to be displayed
		/// in the documentation. Multiple entries will be combined with spaces.</param>
		/// <param name="_splittable">Flag to indicate that this segment may be split
		/// across multiple card faces.</param>
		/// <param name="_acceptRemaining">Flag to indicate that this segment accepts
		/// and remaining area on the card, which will be included when drawing the outline.</param>
		/// <param name="paragraph">Paragraph indentation specification for non-list features in this configuration.</param>
		/// <param name="justification">The horizontal justification to use for the text within the segment area.</param>
		/// <param name="alignment">The vertical alignment to use for the text within the segment area.</param>
		/// <param name="heightStrategy">The height calculation strategy to use when arranging the text within the segment area.</param>
		/// <param name="list">Paragraph indentation specification for list features in this configuration.</param>
		/// <param name="bullet">List bullet point specification for this configuration.</param>
		/// <param name="_atPosition">Specifies an override position for this segment
		/// when drawing the card content.</param>
		/// <param name="format">Regular expressions controlling formatting to be applied to feature texts.</param>
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
			BulletArg? bullet = null,
			int[]? _atPosition = null,
			RegexFormats? format = null
		) : base(parent, _name, _description, _splittable, _acceptRemaining, _atPosition, format) {

			this.content = _content ?? CardFeatureEnvironments.TextExpression;

			this.paragraphIndent = (paragraph ?? new ParagraphIndentArg()).Indent;

			this.justification = justification;
			this.alignment = alignment;
			this.heightStrategy = heightStrategy;

			this.listIndent = (list ?? new ParagraphIndentArg()).Indent;

			this.bullet = bullet ?? new BulletArg();
		}

	}

	/// <summary>
	/// This element represents a card segment which displays features as
	/// rows of a table. Row colors and cell layout parameters can be specified
	/// for the table, along with outlines for the segment area.
	/// </summary>
	public class TableCardSegmentConfig : AbstractCardSegmentConfig {

		public readonly bool equalSizeFeatures;
		public readonly bool spaceFeatures;
		public readonly (float column, float row) tableSpacing;
		public readonly float edgeOffset;

		public readonly TextHeightStrategy cellHeightStrategy;

		public readonly Color[] tableColors;

		/// <summary>
		/// Constructor for <see cref="TableCardSegmentConfig"/>.
		/// </summary>
		/// <param name="parent" exclude="true">The parent element for this configuration.</param>
		/// <param name="_name">The name for this segment configuration.</param>
		/// <param name="_description">A description for this segment configuration, to be displayed
		/// in the documentation. Multiple entries will be combined with spaces.</param>
		/// <param name="_splittable">Flag to indicate that this segment may be split
		/// across multiple card faces.</param>
		/// <param name="_acceptRemaining">Flag to indicate that this segment accepts
		/// and remaining area on the card, which will be included when drawing the outline.</param>
		/// <param name="_equalSizeFeatures">Flag to indicate that all rows (i.e. features) should be
		/// given the same amount of space in the table.</param>
		/// <param name="_spaceFeatures">Flag to indicate that each row (i.e. feature) should take an
		/// equal share of any remaining space in the segment area.</param>
		/// <param name="tableSpacing">Column and row spacing for the table cells.</param>
		/// <param name="edgeOffset">A horizontal offset for the table contents from the edge of
		/// the segment area.</param>
		/// <param name="tableColors">Colors for the table rows, which will used cyclically over the
		/// whole table.</param>
		/// <param name="cellHeightStrategy">The height calculation strategy to use when arranging text within each cell area.</param>
		/// <param name="_atPosition">Specifies an override position for this segment
		/// when drawing the card content.</param>
		/// <param name="format">Regular expressions controlling formatting to be applied to feature texts.</param>
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
			TextHeightStrategy cellHeightStrategy = TextHeightStrategy.AscentDescent,
			int[]? _atPosition = null,
			RegexFormats? format = null
		) : base(parent, _name, _description, _splittable, _acceptRemaining, _atPosition, format) {

			this.equalSizeFeatures = _equalSizeFeatures;
			this.spaceFeatures = _spaceFeatures;
			this.tableSpacing = tableSpacing;
			this.edgeOffset = edgeOffset;

			this.tableColors = tableColors ?? new Color[] { Color.Transparent, ColorUtils.FromGrayscale(0.6f) };

			this.cellHeightStrategy = cellHeightStrategy;
		}

	}

	/// <summary>
	/// This element represents a card feature configuration, and the layout
	/// with which the corresponding feature data will be drawn to the card.
	/// You can also supply "format" patterns, which can be used to apply text
	/// formatting to the feature text content before drawing.
	/// </summary>
	public class CardFeatureConfig : IHasVariableDefinitionBox, ICardConfigComponent {

		public readonly AbstractCardSegmentConfig cardSegmentConfig;

		public string? Name { get; }
		public string? Description { get; }

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
		/// Constructor for <see cref="CardFeatureConfig"/>.
		/// </summary>
		/// <param name="cardSegment" exclude="true">The parent segment configuration.</param>
		/// <param name="_name">The name for this feature configuration.</param>
		/// <param name="_description">A description for this feature configuration, to be displayed
		/// in the documentation. Multiple entries will be combined with spaces.</param>
		/// <param name="format">Regular expressions controlling formatting to be applied to the feature text.</param>
		public CardFeatureConfig(
			AbstractCardSegmentConfig cardSegment,
			string? _name = null,
			List<string>? _description = null,
			RegexFormats? format = null
		) {

			this.cardSegmentConfig = cardSegment;

			this.Name = _name;
			this.Description = _description is not null ? string.Join(" ", _description.Select(s => s.Trim())) : null;

			this.layout = null;
			this.regexFormats = format;

			this.definitions = new DefinitionGroup();

			this.variableBox = new VariableDefinitionBox(CardFeatureEnvironments.BaseDefinitions, this.definitions, cardSegmentConfig.Variables);
		}

	}


	public class VariableDefinitionBox : IVariableDefinitionBox {

		public static readonly VariableDefinitionBox Empty = new VariableDefinitionBox(new DefinitionGroup(), new DefinitionGroup(), null);

		public readonly DefinitionGroup baseDefinitions;
		public readonly DefinitionGroup definitions;
		public readonly IVariableDefinitionBox? fallback;

		public bool IsEmpty => baseDefinitions.Count == 0 && definitions.Count == 0 && (fallback is null || fallback.IsEmpty);

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