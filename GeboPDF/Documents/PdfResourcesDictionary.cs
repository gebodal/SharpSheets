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

		private readonly ResourcesRegistry<PdfGraphicsStateParameterDictionary> extGStateRegistry;
		private readonly ResourcesRegistry<PdfColorSpace> colorSpaceRegistry;
		private readonly ResourcesRegistry<IPdfPattern> patternRegistry;
		private readonly ResourcesRegistry<PdfShadingDictionary> shadingRegistry;
		private readonly ResourcesRegistry<PdfXObject> xObjectRegistry;
		private readonly ResourcesRegistry<PdfFont> fontRegistry;

		public PdfResourcesDictionary(bool standalone) {
			this.Standalone = standalone;

			allRegistered = new HashSet<PdfName>();

			// Could the creation of these be delayed unless needed? Seems a lot of effort to go to, considering how infrequently most will be used
			extGStateRegistry = new ResourcesRegistry<PdfGraphicsStateParameterDictionary>(gs => PdfIndirectReference.Create(gs), "Gs", allRegistered);
			colorSpaceRegistry = new ResourcesRegistry<PdfColorSpace>(cs => PdfIndirectReference.Create(cs), "Cs", allRegistered);
			patternRegistry = new ResourcesRegistry<IPdfPattern>(p => p.Reference, "P", allRegistered);
			shadingRegistry = new ResourcesRegistry<PdfShadingDictionary>(s => PdfIndirectReference.Create(s), "S", allRegistered);
			xObjectRegistry = new ResourcesRegistry<PdfXObject>(x => x.Reference, "XO", allRegistered);
			fontRegistry = new ResourcesRegistry<PdfFont>(f => f.FontReference, "F", allRegistered);
		}

		public override int Count {
			get {
				int count = 0;
				if (extGStateRegistry.Dictionary != null) { count += 1; }
				if (colorSpaceRegistry.Dictionary != null) { count += 1; }
				if (patternRegistry.Dictionary != null) { count += 1; }
				if (shadingRegistry.Dictionary != null) { count += 1; }
				if (xObjectRegistry.Dictionary != null) { count += 1; }
				if (fontRegistry.Dictionary != null) { count += 1; }
				return count;
			}
		}

		public override IEnumerator<KeyValuePair<PdfName, PdfObject>> GetEnumerator() {
			if (extGStateRegistry.Dictionary != null) { yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.ExtGState, extGStateRegistry.Dictionary); }
			if (colorSpaceRegistry.Dictionary != null) { yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.ColorSpace, colorSpaceRegistry.Dictionary); }
			if (patternRegistry.Dictionary != null) { yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.Pattern, patternRegistry.Dictionary); }
			if (shadingRegistry.Dictionary != null) { yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.Shading, shadingRegistry.Dictionary); }
			if (xObjectRegistry.Dictionary != null) { yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.XObject, xObjectRegistry.Dictionary); }
			if (fontRegistry.Dictionary != null) { yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.Font, fontRegistry.Dictionary); }
			// Procedure sets? (For backwards compatibility?)
		}

		public IEnumerable<PdfObject> CollectObjects() {
			if (Standalone) {
				yield return this;
			}

			foreach (PdfGraphicsStateParameterDictionary extGState in extGStateRegistry.Entries) {
				yield return extGState;
			}

			foreach (PdfColorSpace colorspace in colorSpaceRegistry.Entries) {
				yield return colorspace;
			}

			foreach (IPdfPattern pattern in patternRegistry.Entries) {
				foreach (PdfObject patternObj in pattern.CollectObjects()) {
					yield return patternObj;
				}
			}

			foreach (PdfShadingDictionary shading in shadingRegistry.Entries) {
				yield return shading;
			}

			foreach (PdfXObject xObj in xObjectRegistry.Entries) {
				foreach (PdfObject xObjComponent in xObj.CollectObjects()) {
					yield return xObjComponent;
				}
			}

			foreach (PdfFont font in fontRegistry.Entries) {
				foreach (PdfObject fontObject in font.CollectObjects()) {
					yield return fontObject;
				}
			}
		}

		public bool AddGraphicsState(PdfGraphicsStateParameterDictionary extGState, out PdfName extGStateName) => extGStateRegistry.Add(extGState, out extGStateName);
		public bool AddGraphicsState(PdfName extGStateName, PdfGraphicsStateParameterDictionary extGState) => extGStateRegistry.Add(extGStateName, extGState);

		public bool AddColorSpace(PdfColorSpace colorspace, out PdfName colorspaceName) => colorSpaceRegistry.Add(colorspace, out colorspaceName);
		public bool AddColorSpace(PdfName colorspaceName, PdfColorSpace colorspace) => colorSpaceRegistry.Add(colorspaceName, colorspace);
		
		public bool AddPattern(IPdfPattern pattern, out PdfName patternName) => patternRegistry.Add(pattern, out patternName);
		public bool AddPattern(PdfName patternName, IPdfPattern pattern) => patternRegistry.Add(patternName, pattern);
		
		public bool AddShading(PdfShadingDictionary shading, out PdfName shadingName) => shadingRegistry.Add(shading, out shadingName);
		public bool AddShading(PdfName shadingName, PdfShadingDictionary shading) => shadingRegistry.Add(shadingName, shading);

		public bool AddXObject(PdfXObject xObject, out PdfName xObjectName) {
			string? objNamePattern = null;
			if(xObject is PdfImageXObject) {
				objNamePattern = "Im"; // This is probably foolish, but I like the aesthetics when reading the documents
			}
			return xObjectRegistry.Add(xObject, out xObjectName, objNamePattern);
		}
		public bool AddXObject(PdfName xObjectName, PdfXObject xObject) => xObjectRegistry.Add(xObjectName, xObject);

		public bool AddFont(PdfFont font, out PdfName fontName) => fontRegistry.Add(font, out fontName);
		public bool AddFont(PdfName fontName, PdfFont font) => fontRegistry.Add(fontName, font);

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


		private class ResourcesRegistry<T> where T : notnull {

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

}
