using SharpSheets.Parsing;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace SharpSheets.Exceptions {

	[Serializable]
	public class SharpParsingException : SharpSheetsException, ICloneable {

		public DocumentSpan? Location { get; private set; }

		public SharpParsingException(DocumentSpan? location, string message, Exception? innerException) : base(message, innerException) {
			Location = location;
		}

		public SharpParsingException(DocumentSpan? location, string message) : base(message) {
			Location = location;
		}

		protected SharpParsingException(SerializationInfo info, StreamingContext context) : base(info, context) { }

		public virtual object Clone() {
			return new SharpParsingException(Location, Message, InnerException);
		}

		public SharpParsingException AtLocation(DocumentSpan location) {
			SharpParsingException newEx = (SharpParsingException)this.Clone();
			newEx.Location = location;
			return newEx;
		}
	}

	[Serializable]
	public class MissingParameterException : SharpParsingException {

		public string ParameterName { get; private set; }
		public Type ParameterType { get; private set; }

		public MissingParameterException(DocumentSpan location, string name, Type type, string message, Exception? innerException) : base(location, message, innerException) {
			ParameterName = name;
			ParameterType = type;
		}

		public MissingParameterException(DocumentSpan location, string name, Type type, Exception innerException)
			: this(location, name, type, $"No value for required parameter \"{name}\" ({type.Name}) provided.", innerException) { }

		public MissingParameterException(DocumentSpan location, string name, Type type, string message) : base(location, message) {
			ParameterName = name;
			ParameterType = type;
		}

		public MissingParameterException(DocumentSpan location, string name, Type type)
			: this(location, name, type, $"No value for required parameter \"{name}\" ({type.Name}) provided.") { }

		//protected MissingParameterException(SerializationInfo info, StreamingContext context) : base(info, context) { }

		public override object Clone() {
			return new MissingParameterException(Location!.Value, ParameterName, ParameterType, Message, InnerException);
		}
	}

	[Serializable]
	public class SharpFactoryException : SharpSheetsException {

		public IReadOnlyList<SharpParsingException> Errors { get; }

		public SharpFactoryException(IReadOnlyList<SharpParsingException> errors, string message) : base(message) {
			Errors = errors;
		}

		//protected SharpFactoryException(SerializationInfo info, StreamingContext context) : base(info, context) { }
	}

	public class SharpParsingExceptionComparer : IEqualityComparer<SharpParsingException> {
		public static SharpParsingExceptionComparer Instance { get; } = new SharpParsingExceptionComparer();

		public bool Equals(SharpParsingException? x, SharpParsingException? y) {
			if (x == null || y == null) {
				return x == null && y == null;
			}
			return x.Location.Equals(y.Location) && x.Message == y.Message && x.InnerException == y.InnerException; // Inner exception check OK?
		}

		public int GetHashCode(SharpParsingException ex) {
			return (ex.Location.ToString() + ex.Message.ToString()).GetHashCode();
		}
	}

	public class SharpParsingWarningException : SharpParsingException {

		public SharpParsingWarningException(DocumentSpan? location, string message, Exception? innerException) : base(location, message, innerException) { }

		public SharpParsingWarningException(DocumentSpan? location, string message) : base(location, message) { }

		public override object Clone() {
			return new SharpParsingWarningException(Location, Message, InnerException);
		}

	}

	public class FontLicenseWarningException : SharpParsingWarningException {

		public FontLicenseWarningException(DocumentSpan location, string message) : base(location, message) { }

		public override object Clone() {
			return new FontLicenseWarningException(Location!.Value, Message);
		}

	}

}
