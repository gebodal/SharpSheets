using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace SharpEditor.Utilities {

	public static class ResourceUtilities {

		public static string[] GetResources(Assembly assembly, string prefix) {
			return assembly.GetManifestResourceNames().Where(n => n.StartsWith(prefix)).ToArray();
		}

		public static string? GetResource(Assembly assembly, string name) {
			return assembly.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith(name));
		}

		public static Stream? GetResourceStream(Assembly assembly, string name) {
			if (GetResource(assembly, name) is string resourceName) {
				return assembly.GetManifestResourceStream(resourceName);
			}
			else {
				return null;
			}
		}

		public static Uri GetAssetUri(string assetPath) {
			string assemblyName = Assembly.GetEntryAssembly()?.GetName().Name ?? throw new InvalidOperationException($"Could not load assembly for asset: {assetPath}");
			return new Uri($"avares://{assemblyName}/{assetPath}");
		}

	}

}
