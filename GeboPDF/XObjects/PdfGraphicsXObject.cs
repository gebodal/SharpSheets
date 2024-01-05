using GeboPdf.Documents;
using GeboPdf.Graphics;
using GeboPdf.Objects;
using System.Collections.Generic;
using System.IO;

namespace GeboPdf.XObjects {

	public class PdfGraphicsXObject : PdfXObject {

		public readonly PdfRectangle bBox;
		public readonly PdfMatrix matrix;

		public readonly PdfResourcesDictionary resources;

		public readonly GraphicsStream graphics;

		public PdfGraphicsXObject(PdfRectangle bBox, PdfMatrix matrix) : base() {
			this.bBox = bBox;
			this.matrix = matrix;

			this.resources = new PdfResourcesDictionary(false);

			this.graphics = new GraphicsStream(this.resources, true);
		}

		public override bool AllowEncoding => graphics.AllowEncoding;

		public override MemoryStream GetStream() => graphics.GetStream();

		public override int Count => 5;

		public override IEnumerator<KeyValuePair<PdfName, PdfObject>> GetEnumerator() {
			yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.Type, PdfNames.XObject);
			yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.Subtype, PdfNames.Form);
			yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.BBox, bBox);
			yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.Matrix, matrix);
			yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.Resources, resources);
		}

		public override IEnumerable<PdfObject> CollectObjects() {
			yield return this;
			foreach (PdfObject resourceObj in resources.CollectObjects()) {
				yield return resourceObj;
			}
		}

		public override int GetHashCode() => base.GetHashCode();

		public override bool Equals(object? obj) {
			if(ReferenceEquals(this, obj)) {
				return true;
			}
			else if(obj is PdfGraphicsXObject other) {
				if(bBox.Equals(other.bBox) && matrix.Equals(other.matrix) && resources.Equals(other.resources)) {
					return graphics.Equals(other.graphics);
				}
			}
			return false;
		}

	}

}
