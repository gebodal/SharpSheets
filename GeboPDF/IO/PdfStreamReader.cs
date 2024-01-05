using GeboPdf.Objects;
using GeboPdf.Utilities;
using GeboPdf.XObjects;
using GeboPDF.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;

namespace GeboPdf.IO {

	public class PdfStreamReader {

		private readonly Stream _stream;

		private readonly XRefTable xref;

		private readonly Dictionary<PdfObjectKey, PdfObject> _cache;

		private readonly PdfDictionary root;
		private readonly PdfDictionary? info;

		private readonly PdfDictionary[] pages;
		public int PageCount => pages.Length;

		private readonly Dictionary<string, PdfObject> fieldValues;

		public PdfStreamReader(Stream stream) {
			if (!stream.CanRead) {
				throw new ArgumentException("Provided stream is not readable.");
			}
			if (!stream.CanSeek) {
				throw new ArgumentException("PdfStreamReader cannot read from a Stream that is not seekable.");
			}

			_stream = stream;
			this._cache = new Dictionary<PdfObjectKey, PdfObject>();

			long xRefPosition = FindXRefLocation(stream);
			xref = ReadXRef(_stream, xRefPosition, this._cache);

			/*
			Console.WriteLine($"Cross-Reference Table (root {xref.Root}, info {xref.Info}):");
			foreach (KeyValuePair<PdfObjectKey, PdfObjectLocation> entry in xref.OrderBy(kv => kv.Key.Index)) {
				Console.Write($"{entry.Key}: ");
				if(entry.Value.Type == PdfObjectLocationType.Offset) {
					Console.Write($"{entry.Value.Offset,10}");
				}
				else {
					Console.Write($"{entry.Value.StreamObject,5} {entry.Value.Index,5}");
				}
				Console.WriteLine();
			}
			*/

			root = (ReadDocumentObject(xref.Root, false) as PdfDictionary) ?? throw new FormatException("Could not read document root.");

			info = xref.Info is null ? null : ReadDocumentObject(xref.Info, false) as PdfDictionary;

			PdfIndirectReferenceStandIn pagesRef = (root.GetValueOrFallback(PdfNames.Pages, null) as PdfIndirectReferenceStandIn) ?? throw new FormatException("Could not find pages reference in document root.");

			PdfDictionary rootPagesDict = (ReadDocumentObject((PdfObjectKey)pagesRef, false) as PdfDictionary) ?? throw new FormatException("Could not read pages dictionary.");
			if(!(rootPagesDict.TryGetValue(PdfNames.Type, out PdfObject? pagesType) && pagesType is PdfName pagesTypeName && pagesTypeName.Equals(PdfNames.Pages))) {
				throw new FormatException("Pages dictionary has incorrect Type.");
			}

			/*
			Console.WriteLine(root);
			Console.WriteLine(info);
			Console.WriteLine(rootPagesDict);
			Console.WriteLine($"(root {xref.Root}, info {xref.Info})");
			*/

			this.pages = CollectPages(rootPagesDict).ToArray();

			/*
			Console.WriteLine($"\nPages (count = {collectedPages.Count}, reference = {pagesRef}, location = {{{pagesLoc}}}):");
			foreach (PdfDictionary pageObj in collectedPages) {
				Console.WriteLine(pageObj);
			}
			*/

			/*
			Console.WriteLine(this.pages[0]);
			foreach (PdfIndirectReferenceStandIn index in CollectPageResources(this.pages[0]).OrderBy(r => r)) {
				Console.WriteLine(index + " " + ReadDocumentObject((PdfObjectKey)index, false));
			}
			*/

			//Console.WriteLine();
			fieldValues = GetFormFields();

			/*
			foreach(KeyValuePair<string, PdfObject> fieldEntry in fieldValues) {
				Console.WriteLine($"{fieldEntry.Key}: {fieldEntry.Value}");
			}
			*/
		}

		public PdfFormXObject GetPageAsXObject(int page) {

			PdfDictionary pageDict = pages[page];

			PdfObject[] pageResourceObjects = CollectPageResources(pageDict).Select(i => ReadDocumentObject((PdfObjectKey)i, false)).ToArray();

			PdfArray? bBoxArray = GetPageProperty<PdfArray>(pageDict, PdfNames.TrimBox, false);
			if (bBoxArray is null) {
				bBoxArray = GetPageProperty<PdfArray>(pageDict, PdfNames.CropBox, false);
			}
			if (bBoxArray is null) {
				bBoxArray = GetPageProperty<PdfArray>(pageDict, PdfNames.MediaBox, true);
			}

			if (bBoxArray is null) {
				throw new FormatException("Could not find Page size.");
			}
			else if (bBoxArray.Length != 4) {
				throw new FormatException($"Invalid Page size (expected 4 values, got {bBoxArray.Length}).");
			}

			PdfRectangle bBox = PdfRectangle.FromCorners(GetReal(bBoxArray[0]), GetReal(bBoxArray[1]), GetReal(bBoxArray[2]), GetReal(bBoxArray[3]));
			PdfMatrix matrix = new PdfMatrix(Transform.Translate(-bBox.LowerLeftX, -bBox.LowerLeftY));

			PdfStream? contentsStream = GetPageProperty<PdfStream>(pageDict, PdfNames.Contents, false);

			if(contentsStream is null) {
				throw new FormatException("Could not find page dict Contents entry.");
			}

			PdfObject? contentsFilter = contentsStream.GetValueOrFallback(PdfNames.Filter, null);
			PdfObject? contentsDecodeParms = contentsStream.GetValueOrFallback(PdfNames.DecodeParms, null);

			//PdfDictionary pageResources = (PdfDictionary)ReplaceReferenceIndexes(GetPageProperty<PdfDictionary>(pageDict, PdfNames.Resources, true), _cache);
			PdfDictionary pageResources = GetPageProperty<PdfDictionary>(pageDict, PdfNames.Resources, true) ?? new PdfDictionary();

			/*
			for(int i=0; i<pageResourceObjects.Length; i++) {
				pageResourceObjects[i] = ReplaceReferenceIndexes(pageResourceObjects[i], _cache);
			}
			*/

			return new PdfFormXObject(
				bBox, matrix,
				contentsStream.GetStream(),
				contentsFilter, contentsDecodeParms,
				pageResources, pageResourceObjects);
		}

		private static float GetReal(PdfObject obj) {
			if(obj is PdfFloat floatObj) {
				return floatObj.Value;
			}
			else if (obj is PdfInt intObj) {
				return intObj.Value;
			}
			else {
				throw new FormatException($"Cannot get real value from {obj.GetType().Name}.");
			}
		}

		/*
		private static PdfObject ReplaceReferenceIndexes(PdfObject obj, IReadOnlyDictionary<PdfObjectKey, PdfObject> mapping) {
			if (obj is PdfIndirectReferenceIndex refIndex) {
				PdfObject refObj = mapping[(PdfObjectKey)refIndex];
				refObj = ReplaceReferenceIndexes(refObj, mapping);
				return new PdfIndirectReference(refObj);
			}
			else if (obj is PdfArray array) {
				return new PdfArray(array.Select(i => ReplaceReferenceIndexes(i, mapping)));
			}
			else if (obj is PdfDictionary dict) {
				PdfDictionary replacedDict = new PdfDictionary();
				foreach (KeyValuePair<PdfName, PdfObject> entry in dict) {
					replacedDict.Add(entry.Key, ReplaceReferenceIndexes(entry.Value, mapping));
				}
				return replacedDict;
			}
			else if(obj is PdfStream stream) {
				PdfDictionary newDict = (PdfDictionary)ReplaceReferenceIndexes(stream.GetDictionary(), mapping);
				return new PdfStream(newDict, stream.GetStream(), stream.AllowEncoding);
			}
			else {
				return obj;
			}
		}
		*/

		private IEnumerable<PdfDictionary> CollectPages(PdfDictionary pages) {
			PdfArray kids = (pages.GetValueOrFallback(PdfNames.Kids, null) as PdfArray) ?? throw new FormatException("Could not find kids list for this pages dictionary.");

			//Console.WriteLine("Kids: " + kids);

			foreach(PdfObject kid in kids) {
				if(kid is not PdfIndirectReferenceStandIn kidRef) {
					throw new FormatException("Pages kids array must contain only indirect-reference objects.");
				}

				PdfDictionary kidObjDict = (ReadDocumentObject((PdfObjectKey)kidRef, false) as PdfDictionary) ?? throw new FormatException("Could not read kid object dictionary.");
				PdfName kidTypeName = (kidObjDict.GetValueOrFallback(PdfNames.Type, null) as PdfName) ?? throw new FormatException("Pages kid object does not have readable Type.");
				
				if (kidTypeName.Equals(PdfNames.Pages)) {
					foreach(PdfDictionary leafPage in CollectPages(kidObjDict)) {
						yield return leafPage;
					}
				}
				else if (kidTypeName.Equals(PdfNames.Page)) {
					yield return kidObjDict;
				}
				else {
					throw new FormatException("Pages kid object has incorrect Type.");
				}
			}
		}

		private HashSet<PdfIndirectReferenceStandIn> CollectPageResources(PdfDictionary pageDict) {
			HashSet<PdfIndirectReferenceStandIn> references = new HashSet<PdfIndirectReferenceStandIn>();

			bool inheritResources = false;

			PdfObject? pageResourcesRef = pageDict.GetValueOrFallback(PdfNames.Resources, null);
			if (pageResourcesRef is null) {
				inheritResources = true;
			}
			else {
				CollectReferencesRecurse(pageResourcesRef, references, false);
			}

			if (inheritResources) {
				PdfIndirectReferenceStandIn? parentRef = pageDict.GetValueOrFallback(PdfNames.Parent, null) as PdfIndirectReferenceStandIn;
				while (inheritResources && parentRef != null) {
					PdfDictionary parentDict = (ReadDocumentObject((PdfObjectKey)parentRef, false) as PdfDictionary) ?? throw new FormatException("Could not read parent object in page tree.");

					PdfIndirectReferenceStandIn? parentResourcesRef = parentDict.GetValueOrFallback(PdfNames.Resources, null) as PdfIndirectReferenceStandIn;
					if (parentResourcesRef is not null) {
						CollectReferencesRecurse(parentResourcesRef, references, false);
						inheritResources = false;
					}

					parentRef = parentDict.GetValueOrFallback(PdfNames.Parent, null) as PdfIndirectReferenceStandIn;
				}
			}

			return references;
		}

		private T? GetPageProperty<T>(PdfDictionary pageDict, PdfName property, bool inheritable) where T : PdfObject {

			PdfObject? propertyObj = pageDict.GetValueOrFallback(property, null);
			if (propertyObj is not null) {
				return ResolveObject<T>(propertyObj, false);
			}

			if (inheritable) {
				PdfIndirectReferenceStandIn? parentRef = pageDict.GetValueOrFallback(PdfNames.Parent, null) as PdfIndirectReferenceStandIn;
				if (parentRef != null) {
					PdfDictionary parentDict = (ReadDocumentObject((PdfObjectKey)parentRef, false) as PdfDictionary) ?? throw new FormatException("Could not read parent object in page tree.");

					return GetPageProperty<T>(parentDict, property, true);
				}
			}

			return null;
		}

		public Dictionary<string, PdfObject> GetFormFields() {
			PdfObject? acroFormEntry = root.GetValueOrFallback(PdfNames.AcroForm, null);
			PdfDictionary acroFormDict;
			if(acroFormEntry is null) {
				return new Dictionary<string, PdfObject>();
			}
			else {
				acroFormDict = ResolveObject<PdfDictionary>(acroFormEntry, false);
			}

			PdfObject? fieldsEntry = acroFormDict.GetValueOrFallback(PdfNames.Fields, null);
			PdfArray fieldsArray;
			if (fieldsEntry is null) {
				throw new FormatException("Could not find AcroForm Fields entry.");
			}
			else {
				fieldsArray = ResolveObject<PdfArray>(fieldsEntry, false);
			}

			/*
			foreach(PdfObject fieldObj in fieldsArray) {
				Console.WriteLine(fieldObj);
			}
			*/

			return fieldsArray
				.Select(i => ResolveObject<PdfDictionary>(i, false))
				.SelectMany(d => CollectFormFieldsRecurse(d, null))
				.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
		}

		private IEnumerable<KeyValuePair<string, PdfObject>> CollectFormFieldsRecurse(PdfDictionary formDict, string? parentName) {
			PdfObject? kidsEntry = formDict.GetValueOrFallback(PdfNames.Kids, null);
			PdfArray? kidsArray = kidsEntry != null ? ResolveObject<PdfArray>(kidsEntry, false) : null;

			PdfObject? partialNameEntry = formDict.GetValueOrFallback(PdfNames.PartialFieldName, null);
			PdfString? partialNameValue = partialNameEntry != null ? ResolveObject<PdfString>(partialNameEntry, false) : null;

			PdfObject? valueEntry = formDict.GetValueOrFallback(PdfNames.FieldValue, null);
			PdfObject? valueObj = valueEntry != null ? ResolveObject<PdfObject>(valueEntry, false) : null;

			// Regardless of whether this is a Hex string, we need to access the string name
			string? partialName = partialNameValue is null ? null : PdfStringEncoding.GetLiteralString(partialNameValue.Value);

			string? name = null;
			if (parentName != null && partialName != null) {
				name = parentName + "." + partialName;
			}
			else if (parentName != null) {
				name = parentName;
			}
			else if (partialName != null) {
				name = partialName;
			}

			if (name != null && valueObj is not null) {
				yield return new KeyValuePair<string, PdfObject>(name, valueObj);
			}

			if (kidsArray != null) {
				foreach (PdfObject kidsObj in kidsArray) {
					PdfDictionary kidDict = ResolveObject<PdfDictionary>(kidsObj, false);
					foreach (KeyValuePair<string, PdfObject> kidField in CollectFormFieldsRecurse(kidDict, name)) {
						yield return kidField;
					}
				}
			}
		}

		private T ResolveObject<T>(PdfObject obj, bool decodeStreams) where T : PdfObject {
			if(obj is null) {
				throw new ArgumentNullException(nameof(obj));
			}

			if (obj is T correctType) {
				return correctType;
			}
			else if (obj is PdfIndirectReferenceStandIn reference) {
				T? resolved = ReadDocumentObject((PdfObjectKey)reference, decodeStreams) as T;

				if (resolved is null) {
					throw new FormatException($"Could not read {typeof(T).Name}.");
				}

				return resolved;
			}
			else {
				throw new FormatException($"Cannot resolve object of type {obj.GetType().Name} into {typeof(T).Name}.");
			}
		}

		private HashSet<PdfIndirectReferenceStandIn> CollectReferences(PdfObject obj, bool decodeStreams) {
			HashSet<PdfIndirectReferenceStandIn> references = new HashSet<PdfIndirectReferenceStandIn>();
			CollectReferencesRecurse(obj, references, decodeStreams);
			return references;
		}

		private void CollectReferencesRecurse(PdfObject obj, HashSet<PdfIndirectReferenceStandIn> references, bool decodeStreams) {
			if (obj is PdfIndirectReferenceStandIn refIndex) {
				if (!references.Contains(refIndex)) {
					references.Add(refIndex);
					PdfObject referencedObj = ReadDocumentObject((PdfObjectKey)refIndex, decodeStreams);
					CollectReferencesRecurse(referencedObj, references, decodeStreams);
				}
			}
			else if (obj is AbstractPdfArray array) {
				foreach (PdfObject arrayObj in array) {
					CollectReferencesRecurse(arrayObj, references, decodeStreams);
				}
			}
			else if (obj is AbstractPdfDictionary dict) {
				foreach (PdfObject dictValueObj in dict.Select(kv => kv.Value)) {
					CollectReferencesRecurse(dictValueObj, references, decodeStreams);
				}
			}
		}

		#region Cross Reference Tables

		private static XRefTable ReadXRef(Stream stream, long xRefPosition, Dictionary<PdfObjectKey, PdfObject> register) {
			//stream.Position = xRefPosition;
			stream.Seek(xRefPosition, SeekOrigin.Begin);

			int current = stream.ReadByte();
			if (current == -1) {
				throw new FormatException("Cannot read xref table.");
			}
			else if (current == 120) { // 'x'
				if(!IsMatch(stream, Encoding.ASCII.GetBytes("ref"))) {
					throw new FormatException("Could not find xref keyword for xref table.");
				}
				stream.Seek(3, SeekOrigin.Current);
				SkipWhitespace(stream);
				return ReadXRefTable(stream, register);
			}
			else if (IsDigit((byte)current)) {
				stream.Seek(-1, SeekOrigin.Current);
				return ReadXRefObj(stream, register);
			}
			else if(current == 60) { // '<'
				stream.Seek(-1, SeekOrigin.Current);
				return ReadXRefDictionary(stream, register);
			}
			else {
				throw new FormatException("Cannot read xref values.");
			}
		}

		private static XRefTable ReadXRefTable(Stream stream, Dictionary<PdfObjectKey, PdfObject> register) {
			int current = stream.ReadByte();
			stream.Seek(-1, SeekOrigin.Current);

			Dictionary<uint, (long offset,long generation)> tableData = new Dictionary<uint, (long,long)>();

			while (current != -1 && IsDigit((byte)current)) {

				uint firstObjNum = ReadUInt(stream);
				SkipWhitespace(stream);
				uint numObj = ReadUInt(stream);
				SkipWhitespace(stream);

				byte[] buffer = new byte[20];
				for (uint i = 0; i < numObj; i++) {
					stream.Read(buffer, 0, 20);

					uint objectNum = firstObjNum + i;
					long byteOffset = long.Parse(Encoding.ASCII.GetString(buffer, 0, 10));
					long generationNum = long.Parse(Encoding.ASCII.GetString(buffer, 11, 5));
					char entryState = (char)buffer[17];

					//Console.WriteLine($"Object {objectNum,4}: {byteOffset,10} {generationNum,5} {entryState}");

					if(entryState == 'n') {
						tableData.Add(objectNum, (byteOffset, generationNum));
					}
				}

				current = stream.ReadByte();
				stream.Seek(-1, SeekOrigin.Current);
			}

			SkipWhitespace(stream);
			if(!IsMatchConsume(stream, Encoding.ASCII.GetBytes("trailer"))) {
				throw new FormatException("Could not find trailer keyword for xref table.");
			}
			SkipWhitespace(stream);

			PdfDictionary trailer = (ReadRawObject(stream, stream.Length, register) as PdfDictionary) ?? throw new FormatException("Could not read trailer dictionary.");

			//Console.WriteLine(trailer);

			PdfIndirectReferenceStandIn root = (trailer[PdfNames.Root] as PdfIndirectReferenceStandIn) ?? throw new FormatException("Could not read document root in trailer dictionary.");
			PdfIndirectReferenceStandIn? info = trailer.GetValueOrFallback(PdfNames.Info, null) as PdfIndirectReferenceStandIn;

			XRefTable table = new XRefTable((PdfObjectKey)root, (PdfObjectKey?)info);

			foreach(KeyValuePair<uint, (long offset, long generation)> entry in tableData) {
				table.Add(new PdfObjectKey(entry.Key, (uint)entry.Value.generation), new PdfObjectLocation(entry.Value.offset));
			}

			int? prev = (trailer.GetValueOrFallback(PdfNames.Previous, null) as PdfInt)?.Value;
			if (prev.HasValue) {
				XRefTable prevTable = ReadXRef(stream, prev.Value, register);

				table = table.Append(prevTable);
			}

			return table;
		}

		private static XRefTable ReadXRefObj(Stream stream, Dictionary<PdfObjectKey, PdfObject> register) {
			//Console.WriteLine("Reading xref object");
			SkipObjectHeader(stream, out _, out _);
			return ReadXRefDictionary(stream, register);
		}

		private static XRefTable ReadXRefDictionary(Stream stream, Dictionary<PdfObjectKey, PdfObject> register) {

			PdfStream pdfStream = ReadStream(stream, true, register);

			/*
			Console.WriteLine("<<");
			foreach(KeyValuePair<PdfName, PdfObject> entry in pdfStream) {
				Console.WriteLine(entry.Key + " " + entry.Value);
			}
			Console.WriteLine(">>");
			*/

			PdfIndirectReferenceStandIn root = (pdfStream[PdfNames.Root] as PdfIndirectReferenceStandIn) ?? throw new FormatException("Could not find document root in cross-reference dictionary.");
			PdfIndirectReferenceStandIn? info = pdfStream.GetValueOrFallback(PdfNames.Info, null) as PdfIndirectReferenceStandIn;

			byte[] streamData = pdfStream.GetStream().ToArray();

			//Console.WriteLine("Cross Reference stream content");

			int size = ((PdfInt)pdfStream[PdfNames.Size]).Value;
			PdfArray index = (pdfStream.GetValueOrFallback(PdfNames.Index, null) as PdfArray) ?? new PdfArray((PdfInt)0, (PdfInt)size);
			uint firstObjNum = (uint)((PdfInt)index[0]).Value;
			uint numObj = (uint)((PdfInt)index[1]).Value;
			int[] fieldSizes = ((PdfArray)pdfStream[PdfNames.FieldSizes]).Select(s => ((PdfInt)s).Value).ToArray();
			int totalFieldSize = fieldSizes.Sum();

			long[,] xreftable = new long[numObj, fieldSizes.Length];

			for (int row = 0; row < numObj; row++) {
				int byteIndex = 0;
				for (int i = 0; i < fieldSizes.Length; i++) {

					long value = 0L;
					for (int b = 0; b < fieldSizes[i]; b++) {
						value = (value << 8) | (long)streamData[row * totalFieldSize + byteIndex + b];
					}
					xreftable[row, i] = value;

					byteIndex += fieldSizes[i];
				}
			}

			XRefTable table = new XRefTable((PdfObjectKey)root, (PdfObjectKey?)info);

			//Console.WriteLine("Table:");
			for (int i = 0; i < xreftable.GetLength(0); i++) {
				/*
				for (int j = 0; j < xreftable.GetLength(1); j++) {
					Console.Write($"({i,2},{j,2}): {xreftable[i, j],5} ");
				}
				Console.WriteLine();
				*/
				if (xreftable[i, 0] == 0) {
					/*
					uint objectNum = (uint)(firstObjNum + i);
					long nextFreeObjNum = xreftable[i, 1];
					long generationNum = xreftable[i, 2];
					char entryState = 'f';
					Console.WriteLine($"Object {objectNum,4}: {nextFreeObjNum,10} {generationNum,5} {entryState}");
					*/
				}
				else if (xreftable[i, 0] == 1) {
					uint objectNum = (uint)(firstObjNum + i);
					long byteOffset = xreftable[i, 1];
					long generationNum = xreftable[i, 2];
					//char entryState = 'n';
					//Console.WriteLine($"Object {objectNum,4}: {byteOffset,10} {generationNum,5} {entryState}");

					table.Add(new PdfObjectKey(objectNum, (uint)generationNum), new PdfObjectLocation(byteOffset));
				}
				else if (xreftable[i, 0] == 2) {
					uint objectNum = (uint)(firstObjNum + i);
					long objStmNum = xreftable[i, 1]; // Index of the object stream that contains this object
					long stmIdx = xreftable[i, 2]; // Index of this object in the object stream
					//Console.WriteLine($"ObjStm {objectNum,4}: {objStmNum,10} {stmIdx,10}");

					table.Add(new PdfObjectKey(objectNum, 0), new PdfObjectLocation((uint)objStmNum, (uint)stmIdx));
				}
				else {
					throw new FormatException($"Invalid cross-reference stream entry type: {xreftable[i, 0]}");
				}
			}

			int? prev = (pdfStream.GetValueOrFallback(PdfNames.Previous, null) as PdfInt)?.Value;
			if (prev.HasValue) {
				XRefTable prevTable = ReadXRef(stream, prev.Value, register);

				table = table.Append(prevTable);
			}

			return table;
		}

		private static long FindXRefLocation(Stream stream) {
			stream.Seek(0, SeekOrigin.End);

			if (!FindReverse(stream, Encoding.ASCII.GetBytes("startxref"), 1024)) { // Acrobat only requires EOF marker be in the last 1024 bytes of file
				throw new FormatException("Cannot find startxref marker in file.");
			}

			stream.Seek(10, SeekOrigin.Current); // Move past "\nstartxref"
			SkipWhitespace(stream);

			int current = stream.ReadByte();
			if (current == -1 || !IsDigit((byte)current)) {
				throw new FormatException("Cannot find xref location in file.");
			}

			long xrefLocation1 = 0;

			while (current != -1 && IsDigit((byte)current)) {
				xrefLocation1 *= 10;
				xrefLocation1 += GetDigit((byte)current);
				current = stream.ReadByte();
			}

			stream.Seek(-1, SeekOrigin.Current);
			SkipWhitespace(stream);

			if (!IsMatch(stream, Encoding.ASCII.GetBytes("%%EOF"))) {
				throw new FormatException("Cannot find EOF marker in file.");
			}

			return xrefLocation1;
		}

		#endregion

		private PdfObject ReadDocumentObject(PdfObjectKey key, bool decodeStreams) {
			if (key is null) { throw new ArgumentNullException(nameof(key)); }

			if(_cache.TryGetValue(key, out PdfObject? cachedObj)) {
				return cachedObj;
			}
			
			PdfObjectLocation location = xref.GetLocation(key) ?? throw new ArgumentException($"{nameof(key)} not found in xref table.");

			if (location.Type == PdfObjectLocationType.Offset) {
				PdfObject obj = ReadDocumentObject(location.Offset, decodeStreams);
				_cache.Add(key, obj);
				return obj;
			}
			else { // Object stream
				PdfObjectKey streamKey = new PdfObjectKey(location.StreamObject, 0);
				PdfObjectLocation streamLoc = xref.GetLocation(streamKey) ?? throw new FormatException("Could not find stream location.");
				if(streamLoc.Type == PdfObjectLocationType.Stream) {
					throw new FormatException("Stream objects cannot be read from inside streams.");
				}

				PdfStream objStream;
				if (_cache.TryGetValue(streamKey, out PdfObject? cachedObjStream)) {
					objStream = (PdfStream)cachedObjStream;
				}
				else {
					_stream.Position = streamLoc.Offset;
					SkipObjectHeader(_stream, out _, out _);
					objStream = ReadStream(_stream, true, _cache);
					_cache.Add(streamKey, objStream);
				}

				PdfObject obj = ReadObjectFromStream(objStream, location.Index, _cache);
				_cache.Add(key, obj);
				return obj;
			}
		}

		private static PdfObject ReadObjectFromStream(PdfStream objStream, uint index, Dictionary<PdfObjectKey, PdfObject> register) {
			int n = (objStream.GetValueOrFallback(PdfNames.NumObjects, null) as PdfInt)?.Value ?? throw new FormatException("Object streams must have a listed number of objects.");
			int first = (objStream.GetValueOrFallback(PdfNames.First, null) as PdfInt)?.Value ?? throw new FormatException("Object streams must have a listed offset for first object.");

			uint[] objectIdxs = new uint[n];
			long[] objectOffsets = new long[n];

			MemoryStream s = objStream.GetStream();
			s.Seek(0, SeekOrigin.Begin);
			SkipWhitespace(s);

			for(int i=0; i<n; i++) {
				uint objNidx = ReadUInt(s);
				SkipWhitespace(s);
				long objNoffset = ReadLong(s);
				SkipWhitespace(s);
				objectIdxs[i] = objNidx;
				objectOffsets[i] = objNoffset;
			}

			//Console.WriteLine(Encoding.ASCII.GetString(s.ToArray()));

			long startPos = first + objectOffsets[index];
			long maxPos = (index < n - 1 ? (first + objectOffsets[index + 1]) : s.Length);

			//Console.WriteLine("ObjStm: " + Encoding.ASCII.GetString(s.ToArray()).Substring((int)startPos, (int)(maxPos - startPos)));

			s.Position = startPos;
			return ReadRawObject(s, maxPos, register);
		}

		private PdfObject ReadDocumentObject(long offset, bool decodeStreams) {
			_stream.Position = offset;
			SkipObjectHeader(_stream, out _, out _);

			PdfObject pdfObj = ReadRawObject(_stream, _stream.Length, _cache);

			SkipWhitespace(_stream);
			if (IsMatchConsume(_stream, Encoding.ASCII.GetBytes("stream"))) {
				if (pdfObj is not PdfDictionary streamDict) {
					throw new FormatException("Stream must have a direct object dictionary.");
				}

				int current = _stream.ReadByte();
				if (current == '\r') {
					current = _stream.ReadByte();
				}
				if (current != '\n') {
					throw new FormatException("Invalid follow-on byte from stream tag.");
				}

				pdfObj = ReadStream(streamDict, decodeStreams);
			}

			return pdfObj;
		}

		private PdfStream ReadStream(PdfDictionary streamDictionary, bool decode) {

			PdfInt? streamLen = null;
			if (streamDictionary.TryGetValue(PdfNames.Length, out PdfObject? lenObj)) {
				long position = _stream.Position;
				streamLen = ResolveObject<PdfInt>(lenObj, false);
				_stream.Position = position;
			}

			if (streamLen == null) {
				throw new FormatException("No valid stream Length provided.");
			}

			streamDictionary.Remove(PdfNames.Length);

			byte[] streamContent = new byte[streamLen.Value];
			int bytesRead = _stream.Read(streamContent, 0, streamLen.Value);

			if (bytesRead != streamLen.Value) {
				throw new FormatException("Could not read stream content.");
			}

			MemoryStream streamData = new MemoryStream(streamContent);

			if (decode) {
				DecodeStream(ref streamDictionary, ref streamData);
			}

			return new PdfStream(streamDictionary, streamData, false);
		}

		private enum ParseState { None, OpenedAngleBracket, CloseAngleBracket, Name, LiteralString, HexString, Numeric, Float, Comment }
		private enum PdfObjectType { Dictionary, Array }

		private static PdfObject ReadRawObject(Stream stream, long maxPos, Dictionary<PdfObjectKey, PdfObject> register) {

			ParseState state = ParseState.None;
			byte current;

			Stack<PdfObjectType> objectStack = new Stack<PdfObjectType>();
			Stack<List<PdfObject>> items = new Stack<List<PdfObject>>();
			items.Push(new List<PdfObject>());

			StringBuilder builder = new StringBuilder();

			bool escaped = false;
			int bracketCount = 0;

			ParseState previousState = ParseState.None;

			while (true) {
				if (stream.Position >= maxPos) {
					break;
				}

				int next = stream.ReadByte();
				if (next == -1) {
					//throw new EndOfStreamException("Stream ended before end of object.");
					break;
				}
				current = (byte)next;

				if (state == ParseState.None) {
					if (whitespace.Contains(current)) {
						continue;
					}
					else if (current == '<') {
						state = ParseState.OpenedAngleBracket;
					}
					else if (current == '>') {
						state = ParseState.CloseAngleBracket;
					}
					else if (current == '[') {
						objectStack.Push(PdfObjectType.Array);
						items.Push(new List<PdfObject>());
						state = ParseState.None;
					}
					else if (current == ']') {
						if (objectStack.Count > 0 && objectStack.Peek() == PdfObjectType.Array) {
							objectStack.Pop();
							List<PdfObject> arrayItems = items.Pop();
							PdfArray array = new PdfArray(arrayItems);
							items.Peek().Add(array);
							state = ParseState.None;
						}
						else {
							throw new FormatException("Invalid array end sequence encountered.");
						}
					}
					else if (current == '(') {
						state = ParseState.LiteralString;
						builder.Clear();
						bracketCount = 0;
					}
					else if (IsDigit(current)) {
						state = ParseState.Numeric;
						stream.Seek(-1, SeekOrigin.Current); // Better way?
						builder.Clear();
					}
					else if (current == '-' || current == '+') {
						state = ParseState.Numeric;
						builder.Clear();
						builder.Append((char)current); // +/- only allowed at start of numeric
					}
					else if (current == '/') {
						state = ParseState.Name;
						builder.Clear();
					}
					else if (current == 't') {
						long oldPos = stream.Position;
						if (IsMatchConsume(stream, Encoding.ASCII.GetBytes("rue"))) {
							items.Peek().Add(new PdfBoolean(true));
						}
						else {
							stream.Position = oldPos;
							break;
						}
					}
					else if (current == 'f') {
						long oldPos = stream.Position;
						if (IsMatchConsume(stream, Encoding.ASCII.GetBytes("alse"))) {
							items.Peek().Add(new PdfBoolean(false));
						}
						else {
							stream.Position = oldPos;
							break;
						}
					}
					else if (current == 'n') {
						long oldPos = stream.Position;
						if (IsMatchConsume(stream, Encoding.ASCII.GetBytes("ull"))) {
							items.Peek().Add(PdfObject.Null);
						}
						else {
							stream.Position = oldPos;
							break;
						}
					}
					else if (current == 'R' && items.Peek().Count >= 2 && items.Peek()[^2] is PdfInt refIdx && items.Peek()[^1] is PdfInt refGen) {
						items.Peek().RemoveAt(items.Peek().Count - 1);
						items.Peek().RemoveAt(items.Peek().Count - 1);

						PdfObjectKey key = new PdfObjectKey((uint)refIdx.Value, (uint)refGen.Value);
						PdfIndirectReferenceStandIn reference = new PdfIndirectReferenceStandIn(key, register);
						items.Peek().Add(reference);
					}
					else if (current == '%') {
						previousState = state;
						state = ParseState.Comment;
					}
					else {
						stream.Seek(-1, SeekOrigin.Current);
						break; // We have encountered an invalid character (presumably a "stream" or "endobj")
					}
				}
				else if (state == ParseState.OpenedAngleBracket) {
					if (current == '<') {
						objectStack.Push(PdfObjectType.Dictionary);
						items.Push(new List<PdfObject>());
						state = ParseState.None;
					}
					else {
						state = ParseState.HexString;
						stream.Seek(-1, SeekOrigin.Current); // Better way?
						builder.Clear();
					}
				}
				else if (state == ParseState.CloseAngleBracket) {
					if (current == '>' && objectStack.Count > 0 && objectStack.Peek() == PdfObjectType.Dictionary) {
						objectStack.Pop();
						List<PdfObject> dictionaryItems = items.Pop();
						PdfDictionary dictionary = MakeDictionary(dictionaryItems);
						items.Peek().Add(dictionary);
						state = ParseState.None;
					}
					else {
						throw new FormatException("Invalid dictionary end sequence encountered.");
					}
				}
				else if (state == ParseState.LiteralString) {
					if (escaped) {
						escaped = false;
					}
					else if (current == '\\') {
						escaped = true;
					}
					else if (current == '(') {
						bracketCount++;
					}
					else if (current == ')') {
						if (bracketCount > 0) {
							bracketCount--;
						}
						else {
							PdfString literalStr = new PdfRawString(Encoding.ASCII.GetBytes(builder.ToString()), false);
							items.Peek().Add(literalStr);
							state = ParseState.None;
							continue;
						}
					}
					builder.Append((char)current);
				}
				else if (state == ParseState.HexString) {
					if (current == '>') {
						if (builder.Length % 2 != 0) { // Odd number of characters
							builder.Append('0'); // PDF spec assumes 0 for unbalanced digit pairs
						}
						PdfString hexStr = MakeHexString(builder.ToString());
						items.Peek().Add(hexStr);
						state = ParseState.None;
						continue;
					}
					else if (whitespace.Contains(current)) {
						continue;
					}
					else if (IsHexDigit(current)) {
						builder.Append((char)current);
					}
					else {
						throw new FormatException("Invalid byte encountered in hex string.");
					}
				}
				else if (state == ParseState.Name) {
					if (!(whitespace.Contains(current) || delimiters.Contains(current))) {
						builder.Append((char)current);
					}
					else {
						PdfName name = new PdfName(PdfName.DecodeNameString(builder.ToString()));
						items.Peek().Add(name);
						state = ParseState.None;
						stream.Seek(-1, SeekOrigin.Current);
					}
				}
				else if (state == ParseState.Numeric) {
					if (current == '.') {
						state = ParseState.Float;
						builder.Append('.');
					}
					else if (IsDigit(current)) {
						builder.Append((char)current);
					}
					else {
						int value = int.Parse(builder.ToString());
						items.Peek().Add(new PdfInt(value));
						state = ParseState.None;
						stream.Seek(-1, SeekOrigin.Current);
					}
				}
				else if (state == ParseState.Float) {
					if (IsDigit(current)) {
						builder.Append((char)current);
					}
					else {
						float value = float.Parse(builder.ToString());
						items.Peek().Add(new PdfFloat(value));
						state = ParseState.None;
						stream.Seek(-1, SeekOrigin.Current);
					}
				}
				else if (state == ParseState.Comment) {
					if (current == '\r' || current == '\n') {
						state = previousState;
					}
				}
				else {
					throw new PdfInvalidOperationException("Invalid parse state encountered.");
				}

				//Console.Write((char)current);
			}

			//Console.WriteLine();

			if (items.Count != 1) {
				throw new FormatException("Encountered incomplete object.");
			}
			if (items.Peek().Count != 1) {
				throw new FormatException("Invalid object format (too many objects without a container).");
			}

			return items.Peek()[0];
		}

		private static PdfDictionary MakeDictionary(List<PdfObject> dictionaryItems) {
			if (dictionaryItems.Count % 2 != 0) {
				throw new FormatException("Invalid number of items in dictionary (must come in Key-Value pairs).");
			}

			PdfDictionary dictionary = new PdfDictionary();

			for (int i = 0; i < dictionaryItems.Count; i += 2) {
				PdfName key;
				if(dictionaryItems[i] is PdfName name) {
					key = name;
				}
				else {
					throw new FormatException($"Invalid dictionary key (must be a PdfName, but encountered {dictionaryItems[i].GetType().Name}).");
				}

				dictionary.Add(key, dictionaryItems[i + 1]);
			}

			return dictionary;
		}

		private static PdfString MakeHexString(string hexString) {
			byte[] bytes = new byte[hexString.Length / 2];
			for (int i = 0; i < hexString.Length; i += 2) {
				bytes[i / 2] = HexWriter.ToByte(hexString[i], hexString[i + 1]);
			}
			return new PdfRawString(bytes, true);
		}

		private static PdfStream ReadStream(Stream stream, bool decode, Dictionary<PdfObjectKey, PdfObject> register) {

			PdfObject obj = ReadRawObject(stream, stream.Length, register);

			PdfDictionary? streamDictionary = obj as PdfDictionary;

			if (streamDictionary == null) {
				throw new FormatException("Cannot find stream dictionary.");
			}

			PdfInt? streamLen = null;
			if(streamDictionary.TryGetValue(PdfNames.Length, out PdfObject? lenObj)) {
				streamLen = lenObj as PdfInt;
			}

			if (streamLen == null) {
				throw new FormatException("No valid stream Length provided.");
			}

			streamDictionary.Remove(PdfNames.Length);

			SkipWhitespace(stream);
			if (!IsMatchConsume(stream, Encoding.ASCII.GetBytes("stream"))) {
				throw new FormatException("No stream tag found.");
			}

			int current = stream.ReadByte();
			if(current == '\r') {
				current = stream.ReadByte();
			}
			if(current != '\n') {
				throw new FormatException("Invalid follow-on byte from stream tag.");
			}

			byte[] streamContent = new byte[streamLen.Value];
			int bytesRead = stream.Read(streamContent, 0, streamLen.Value);

			if(bytesRead != streamLen.Value) {
				throw new FormatException("Could not read stream content.");
			}

			MemoryStream streamData = new MemoryStream(streamContent, 0, streamContent.Length, false, true);

			if (decode) {
				DecodeStream(ref streamDictionary, ref streamData);
			}

			return new PdfStream(streamDictionary, streamData, false);
		}

		private static void DecodeStream(ref PdfDictionary streamDictionary, ref MemoryStream streamData) {
			PdfObject? filter = streamDictionary.TryGetValue(PdfNames.Filter, out PdfObject? filterObj) ? filterObj : null;
			PdfDictionary? decodeParms = (streamDictionary.TryGetValue(PdfNames.DecodeParms, out PdfObject? dps) ? dps : null) as PdfDictionary;

			if (filter != null) {
				if (filter is PdfName filterName && filterName.Equals(PdfNames.FlateDecode)) {
					int predictor = (decodeParms?.GetValueOrFallback(PdfNames.Predictor, null) as PdfInt)?.Value ?? 1;
					int columns = (decodeParms?.GetValueOrFallback(PdfNames.Columns, null) as PdfInt)?.Value ?? 1;
					streamData = Deflate1950.Decompress(streamData, predictor: predictor, columns: columns);
				}
				else {
					throw new NotImplementedException("Only FlateDecode is currently supported for decoding.");
				}

				streamDictionary.Remove(PdfNames.Filter);
				streamDictionary.Remove(PdfNames.DecodeParms);
			}
		}

		/// <summary>
		/// Read the previous count bytes from the Stream, including the byte at the current Position.
		/// </summary>
		/// <param name="stream"></param>
		/// <param name="count"></param>
		/// <returns></returns>
		private static int ReadReverse(Stream stream, byte[] buffer, int count) {
			stream.Seek(-(count - 1), SeekOrigin.Current);
			return stream.Read(buffer, 0, count);
		}

		private static byte ReadPrevious(Stream stream) {
			if (stream.Position == 0) {
				throw new InvalidOperationException("Cannot read past start of stream.");
			}

			stream.Seek(-1, SeekOrigin.Current);
			int result = stream.ReadByte();
			if (result < 0) {
				throw new InvalidOperationException("Error reading from stream.");
			}
			stream.Seek(-1, SeekOrigin.Current);
			return (byte)result;
		}

		private static bool FindReverse(Stream stream, byte[] data, int maxSearch) {
			int head = data.Length - 1;
			byte previous;
			for(int i=0; i<maxSearch; i++) {
				previous = ReadPrevious(stream);

				if(previous == data[head]) {
					head--;
					if(head < 0) {
						return true;
					}
				}
				else {
					head = data.Length - 1;
				}
			}
			return false;
		}

		private static bool IsMatch(Stream stream, byte[] data) {
			long oldPos = stream.Position;

			byte[] buffer = new byte[data.Length];
			int count = stream.Read(buffer, 0, data.Length);

			if (count != data.Length) {
				stream.Position = oldPos;
				return false;
			}
			else {
				for (int i = 0; i < data.Length; i++) {
					if (data[i] != buffer[i]) {
						stream.Position = oldPos;
						return false;
					}
				}
				stream.Position = oldPos;
				return true;
			}
		}

		private static bool IsMatchConsume(Stream stream, byte[] data) {
			for (int i = 0; i < data.Length; i++) {
				int next = stream.ReadByte();
				if (next == -1) {
					return false;
				}
				else if (data[i] != (byte)next) {
					return false;
				}
			}

			return true;
		}

		private static uint ReadUInt(Stream stream) {
			uint value = 0;
			bool foundDigit = false;
			int current = stream.ReadByte();
			while (current != -1 && IsDigit((byte)current)) {
				value *= 10;
				value += (uint)GetDigit((byte)current);
				current = stream.ReadByte();
				foundDigit = true;
			}
			if (!foundDigit) {
				throw new FormatException("Could not read unsigned integer value.");
			}
			else {
				return value;
			}
		}

		private static long ReadLong(Stream stream) {
			long value = 0;
			bool foundDigit = false;
			int current = stream.ReadByte();
			while (current != -1 && IsDigit((byte)current)) {
				value *= 10;
				value += (long)GetDigit((byte)current);
				current = stream.ReadByte();
				foundDigit = true;
			}
			if (!foundDigit) {
				throw new FormatException("Could not read unsigned integer value.");
			}
			else {
				return value;
			}
		}

		private static void SkipWhitespace(Stream stream) {
			int current = stream.ReadByte();
			while(current != -1 && whitespace.Contains((byte)current)) {
				current = stream.ReadByte();
			}
			if (current != -1) {
				stream.Seek(-1, SeekOrigin.Current);
			}
			return;
		}

		private static void SkipObjectHeader(Stream stream, out uint objNum, out uint genNum) {
			objNum = ReadUInt(stream); // Object number
			SkipWhitespace(stream);
			genNum = ReadUInt(stream); // Generation number
			SkipWhitespace(stream);
			if (!IsMatchConsume(stream, Encoding.ASCII.GetBytes("obj"))) {
				throw new FormatException("Invalid object header.");
			}
			SkipWhitespace(stream);
		}

		#region Character Sets

		private static readonly HashSet<byte> whitespace = new HashSet<byte>() {
			0, // Null
			9, // Tab
			10, // Line feed
			12, // Form feed
			13, // Carriage return
			32 // Space
		};

		private static readonly HashSet<byte> delimiters = new HashSet<byte>() {
			(byte)'(', (byte)')',
			(byte)'<', (byte)'>',
			(byte)'[', (byte)']',
			(byte)'{', (byte)'}',
			(byte)'/',
			(byte)'%'
		};

		private static bool IsDigit(byte b) {
			return b >= 48 && b <= 57; // '0' to '9'
		}
		private static int GetDigit(byte b) {
			return (int)(b - 48); // -'0'
		}

		private static bool IsHexDigit(byte b) {
			return ('0' <= b && b <= '9') || ('A' <= b && b <= 'F') || ('a' <= b && b <= 'f');
		}

		#endregion

		/*
		private class PdfIndirectReferenceIndex : PdfObject {

			public uint Index { get; }
			public uint Generation { get; }

			public PdfIndirectReferenceIndex(uint index, uint generation) {
				Index = index;
				Generation = generation;
			}

			public static explicit operator PdfObjectKey(PdfIndirectReferenceIndex index) {
				if(index is null) { return null; }
				return new PdfObjectKey(index.Index, index.Generation);
			}

			public override int GetHashCode() {
				unchecked {
					int hash = 17;
					hash = hash * 23 + Index.GetHashCode();
					hash = hash * 23 + Generation.GetHashCode();
					return hash;
				}
			}

			public override bool Equals(object? obj) {
				if (obj is PdfIndirectReferenceIndex other) {
					return Index == other.Index && Generation == other.Generation;
				}
				return false;
			}

			public override string ToString() {
				return $"{Index} {Generation} R";
			}
		}
		*/

		private class PdfIndirectReferenceStandIn : PdfIndirectReference, IComparable<PdfIndirectReferenceStandIn> {

			private readonly Dictionary<PdfObjectKey, PdfObject> register;
			private readonly PdfObjectKey key;

			public override PdfObject Subject {
				get {
					if(register.TryGetValue(key, out PdfObject? subject)) {
						return subject;
					}
					throw new FormatException($"Could not find object {key.Index} {key.Generation} R.");
				}
			}

			public PdfIndirectReferenceStandIn(PdfObjectKey key, Dictionary<PdfObjectKey, PdfObject> register) : base() {
				this.key = key;
				this.register = register;
			}

			[return: NotNullIfNotNull(nameof(reference))]
			public static explicit operator PdfObjectKey?(PdfIndirectReferenceStandIn? reference) {
				if (reference is null) { return null; }
				return reference.key;
			}

			public override int GetHashCode() => key.GetHashCode();

			public override bool Equals(object? obj) {
				if (obj is PdfIndirectReferenceStandIn other) {
					return key.Equals(other.key);
				}
				else {
					return base.Equals(obj);
				}
			}

			public override string ToString() {
				return $"{key.Index} {key.Generation} R";
			}

			public int CompareTo(PdfIndirectReferenceStandIn? other) {
				if(other is null) { return 1; } // Sensible default?
				return key.Index.CompareTo(other.key.Index);
			}
		}

	}

	public class XRefTable : IEnumerable<KeyValuePair<PdfObjectKey, PdfObjectLocation>> {

		private readonly Dictionary<PdfObjectKey, PdfObjectLocation> table;

		public PdfObjectKey Root { get; }
		public PdfObjectKey? Info { get; }

		public XRefTable(PdfObjectKey root, PdfObjectKey? info) {
			this.table = new Dictionary<PdfObjectKey, PdfObjectLocation>();
			this.Root = root;
			this.Info = info;
		}

		public void Add(PdfObjectKey key, PdfObjectLocation location) {
			this.table.Add(key, location);
		}

		public PdfObjectLocation? GetLocation(PdfObjectKey? key) {
			if (key is null) { return null; }
			return table.TryGetValue(key, out PdfObjectLocation? location) ? location : null;
		}
		public bool TryGetLocation(PdfObjectKey key, [MaybeNullWhen(false)] out PdfObjectLocation location) {
			return table.TryGetValue(key, out location);
		}

		/*
		public PdfObjectLocation GetLocation(PdfIndirectReferenceIndex index) {
			if (index is null) { return null; }
			return table.TryGetValue(new PdfObjectKey((uint)index.Index, (uint)index.Generation), out PdfObjectLocation location) ? location : null;
		}
		public bool TryGetLocation(PdfIndirectReferenceIndex index, out PdfObjectLocation location) {
			return table.TryGetValue(new PdfObjectKey((uint)index.Index, (uint)index.Generation), out location);
		}
		*/

		public XRefTable Append(XRefTable other) {
			XRefTable merged = new XRefTable(Root, Info);

			foreach (KeyValuePair<PdfObjectKey, PdfObjectLocation> entry in other.table) {
				if (!table.ContainsKey(entry.Key)) {
					merged.Add(entry.Key, entry.Value);
				}
			}

			foreach (KeyValuePair<PdfObjectKey, PdfObjectLocation> entry in table) {
				merged.Add(entry.Key, entry.Value);
			}

			return merged;
		}

		public IEnumerator<KeyValuePair<PdfObjectKey, PdfObjectLocation>> GetEnumerator() => table.GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)table).GetEnumerator();
	}

	public class PdfObjectKey : IEquatable<PdfObjectKey> {

		public readonly uint Index;
		public readonly uint Generation;

		public PdfObjectKey(uint index, uint generation) {
			Index = index;
			Generation = generation;
		}

		public override bool Equals(object? obj) => Equals(obj as PdfObjectKey);
		public bool Equals(PdfObjectKey? other) => other != null && Index == other.Index && Generation == other.Generation;

		public override int GetHashCode() {
			return HashCode.Combine(Index, Generation);
		}

		/*
		public static PdfObjectKey FromReference(PdfIndirectReferenceIndex refIndex) {
			return (refIndex is null) ? null : new PdfObjectKey((uint)refIndex.Index, (uint)refIndex.Generation);
		}
		*/

		public override string ToString() {
			return $"{Index,4} {Generation,4}";
		}

	}

	public enum PdfObjectLocationType : byte { Offset, Stream }
	public class PdfObjectLocation {
		public PdfObjectLocationType Type { get; }

		public long Offset { get; }

		public uint StreamObject { get; }
		public uint Index { get; }

		public PdfObjectLocation(long offset) {
			Type = PdfObjectLocationType.Offset;
			Offset = offset;
			StreamObject = 0;
			Index = 0;
		}

		public PdfObjectLocation(uint streamObject, uint index) {
			Type = PdfObjectLocationType.Stream;
			Offset = 0;
			StreamObject = streamObject;
			Index = index;
		}

		public override string ToString() {
			if(Type == PdfObjectLocationType.Offset) {
				return $"Offset = {Offset}";
			}
			else {
				return $"ObjStm = {StreamObject}, Index = {Index}";
			}
		}
	}

}
