using SharpSheets.Parsing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpEditorAvalonia {

	public struct TemplateError {

		public readonly string filePath;
		public readonly DocumentSpan location;
		public readonly Exception error;

		public TemplateError(string filePath, DocumentSpan location, Exception error) {
			this.filePath = filePath;
			this.location = location;
			this.error = error;
		}

	}

}
