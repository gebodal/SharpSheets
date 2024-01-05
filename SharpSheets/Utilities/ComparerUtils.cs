using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace SharpSheets.Utilities {

	public sealed class IdentityEqualityComparer<T> : IEqualityComparer<T> where T : class {
		public int GetHashCode(T value) {
			return RuntimeHelpers.GetHashCode(value); // TODO System.HashCode.Combine?
		}

		public bool Equals(T? left, T? right) {
			return object.ReferenceEquals(left, right); // Reference identity comparison
			//return left == right; 
		}
	}

}
