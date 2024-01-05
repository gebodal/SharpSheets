using GeboPdf.Documents;
using GeboPdf.Objects;
using GeboPdf.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeboPdf.XObjects {

	public class PdfFormXObject : PdfXObject {

		public float Width => bBox.Width;
		public float Height => bBox.Height;

		public readonly PdfRectangle bBox;
		public readonly PdfMatrix matrix;

		private readonly MemoryStream stream;
		private readonly PdfObject? filter;
		private readonly PdfObject? decodeParms;

		private readonly AbstractPdfDictionary resources;
		private readonly PdfObject[] resourceObjects;

		public PdfFormXObject(PdfRectangle bBox, PdfMatrix matrix, MemoryStream stream, PdfObject? filter, PdfObject? decodeParms, AbstractPdfDictionary resources, PdfObject[] resourceObjects) : base() {
			this.bBox = bBox;
			this.matrix = matrix;

			this.stream = stream;
			this.filter = filter;
			this.decodeParms = decodeParms;
			AllowEncoding = (filter is null);

			this.resources = resources;
			this.resourceObjects = resourceObjects;
		}

		public override bool AllowEncoding { get; }

		public override MemoryStream GetStream() => stream;

		public override int Count {
			get {
				int count = 5;
				if(filter is not null) { count += 1; }
				if(decodeParms is not null) { count += 1; }
				return count;
			}
		}

		public override IEnumerator<KeyValuePair<PdfName, PdfObject>> GetEnumerator() {
			yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.Type, PdfNames.XObject);
			yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.Subtype, PdfNames.Form);
			yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.BBox, bBox);
			yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.Matrix, matrix);
			yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.Resources, resources);
			if (filter is not null) {
				yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.Filter, filter);
			}
			if (decodeParms is not null) {
				yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.DecodeParms, decodeParms);
			}
		}

		public override IEnumerable<PdfObject> CollectObjects() {
			yield return this;
			foreach (PdfObject resourceObj in resourceObjects) {
				yield return resourceObj;
			}
		}

		public override int GetHashCode() => base.GetHashCode();

		public override bool Equals(object? obj) {
			return base.Equals(obj); // The only thing not checked is resourceObjects, but if the streams are equal then there can be no issues
		}

	}

}
