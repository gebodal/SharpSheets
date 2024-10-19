using AvaloniaEdit.Document;
using SharpSheets.Canvas;
using SharpSheets.Cards.Definitions;
using SharpSheets.Parsing;
using SharpSheets.Sheets;
using SharpSheets.Widgets;
using SharpEditor.DataManagers;
using System.Linq;

namespace SharpEditor.Parsing.ParsingState {

	public sealed class CharacterSheetParsingState : SharpConfigParsingState<SharpConfigSpan> {

		public CharacterSheetParsingState(TextDocument document) : base(document) { }

		public override IParser Parser { get; } = new SheetConfigurationParser(SharpEditorRegistries.WidgetFactoryInstance);
		public override string Extension { get { return SharpEditorFileInfo.SharpConfigExtension; } }
		public override string FileFilter { get { return SharpEditorFileInfo.SharpConfigFileFilter1; } }
		public override string DefaultFilename { get { return "sharppages"; } }

		public override bool HasDesignerContent { get; } = true;
		public override bool HasGeneratedContent { get; } = true;

		private SharpPageList? sheets;
		public override IDocumentContent? DrawableContent { get { return sheets; } }

		protected override void LoadContent(ResultEntry result) {
			Origins = new ContextOrigins<SharpConfigSpan>(this, result.results.rootEntity as IContext, result.results.origins?.GetData().ToDictionary(kv => kv.Key, kv => (IContext)kv.Value));
			sheets = result.content as SharpPageList;
		}

		protected override void CreateAdditionalSpans() {
			return;
		}

		protected override Definition? GetDefinition(string name, IContext context) {
			return null;
		}

		public override SharpConfigSpan Create(int startOffset, int length) {
			ValidateSpan(startOffset, length);
			return new SharpConfigSpan(this, startOffset, length);
		}

		protected override SharpConfigColorizingTransformer<SharpConfigSpan> MakeColorizer() {
			return new SharpConfigColorizingTransformer<SharpConfigSpan>(this, SharpEditorRegistries.WidgetFactoryInstance, SharpEditorRegistries.ShapeFactoryInstance);
		}
	}

}
