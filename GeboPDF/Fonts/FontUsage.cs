using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeboPDF.Fonts {

	public class FontUsage {

		public bool All { get; private set; }
		public IReadOnlySet<char> Characters => usedChars;

		private readonly HashSet<char> usedChars;

		public FontUsage() {
			usedChars = new HashSet<char>();
			All = false;
		}

		private FontUsage(HashSet<char> usedChars, bool allUsed) {
			this.usedChars = new HashSet<char>(usedChars);
			All = allUsed;
		}

		public void AddChar(char c) {
			usedChars.Add(c);
		}

		public void AddAll() {
			All = true;
		}

		public void UnionWith(FontUsage other) {
			this.All |= other.All;
			if (!this.All) {
				this.usedChars.UnionWith(other.usedChars);
			}
		}

		public static FontUsage Combine(FontUsage a, FontUsage b) {
			bool allUsed = a.All || b.All;

			HashSet<char> allChars = new HashSet<char>();
			if (!allUsed) {
				allChars.UnionWith(a.usedChars);
				allChars.UnionWith(b.usedChars);
			}

			return new FontUsage(allChars, allUsed);
		}

	}

}
