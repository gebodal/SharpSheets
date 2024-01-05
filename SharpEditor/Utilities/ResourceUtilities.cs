using System.Linq;
using System.Reflection;

namespace SharpEditor.Utilities {

	public static class ResourceUtilities {

		public static string[] GetResources(Assembly assembly, string prefix) {
			return assembly.GetManifestResourceNames().Where(n => n.StartsWith(prefix)).ToArray();
		}

	}

}
