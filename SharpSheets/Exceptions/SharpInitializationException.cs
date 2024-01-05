using SharpSheets.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace SharpSheets.Exceptions {
	[Serializable]
	public class SharpInitializationException : SharpSheetsException {

		public Type DeclaringType { get; }
		public IReadOnlyList<string> Arguments { get; } // TODO Should this be a set?

		public SharpInitializationException(IList<string> arguments, Type declaringType, string message, Exception innerException) : base(message, innerException) {
			this.Arguments = arguments.ToArray();
			this.DeclaringType = declaringType;
		}

		public SharpInitializationException(IList<string> arguments, Type declaringType, string message) : base(message) {
			this.Arguments = arguments.ToArray();
			this.DeclaringType = declaringType;
		}

		public SharpInitializationException(string argument, Type declaringType, string message, Exception innerException) : base(message, innerException) {
			this.Arguments = argument.Yield().ToArray();
			this.DeclaringType = declaringType;
		}

		public SharpInitializationException(string argument, Type declaringType, string message) : base(message) {
			this.Arguments = argument.Yield().ToArray();
			this.DeclaringType = declaringType;
		}

		//protected SharpInitializationException(SerializationInfo info, StreamingContext context) : base(info, context) { }
	}

}