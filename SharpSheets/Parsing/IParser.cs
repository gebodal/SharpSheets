using SharpSheets.Utilities;
using System.Collections.Generic;
using System.Linq;
using SharpSheets.Exceptions;
using System.Collections;

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
		public readonly IReadOnlyDictionary<object, IDocumentEntity>? origins;
		public readonly IReadOnlyList<SharpParsingException> errors;
		//public readonly IReadOnlySet<int>? unusedLines;
		public readonly IReadOnlySet<int>? usedLines;
		public readonly IReadOnlyLineOwnership? lineOwners;
		//public readonly IReadOnlyDictionary<int, IDocumentEntity>? parents;
		public readonly IReadOnlyList<FilePath> dependencies;

		public CompilationResult(
				IDocumentEntity rootEntity,
				IReadOnlyDictionary<object, IDocumentEntity>? origins,
				IReadOnlyList<SharpParsingException> errors,
				//IReadOnlySet<int>? unusedLines,
				IReadOnlySet<int>? usedLines,
				IReadOnlyLineOwnership? lineOwners,
				//IReadOnlyDictionary<int, IDocumentEntity>? parents,
				IReadOnlyList<FilePath> dependencies
			) {
			this.rootEntity = rootEntity;
			this.origins = origins;
			this.errors = errors;
			//this.unusedLines = unusedLines;
			this.usedLines = usedLines;
			this.lineOwners = lineOwners;
			//this.parents = parents;
			this.dependencies = dependencies;
		}

		public static CompilationResult CompileResult(VisitTrackingContext rootEntry, Dictionary<object, IDocumentEntity>? origins, List<SharpParsingException> parsingErrors, List<SharpParsingException> buildErrors, List<FilePath> dependencies, IEnumerable<int> knownUsedLines, IReadOnlyLineOwnership knownLineOwners) {
			HashSet<int> usedLines = rootEntry.GetUsedLines();
			if (knownUsedLines != null) {
				usedLines.UnionWith(knownUsedLines);
			}
			//unusedLines.UnionWith(parsingErrors.Where(e=>e.Location.HasValue).Select(e => e.Location.Value.Line)); // Build errors?
			IReadOnlyLineOwnership lineOwners = rootEntry.CalculateLineOwnership();

			if (!knownLineOwners.IsEmpty) {
				lineOwners = new LineOwnership {
					lineOwners,
					knownLineOwners
				};

				//usedLines.UnionWith(knownLineOwners.Select(kv => kv.Key));
				usedLines.UnionWith(knownLineOwners.GetUsedLines());
			}

			if(origins is not null) {
				usedLines.UnionWith(origins.Values.Select(o => o.Location.Line));
			}

			//Dictionary<int, IDocumentEntity> parents = rootEntry.TraverseChildren(true).Cast<IDocumentEntity>().ToDictionary(c => c.Location.Line);

			List<SharpParsingException> allErrors = new List<SharpParsingException>();
			allErrors.AddRange(parsingErrors);
			allErrors.AddRange(buildErrors);

			return new CompilationResult(rootEntry, origins, allErrors, usedLines, lineOwners, dependencies);
		}

		public CompilationResult WithOrigins(Dictionary<object, IDocumentEntity> newOrigins) {
			return new CompilationResult(rootEntity, newOrigins, errors, usedLines, lineOwners, dependencies);
		}
	}

	public interface IReadOnlyLineOwnership : IEnumerable<KeyValuePair<int, IReadOnlySet<int>>> {
		bool IsEmpty { get; }

		IReadOnlySet<int> GetOwners(int line);
		IReadOnlySet<int> GetChildren(int line);

		IReadOnlySet<int> GetUsedLines();
	}

	public class LineOwnership : IReadOnlyLineOwnership {

		// Line owners: Key: line number, Value: all lines which reference that line
		private readonly Dictionary<int, HashSet<int>> lineOwners;

		// Line children: Key: line number, Value: all lines used by that line
		private readonly Dictionary<int, HashSet<int>> lineChildren;

		public bool IsEmpty { get { return lineOwners.Count == 0; } }

		public static readonly IReadOnlyLineOwnership Empty = new LineOwnership();

		public LineOwnership() {
			this.lineOwners = new Dictionary<int, HashSet<int>>();
			this.lineChildren = new Dictionary<int, HashSet<int>>();
		}

		public void Add(int ownedLine, int owner) {
			if (!lineOwners.ContainsKey(ownedLine)) {
				lineOwners[ownedLine] = new HashSet<int>();
			}
			lineOwners[ownedLine].Add(owner);

			if (!lineChildren.ContainsKey(owner)) {
				lineChildren[owner] = new HashSet<int>();
			}
			lineChildren[owner].Add(ownedLine);
		}

		public void Add(int ownedLine, IEnumerable<int> owners) {
			foreach (int owner in owners) {
				Add(ownedLine, owner);
			}
		}

		public void Add(IReadOnlyLineOwnership other) {
			foreach ((int ownedLine, IReadOnlySet<int> owners) in other) {
				foreach(int owner in owners) {
					Add(ownedLine, owner);
				}
			}
		}

		public IReadOnlySet<int> GetOwners(int line) {
			return lineOwners.GetValueOrDefault(line) ?? new HashSet<int>();
		}

		public IReadOnlySet<int> GetChildren(int line) {
			return lineChildren.GetValueOrDefault(line) ?? new HashSet<int>();
		}

		public IEnumerator<KeyValuePair<int, IReadOnlySet<int>>> GetEnumerator() {
			return lineOwners.Select(kv => new KeyValuePair<int, IReadOnlySet<int>>(kv.Key, kv.Value)).GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator() {
			return GetEnumerator();
		}

		public IReadOnlySet<int> GetUsedLines() {
			// TODO Is this sufficient?
			return new HashSet<int>(lineOwners.Keys.Concat(lineChildren.Keys));
		}
	}

}
