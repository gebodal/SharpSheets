using GeboPdf.Fonts;
using GeboPdf.IO;
using GeboPdf.Objects;
using GeboPdf.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Permissions;
using System.Text;

namespace GeboPdf.Documents {

	public class PdfDocumentWriter {

		private readonly PdfStreamWriter _stream;

		public bool CompressStreams { get; set; } = true;

		public PdfDocumentWriter(PdfStreamWriter stream) {
			this._stream = stream;
		}

		private void WriteString(PdfString value) {
			if (value.HexString) {
				_stream.WriteASCII("<");
			}
			else {
				_stream.WriteASCII("(");
			}

			_stream.Write(value.Value);

			if (value.HexString) {
				_stream.WriteASCII(">");
			}
			else {
				_stream.WriteASCII(")");
			}
		}

		private void WriteArray(AbstractPdfArray values, DocumentObjectCollection evaluator) { // Special type?
			_stream.WriteASCII("[");

			for(int i=0; i<values.Length; i++) {
				if(i > 0) {
					_stream.WriteSpace();
				}
				WriteObject(values[i], evaluator);
			}

			_stream.WriteASCII("]");
		}

		private void WriteDictionaryEntry(KeyValuePair<PdfName, PdfObject> entry, DocumentObjectCollection evaluator, bool useEOL) {
			_stream.WriteName(entry.Key);
			_stream.WriteSpace();
			WriteObject(entry.Value, evaluator);
			if (useEOL) {
				_stream.WriteEOL();
			}
			else {
				_stream.WriteSpace();
			}
		}

		private void WriteDictionary(AbstractPdfDictionary values, DocumentObjectCollection evaluator, bool useEOL, params KeyValuePair<PdfName, PdfObject>[] additionalValues) { // Special type?
			if(additionalValues.Length == 0 && values.Count == 0) {
				_stream.WriteASCII("<< >>");
				return;
			}
			
			_stream.WriteASCII("<<");
			if (useEOL) {
				_stream.WriteEOL();
			}
			else {
				_stream.WriteSpace();
			}

			foreach(KeyValuePair<PdfName, PdfObject> entry in values) {
				WriteDictionaryEntry(entry, evaluator, useEOL);
			}
			foreach (KeyValuePair<PdfName, PdfObject> additionalEntry in additionalValues) {
				WriteDictionaryEntry(additionalEntry, evaluator, useEOL);
			}

			_stream.WriteASCII(">>");
		}

		private void WriteIndirectReference(PdfIndirectReference reference, DocumentObjectCollection evaluator) {
			int referenceIndex = evaluator.ReferenceEvaluator(reference.Subject);

			_stream.WriteASCII($"{referenceIndex} 0 R");
		}

		private void WriteIndirectFontReference(PdfIndirectFontReference reference, DocumentObjectCollection evaluator) {
			int referenceIndex = evaluator.FontReferenceEvaluator(reference.Subject);

			_stream.WriteASCII($"{referenceIndex} 0 R");
		}

		private void WriteObject(PdfObject value, DocumentObjectCollection evaluator) {
			if (value is PdfBoolean boolVal) {
				_stream.WriteBool(boolVal.Value);
			}
			else if (value is PdfInt intVal) {
				_stream.WriteInt(intVal.Value);
			}
			else if (value is PdfFloat floatVal) {
				_stream.WriteFloat(floatVal.Value);
			}
			else if (value is PdfString stringVal) {
				WriteString(stringVal);
			}
			else if (value is PdfName nameVal) {
				_stream.WriteName(nameVal);
			}
			else if (value is AbstractPdfArray arrayVal) {
				WriteArray(arrayVal, evaluator);
			}
			else if (value is AbstractPdfStream) {
				throw new PdfInvalidOperationException("Stream objects must be explictly written as standalone objects.");
			}
			else if (value is AbstractPdfDictionary dictVal) {
				WriteDictionary(dictVal, evaluator, true);
			}
			else if (value is PdfNull) {
				_stream.WriteNull();
			}
			else if (value is PdfIndirectReference referenceVal) {
				WriteIndirectReference(referenceVal, evaluator);
			}
			else if (value is PdfIndirectFontReference fontReferenceVal) {
				WriteIndirectFontReference(fontReferenceVal, evaluator);
			}
			else if (value is PdfProxyObject proxyVal) {
				WriteObject(proxyVal.Content, evaluator);
			}
			else {
				throw new PdfInvalidOperationException($"Cannot write object of type {value.GetType()}.");
			}
		}

		private void WriteStream(AbstractPdfStream stream, DocumentObjectCollection evaluator) {
			//AbstractPdfDictionary streamDictionary = stream.GetDictionary();

			MemoryStream originalStreamData = stream.GetStream();
			lock (originalStreamData) { // Crude thread safety attempt
				MemoryStream streamData;
				if (CompressStreams && stream.AllowEncoding) {
					streamData = Deflate1950.Compress(originalStreamData);
				}
				else {
					streamData = originalStreamData;
				}

				KeyValuePair<PdfName, PdfObject>[] streamEntries = new KeyValuePair<PdfName, PdfObject>[(CompressStreams && stream.AllowEncoding) ? 2 : 1];
				streamEntries[0] = new KeyValuePair<PdfName, PdfObject>(PdfNames.Length, new PdfInt(streamData.Length));
				if (CompressStreams && stream.AllowEncoding) {
					streamEntries[1] = new KeyValuePair<PdfName, PdfObject>(PdfNames.Filter, PdfNames.FlateDecode);
				}

				WriteDictionary(stream, evaluator, true, streamEntries);
				_stream.WriteEOL();

				_stream.WriteASCII("stream");
				_stream.WriteEOL();

				_stream.Write(streamData);

				_stream.WriteEOL();
				_stream.WriteASCII("endstream");
				//WriteEOL();
			}
		}

		private long WriteIndirectObject(PdfObject indirectObject, DocumentObjectCollection evaluator) {
			long objectPosition = _stream.bytesWritten;

			int objectIndex = evaluator.ReferenceEvaluator(indirectObject);

			_stream.WriteASCII($"{objectIndex} 0 obj");
			_stream.WriteEOL();

			if (indirectObject is AbstractPdfStream streamObject) {
				WriteStream(streamObject, evaluator);
			}
			else {
				WriteObject(indirectObject, evaluator);
			}

			_stream.WriteEOL();
			_stream.WriteASCII("endobj");
			_stream.WriteEOL();

			return objectPosition;
		}

		private void WriteHeader() {
			// Write header and intro bytes
			_stream.WriteASCII("%PDF-1.7"); // "%PDF\u002D1.7"
			_stream.WriteEOL();
			_stream.WriteASCII("%");
			_stream.Write(248);
			_stream.Write(254); // 240
			_stream.Write(230);
			_stream.Write(163);
			/*
			_stream.Write(226);
			_stream.Write(227);
			_stream.Write(207);
			_stream.Write(211);
			*/
			_stream.WriteEOL();
			_stream.WriteASCII("% Made using " + GeboData.GetProducerString());
			_stream.WriteEOL();
		}

		private long WriteXrefTable(long[] byteOffsets) {
			long xrefPosition = _stream.bytesWritten;

			_stream.WriteASCII("xref");
			_stream.WriteEOL();

			_stream.WriteASCII($"0 {byteOffsets.Length + 1}");
			_stream.WriteEOL();

			_stream.WriteASCII("0000000001 65535 f");
			//WriteEOL(); // This must be a 2-byte EOL marker
			_stream.Write(13); // Carriage Return char
			_stream.Write(10); // Line Feed (newline) char

			for (int i=0; i<byteOffsets.Length; i++) {
				_stream.WriteASCII($"{byteOffsets[i]:0000000000} 00000 n");
				//WriteEOL(); // This must be a 2-byte EOL marker
				_stream.Write(13); // Carriage Return char
				_stream.Write(10); // Line Feed (newline) char
			}

			return xrefPosition;
		}

		private void WriteTrailer(AbstractPdfDictionary root, PdfMetadataDictionary infoDictionary, long[] xrefByteOffsets, DocumentObjectCollection evaluator) {
			long startXref = WriteXrefTable(xrefByteOffsets);

			PdfDictionary trailerDictionary = new PdfDictionary() {
				{ PdfNames.Size, new PdfInt(xrefByteOffsets.Length + 1) },
				{ PdfNames.Root, PdfIndirectReference.Create(root) }
				// TODO Add ID here
			};

			if(infoDictionary != null) {
				trailerDictionary.Add(PdfNames.Info, PdfIndirectReference.Create(infoDictionary));

				using (System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create()) {
					string idData = (infoDictionary?.CreationDate ?? DateTime.Now).ToString("'D:'yyyyMMddHHmmss") + _stream.bytesWritten.ToString(); // Add contents of infoDictionary here?

					byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(idData);
					byte[] hashBytes = md5.ComputeHash(inputBytes);

					PdfString hashStr = new PdfRawString(Encoding.ASCII.GetBytes(HexWriter.ToString(hashBytes)), true);
					PdfArray idArray = new PdfArray(hashStr, hashStr);

					trailerDictionary.Add(PdfNames.ID, idArray);
				}
			}

			_stream.WriteASCII("trailer");
			_stream.WriteEOL();
			WriteDictionary(trailerDictionary, evaluator, false);
			_stream.WriteEOL();

			_stream.WriteASCII("startxref");
			_stream.WriteEOL();
			_stream.WriteInt(startXref);
			_stream.WriteEOL();

			_stream.WriteASCII("%%EOF");
		}

		private class DocumentObjectCollection : IEqualityComparer<PdfObject> {

			private readonly Dictionary<PdfObject, int> collection;
			private readonly Dictionary<PdfFont, PdfObject> fontReferenceObjects;
			public readonly PdfObject[] indirectObjects;

			public bool Equals(PdfObject? x, PdfObject? y) {
				// Use object references as hashcodes, rather than overriden implementations
				return RuntimeHelpers.GetHashCode(x) == RuntimeHelpers.GetHashCode(y);
			}
			public int GetHashCode(PdfObject obj) {
				// Use object references as hashcodes, rather than overriden implementations
				return RuntimeHelpers.GetHashCode(obj);
			}

			public DocumentObjectCollection(IEnumerable<PdfObject> docObjs) {
				collection = new Dictionary<PdfObject, int>(this);
				fontReferenceObjects = new Dictionary<PdfFont, PdfObject>(FontCombinableComparer.Instance);

				IEqualityComparer<PdfObject> comparer = EqualityComparer<PdfObject>.Default;

				FontList fontProxies = new FontList();

				List<PdfObject> indirectObjects = new List<PdfObject>();
				List<int> hashCodes = new List<int>();

				void PerformAppend(PdfObject obj) {
					int objHashCode = comparer.GetHashCode(obj);
					int index = -1;

					for (int i = 0; i < indirectObjects.Count; i++) {
						if (objHashCode == hashCodes[i] && comparer.Equals(indirectObjects[i], obj)) {
							index = i;
							break;
						}
					}

					if (index < 0) {
						index = indirectObjects.Count;
						indirectObjects.Add(obj);
						hashCodes.Add(objHashCode);
					}
					collection.Add(obj, index + 1);
				}

				foreach (PdfObject obj in docObjs) {
					if(obj is PdfFontProxyObject fontProxy) {
						fontProxies.AddFont(fontProxy);
					}
					else if (!collection.ContainsKey(obj)) {
						PerformAppend(obj);
					}
				}

				foreach(PdfFontProxyObject fontProxy in fontProxies) {
					IEnumerable<PdfObject> fontDocObjs = fontProxy.CollectObjects(out PdfIndirectReference fontRef);
					fontReferenceObjects[fontProxy.Font] = fontRef.Subject;
					foreach (PdfObject obj in fontDocObjs) {
						if (!collection.ContainsKey(obj)) {
							PerformAppend(obj);
						}
					}
				}

				this.indirectObjects = indirectObjects.ToArray();
			}

			public int ReferenceEvaluator(PdfObject pdfObj) {
				/*
				if (!collection.ContainsKey(pdfObj)) {
					Console.WriteLine("PROBLEM");
				}
				*/
				return collection[pdfObj];
			}

			public int FontReferenceEvaluator(PdfFont font) {
				return collection[fontReferenceObjects[font]];
			}

			public class FontList : List<PdfFontProxyObject> {

				public FontList() : base() { }

				public void AddFont(PdfFontProxyObject item) {
					for (int i = 0; i < Count; i++) {
						if (this[i].CanCombine(item)) {
							this[i] = this[i].Combine(item);
							return;
						}
					}
					Add(item);
				}

			}
		}

		private static DocumentObjectCollection CollectDocumentObjects(PdfDocument document) {
			return new DocumentObjectCollection(document.CollectObjects());
		}

		public void WriteDocument(PdfDocument document) {
			if (document.PageCount == 0) {
				throw new PdfInvalidOperationException("Cannot write a PDF document with zero pages.");
			}

			//Console.WriteLine("Start collation");
			DocumentObjectCollection documentObjects = CollectDocumentObjects(document);
			//Console.WriteLine("End collation");

			WriteHeader();

			List<long> xrefs = new List<long>();

			foreach (PdfObject obj in documentObjects.indirectObjects) {
				long objOffset = WriteIndirectObject(obj, documentObjects);
				xrefs.Add(objOffset);
			}

			WriteTrailer(document.catalogueDict, document.metadataDict, xrefs.ToArray(), documentObjects);
		}

	}

	public class PdfFontProxyObject : PdfObject {

		public readonly PdfFont Font;
		public readonly FontGlyphUsage FontUsage;

		public PdfFontProxyObject(PdfFont font, FontGlyphUsage fontUsage) {
			Font = font;
			FontUsage = fontUsage;
		}

		public bool CanCombine(PdfFontProxyObject other) {
			//return this.Font.Equals(other.Font);
			return this.Font.CanCombine(other.Font);
		}

		public PdfFontProxyObject Combine(PdfFontProxyObject other) {
			if (Font.TryCombine(other.Font, out PdfFont? combined)) { // Font.Equals(other.Font)
				FontGlyphUsage combinedUsage = FontGlyphUsage.Combine(FontUsage, other.FontUsage);
				return new PdfFontProxyObject(combined, combinedUsage);
			}
			else {
				throw new InvalidOperationException($"Cannot combine font proxies with incompatible fonts.");
			}
		}

		public IEnumerable<PdfObject> CollectObjects(out PdfIndirectReference fontReference) {
			return Font.CollectObjects(FontUsage, out fontReference);
		}

	}

	public sealed class PdfIndirectFontReference : PdfObject {

		public PdfFont Subject { get; }

		private PdfIndirectFontReference(PdfFont subject) {
			this.Subject = subject ?? throw new ArgumentNullException(nameof(subject));
		}

		public static PdfIndirectFontReference Create(PdfFont subject) {
			return new PdfIndirectFontReference(subject);
		}

		public override int GetHashCode() => Subject.GetHashCode();

		public override bool Equals(object? obj) {
			if (obj is PdfIndirectFontReference fontRef) {
				return Subject.Equals(fontRef.Subject);
			}
			return false;
		}

		public override string ToString() {
			return $"FONT_REFERENCE";
		}

	}

}
