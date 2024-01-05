using System;
using System.Runtime.Serialization;

namespace SharpSheets.Exceptions {

	[Serializable]
	public class SharpDrawingException : SharpSheetsException {

		public object Origin { get; private set; }

		public SharpDrawingException(object origin, string message, Exception innerException) : base(message, innerException) {
			Origin = origin;
		}

		public SharpDrawingException(object origin, string message) : base(message) {
			Origin = origin;
		}

		//protected SharpDrawingException(SerializationInfo info, StreamingContext context) : base(info, context) { }
	}

	[Serializable]
	public class SharpLayoutException : SharpDrawingException {
		public SharpLayoutException(object origin, string message, Exception innerException) : base(origin, message, innerException) { }
		public SharpLayoutException(object origin, string message) : base(origin, message) { }
		//protected SharpLayoutException(SerializationInfo info, StreamingContext context) : base(info, context) { }
	}

}
