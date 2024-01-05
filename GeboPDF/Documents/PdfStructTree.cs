using GeboPdf.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeboPdf.Documents {

	public enum StructureType : int {
		Document = 1,
		Part = 2,
		Section = 3,
		Paragraph = 4,
		Span = 5
	}

	public static class StructureTypeUtils {

		public static PdfName GetName(this StructureType structureType) {
			return structureType switch {
				StructureType.Document => new PdfName("Document"),
				StructureType.Part => new PdfName("Part"),
				StructureType.Section => new PdfName("Sect"),
				StructureType.Paragraph => new PdfName("P"),
				StructureType.Span => new PdfName("Span"),
				_ => throw new ArgumentException("Invalid structure type value: " + structureType),
			};
		}

	}

	public class StructureTree : IPdfDocumentContents {

		private readonly List<PdfObject> structParents; // The index of the objects in here is important

		public StructureTree() {
			structParents = new List<PdfObject>();
		}

		public IEnumerable<PdfObject> CollectObjects() {
			yield return new PdfDictionary() {
				{ PdfNames.Type, PdfNames.StructTreeRoot }
				// ParentTree
			};
		}


		private static PdfDictionary GetStructElemDict(StructElem elem, List<PdfObject> objects) {
			PdfDictionary elemDict = new PdfDictionary() {
				{ PdfNames.Type, PdfNames.StructElem },
				{ PdfNames.StructureType, elem.structureType.GetName() }
			};

			if(elem is StructTreeElem intermediate) {
				List<PdfDictionary> kids = new List<PdfDictionary>();

				foreach(StructElem kid in intermediate.kids) {
					PdfDictionary kidDict = GetStructElemDict(kid, objects);
					kidDict.Add(PdfNames.StructTreeParent, PdfIndirectReference.Create(elemDict));

					kids.Add(kidDict);
				}

				objects.AddRange(kids);
				elemDict.Add(PdfNames.StructTreeKids, new PdfArray(kids.Select(k => PdfIndirectReference.Create(k))));
			}
			else if(elem is StructLeafElem leaf) {
				List<PdfDictionary> kids = new List<PdfDictionary>();

				foreach (MarkedContentReference kid in leaf.kids) {
					PdfDictionary kidDict = new PdfDictionary() {
						{ PdfNames.Type, PdfNames.MarkedContentReference },
						{ PdfNames.StructTreePage, PdfIndirectReference.Create(kid.page) },
						{ PdfNames.MarkedContentIdentifier, new PdfInt(kid.mcid) }
					};

					kids.Add(kidDict);
				}

				elemDict.Add(PdfNames.StructTreeKids, new PdfArray(kids));
			}

			return elemDict;
		}

	}

	public abstract class StructElem {

		// Type: /StructElem

		public readonly StructTreeElem? parent;
		public readonly StructureType structureType;

		public bool IsRoot { get { return parent is null; } }

		public StructElem(StructTreeElem? parent, StructureType structureType) {
			this.parent = parent;
			this.structureType = structureType;
		}

	}

	public class StructTreeElem : StructElem {

		public readonly List<StructElem> kids;

		public StructTreeElem(StructTreeElem? parent, StructureType structureType) : base(parent, structureType) {
			this.kids = new List<StructElem>();
		}

		public StructTreeElem() : this(null, 0) { }

		public StructTreeElem AddIntermediateChild(StructureType structureType) {
			StructTreeElem intermediate = new StructTreeElem(this, structureType);
			kids.Add(intermediate);
			return intermediate;
		}

		public StructLeafElem AddLeafChild(StructureType structureType) {
			StructLeafElem leaf = new StructLeafElem(this, structureType);
			kids.Add(leaf);
			return leaf;
		}

	}

	public class StructLeafElem : StructElem {

		public readonly List<MarkedContentReference> kids;

		public StructLeafElem(StructTreeElem? parent, StructureType structureType) : base(parent, structureType) {
			this.kids = new List<MarkedContentReference>();
		}

		public MarkedContentReference AddContent(PdfPage page, int mcid) {
			MarkedContentReference content = new MarkedContentReference(page, mcid);
			kids.Add(content);
			return content;
		}

	}

	public class MarkedContentReference {

		// Type: /MCR
		public readonly PdfPage page;
		public readonly int mcid;

		public MarkedContentReference(PdfPage page, int mcid) {
			this.page = page;
			this.mcid = mcid;
		}

	}

}
