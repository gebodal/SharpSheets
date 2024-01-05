using SharpSheets.Utilities;
using System.Collections.Generic;
using System.Linq;
using SharpSheets.Exceptions;

namespace SharpSheets.Parsing {

	public interface IParser {
		/// <summary></summary>
		/// <param name="origin"></param>
		/// <param name="source"></param>
		/// <param name="config"></param>
		/// <param name="results"></param>
		/// <returns></returns>
		/// <exception cref="FileNotFoundException"></exception>
		/// <exception cref="DirectoryNotFoundException"></exception>
		object? Parse(FilePath origin, DirectoryPath source, string config, out CompilationResult results);
	}

	public interface IParser<T> : IParser {
		/// <summary>
		/// 
		/// </summary>
		/// <param name="origin"></param>
		/// <param name="source"></param>
		/// <param name="config"></param>
		/// <param name="results"></param>
		/// <returns></returns>
		/// <exception cref="FileNotFoundException"></exception>
		/// <exception cref="DirectoryNotFoundException"></exception>
		T? ParseContent(FilePath origin, DirectoryPath source, string config, out CompilationResult results);
	}

	public static class ConfigurationParserUtils {

		/// <summary></summary>
		/// <exception cref="FileNotFoundException"></exception>
		/// <exception cref="DirectoryNotFoundException"></exception>
		public static object? Parse(this IParser parser, FilePath origin, DirectoryPath source, string config) {
			return parser.Parse(origin, source, config, out _);
		}

		/// <summary></summary>
		/// <exception cref="FileNotFoundException"></exception>
		/// <exception cref="DirectoryNotFoundException"></exception>
		public static T? ParseContent<T>(this IParser<T> parser, FilePath origin, DirectoryPath source, string config) {
			return parser.ParseContent(origin, source, config, out _);
		}

	}

	public class CompilationResult {
		public readonly IDocumentEntity rootEntity;
		public readonly Dictionary<object, IDocumentEntity>? origins;
		public readonly List<SharpParsingException> errors;
		//public readonly HashSet<int>? unusedLines;
		public readonly HashSet<int>? usedLines;
		public readonly Dictionary<int, HashSet<int>>? lineOwners;
		public readonly Dictionary<int, HashSet<int>>? lineChildren;
		public readonly Dictionary<int, IDocumentEntity>? parents;
		public readonly List<FilePath> dependencies;

		public CompilationResult(
				IDocumentEntity rootEntity,
				Dictionary<object, IDocumentEntity>? origins,
				List<SharpParsingException> errors,
				//HashSet<int>? unusedLines,
				HashSet<int>? usedLines,
				Dictionary<int, HashSet<int>>? lineOwners,
				Dictionary<int, HashSet<int>>? lineChildren,
				Dictionary<int, IDocumentEntity>? parents,
				List<FilePath> dependencies
			) {
			this.rootEntity = rootEntity;
			this.origins = origins;
			this.errors = errors;
			//this.unusedLines = unusedLines;
			this.usedLines = usedLines;
			this.lineOwners = lineOwners;
			this.lineChildren = lineChildren;
			this.parents = parents;
			this.dependencies = dependencies;
		}

		public static CompilationResult CompileResult(ConfigEntry rootEntry, Dictionary<object, IDocumentEntity>? origins, List<SharpParsingException> parsingErrors, List<SharpParsingException> buildErrors, List<FilePath> dependencies, IEnumerable<int> knownUsedLines) {
			HashSet<int> usedLines = rootEntry.GetUsedLines();
			if (knownUsedLines != null) {
				usedLines.UnionWith(knownUsedLines);
			}
			//unusedLines.UnionWith(parsingErrors.Where(e=>e.Location.HasValue).Select(e => e.Location.Value.Line)); // Build errors?
			Dictionary<int, HashSet<int>> lineOwners = rootEntry.CalculateLineOwnership();

			// Line children: Key: line number, Value: all lines used by that line
			Dictionary<int, HashSet<int>> lineChildren = new Dictionary<int, HashSet<int>>();
			foreach (KeyValuePair<int, HashSet<int>> ownership in lineOwners) {
				int ownedLine = ownership.Key;
				foreach (int owner in ownership.Value) {
					if (!lineChildren.ContainsKey(owner)) {
						lineChildren[owner] = new HashSet<int>();
					}
					lineChildren[owner].Add(ownedLine);
				}
			}

			Dictionary<int, IDocumentEntity> parents = rootEntry.TraverseChildren(true).Cast<IDocumentEntity>().ToDictionary(c => c.Location.Line);

			List<SharpParsingException> allErrors = new List<SharpParsingException>();
			allErrors.AddRange(parsingErrors);
			allErrors.AddRange(buildErrors);

			return new CompilationResult(rootEntry, origins, allErrors, usedLines, lineOwners, lineChildren, parents, dependencies);
		}

		public CompilationResult WithOrigins(Dictionary<object, IDocumentEntity> newOrigins) {
			return new CompilationResult(rootEntity, newOrigins, errors, usedLines, lineOwners, lineChildren, parents, dependencies);
		}
	}

}
