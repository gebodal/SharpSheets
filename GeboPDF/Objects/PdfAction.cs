using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeboPdf.Objects {

	public abstract class PdfAction : AbstractPdfDictionary {

		private readonly PdfName actionType;
		private readonly PdfObject? next;

		public PdfAction(PdfName actionType, PdfObject? next) : base() {
			this.actionType = actionType ?? throw new ArgumentNullException(nameof(actionType));
			this.next = next;
		}

		public override int Count {
			get {
				return 2 + ActionParamsCount + (next is null ? 0 : 1);
			}
		}

		public override IEnumerator<KeyValuePair<PdfName, PdfObject>> GetEnumerator() {
			yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.Type, PdfNames.Action);
			yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.ActionType, actionType);
			if(next is not null) {
				yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.Next, next);
			}
			foreach(KeyValuePair<PdfName, PdfObject> actionEntry in GetActionParams()) {
				yield return actionEntry;
			}
		}

		protected abstract int ActionParamsCount { get; }
		protected abstract IEnumerable<KeyValuePair<PdfName, PdfObject>> GetActionParams();

	}

	public class PdfJavaScriptAction : PdfAction {

		private readonly string javaScript;

		public PdfJavaScriptAction(string javaScript, PdfObject? next) : base(PdfNames.JavaScript, next) {
			this.javaScript = javaScript;
		}

		protected override int ActionParamsCount => 1;

		protected override IEnumerable<KeyValuePair<PdfName, PdfObject>> GetActionParams() {
			yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.JavaScriptCode, new PdfTextString(javaScript));
		}
	}

}
