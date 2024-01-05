using GeboPdf.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;

namespace GeboPdf.Objects {

	public abstract class PdfObject {
		public static readonly PdfNull Null = new PdfNull();

		public override int GetHashCode() => base.GetHashCode();
		public override bool Equals(object? obj) => base.Equals(obj);

		public static bool operator ==(PdfObject? a, PdfObject? b) {
			if(a is null) { return b is null; }
			else { return a.Equals(b); }
		}
		public static bool operator !=(PdfObject? a, PdfObject? b) {
			if (a is null) { return b is not null; }
			else { return !a.Equals(b); }
		}
	}

	[System.Diagnostics.DebuggerDisplay("PdfBoolean({Value})")]
	public class PdfBoolean : PdfObject {

		public bool Value { get; }

		public PdfBoolean(bool value) {
			this.Value = value;
		}

		public static implicit operator PdfBoolean(bool value) {
			return new PdfBoolean(value);
		}

		public override int GetHashCode() => Value.GetHashCode();
		public override bool Equals(object? obj) {
			if (obj is PdfBoolean other) {
				return Value == other.Value;
			}
			return false;
		}

		public override string ToString() {
			return Value.ToString();
		}

	}

	[System.Diagnostics.DebuggerDisplay("PdfInt({Value})")]
	public class PdfInt : PdfObject {

		public int Value { get; }

		public PdfInt(int value) {
			this.Value = value;
		}

		public PdfInt(long value) {
			this.Value = (int)value; // TODO Is this ever a problem?
		}

		public static implicit operator PdfInt(int value) {
			return new PdfInt(value);
		}

		public override int GetHashCode() => Value.GetHashCode();
		public override bool Equals(object? obj) {
			if (obj is PdfInt other) {
				return Value == other.Value;
			}
			return false;
		}

		public override string ToString() {
			return Value.ToString();
		}

	}

	[System.Diagnostics.DebuggerDisplay("PdfFloat({Value})")]
	public class PdfFloat : PdfObject {

		public float Value { get; }

		public PdfFloat(float value) {
			this.Value = value;
		}

		public static implicit operator PdfFloat(float value) {
			return new PdfFloat(value);
		}

		public static explicit operator PdfFloat(PdfInt value) {
			return new PdfFloat((float)value.Value);
		}

		public override int GetHashCode() => Value.GetHashCode();
		public override bool Equals(object? obj) {
			if (obj is PdfFloat other) {
				return Value == other.Value;
			}
			return false;
		}

		public override string ToString() {
			return Value.ToString();
		}

	}

	public abstract class PdfString : PdfObject {

		public abstract bool HexString { get; }
		public abstract byte[] Value { get; }

		public PdfString() { }

		public override int GetHashCode() {
			return HashCode.Combine(HexString, Value);
		}

		public override bool Equals(object? obj) {
			if (ReferenceEquals(this, obj)) { return true; }
			if (obj is PdfString other) {
				if(HexString == other.HexString) {
					return Enumerable.SequenceEqual(Value, other.Value);
				}
			}
			return false;
		}

		public override string ToString() {
			if (HexString) {
				return "<" + HexWriter.ToString(Value) + ">";
			}
			else {
				//return "(" + Regex.Escape(PdfStringEncoding.GetLiteralString(Value)) + ")"; // Don't like this Regex.Escape
				return "(" + Encoding.ASCII.GetString(PdfStringEncoding.GetLiteralStringBytes(PdfStringEncoding.GetLiteralString(Value))) + ")"; // Don't like this Regex.Escape
			}
		}

	}

	public class PdfName : PdfObject, IComparable<PdfName> {

		public string Name { get; }

		public PdfName(string name) {
			if (string.IsNullOrEmpty(name)) {
				throw new ArgumentException("A PDF name string must have content.");
			}
			this.Name = ValidateNameString(name);
		}

		public override int GetHashCode() {
			return ToString().GetHashCode();
		}

		public override bool Equals(object? obj) {
			if(obj is PdfName other) {
				return Equals(this, other);
			}
			return false;
		}

		public override string ToString() {
			return "/" + Name;
		}

		public int CompareTo(PdfName? other) {
			if (other is null) { return 1; } // Valid default assumption?
			return this.Name.CompareTo(other.Name);
		}

		public static bool Equals(PdfName name1, PdfName name2) {
			if(name1 == null && name2 == null) {
				return true;
			}
			else if(name1 == null || name2 == null) {
				return false;
			}
			else {
				return string.Equals(name1.Name, name2.Name, StringComparison.Ordinal);
			}
		}

		public static string ValidateNameString(string name) {
			StringBuilder sb = new StringBuilder();

			for (int i = 0; i < name.Length; i++) {
				int c = name[i];
				if (c == 9 || c == 10 || c == 12 || c == 13 || c == 32) { // Whitespace characters that can be escaped
					sb.Append($"#{c:X2}");
				}
				else if (c == '#' || c == '(' || c == ')' || c == '<' || c == '>' || c == '[' || c == ']' || c == '{' || c == '}' || c == '/' || c == '%') { // Escaped characters
					sb.Append($"#{c:X2}");
				}
				else if (c >= 33 && c <= 126) { // Allowed range
					sb.Append((char)c);
				}
				else if (c > 0 && c < 128) { // Max range
					sb.Append($"#{c:X2}");
				}
				else {
					throw new ArgumentException("Invalid character for PDF name.");
				}
			}

			return sb.ToString();
		}

		public static string DecodeNameString(string name) {
			StringBuilder sb = new StringBuilder();

			for (int i = 0; i < name.Length; i++) {
				int c = name[i];

				if (c == '#') {
					if (i < name.Length - 2) {
						sb.Append((char)HexWriter.ToByte(name[i + 1], name[i + 2]));
						i += 2;
					}
					else {
						throw new ArgumentException("Incomplete escape sequence in PDF name.");
					}
				}
				else if (c == 0 || c == 9 || c == 10 || c == 12 || c == 13 || c == 32) { // Whitespace characters that can be escaped
					throw new ArgumentException("Invalid whitespace character in PDF name.");
				}
				else if (c == '(' || c == ')' || c == '<' || c == '>' || c == '[' || c == ']' || c == '{' || c == '}' || c == '/' || c == '%') { // Escaped characters
					throw new ArgumentException("Invalid delimiter character in PDF name.");
				}
				else {
					sb.Append((char)c);
				}
			}

			return sb.ToString();
		}

	}

	[System.Diagnostics.DebuggerDisplay("Length={Length}")]
	public abstract class AbstractPdfArray : PdfObject, IEnumerable<PdfObject> {

		public abstract int Length { get; }
		public abstract PdfObject this[int i] { get; }

		public override int GetHashCode() {
			unchecked {
				int hash = 17;
				for (int i = 0; i < Length; i++) {
					hash = hash * 23 + this[i].GetHashCode();
				}
				return hash;
			}
		}

		public override bool Equals(object? obj) {
			if(ReferenceEquals(this, obj)) {
				return true;
			}
			else if (obj is AbstractPdfArray other) {
				if (Length == other.Length) {
					for (int i = 0; i < Length; i++) {
						if (!this[i].Equals(other[i])) {
							return false;
						}
					}
					return true;
				}
			}
			return false;
		}

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
		public IEnumerator<PdfObject> GetEnumerator() {
			for (int i = 0; i < Length; i++) {
				yield return this[i];
			}
		}

		public override string ToString() {
			if (Length > 0) {
				return "[ " + string.Join(" ", this.Select(i => i.ToString())) + " ]";
			}
			else {
				return "[ ]";
			}
		}
	}

	[System.Diagnostics.DebuggerDisplay("Length = {Length}")]
	public class PdfArray : AbstractPdfArray {

		private readonly List<PdfObject> values;

		public override int Length => values.Count;
		
		public override PdfObject this[int i] {
			get {
				return values[i];
			}
		}

		public PdfArray() {
			this.values = new List<PdfObject>();
		}

		public PdfArray(IEnumerable<PdfObject> values) {
			this.values = values.ToList();
		}

		public PdfArray(params PdfObject[] values) {
			this.values = values.ToList();
		}

		public void Add(PdfObject value) {
			values.Add(value);
		}

		public static PdfArray MakeArray(params float[] values) {
			return new PdfArray(values.Select(v => new PdfFloat(v)));
		}

		public static PdfArray MakeArray(params int[] values) {
			return new PdfArray(values.Select(v => new PdfInt(v)));
		}

		public static PdfArray MakeArray(params bool[] values) {
			return new PdfArray(values.Select(v => new PdfBoolean(v)));
		}

	}

	[System.Diagnostics.DebuggerDisplay("[{LowerLeftX}, {LowerLeftY}, {Width}, {Height}]")]
	public class PdfRectangle : AbstractPdfArray {

		public override int Length { get; } = 4;

		public override PdfObject this[int i] {
			get {
				if (i == 0) return lowerLeftX;
				else if (i == 1) return lowerLeftY;
				else if (i == 2) return upperRightX;
				else if (i == 3) return upperRightY;
				else throw new IndexOutOfRangeException($"Index {i} is outside bounds for PdfRectangle.");
			}
		}

		public float LowerLeftX => lowerLeftX.Value;
		public float LowerLeftY => lowerLeftY.Value;
		public float UpperRightX => upperRightX.Value;
		public float UpperRightY => upperRightY.Value;

		public float Width => UpperRightX - LowerLeftX;
		public float Height => UpperRightY - LowerLeftY;

		private readonly PdfFloat lowerLeftX;
		private readonly PdfFloat lowerLeftY;
		private readonly PdfFloat upperRightX;
		private readonly PdfFloat upperRightY;

		private PdfRectangle(float lowerLeftX, float lowerLeftY, float upperRightX, float upperRightY) {
			this.lowerLeftX = new PdfFloat(lowerLeftX);
			this.lowerLeftY = new PdfFloat(lowerLeftY);
			this.upperRightX = new PdfFloat(upperRightX);
			this.upperRightY = new PdfFloat(upperRightY);
		}

		public static PdfRectangle FromCorners(float lowerLeftX, float lowerLeftY, float upperRightX, float upperRightY) {
			return new PdfRectangle(lowerLeftX, lowerLeftY, upperRightX, upperRightY);
		}

		public static PdfRectangle FromDimensions(float x, float y, float width, float height) {
			return new PdfRectangle(x, y, x + width, y + height);
		}

		public static PdfRectangle FromDimensions(float width, float height) {
			return new PdfRectangle(0f, 0f, width, height);
		}

	}

	[System.Diagnostics.DebuggerDisplay("[{A}, {B}, {C}, {D}, {E}, {F}]")]
	public class PdfMatrix : AbstractPdfArray {

		public static readonly PdfMatrix Identity = new PdfMatrix(1, 0, 0, 1, 0, 0);

		public override int Length { get; } = 6;

		public override PdfObject this[int i] {
			get {
				if (i == 0) return a;
				else if (i == 1) return b;
				else if (i == 2) return c;
				else if (i == 3) return d;
				else if (i == 4) return e;
				else if (i == 5) return f;
				else throw new IndexOutOfRangeException($"Index {i} is outside bounds for PdfMatrix.");
			}
		}

		public float A => a.Value;
		public float B => b.Value;
		public float C => c.Value;
		public float D => d.Value;
		public float E => e.Value;
		public float F => f.Value;

		private readonly PdfFloat a;
		private readonly PdfFloat b;
		private readonly PdfFloat c;
		private readonly PdfFloat d;
		private readonly PdfFloat e;
		private readonly PdfFloat f;

		public PdfMatrix(float a, float b, float c, float d, float e, float f) {
			this.a = new PdfFloat(a);
			this.b = new PdfFloat(b);
			this.c = new PdfFloat(c);
			this.d = new PdfFloat(d);
			this.e = new PdfFloat(e);
			this.f = new PdfFloat(f);
		}

		public PdfMatrix(Transform t) : this(t.a, t.b, t.c, t.d, t.e, t.f) { }

	}

	[System.Diagnostics.DebuggerDisplay("Count = {Count}")]
	public abstract class AbstractPdfDictionary : PdfObject, IEnumerable<KeyValuePair<PdfName, PdfObject>> {
		public abstract int Count { get; }
		public abstract IEnumerator<KeyValuePair<PdfName, PdfObject>> GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		public override int GetHashCode() {
			unchecked {
				int hash = 17;
				foreach (KeyValuePair<PdfName, PdfObject> entry in this.OrderBy(kv => kv.Key)) {
					hash = hash * 23 + entry.GetHashCode();
				}
				return hash;
			}
		}

		public static bool Equals(AbstractPdfDictionary? x, AbstractPdfDictionary? y) {
			if (ReferenceEquals(x, y)) {
				return true;
			}
			else if (x is AbstractPdfStream ^ y is AbstractPdfStream) {
				return false;
			}

			if (x!.Count != y!.Count) {
				return false;
			}

			/*
			PdfName[] xKeys = x.Select(kv => kv.Key).ToArray();
			PdfName[] yKeys = y.Select(kv => kv.Key).ToArray();

			if (xKeys.Except(yKeys).Any()) {
				return false;
			}
			if (yKeys.Except(xKeys).Any()) {
				return false;
			}
			*/

			// Is this working...?
			foreach ((PdfName n1, PdfObject o1, PdfName n2, PdfObject o2) entry in x.OrderBy(kv => kv.Key).Zip(y.OrderBy(kv => kv.Key), (e1, e2) => (e1.Key, e1.Value, e2.Key, e2.Value))) {
				if (!entry.n1.Equals(entry.n2) || !entry.o1.Equals(entry.o2)) {
					return false;
				}
			}
			// Can we do something more like this? https://stackoverflow.com/a/21758422/11002708
			// We would need indexers on the AbstractDictionary class
			return true;
		}

		public override bool Equals(object? obj) {
			if (obj is AbstractPdfDictionary other) {
				return Equals(this, other);
			}
			return false;
		}

		public static readonly AbstractPdfDictionary Empty = new EmptyPdfDictionary();

		#region Empty

		private class EmptyPdfDictionary : AbstractPdfDictionary {
			public override int Count { get; } = 0;
			public override IEnumerator<KeyValuePair<PdfName, PdfObject>> GetEnumerator() { yield break; }
		}

		#endregion

		public override string ToString() {
			if (Count > 0) {
				return "<< " + string.Join(" ", this.Select(kv => $"{kv.Key} {kv.Value}")) + " >>";
			}
			else {
				return "<< >>";
			}
		}
	}

	[System.Diagnostics.DebuggerDisplay("Count = {Count}")]
	public class PdfDictionary : AbstractPdfDictionary, IDictionary<PdfName, PdfObject>, IReadOnlyDictionary<PdfName, PdfObject> {

		private readonly Dictionary<PdfName, PdfObject> dict;

		public override int Count => dict.Count;
		public IEnumerable<PdfName> Keys => dict.Keys;
		public IEnumerable<PdfObject> Values => dict.Values;
		ICollection<PdfName> IDictionary<PdfName, PdfObject>.Keys => dict.Keys;
		ICollection<PdfObject> IDictionary<PdfName, PdfObject>.Values => dict.Values;
		public bool IsReadOnly => ((ICollection<KeyValuePair<PdfName, PdfObject>>)dict).IsReadOnly;

		public PdfObject this[PdfName name] {
			get {
				return dict[name];
			}
			set {
				Set(name, value);
			}
		}

		public PdfDictionary() {
			this.dict = new Dictionary<PdfName, PdfObject>();
		}

		public void Add(PdfName name, PdfObject value) {
			if (dict.ContainsKey(name)) {
				throw new ArgumentException("Key already exists in PdfDictionary: " + name);
			}

			Set(name, value);
		}

		public virtual void Set(PdfName name, PdfObject value) {
			dict[name] = value ?? throw new ArgumentNullException(nameof(value));
		}

		public virtual bool Remove(PdfName name) {
			return dict.Remove(name);
		}

		public bool Contains(PdfName name) {
			return dict.ContainsKey(name);
		}

		public PdfName? GetKey(PdfObject obj) {
			return dict.Keys.FirstOrDefault(k => dict[k] == obj);
		}

		public override IEnumerator<KeyValuePair<PdfName, PdfObject>> GetEnumerator() {
			return dict.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator() {
			return GetEnumerator();
		}

		public bool ContainsKey(PdfName key) => dict.ContainsKey(key);
		public bool TryGetValue(PdfName key, [MaybeNullWhen(false)] out PdfObject value) => dict.TryGetValue(key, out value);
		public void Add(KeyValuePair<PdfName, PdfObject> item) => ((ICollection<KeyValuePair<PdfName, PdfObject>>)dict).Add(item);
		public void Clear() => ((ICollection<KeyValuePair<PdfName, PdfObject>>)dict).Clear();
		public bool Contains(KeyValuePair<PdfName, PdfObject> item) => ((ICollection<KeyValuePair<PdfName, PdfObject>>)dict).Contains(item);
		public void CopyTo(KeyValuePair<PdfName, PdfObject>[] array, int arrayIndex) => ((ICollection<KeyValuePair<PdfName, PdfObject>>)dict).CopyTo(array, arrayIndex);
		public bool Remove(KeyValuePair<PdfName, PdfObject> item) => ((ICollection<KeyValuePair<PdfName, PdfObject>>)dict).Remove(item);

		public override int GetHashCode() => dict.GetHashCode();
		public override bool Equals(object? obj) => base.Equals(obj);
	}

	public abstract class AbstractPdfStream : AbstractPdfDictionary {

		public readonly PdfIndirectReference Reference;

		public abstract bool AllowEncoding { get; }

		public AbstractPdfStream() {
			this.Reference = PdfIndirectReference.Create(this);
		}

		public abstract MemoryStream GetStream();
		//public abstract AbstractPdfDictionary GetDictionary();

		public override int GetHashCode() {
			unchecked {
				int hash = 17;
				hash = hash * 23 + GetStream().Length.GetHashCode();
				hash = hash * 23 + base.GetHashCode();
				return hash;
			}
		}

		public override bool Equals(object? obj) {
			if(ReferenceEquals(this, obj)) {
				return true;
			}
			else if(obj is AbstractPdfStream other) {
				if(GetType() != other.GetType()) {
					return false; // By default, PdfStream objects can only be equal if they are the same type (this may cause issues?)
				}

				if(Count != other.Count) {
					return false;
				}

				MemoryStream myStream = GetStream();
				MemoryStream otherStream = other.GetStream();
				if(myStream.Length != otherStream.Length) {
					return false;
				}

				if (base.Equals(other)) { // AbstractPdfDictionary Equals
					return false;
				}

				byte[] myBuffer = myStream.ToArray(); // myStream.GetBuffer();
				byte[] otherBuffer = otherStream.ToArray(); // otherStream.GetBuffer();

				for(int i=0; i<myStream.Length; i++) {
					if (myBuffer[i] != otherBuffer[i]) {
						return false;
					}
				}

				return true;
			}
			return false;
		}

		public override string ToString() => base.ToString() + " stream";

	}

	public class PdfStream : AbstractPdfStream, IReadOnlyDictionary<PdfName, PdfObject> {
		public override bool AllowEncoding { get; }

		public override int Count => dictionary.Count;
		public IEnumerable<PdfName> Keys => dictionary.Keys;
		public IEnumerable<PdfObject> Values => dictionary.Values;
		public PdfObject this[PdfName key] => dictionary[key];

		private readonly PdfDictionary dictionary;
		private readonly MemoryStream stream;

		public PdfStream(PdfDictionary dictionary, MemoryStream stream, bool allowEncoding) : base() {
			this.dictionary = dictionary;
			this.stream = stream;
			this.AllowEncoding = allowEncoding;
		}

		public PdfDictionary GetDictionary() => dictionary;
		public override MemoryStream GetStream() => stream;
		public override IEnumerator<KeyValuePair<PdfName, PdfObject>> GetEnumerator() => dictionary.GetEnumerator();

		public bool ContainsKey(PdfName key) => dictionary.ContainsKey(key);
		public bool TryGetValue(PdfName key, [MaybeNullWhen(false)] out PdfObject value) => dictionary.TryGetValue(key, out value);
	}

	[System.Diagnostics.DebuggerDisplay("PdfNull")]
	public sealed class PdfNull : PdfObject {

		public PdfNull() { }

		public override int GetHashCode() { return 0; }
		public override bool Equals(object? obj) {
			return obj is PdfNull;
		}

		public override string ToString() {
			return "null";
		}
	}

	public abstract class PdfIndirectReference : PdfObject {

		public abstract PdfObject Subject { get; }

		public static PdfIndirectReference Create(PdfObject subject) {
			return new PdfIndirectReferenceSubject(subject);
		}

		public override int GetHashCode() => Subject.GetHashCode();

		public override bool Equals(object? obj) {
			if (obj is PdfIndirectReference pdfRef) {
				return Subject.Equals(pdfRef.Subject);
			}
			return false;
		}

		public override string ToString() {
			return $"REFERENCE({Subject.GetType().Name})";
		}

		private class PdfIndirectReferenceSubject : PdfIndirectReference {

			public override PdfObject Subject { get; }

			public PdfIndirectReferenceSubject(PdfObject subject) {
				if (subject is PdfIndirectReference) {
					// Should this just be "this.Subject = other.Subject"?
					throw new ArgumentException("A reference cannot have another reference as its subject.");
				}
				this.Subject = subject ?? throw new ArgumentNullException(nameof(subject));
			}
			
		}
	}

	public abstract class PdfProxyObject : PdfObject {

		public abstract PdfObject Content { get; }

		public override int GetHashCode() => Content.GetHashCode();

		public override bool Equals(object? obj) {
			if (obj is PdfProxyObject proxy) {
				return Content.Equals(proxy.Content);
			}
			return false;
		}

		public override string ToString() {
			return (Content is null) ? "NULL" : (Content.ToString() ?? "ERROR"); // TODO Unhappy with this. Can we force PdfObject subclasses to have non-nullable ToString()?
		}
	}

}
