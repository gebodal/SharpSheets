using System;
using System.Runtime.Serialization;

namespace SharpSheets.Exceptions {

	[Serializable]
	public abstract class SharpSheetsException : Exception {
		public SharpSheetsException(string message) : base(message) { }
		public SharpSheetsException(string message, Exception? innerException) : base(message, innerException) { }
		protected SharpSheetsException(SerializationInfo info, StreamingContext context) : base(info, context) { }

		// TODO Add a Severity enum here to indicate if this is a warning or an actual error?
	}

}