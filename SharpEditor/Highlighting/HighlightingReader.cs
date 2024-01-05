using System;
using System.IO;
using System.Xml;
using System.Xml.Schema;

namespace SharpEditor.Highlighting {

	public class HighlightingReader : XmlReader {

		private readonly XmlReader _reader;
		private readonly string style;

		public HighlightingReader(Stream input, string style) {
			_reader = new XmlTextReader(input); // XmlReader.Create(input);
			this.style = style ?? throw new ArgumentNullException(nameof(style));
		}

		private bool SkipElement() {
			string? enabled = _reader.GetAttribute("enabled");
			if(enabled == null) {
				return false;
			}
			else {
				return enabled != style;
			}
		}

		public override bool Read() {
			bool result = _reader.Read();
			
			while (_reader.ReadState == ReadState.Interactive && SkipElement()) {
				_reader.Skip();
                result = _reader.Read();
            }

            //return _reader.ReadState == ReadState.Interactive;
            //return _reader.NodeType != XmlNodeType.None;
            return result;
		}

		public override bool MoveToFirstAttribute() {
			bool result = _reader.MoveToFirstAttribute();
            if(_reader.Name == "enabled") {
                result = _reader.MoveToNextAttribute();
            }
            return result;
		}
		public override bool MoveToNextAttribute() {
            bool result = _reader.MoveToNextAttribute();
            if (_reader.Name == "enabled") {
                result = _reader.MoveToNextAttribute();
            }
            return result;
        }

		#region Wrapper

		public override XmlNodeType NodeType => _reader.NodeType;
		public override string LocalName => _reader.LocalName;
		public override string NamespaceURI => _reader.NamespaceURI;
		public override string Prefix => _reader.Prefix;
		public override string Value => _reader.Value;
		public override int Depth => _reader.Depth;
		public override string BaseURI => _reader.BaseURI;
		public override bool IsEmptyElement => _reader.IsEmptyElement;
		public override int AttributeCount => _reader.AttributeCount;
		public override bool EOF => _reader.EOF;
		public override ReadState ReadState => _reader.ReadState;
		public override XmlNameTable NameTable => _reader.NameTable;

		public override string? GetAttribute(string name) => _reader.GetAttribute(name);
		public override string? GetAttribute(string name, string? namespaceURI) => _reader.GetAttribute(name, namespaceURI);
		public override string GetAttribute(int i) => _reader.GetAttribute(i);
		public override string? LookupNamespace(string prefix) => _reader.LookupNamespace(prefix);
		public override bool MoveToAttribute(string name) => _reader.MoveToAttribute(name);
		public override bool MoveToAttribute(string name, string? ns) => _reader.MoveToAttribute(name, ns);
		public override bool MoveToElement() => _reader.MoveToElement();
		//public override bool MoveToFirstAttribute() => _reader.MoveToFirstAttribute();
		//public override bool MoveToNextAttribute() => _reader.MoveToNextAttribute();
		public override bool ReadAttributeValue() => _reader.ReadAttributeValue();
		public override void ResolveEntity() => _reader.ResolveEntity();

		#endregion
	}

    /*
    public class XmlWrappingReader : XmlReader, IXmlLineInfo {
        protected XmlReader reader;
        protected IXmlLineInfo readerAsIXmlLineInfo;

        public XmlWrappingReader(XmlReader baseReader) {
            this.reader = baseReader;
        }

        public override void Close() {
            this.reader.Close();
        }

        protected override void Dispose(bool disposing) {
            ((IDisposable)this.reader).Dispose();
        }

        public override string GetAttribute(int i) {
            return this.reader.GetAttribute(i);
        }

        public override string GetAttribute(string name) {
            return this.reader.GetAttribute(name);
        }

        public override string GetAttribute(string name, string namespaceURI) {
            return this.reader.GetAttribute(name, namespaceURI);
        }

        public virtual bool HasLineInfo() {
            if (this.readerAsIXmlLineInfo != null) {
                return this.readerAsIXmlLineInfo.HasLineInfo();
            }
            return false;
        }

        public override string LookupNamespace(string prefix) {
            return this.reader.LookupNamespace(prefix);
        }

        public override void MoveToAttribute(int i) {
            this.reader.MoveToAttribute(i);
        }

        public override bool MoveToAttribute(string name) {
            return this.reader.MoveToAttribute(name);
        }

        public override bool MoveToAttribute(string name, string ns) {
            return this.reader.MoveToAttribute(name, ns);
        }

        public override bool MoveToElement() {
            return this.reader.MoveToElement();
        }

        public override bool MoveToFirstAttribute() {
            return this.reader.MoveToFirstAttribute();
        }

        public override bool MoveToNextAttribute() {
            return this.reader.MoveToNextAttribute();
        }

        public override bool Read() {
            return this.reader.Read();
        }

        public override bool ReadAttributeValue() {
            return this.reader.ReadAttributeValue();
        }

        public override void ResolveEntity() {
            this.reader.ResolveEntity();
        }

        public override void Skip() {
            this.reader.Skip();
        }


        public override int AttributeCount {
            get {
                return this.reader.AttributeCount;
            }
        }

        public override string BaseURI {
            get {
                return this.reader.BaseURI;
            }
        }

        public override bool CanResolveEntity {
            get {
                return this.reader.CanResolveEntity;
            }
        }

        public override int Depth {
            get {
                return this.reader.Depth;
            }
        }

        public override bool EOF {
            get {
                return this.reader.EOF;
            }
        }

        public override bool HasAttributes {
            get {
                return this.reader.HasAttributes;
            }
        }

        public override bool HasValue {
            get {
                return this.reader.HasValue;
            }
        }

        public override bool IsDefault {
            get {
                return this.reader.IsDefault;
            }
        }

        public override bool IsEmptyElement {
            get {
                return this.reader.IsEmptyElement;
            }
        }

        public virtual int LineNumber {
            get {
                if (this.readerAsIXmlLineInfo != null) {
                    return this.readerAsIXmlLineInfo.LineNumber;
                }
                return 0;
            }
        }

        public virtual int LinePosition {
            get {
                if (this.readerAsIXmlLineInfo != null) {
                    return this.readerAsIXmlLineInfo.LinePosition;
                }
                return 0;
            }
        }

        public override string LocalName {
            get {
                return this.reader.LocalName;
            }
        }

        public override string Name {
            get {
                return this.reader.Name;
            }
        }

        public override string NamespaceURI {
            get {
                return this.reader.NamespaceURI;
            }
        }

        public override XmlNameTable NameTable {
            get {
                return this.reader.NameTable;
            }
        }

        public override XmlNodeType NodeType {
            get {
                return this.reader.NodeType;
            }
        }

        public override string Prefix {
            get {
                return this.reader.Prefix;
            }
        }

        public override char QuoteChar {
            get {
                return this.reader.QuoteChar;
            }
        }

        protected XmlReader Reader {
            get {
                return this.reader;
            }
            set {
                this.reader = value;
                this.readerAsIXmlLineInfo = value as IXmlLineInfo;
            }
        }

        public override System.Xml.ReadState ReadState {
            get {
                return this.reader.ReadState;
            }
        }

        public override IXmlSchemaInfo SchemaInfo {
            get {
                return this.reader.SchemaInfo;
            }
        }

        public override XmlReaderSettings Settings {
            get {
                return this.reader.Settings;
            }
        }

        public override string Value {
            get {
                return this.reader.Value;
            }
        }

        public override Type ValueType {
            get {
                return this.reader.ValueType;
            }
        }

        public override string XmlLang {
            get {
                return this.reader.XmlLang;
            }
        }

        public override System.Xml.XmlSpace XmlSpace {
            get {
                return this.reader.XmlSpace;
            }
        }
    }
    */
}
