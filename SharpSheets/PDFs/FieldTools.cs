using GeboPdf.IO;
using GeboPdf.Objects;
using SharpSheets.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SharpSheets.PDFs {

	public static class FieldTools {

		/*
		public static void PopulateFields(PdfDocument document, Dictionary<string, string> source) {
			PdfAcroForm acroForm = PdfAcroForm.GetAcroForm(document, false);

			if (acroForm != null) {
				IDictionary<string, PdfFormField> fields = acroForm.GetFormFields();

				foreach (KeyValuePair<string, string> entry in source) {
					if (fields.ContainsKey(entry.Key)) {
						//Console.WriteLine($"{entry.Key}: {fields[entry.Key].GetFormType().GetValue()} => [{string.Join(", ", fields[entry.Key].GetAppearanceStates())}] => \"{entry.Value}\"");
						fields[entry.Key].SetValue(entry.Value, false);
					}
				}
			}

			acroForm.SetNeedAppearances(true);
		}

		public static void PopulateFields(string pdfFilepath, string source) {
			PdfReader reader = new PdfReader(pdfFilepath);
			PdfDocument document = new PdfDocument(reader);

			PopulateFields(document, ExtractFields(source));

			document.Close();
			reader.Close();
		}

		public static Dictionary<string, string> ExtractFields(PdfDocument source) {
			PdfAcroForm sourceAcroForm = PdfAcroForm.GetAcroForm(source, false);

			Dictionary<string, string> entries = new Dictionary<string, string>();

			if (sourceAcroForm != null) {
				foreach (KeyValuePair<string, PdfFormField> entry in sourceAcroForm.GetFormFields()) {
					entries[entry.Key] = entry.Value.GetValueAsString();
				}
			}

			return entries;
		}
		*/

		/*
		public static Dictionary<string, string> ExtractFields(string source) {
			if (source.EndsWith(".txt")) {
				Dictionary<string, string> entries = new Dictionary<string, string>();
				foreach (string line in File.ReadAllLines(source, System.Text.Encoding.UTF8).Where(l => !string.IsNullOrWhiteSpace(l))) {
					string[] parts = line.SplitAndTrim(2, ':');
					if (parts.Length == 2) {
						entries[parts[0]] = parts[1].Replace("\\n", "\n");
					}
				}
				return entries;
			}
			else if (source.EndsWith(".pdf")) {
				PdfReader sourceReader = new PdfReader(source);
				PdfDocument sourceDocument = new PdfDocument(sourceReader);

				Dictionary<string, string> entries = ExtractFields(sourceDocument);

				sourceDocument.Close();
				sourceReader.Close();

				return entries;
			}
			else {
				throw new ArgumentException("Supplied source file must be either a text (.txt) or PDF (.pdf) file. Received " + source);
			}
		}
		*/

		/// <summary>
		/// 
		/// </summary>
		/// <param name="source"></param>
		/// <returns></returns>
		/// <exception cref="IOException"></exception>
		/// <exception cref="NotSupportedException"></exception>
		/// <exception cref="System.Security.SecurityException"></exception>
		/// <exception cref="UnauthorizedAccessException"></exception>
		public static Dictionary<string, PdfObject> ExtractFields(string source) {
			using (FileStream objStream = new FileStream(source, FileMode.Open, FileAccess.Read)) {
				PdfStreamReader pdfStreamReader = new PdfStreamReader(objStream);

				return new Dictionary<string, PdfObject>(pdfStreamReader.GetFormFields(), StringComparer.Ordinal);
			}
		}

		/*
		public static void SaveFields(PdfDocument source, string output) {
			File.WriteAllLines(output, ExtractFields(source).Select(kv => kv.Key + ": " + kv.Value).ToArray());
		}

		public static void SaveFields(string source, string output) {
			File.WriteAllLines(output, ExtractFields(source).Select(kv => kv.Key + ": " + kv.Value).ToArray());
		}
		*/
	}

}
