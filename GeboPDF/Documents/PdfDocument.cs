using GeboPdf.Graphics;
using GeboPdf.Objects;
using GeboPdf.Patterns;
using GeboPdf.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GeboPdf.Documents {

	public interface IPdfDocumentContents {
		IEnumerable<PdfObject> CollectObjects();
	}

	public class PdfDocument : IPdfDocumentContents {

		protected internal readonly PdfCatalogueDictionary catalogueDict;
		protected internal readonly PdfMetadataDictionary metadataDict;

		public PdfDocument() {
			catalogueDict = new PdfCatalogueDictionary();
			metadataDict = new PdfMetadataDictionary();
		}

		public IEnumerable<PdfObject> CollectObjects() {
			foreach(PdfObject catalogueObj in catalogueDict.CollectObjects()) {
				yield return catalogueObj;
			}
			yield return metadataDict;
		}

		public PdfAcroForm AcroForm => catalogueDict.acroForm;
		public PdfMarkInfoDictionary MarkInfo => catalogueDict.markInfo;

		public int PageCount => catalogueDict.pagesDict.PageCount;

		public PdfPage AddPage(float width, float height) => catalogueDict.pagesDict.AddPage(width, height);
		
		public string? Creator { get { return metadataDict.Creator; } set { metadataDict.Creator = value; } }
		public string? Title { get { return metadataDict.Title; } set { metadataDict.Title = value; } }
		public string? Author { get { return metadataDict.Author; } set { metadataDict.Author = value; } }
		public string? Subject { get { return metadataDict.Subject; } set { metadataDict.Subject = value; } }
		public string[]? Keywords { get { return metadataDict.Keywords; } set { metadataDict.Keywords = value; } }
		public DateTime? CreationDate { get { return metadataDict.CreationDate; } set { metadataDict.CreationDate = value; } }
		public DateTime? ModDate { get { return metadataDict.ModDate; } set { metadataDict.ModDate = value; } }

	}

	public class PdfCatalogueDictionary : AbstractPdfDictionary, IPdfDocumentContents {

		public readonly PdfPagesDictionary pagesDict;
		public readonly PdfAcroForm acroForm;
		public readonly PdfMarkInfoDictionary markInfo;

		public PdfCatalogueDictionary() : base() {
			pagesDict = new PdfPagesDictionary();
			acroForm = new PdfAcroForm();
			markInfo = new PdfMarkInfoDictionary();
		}

		public override int Count {
			get {
				int count = 2; // Type, Pages
				if (acroForm.HasFields) {
					count += 1;
				}
				if(markInfo.Count > 0) {
					count += 1;
				}
				return count;
			}
		}

		public override IEnumerator<KeyValuePair<PdfName, PdfObject>> GetEnumerator() {
			yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.Type, PdfNames.Catalogue);
			yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.Pages, PdfIndirectReference.Create(pagesDict));

			// TODO There are more entries in this dictionary, that should probably be considered! (See specification)

			if (acroForm.HasFields) {
				yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.AcroForm, acroForm.Reference);
			}

			if (markInfo.Count > 0) {
				yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.MarkInfo, markInfo);
			}
		}

		public IEnumerable<PdfObject> CollectObjects() {
			yield return this;
			foreach (PdfObject pagesObj in pagesDict.CollectObjects()) {
				yield return pagesObj;
			}
			if (acroForm.HasFields) {
				foreach (PdfObject acroformObj in acroForm.CollectObjects()) {
					yield return acroformObj;
				}
			}
		}

	}

	public class PdfPagesDictionary : AbstractPdfDictionary, IPdfDocumentContents {

		public readonly PdfResourcesDictionary pagesResources; // What really is the point of this just now? Is it required?

		public int PageCount => pages.Count;

		private readonly List<PdfPage> pages;

		public PdfPagesDictionary() : base() {
			pagesResources = new PdfResourcesDictionary(true);

			pages = new List<PdfPage>();
		}

		public PdfPage AddPage(float width, float height) {
			PdfPage page = new PdfPage(this, width, height);
			pages.Add(page);
			return page;
		}

		public override int Count {
			get { return 4; }
		}

		public override IEnumerator<KeyValuePair<PdfName, PdfObject>> GetEnumerator() {
			yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.Type, PdfNames.Pages);
			yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.Kids, new PdfArray(pages.Select(p => p.Reference))); // This should, in theory, be a balanced tree, but that's probably overkill for now
			yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.Count, new PdfInt(pages.Count)); // This is technically the number of leaf nodes in the Pages tree, not just the length of Kids

			if (pagesResources.Count > 0) {
				yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.Resources, PdfIndirectReference.Create(pagesResources));
			}
			else {
				yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.Resources, pagesResources);
			}
		}

		public IEnumerable<PdfObject> CollectObjects() {
			yield return this;
			foreach (PdfPage page in pages) {
				foreach (PdfObject pageObj in page.CollectObjects()) {
					yield return pageObj;
				}
			}
			foreach (PdfObject resourcesObj in pagesResources.CollectObjects()) {
				yield return resourcesObj;
			}
		}
	}

	public class PdfPage : AbstractPdfDictionary, IPdfDocumentContents {

		public readonly PdfIndirectReference Reference;

		public readonly float width;
		public readonly float height;

		private readonly PdfPagesDictionary parent;

		private readonly PdfResourcesDictionary pageResources;
		public readonly List<PdfAcroField> pageAnnotations;

		public readonly PageGraphicsStream contents;

		public PdfPage(PdfPagesDictionary parent, float width, float height) {
			this.width = width;
			this.height = height;

			this.parent = parent;

			pageResources = new PdfResourcesDictionary(true);
			pageAnnotations = new List<PdfAcroField>();

			contents = new PageGraphicsStream(this, pageResources);

			Reference = PdfIndirectReference.Create(this);
		}

		public override int Count {
			get {
				int count = 5;
				if (pageAnnotations.Count > 0) { count += 1; }
				return count;
			}
		}

		public override IEnumerator<KeyValuePair<PdfName, PdfObject>> GetEnumerator() {
			yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.Type, PdfNames.Page);
			yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.MediaBox, PdfRectangle.FromDimensions(0, 0, width, height));
			yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.Parent, PdfIndirectReference.Create(parent));
			yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.Contents, PdfIndirectReference.Create(contents));

			if (pageResources.Count > 0) {
				yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.Resources, PdfIndirectReference.Create(pageResources));
			}
			else {
				yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.Resources, pageResources);
			}

			if (pageAnnotations.Count > 0) {
				yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.Annots, new PdfArray(pageAnnotations.Select(a => a.DictionaryReference)));
			}

			// TODO There are more Page dictionary elements that should perhaps be considered (see specification)
		}

		public IEnumerable<PdfObject> CollectObjects() {
			yield return this;
			foreach(PdfObject resourcesObj in pageResources.CollectObjects()) {
				yield return resourcesObj;
			}
			yield return contents;
		}

	}

	public class PdfMetadataDictionary : AbstractPdfDictionary {

		public string? Creator { get; set; }
		public string? Title { get; set; }
		public string? Author { get; set; }
		public string? Subject { get; set; }
		public string[]? Keywords { get; set; } // TODO This is not being included!

		public DateTime? CreationDate { get; set; }
		public DateTime? ModDate { get; set; }

		public PdfMetadataDictionary() : base() { }

		public override int Count {
			get {
				int count = 3; // Producer, CreationDate, ModDate

				if (!string.IsNullOrWhiteSpace(Creator)) {
					count++;
				}
				if (!string.IsNullOrWhiteSpace(Title)) {
					count++;
				}
				if (!string.IsNullOrWhiteSpace(Author)) {
					count++;
				}
				if (!string.IsNullOrWhiteSpace(Subject)) {
					count++;
				}

				return count;
			}
		}

		public override IEnumerator<KeyValuePair<PdfName, PdfObject>> GetEnumerator() {
			yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.Producer, new PdfTextString(GeboData.GetProducerString()));

			if (!string.IsNullOrWhiteSpace(Creator)) {
				yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.Creator, new PdfTextString(Creator));
			}
			if (!string.IsNullOrWhiteSpace(Title)) {
				yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.Title, new PdfTextString(Title));
			}
			if (!string.IsNullOrWhiteSpace(Author)) {
				yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.Author, new PdfTextString(Author));
			}
			if (!string.IsNullOrWhiteSpace(Subject)) {
				yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.Subject, new PdfTextString(Subject));
			}

			PdfDateString nowString = new PdfDateString(DateTime.Now);

			PdfDateString creationString;
			if (CreationDate.HasValue) {
				creationString = new PdfDateString(CreationDate.Value);
			}
			else {
				creationString = nowString;
			}
			yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.CreationDate, creationString);

			PdfDateString modString;
			if (ModDate.HasValue) {
				modString = new PdfDateString(ModDate.Value);
			}
			else {
				modString = creationString;
			}
			yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.ModDate, modString);
		}
	}

	public class PdfMarkInfoDictionary : AbstractPdfDictionary {

		public bool Marked { get; set; } = false;
		public bool UserProperties { get; set; } = false;
		public bool Suspects { get; set; } = false;

		public PdfMarkInfoDictionary() : base() { }

		public override int Count {
			get {
				int count = 0;

				if (Marked) {
					count++;
				}
				if (UserProperties) {
					count++;
				}
				if (Suspects) {
					count++;
				}

				return count;
			}
		}

		public override IEnumerator<KeyValuePair<PdfName, PdfObject>> GetEnumerator() {
			if (Marked) {
				yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.Marked, new PdfBoolean(Marked));
			}
			if (UserProperties) {
				yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.UserProperties, new PdfBoolean(UserProperties));
			}
			if (Suspects) {
				yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.Suspects, new PdfBoolean(Suspects));
			}
		}
	}

}