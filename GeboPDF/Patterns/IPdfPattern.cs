using GeboPdf.Documents;
using GeboPdf.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeboPdf.Patterns {

	public enum PdfPatternType : int {
		Tiling = 1,
		Shading = 2
	}

	public interface IPdfPattern : IPdfDocumentContents {
		PdfPatternType PatternType { get; }
		PdfIndirectReference Reference { get; }
	}

}
