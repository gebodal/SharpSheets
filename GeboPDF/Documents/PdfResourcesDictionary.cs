using GeboPdf.Fonts;
using GeboPdf.Graphics;
using GeboPdf.Objects;
using GeboPdf.Patterns;
using GeboPdf.XObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeboPdf.Documents {

	public class PdfResourcesDictionary : AbstractPdfDictionary, IPdfDocumentContents {

		public bool Standalone { get; }

		private readonly HashSet<PdfName> allRegistered;

		private readonly ResourcesRegistry<PdfGraphicsStateParameterDictionary> extGStateRegistry1;
		private readonly ResourcesRegistry<PdfColorSpace> colorSpaceRegistry1;
		private readonly ResourcesRegistry<IPdfPattern> patternRegistry1;
		private readonly ResourcesRegistry<PdfShadingDictionary> shadingRegistry1;
		private readonly ResourcesRegistry<PdfXObject> xObjectRegistry1;
		private readonly ResourcesRegistry<PdfFont> fontRegistry1;

		public PdfResourcesDictionary(bool standalone) {
			this.Standalone = standalone;

			allRegistered = new HashSet<PdfName>();

			// Could the creation of these be delayed unless needed? Seems a lot of effort to go to, considering how infrequently most will be used
			extGStateRegistry1 = new ResourcesRegistry<PdfGraphicsStateParameterDictionary>(gs => PdfIndirectReference.Create(gs), "Gs", allRegistered);
			colorSpaceRegistry1 = new ResourcesRegistry<PdfColorSpace>(cs => PdfIndirectReference.Create(cs), "Cs", allRegistered);
			patternRegistry1 = new ResourcesRegistry<IPdfPattern>(p => p.Reference, "P", allRegistered);
			shadingRegistry1 = new ResourcesRegistry<PdfShadingDictionary>(s => PdfIndirectReference.Create(s), "S", allRegistered);
			xObjectRegistry1 = new ResourcesRegistry<PdfXObject>(x => x.Reference, "XO", allRegistered);
			fontRegistry1 = new ResourcesRegistry<PdfFont>(f => f.FontReference, "F", allRegistered);
		}

		public override int Count {
			get {
				int count = 0;
				if (extGStateRegistry1.Dictionary != null) { count += 1; }
				if (colorSpaceRegistry1.Dictionary != null) { count += 1; }
				if (patternRegistry1.Dictionary != null) { count += 1; }
				if (shadingRegistry1.Dictionary != null) { count += 1; }
				if (xObjectRegistry1.Dictionary != null) { count += 1; }
				if (fontRegistry1.Dictionary != null) { count += 1; }
				return count;
			}
		}

		public override IEnumerator<KeyValuePair<PdfName, PdfObject>> GetEnumerator() {
			if (extGStateRegistry1.Dictionary != null) { yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.ExtGState, extGStateRegistry1.Dictionary); }
			if (colorSpaceRegistry1.Dictionary != null) { yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.ColorSpace, colorSpaceRegistry1.Dictionary); }
			if (patternRegistry1.Dictionary != null) { yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.Pattern, patternRegistry1.Dictionary); }
			if (shadingRegistry1.Dictionary != null) { yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.Shading, shadingRegistry1.Dictionary); }
			if (xObjectRegistry1.Dictionary != null) { yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.XObject, xObjectRegistry1.Dictionary); }
			if (fontRegistry1.Dictionary != null) { yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.Font, fontRegistry1.Dictionary); }
			// Procedure sets? (For backwards compatibility?)
		}

		public IEnumerable<PdfObject> CollectObjects() {
			if (Standalone) {
				yield return this;
			}

			foreach (PdfGraphicsStateParameterDictionary extGState in extGStateRegistry1.Entries) {
				yield return extGState;
			}

			foreach (PdfColorSpace colorspace in colorSpaceRegistry1.Entries) {
				yield return colorspace;
			}

			foreach (IPdfPattern pattern in patternRegistry1.Entries) {
				foreach (PdfObject patternObj in pattern.CollectObjects()) {
					yield return patternObj;
				}
			}

			foreach (PdfShadingDictionary shading in shadingRegistry1.Entries) {
				yield return shading;
			}

			foreach (PdfXObject xObj in xObjectRegistry1.Entries) {
				foreach (PdfObject xObjComponent in xObj.CollectObjects()) {
					yield return xObjComponent;
				}
			}

			foreach (PdfFont font in fontRegistry1.Entries) {
				foreach (PdfObject fontObject in font.CollectObjects()) {
					yield return fontObject;
				}
			}
		}

		public bool AddGraphicsState(PdfGraphicsStateParameterDictionary extGState, out PdfName extGStateName) => extGStateRegistry1.Add(extGState, out extGStateName);
		public bool AddGraphicsState(PdfName extGStateName, PdfGraphicsStateParameterDictionary extGState) => extGStateRegistry1.Add(extGStateName, extGState);

		public bool AddColorSpace(PdfColorSpace colorspace, out PdfName colorspaceName) => colorSpaceRegistry1.Add(colorspace, out colorspaceName);
		public bool AddColorSpace(PdfName colorspaceName, PdfColorSpace colorspace) => colorSpaceRegistry1.Add(colorspaceName, colorspace);
		
		public bool AddPattern(IPdfPattern pattern, out PdfName patternName) => patternRegistry1.Add(pattern, out patternName);
		public bool AddPattern(PdfName patternName, IPdfPattern pattern) => patternRegistry1.Add(patternName, pattern);
		
		public bool AddShading(PdfShadingDictionary shading, out PdfName shadingName) => shadingRegistry1.Add(shading, out shadingName);
		public bool AddShading(PdfName shadingName, PdfShadingDictionary shading) => shadingRegistry1.Add(shadingName, shading);

		public bool AddXObject(PdfXObject xObject, out PdfName xObjectName) {
			string? objNamePattern = null;
			if(xObject is PdfImageXObject) {
				objNamePattern = "Im"; // This is probably foolish, but I like the aesthetics when reading the documents
			}
			return xObjectRegistry1.Add(xObject, out xObjectName, objNamePattern);
		}
		public bool AddXObject(PdfName xObjectName, PdfXObject xObject) => xObjectRegistry1.Add(xObjectName, xObject);

		public bool AddFont(PdfFont font, out PdfName fontName) => fontRegistry1.Add(font, out fontName);
		public bool AddFont(PdfName fontName, PdfFont font) => fontRegistry1.Add(fontName, font);

		public override int GetHashCode() => base.GetHashCode();
		public override bool Equals(object? obj) {
			if(ReferenceEquals(this, obj)) {
				return true;
			}
			else if(obj is PdfResourcesDictionary other) {
				return base.Equals(other);
			}
			return false;
		}

	}

	public class ResourcesRegistry<T> where T : notnull {

		public PdfDictionary? Dictionary { get; private set; }
		public IEnumerable<T> Entries => registry.Keys;

		private readonly HashSet<PdfName> allRegistered;

		private readonly Dictionary<T, PdfName> registry;

		private readonly Func<T, PdfObject> resolver;
		private readonly string namePattern;

		public ResourcesRegistry(Func<T, PdfObject> resolver, string namePattern, HashSet<PdfName> allRegistered) {
			this.resolver = resolver;
			this.namePattern = namePattern;

			this.Dictionary = null;
			this.registry = new Dictionary<T, PdfName>();

			this.allRegistered = allRegistered;
		}

		public bool Add(T entry, out PdfName name, string? substituteNamePattern = null) {
			if (registry.TryGetValue(entry, out PdfName? existingName)) {
				name = existingName;
				return false;
			}
			else {
				if (Dictionary == null) {
					Dictionary = new PdfDictionary();
				}

				PdfName newName;
				int nameIndex = Dictionary.Count;
				do {
					newName = new PdfName($"{substituteNamePattern ?? namePattern}{nameIndex}");
					nameIndex++;
				} while (allRegistered.Contains(newName));

				Dictionary.Add(newName, resolver(entry));
				registry.Add(entry, newName);
				allRegistered.Add(newName);
				name = newName;
				return true;
			}
		}

		public bool Add(PdfName name, T entry) {
			if (allRegistered.Contains(name)) {
				return false;
			}
			else {
				if (Dictionary == null) {
					Dictionary = new PdfDictionary();
				}

				Dictionary.Add(name, resolver(entry));
				registry.Add(entry, name);
				allRegistered.Add(name);
				return true;
			}
		}

	}

}
