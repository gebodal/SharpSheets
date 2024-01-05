using GeboPdf.Documents;
using GeboPdf.Objects;
using GeboPdf.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeboPdf.Graphics {

	public class PageGraphicsStream : GraphicsStream {

		private readonly PdfPage page;

		public PageGraphicsStream(PdfPage page, PdfResourcesDictionary resources, bool useEOL = true) : base(resources, useEOL) {
			this.page = page;
		}

		private int mcidCount = 0;
		public int BeginMarkedContentWithID(PdfName tag) {
			if (!(streamLevel == GraphicsStreamState.PageDescription || streamLevel == GraphicsStreamState.Text)) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.PageDescription | GraphicsStreamState.Text);
			}

			markedContentSequences.Push(tag);

			int mcid = mcidCount;

			writer.WriteName(tag);
			writer.WriteSpace();
			writer.WriteASCII($"<</MCID {mcid}>>");
			writer.WriteSpace();
			WriteOperator("BDC");

			mcidCount++;

			return mcid;
		}

		public int BeginTaggedMarkedContent(StructureType structureType) {
			if (!(streamLevel == GraphicsStreamState.PageDescription || streamLevel == GraphicsStreamState.Text)) {
				throw new PdfInvalidGraphicsStateException(streamLevel, GraphicsStreamState.PageDescription | GraphicsStreamState.Text);
			}

			PdfName structureName = structureType.GetName();

			markedContentSequences.Push(structureName);

			int mcid = mcidCount;

			writer.WriteName(structureName);
			writer.WriteSpace();
			writer.WriteASCII($"<</MCID {mcid}>>");
			writer.WriteSpace();
			WriteOperator("BDC");

			mcidCount++;

			return mcid;
		}

	}

}
