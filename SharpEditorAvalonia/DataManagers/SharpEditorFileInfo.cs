using System;
using System.Collections.Generic;
using System.IO;

namespace SharpEditorAvalonia.DataManagers {

	public enum DocumentType { UNKNOWN, SHARPCONFIG, CARDSUBJECT, CARDCONFIG, MARKUP }

	public static class SharpEditorFileInfo {

		#region File Extensions

		public static readonly string SharpConfigExtension = ".ssc";
		public static readonly string CardSubjectExtension = ".scs";
		public static readonly string CardConfigExtension = ".scd";
		public static readonly string MarkupExtension = ".sbml";

		public static readonly string CompressedFileExtentions = "*.zip"; // "*.zip;*.gz"

		public static readonly string AllFileFilter1 = "All Files|*.*";

		public static string SharpConfigFileFilter1 => $"Sharp Config|*{SharpConfigExtension}";
		public static string CardSubjectFileFilter1 => $"Card Subjects|*{CardSubjectExtension}";
		public static string CardConfigFileFilter1 => $"Card Config|*{CardConfigExtension}";
		public static string MarkupFileFilter1 => $"Sharp Markup|*{MarkupExtension}";
		public static string SharpSheetsFileFilter1 => $"{SharpEditorData.GetEditorName()}|*{SharpConfigExtension};*{CardSubjectExtension};*{CardConfigExtension};*{MarkupExtension}";
		public static string TemplateFileFilter1 => $"Templates|*{CardConfigExtension};*{MarkupExtension};*{SharpConfigExtension}";

		public static string AllSharpSheetsFileFilters => string.Join("|", SharpSheetsFileFilter1, SharpConfigFileFilter1, CardSubjectFileFilter1, CardConfigFileFilter1, MarkupFileFilter1, AllFileFilter1);
		public static string AllTemplateFileFilters => string.Join("|", TemplateFileFilter1, CardConfigFileFilter1, MarkupFileFilter1, AllFileFilter1);

		public static string AllTemplateImportFileFilters => string.Join("|", $"Templates|*{CardConfigExtension};*{MarkupExtension};{CompressedFileExtentions};*{SharpConfigExtension}", CardConfigFileFilter1, MarkupFileFilter1, SharpConfigFileFilter1, $"Archives|{CompressedFileExtentions}", AllFileFilter1);

		//public static IEnumerable<string> Extensions { get; } = new string[] { CharacterSheetExtension, CardSubjectExtension, CardDefinitionExtension, MarkupExtension };

		public static string GetDocumentFileFilters(DocumentType docType) {
			if (docType == DocumentType.SHARPCONFIG) {
				return string.Join("|", SharpConfigFileFilter1, SharpSheetsFileFilter1, AllFileFilter1);
			}
			else if (docType == DocumentType.CARDSUBJECT) {
				return string.Join("|", CardSubjectFileFilter1, SharpSheetsFileFilter1, AllFileFilter1);
			}
			else if (docType == DocumentType.CARDCONFIG) {
				return string.Join("|", CardConfigFileFilter1, SharpSheetsFileFilter1, AllFileFilter1);
			}
			else if (docType == DocumentType.MARKUP) {
				return string.Join("|", MarkupFileFilter1, SharpSheetsFileFilter1, AllFileFilter1);
			}
			else {
				return AllSharpSheetsFileFilters;
			}
		}

		public static string GetFullFileFilters(string baseFilter, bool includeAll) {
			if (includeAll) {
				List<string> filters = new List<string>() { baseFilter };
				foreach (string filter in new string[] { SharpConfigFileFilter1, CardSubjectFileFilter1, CardConfigFileFilter1, MarkupFileFilter1 }) {
					if (filter != baseFilter) {
						filters.Add(filter);
					}
				}
				filters.Add(SharpSheetsFileFilter1);
				filters.Add(AllFileFilter1);

				return string.Join("|", filters);
			}
			else {
				return string.Join("|", baseFilter, SharpSheetsFileFilter1, AllFileFilter1);
			}
		}

		#endregion

		#region Document Type

		public static DocumentType GetDocumentType(string? filepath) {
			if(filepath is null) {
				return DocumentType.UNKNOWN;
			}

			string extension = Path.GetExtension(filepath);

			if (extension == SharpConfigExtension) {
				return DocumentType.SHARPCONFIG;
			}
			else if (extension == CardConfigExtension) {
				return DocumentType.CARDCONFIG;
			}
			else if (extension == CardSubjectExtension) {
				return DocumentType.CARDSUBJECT;
			}
			else if (extension == MarkupExtension) {
				return DocumentType.MARKUP;
			}
			else {
				return DocumentType.UNKNOWN;
			}
		}

		#endregion

	}

}
