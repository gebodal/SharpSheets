using System;
using System.Text.RegularExpressions;

namespace SharpSheets.Evaluations {
	public readonly struct EvaluationName : IEquatable<EvaluationName>, IComparable<EvaluationName> {

		private readonly string name;
		public bool Valid { get { return IsValid(name); } }

		public EvaluationName(string name) {
			this.name = name;
		}

		public static implicit operator EvaluationName(string name) {
			return new EvaluationName(name);
		}

		private static readonly Regex pattern = new Regex(@"[a-z][a-z0-9]*", RegexOptions.IgnoreCase);
		public static bool IsValid(string name) {
			if (name == null) {
				return false;
			}
			else {
				return pattern.IsMatch(name);
			}
		}

		private static readonly StringComparer NameComparer = StringComparer.InvariantCultureIgnoreCase; // TODO This needs to be available to other classes somehow (methods?)

		public int CompareTo(EvaluationName other) {
			return NameComparer.Compare(this.name, other.name);
		}

		public static bool Equals(EvaluationName a, EvaluationName b) {
			return NameComparer.Equals(a.name, b.name);
		}

		public bool Equals(EvaluationName other) {
			return NameComparer.Equals(this.name, other.name);
		}

		public override bool Equals(object? obj) {
			if (obj is EvaluationName variableName) {
				return NameComparer.Equals(name, variableName.name);
			}
			else {
				return false;
			}
		}

		public override int GetHashCode() {
			return NameComparer.GetHashCode(name ?? "");
			//return (name ?? "").GetHashCode();
		}

		public static bool operator ==(EvaluationName a, EvaluationName b) {
			return NameComparer.Equals(a.name, b.name);
		}
		public static bool operator !=(EvaluationName a, EvaluationName b) {
			return !NameComparer.Equals(a.name, b.name);
		}

		public override string ToString() {
			return (name ?? "").ToLowerInvariant();
		}

	}
}
