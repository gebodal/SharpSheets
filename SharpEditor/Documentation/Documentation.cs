using SharpEditor.DataManagers;
using SharpEditor.Utilities;
using SharpSheets.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace SharpEditor.Documentation {

	public static class Documentation {

		private static readonly DocumentationFile[] documentationFiles;
		public static IReadOnlyList<DocumentationFile> DocumentationItems { get { return documentationFiles; } }
		public static DocumentationNode DocumentationRoot { get; }

		static Documentation() {

			List<DocumentationFile> parsedDocumentationFiles = new List<DocumentationFile>();

			Assembly assembly = Assembly.GetAssembly(typeof(Documentation)) ?? throw new TypeInitializationException("Could not find main assembly.", null);
			string prefix = "SharpEditor.Documentation.Content.";
			//Console.WriteLine("Resource names");
			foreach(string resourceName in ResourceUtilities.GetResources(assembly, prefix)) {
				//Console.WriteLine(resourceName);

				try {
					string name = resourceName.Substring(prefix.Length).Replace("_", " ");
					string[] nameParts = name.SplitAndTrim('.');
					string location = string.Join("/", nameParts.Take(nameParts.Length - 2));
					string title = nameParts[nameParts.Length - 2];

					using (Stream stream = assembly.GetManifestResourceStream(resourceName)!)
					using (StreamReader reader = new StreamReader(stream)) {
						string text = reader.ReadToEnd();

						DocumentationFile documentationFile = Parse(text, title, location);
						parsedDocumentationFiles.Add(documentationFile);

						//Console.WriteLine($"{title}: {location}");
					}
				}
				catch (Exception e) {
					Console.WriteLine("Error reading documentation file: " + resourceName);
					Console.WriteLine(e);
				}
			}
			//Console.WriteLine("Finished reading documentation resources");

			documentationFiles = parsedDocumentationFiles.ToArray();

			DocumentationNode root = new DocumentationNode(int.MinValue, SharpEditorData.GetEditorName());

			foreach (DocumentationFile parsedFile in parsedDocumentationFiles) {
				if(parsedFile.title == "Main") {
					root.GetOrMake(parsedFile.location).main = parsedFile;
				}
				else {
					root.GetOrMake(parsedFile.location).files.Add(parsedFile.title, parsedFile);
				}
			}

			DocumentationRoot = root;
		}

		private static readonly Regex fileIndexRegex = new Regex(@"^(?<index>[0-9]+)\s*(?<title>.+)$");
		private static readonly Regex titleRegex = new Regex(@"^(?<level>\#+)\s*(?<title>.+)$", RegexOptions.IgnoreCase);
		private static readonly Regex collectionRegex = new Regex(@"^\[\[(?<collection>[a-z]+)\]\]$", RegexOptions.IgnoreCase);

		private static DocumentationFile Parse(string text, string title, string path) {

			int index = int.MaxValue;
			if (fileIndexRegex.Match(title) is Match indexMatch && indexMatch.Success) {
				title = indexMatch.Groups["title"].Value;
				index = int.Parse(indexMatch.Groups["index"].Value);
			}

			string[] lines = Regex.Split(text, "\r\n|\r|\n", RegexOptions.Multiline, TimeSpan.FromMilliseconds(500));

			List<DocumentationSection> sections = new List<DocumentationSection>();
			string? currentTitle = null;
			int currentLevel = -1;
			List<IDocumentationSegment> segments = new List<IDocumentationSegment>();
			List<string>? currentParagraph = null;
			List<string>? codeBlock = null;
			DocumentType? codeType = null;

			void FinishParagraph() {
				if (currentParagraph != null && currentParagraph.Count > 0) {
					segments.Add(ParseParagraph(string.Join(" ", currentParagraph)));
				}
				currentParagraph = null;
			}
			void FinishCodeBlock() {
				if (codeBlock != null && codeBlock.Count > 0) {
					segments.Add(new DocumentationCode(string.Join("\n", codeBlock).Trim(), codeType ?? DocumentType.SHARPCONFIG));
				}
				codeBlock = null;
				codeType = null;
			}
			void FinishSection() {
				if (codeBlock != null) {
					throw new FormatException($"Unfinished codeblock in {path} ({currentTitle})");
				}

				FinishParagraph();

				if (currentTitle != null) {
					DocumentationSection newSection = new DocumentationSection(currentLevel, currentTitle, segments.ToArray());
					sections.Add(newSection);
				}

				currentTitle = null;
				currentLevel = -1;
				segments.Clear();
				currentParagraph = null;
			}

			for (int i=0; i<lines.Length; i++) {

				string lineText = lines[i].Trim();

				Match match;
				if (codeBlock != null) {
					if (lineText == "```") {
						FinishCodeBlock();
					}
					else {
						codeBlock.Add(lines[i]);
					}
				}
				else if (string.IsNullOrWhiteSpace(lineText)) {
					FinishParagraph();
				}
				else if ((match = titleRegex.Match(lineText)).Success) {
					string newTitle = match.Groups["title"].Value;
					int newLevel = match.Groups["level"].Value.Length;

					FinishSection();

					currentTitle = newTitle;
					currentLevel = newLevel;
				}
				else if ((match = collectionRegex.Match(lineText)).Success) {
					FinishParagraph();

					string collection = match.Groups["collection"].Value.ToLower();

					DocumentationSectionContents contents;
					try {
						contents = EnumUtils.ParseEnum<DocumentationSectionContents>(collection);
					}
					catch (FormatException) {
						throw new KeyNotFoundException($"Could not find contents for \"{collection}\".");
					}

					segments.Add(new DocumentationContents(contents));
				}
				else if (lineText.StartsWith("```")) {
					FinishParagraph();
					codeBlock = new List<string>();
					if (lineText.Length > 3 && lineText.Substring(3).Trim() is string trimmedType && trimmedType.Length > 0) {
						codeType = EnumUtils.ParseEnum<DocumentType>(trimmedType);
					}
					else {
						codeType = null;
					}
				}
				else {
					if (currentParagraph == null) {
						currentParagraph = new List<string>();
					}
					currentParagraph.Add(lineText);
				}

			}

			FinishSection();

			return new DocumentationFile(index, path, title, sections.ToArray());
		}

		private static readonly Regex textRegex = new Regex(@"
				(?<link> # This
					\[
						(?<text>(\\.|[^\]])+)
					\]
					\(
						(?<path>[^\[\]\(\)]+)
					\)
				)
				|
				\`(?<code>[^`]+)\`
			", RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);
		private static readonly RegexDelimiter<IEnumerable<DocumentationRun>> textParser = new RegexDelimiter<IEnumerable<DocumentationRun>>(textRegex, MatchParser, SpanParser);

		private static readonly Regex linkRegex = new Regex(@"
				\[
					(?<text>(\\.|[^\]])+)
				\]
				\(
					(?<path>[^\[\]\(\)]+)
				\)
			", RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);

		private enum TextParseState { TEXT, CODE, LINKTEXT, LINKLOCATION }

		private static DocumentationParagraph ParseParagraph(string text) {
			List<DocumentationRun> parts = new List<DocumentationRun>();

			TextParseState parseState = TextParseState.TEXT;
			MarkdownFormat format = MarkdownFormat.REGULAR;
			int head = 0;
			int index = 0;
			bool escaped = false;

			void FinishText() {
				parts.Add(DocumentationRun.FromText(CleanText(text[head..index]), format));
			}

			while (index < text.Length) {
				if (escaped) {
					escaped = false;
				}
				else if (text[index] == '\\') {
					escaped = true;
				}
				else if (parseState == TextParseState.CODE) {
					if (text[index] == '`') {
						parts.Add(DocumentationRun.FromCode(text[(head + 1)..index]));
						head = index + 1;
						parseState = TextParseState.TEXT;
					}
				}
				else if (parseState == TextParseState.LINKTEXT) {
					if (text[index] == ']') {
						if (text.Length > index + 1 && text[index + 1] == '(') {
							parseState = TextParseState.LINKLOCATION;
							index++;
						}
						else {
							parseState = TextParseState.TEXT;
						}
					}
				}
				else if (parseState == TextParseState.LINKLOCATION) {
					if (text[index] == ')') {
						parts.Add(ParseLink(text[head..(index + 1)], format));
						head = index + 1;
						parseState = TextParseState.TEXT;
					}
				}
				else { // parseState == TextParseState.TEXT
					if (text[index] == '`') {
						FinishText();
						head = index;
						parseState = TextParseState.CODE;
					}
					else if (text[index] == '[') {
						FinishText();
						head = index;
						parseState = TextParseState.LINKTEXT;
					}
					else if (text[index] == '*' && text.Length > index + 1 && text[index + 1] == '*') {
						FinishText();
						head = index + 2;
						if ((format & MarkdownFormat.BOLD) == MarkdownFormat.BOLD) {
							format ^= MarkdownFormat.BOLD;
						}
						else {
							format |= MarkdownFormat.BOLD;
						}
					}
					else if (text[index] == '_' && text.Length > index + 1 && text[index + 1] == '_') {
						FinishText();
						head = index + 2;
						if ((format & MarkdownFormat.ITALIC) == MarkdownFormat.ITALIC) {
							format ^= MarkdownFormat.ITALIC;
						}
						else {
							format |= MarkdownFormat.ITALIC;
						}
					}
				}

				index++;
			}

			if(index > head) {
				parts.Add(DocumentationRun.FromText(CleanText(text[head..]), format));
			}

			return new DocumentationParagraph(parts.ToArray());
		}

		private static DocumentationRun ParseLink(string text, MarkdownFormat format) {
			Match match = linkRegex.Match(text);

			if (!match.Success) {
				throw new FormatException("Invalid documentation link: " + text);
			}

			string linkText = CleanText(match.Groups["text"].Value);
			string path = match.Groups["path"].Value;
			
			DocumentationLink link = DocumentationLink.Parse(path);
			
			return DocumentationRun.FromLink(link, linkText, format);
		}

		private static IEnumerable<DocumentationRun> ParseText(string text, bool prependSpace) {
			if (prependSpace) {
				yield return DocumentationRun.FromText(" ", MarkdownFormat.REGULAR);
			}
			foreach (DocumentationRun run in textParser.Split(text).SelectMany(i => i)) {
				yield return run;
			}
		}
		private static IEnumerable<DocumentationRun> MatchParser(Match match) {
			if (match.Groups["link"].Success) {
				string text = CleanText(match.Groups["text"].Value);
				string path = match.Groups["path"].Value;
				DocumentationLink link = DocumentationLink.Parse(path);
				yield return DocumentationRun.FromLink(link, text, MarkdownFormat.REGULAR);
			}
			else if (match.Groups["code"].Success) {
				string code = match.Groups["code"].Value;
				yield return DocumentationRun.FromCode(code);
			}
			else {
				throw new ArgumentException("Invalid regex match.");
			}
		}
		private static IEnumerable<DocumentationRun> SpanParser(string text) {
			yield return DocumentationRun.FromText(CleanText(text), MarkdownFormat.REGULAR);
		}
		private static string CleanText(string text) {
			// Replace dashes
			text = Regex.Replace(text, @"\-\-\-", "\u2014");
			text = Regex.Replace(text, @"\-\-", "\u2013");

			return text;
		}

	}

	public class DocumentationNode {

		public readonly int index;
		public readonly DocumentationNode? parent;
		public DocumentationNode? Root { get { return parent is null ? null : (parent.Root ?? parent); } }

		public readonly string name;
		public DocumentationFile? main;
		public Dictionary<string, DocumentationFile> files;

		public Dictionary<string, DocumentationNode> nodes;

		public string Path { get { return (parent != null ? parent.Path + "/" : "") + name; } }
		public bool HasSubsections { get { return files.Count > 0 || nodes.Count > 0; } }

		public DocumentationNode(int index, DocumentationNode? parent, string name) {
			this.index = index;
			this.parent = parent;
			this.name = name;
			this.files = new Dictionary<string, DocumentationFile>();
			this.nodes = new Dictionary<string, DocumentationNode>();
		}
		public DocumentationNode(int index, string name) : this(index, null, name) { }

		private static readonly Regex indexRegex = new Regex(@"^(?<index>[0-9]+)\s*(?<title>.+)$");

		public DocumentationNode GetOrMake(string location) {
			if (location.Length == 0) {
				return this;
			}

			string[] locationParts = location.Split('/');
			string nodeName = locationParts[0];

			int index = int.MaxValue;
			if(indexRegex.Match(nodeName) is Match indexMatch && indexMatch.Success) {
				nodeName = indexMatch.Groups["title"].Value;
				index = int.Parse(indexMatch.Groups["index"].Value);
			}

			if (!nodes.TryGetValue(nodeName, out DocumentationNode? childNode)) {
				childNode = new DocumentationNode(index, this, nodeName);
				nodes.Add(nodeName, childNode);
			}

			if (locationParts.Length > 1) {
				return childNode.GetOrMake(string.Join("/", locationParts.Skip(1)));
			}
			else {
				return childNode;
			}
		}

		public DocumentationNode? GetNode(string location) {
			if (location.Length == 0) {
				return this;
			}

			string[] locationParts = location.Split('/');
			string nodeName = locationParts[0];

			if (nodes.TryGetValue(nodeName, out DocumentationNode? childNode)) {
				if (locationParts.Length > 1) {
					return childNode.GetNode(string.Join("/", locationParts.Skip(1)));
				}
				else {
					return childNode;
				}
			}
			else if(Root is DocumentationNode rootNode) {
				return rootNode.GetNode(location);
			}
			else {
				return null;
			}
		}

		public DocumentationFile? GetFile(string location) {
			if (location.Length == 0) {
				return this.main;
			}

			string[] locationParts = location.Split('/');
			string nodeName = locationParts[0];

			if (locationParts.Length == 1 && files.TryGetValue(nodeName, out DocumentationFile? childFile)) {
				return childFile;
			}
			else if (nodes.TryGetValue(nodeName, out DocumentationNode? childNode)) {
				if (locationParts.Length > 1) {
					return childNode.GetFile(string.Join("/", locationParts.Skip(1)));
				}
				else {
					return childNode.main;
				}
			}
			else if (Root is DocumentationNode rootNode) {
				return rootNode.GetFile(location);
			}
			else {
				return null;
			}
		}

	}

	public class DocumentationFile {

		public readonly int index;
		public readonly string location;
		public readonly string title;
		public readonly DocumentationSection[] sections;

		public DocumentationFile(int index, string location, string title, DocumentationSection[] sections) {
			this.index = index;
			this.location = location;
			this.title = title;
			this.sections = sections;
		}
	}

	
	public class DocumentationSection {

		public readonly int level;
		public readonly string title;
		public readonly IDocumentationSegment[] segments;

		public DocumentationSection(int level, string title, IDocumentationSegment[] segments) {
			this.level = level;
			this.title = title;
			this.segments = segments;
		}
	}

	public interface IDocumentationSegment { }

	public class DocumentationParagraph : IDocumentationSegment {

		public readonly DocumentationRun[] parts;

		public DocumentationParagraph(DocumentationRun[] parts) {
			this.parts = parts;
		}

	}

	public enum DocumentationLinkType { UNKNOWN, WIDGET, SHAPE, MARKUP, ENUM }
	public class DocumentationLink {
		public readonly DocumentationLinkType linkType;
		public readonly string location;

		public DocumentationLink(DocumentationLinkType linkType, string location) {
			this.linkType = linkType;
			this.location = location;
		}

		private static readonly Regex linkRegex = new Regex(@"
				(
					(?<type>[a-z]+) \:\:
				)?
				(?<path>.+)
			", RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);
		public static DocumentationLink Parse(string link) {
			Match match = linkRegex.Match(link);

			if (match.Success) {
				DocumentationLinkType linkType = DocumentationLinkType.UNKNOWN;

				if (match.Groups["type"].Success) {
					try {
						linkType = EnumUtils.ParseEnum<DocumentationLinkType>(match.Groups["type"].Value);
					}
					catch (FormatException) {
						throw new FormatException("Invalid document link type.");
					}
				}

				string location = match.Groups["path"].Value;

				return new DocumentationLink(linkType, location);
			}

			throw new FormatException("Badly formatted link.");
		}
	}

	public enum MarkdownFormat { REGULAR = 0b000, BOLD = 0b001, ITALIC = 0b010, BOLDITALIC = 0b011, CODE = 0b100 }

	public class DocumentationRun {

		public readonly DocumentationLink? link;
		public readonly string text;
		public bool IsCode => format == MarkdownFormat.CODE;
		public readonly MarkdownFormat format;

		private DocumentationRun(DocumentationLink? link, string text, MarkdownFormat format) {
			this.link = link;
			this.text = text;
			this.format = format;
		}

		public static DocumentationRun FromText(string text, MarkdownFormat format) {
			return new DocumentationRun(null, text, format);
		}
		public static DocumentationRun FromLink(DocumentationLink link, string text, MarkdownFormat format) {
			return new DocumentationRun(link, text, format);
		}
		public static DocumentationRun FromCode(string code) {
			return new DocumentationRun(null, code, MarkdownFormat.CODE);
		}

	}

	public class DocumentationCode : IDocumentationSegment {

		public readonly string code;
		public readonly DocumentType highlighting;

		public DocumentationCode(string code, DocumentType highlighting) {
			this.code = code;
			this.highlighting = highlighting;
		}

	}

	public enum DocumentationSectionContents {
		Widgets,
		Shapes,
		Boxes, LabelledBoxes, TitledBoxes,
		TitleStyles,
		Bars, UsageBars,
		Details,
		MarkupElements,
		BasisEnvironmentVariables, BasisEnvironmentFunctions,
		MarkupEnvironmentVariables, MarkupEnvironmentFunctions,
		CardSubjectEnvironmentVariables,
		CardOutlineEnvironmentVariables,
		CardSegmentEnvironmentVariables,
		CardSegmentOutlineEnvironmentVariables,
		CardFeatureEnvironmentVariables,
		CardConfigs, CardStructures
	}
	
	public class DocumentationContents : IDocumentationSegment {

		public readonly DocumentationSectionContents contents;

		public DocumentationContents(DocumentationSectionContents contents) {
			this.contents = contents;
		}
	}

}
