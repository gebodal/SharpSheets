using System.Collections.Generic;
using System;
using System.Text.RegularExpressions;
using System.Linq;
using SharpSheets.Utilities;
using SharpSheets.Parsing;
using SharpSheets.Cards.CardConfigs;
using SharpSheets.Exceptions;

namespace SharpSheets.Cards.CardSubjects {

	public class CardSubjectParser {

		// This class can be created in one of two ways:
		// With an ICardConfigRegistry that configs are drawn from
		// Or with a single CardConfig that all subjects are forced to adopt (for use when parsing CardConfig archives)

		private readonly CardSetConfig? enforcedConfig;
		private readonly ICardSetConfigRegistry? configRegistry;

		public CardSubjectParser(ICardSetConfigRegistry configRegistry) {
			this.configRegistry = configRegistry;
			this.enforcedConfig = null;
		}

		public CardSubjectParser(CardSetConfig enforcedConfig) {
			this.enforcedConfig = enforcedConfig;
			this.configRegistry = null;
		}

		private static readonly Regex commentRegex = new Regex(@"(?<!\\)(?:(\\\\)*)(?<comment>\%.+)$");

		private static readonly Regex divisionRegex = new Regex(@"^(\=+|\s+\=\=+)\s*(?=\%|$)");
		private static readonly Regex cardConfigRegex = new Regex(@"^(\#\=\s*(?<path>.+)|\#\!.*)$");
		private static readonly Regex titleRegex = new Regex(@"
			^ # Must be at start of string
			(?<titlestyle>\#\>|\#+)
			\s*
			(?<title> # Captures the whole title string
				(?<titletext>([^\(\)\[\]\#]*[^\s\(\)\[\]\#])?) # Title content (can be empty)
				(\s*\((?<titlenote>[^\(\)\[\]]*)\))? # Optional note in ()-brackets
				(\s*\[(?<titledetails>[^\[\]]*)\])? # Optional details in []-brackets
			)
			$ # Must be end of string
			", RegexOptions.IgnorePatternWhitespace);
		private static readonly Regex entryRegex = new Regex(@"
			^ # Must be at start of string
			(?![\#\=]) # First character must not be # or =
			(?<list>\+\s+)? # Optional list specifier
			(?<entryname>
				([^\(\)\[\]\{:\.]|(?<=\\)\{)* # All but last character may contain a space
				([^\(\)\[\]\{:\.\ ]|(?<=\\)\{) # Last character must not be a space
				(?=\s*[\(\[\:]) # Must be followed by '(', '[', or ':' to be an entry name
			)
			(
				\s*
				\(
				(?<entrynote>([^\(\)\{:]|(?<=\\)\{)*)
				\)
			|
				\s*
				\[
				(?<entrydetails>([^\[\]]|\[.+\])*) # If there are square braces, they must be complete
				\]
			)* # Zero or more repetitions of this pattern
			\s*
			(:\s*(?<entrytext>.+))?
			$ # Must be end of string
			", RegexOptions.IgnorePatternWhitespace | RegexOptions.IgnoreCase);
		private static readonly Regex propertyNameRegex = new Regex(@"[a-z][a-z0-9\s]*", RegexOptions.IgnoreCase);
		private static readonly Regex lineTerminatorRegex = new Regex(@"(?<escape>\\)?(?<terminator>\\\\)$");
		private static readonly Regex listItemRegex = new Regex(@"^\+\s+(?<content>.+)$");

		private static readonly Regex trimRegex = new Regex(@"^\s*(?<trimmed>.*\S)\s*$");

		private DocumentSpan GetGroupSpan(Group matchGroup, DocumentSpan textSpan) {
			return new DocumentSpan(textSpan.Offset + matchGroup.Index, textSpan.Line, textSpan.Column + matchGroup.Index, matchGroup.Length);
		}
		private ContextValue<string>? GetGroupValue(Group matchGroup, DocumentSpan textSpan) {
			if (matchGroup.Success) {
				Match trimmed = trimRegex.Match(matchGroup.Value);
				Group trimmedGroup = trimmed.Groups[1];
				if (trimmedGroup.Success) {
					DocumentSpan matchSpan = GetGroupSpan(matchGroup, textSpan);
					DocumentSpan trimmedSpan = new DocumentSpan(matchSpan.Offset + trimmedGroup.Index, matchSpan.Line, matchSpan.Column + trimmedGroup.Index, trimmedGroup.Length);
					return new ContextValue<string>(trimmedSpan, trimmedGroup.Value);
				}

				return new ContextValue<string>(GetGroupSpan(matchGroup, textSpan), matchGroup.Value);
			}
			else {
				//DocumentSpan invalidGroupSpan = new DocumentSpan(textSpan.Offset, textSpan.Line, textSpan.Column, 0);
				//return new ContextValue<string?>(invalidGroupSpan, null);
				return null;
			}
		}
		private ContextValue<string> TrimText(ContextValue<string> text) {
			if (text.Value != null) {
				Match trimmed = trimRegex.Match(text.Value);
				Group trimmedGroup = trimmed.Groups[1];
				if (trimmedGroup.Success) {
					DocumentSpan trimmedSpan = GetGroupSpan(trimmedGroup, text.Location);
					return new ContextValue<string>(trimmedSpan, trimmedGroup.Value);
				}
			}

			return text;
		}

		/*
		private ContextValue<string> GetDetails(Group detailsMatchGroup, DocumentSpan lineSpan) {
			string detailsText = detailsMatchGroup.Value.ToString().Trim();
			ContextValue<string> details = new ContextValue<string>(new DocumentSpan(lineSpan.Line, lineSpan.Column + detailsMatchGroup.Index, detailsMatchGroup.Length), detailsText);
			return details;
		}
		*/

		private Dictionary<string, ContextProperty<string>> CreateNewInitialSetup() {
			return new Dictionary<string, ContextProperty<string>>(StringComparer.InvariantCultureIgnoreCase);
		}

		public CardSubjectDocument Parse(DirectoryPath source, string description, out CompilationResult results) {

			// TODO There could well be problems with line numbers here when dealing with reading archives rather than current files
			// TODO Ideally, archive subject errors should be traced back to the card config inclusion line (should we modify the CardSubject we get from the archive?)
			// As in: config.TryGetArchived(int line, string name, out CardSubject archiveSubject)

			CardSubjectDocument parsedSubjectDocument = new CardSubjectDocument();
			CardSubjectSet currentParsedSubjectSet = new CardSubjectSet(parsedSubjectDocument, DocumentSpan.Imaginary);
			parsedSubjectDocument.AddSubjectSet(currentParsedSubjectSet);

			List<SharpParsingException> errors = new List<SharpParsingException>();
			//List<List<CardSubjectConcrete>> parsedSubjects = new List<List<CardSubjectConcrete>> { new List<CardSubjectConcrete>() };

			LineOwnership lineOwnership = new LineOwnership();
			//Dictionary<int, IDocumentEntity> parents = new Dictionary<int, IDocumentEntity>();
			//HashSet<int> unusedLines = new HashSet<int>();
			HashSet<int> usedLines = new HashSet<int>();

			void AssignRelation(DocumentSpan parent, DocumentSpan child) {
				lineOwnership.Add(child.Line, parent.Line);
			}

			Stack<CardSetConfig> configStack = new Stack<CardSetConfig>();
			if (enforcedConfig != null) { configStack.Push(enforcedConfig); }

			//string[] lines = Regex.Split(description, "\r\n|\r|\n", RegexOptions.Multiline, TimeSpan.FromMilliseconds(500));

			CardSubjectBuilder? currentSubject = null;
			Dictionary<string, ContextProperty<string>>? initialSetup = null;

			void BuildCurrentSubject() {
				if (initialSetup != null) {
					currentSubject.SetProperties(initialSetup.Values);
					initialSetup = null;
				}

				CardSubject? finalSubject = currentSubject.Build(currentParsedSubjectSet, out List<SharpParsingException> buildErrors);
				errors.AddRange(buildErrors); // TODO A way to add Subject origin line if error line is null? (This should ideally be properly nested, so at each point in the Build recusrion, the parent Line is used if no line information is available)
				if (finalSubject != null) {
					currentParsedSubjectSet.AddSubject(finalSubject);
				}
				currentSubject = null;
			}

			bool useLastFeature = false;
			foreach (ContextValue<string> lineValue in LineSplitting.SplitLines(description)) { // (int i = 0; i < lines.Length; i++) {
				string lineText = lineValue.Value.TrimEnd();
				if(commentRegex.Match(lineText) is Match commentMatch && commentMatch.Success) {
					lineText = lineText.Substring(0, commentMatch.Groups["comment"].Index).TrimEnd();
				}
				lineText = StringParsing.Unescape(lineText, '%'); // Is this working properly?

				if (string.IsNullOrWhiteSpace(lineText)) {
					useLastFeature = false;
					continue; // Ignore empty lines
				}

				usedLines.Add(lineValue.Location.Line); // By default assume non-empty lines are used

				Match match;
				DocumentSpan lineSpan = new DocumentSpan(lineValue.Location.Offset, lineValue.Location.Line, lineValue.Location.Column, lineText.Length);

				if ((match = cardConfigRegex.Match(lineText)).Success) {
					if (currentSubject != null) {
						BuildCurrentSubject();
					}
					if (match.Groups["path"].Success) {
						if (configRegistry != null) {
							Group pathGroup = match.Groups["path"];
							DocumentSpan configLocation = new DocumentSpan(lineSpan.Offset + pathGroup.Index, lineSpan.Line, lineSpan.Column + pathGroup.Index, pathGroup.Length);

							CardSetConfig? config = configRegistry.GetSetConfig(source, pathGroup.Value, out List<SharpParsingException> configErrors);
							errors.AddRange(configErrors.Select(e => e.AtLocation(configLocation)));

							if (config != null) {
								parsedSubjectDocument.AddConfiguration(configLocation, config);
								configStack.Push(config);
							}
							else {
								errors.Add(new SharpParsingException(configLocation, "Could not get card set configuration."));
							}
						}
						else { // enforcedConfig == null
							errors.Add(new SharpParsingException(lineSpan, "Cannot change an enforced configuration during parse."));
						}
					}
					else { // Otherwise we are popping the last configuration from the stack
						if (enforcedConfig != null) {
							errors.Add(new SharpParsingException(lineSpan, "Cannot remove an enforced configuration."));
						}
						else if (configStack.Count > 0) {
							configStack.Pop();
						}
						else {
							errors.Add(new SharpParsingException(lineSpan, "No card configurations to pop."));
						}
					}
				}
				else if ((match = divisionRegex.Match(lineText)).Success) {
					// Indicates that a new segment is starting
					if (currentSubject != null) {
						BuildCurrentSubject();
					}
					currentParsedSubjectSet = new CardSubjectSet(parsedSubjectDocument, lineSpan);
					parsedSubjectDocument.AddSubjectSet(currentParsedSubjectSet);
					//parsedSubjects.Add(new List<CardSubjectConcrete>());
				}
				else if ((match = titleRegex.Match(lineText)).Success) {
					string titleStyle = match.Groups["titlestyle"].Value.Trim();
					int titleLevel = titleStyle.Length;

					Group wholeTitleGroup = match.Groups["title"];
					ContextValue<string> wholeTitleValue = GetGroupValue(wholeTitleGroup, lineSpan)!.Value; // This cannot be null if match was successful
					Group titleTextGroup = match.Groups["titletext"];
					ContextValue<string> titleTextValue = GetGroupValue(titleTextGroup, lineSpan)!.Value; // Cannot be null, but might be empty string
					Group titleNoteGroup = match.Groups["titlenote"];
					ContextValue<string>? titleNoteValue = GetGroupValue(titleNoteGroup, lineSpan);
					Group titleDetailsGroup = match.Groups["titledetails"];
					ContextValue<string>? titleDetailsValue = GetGroupValue(titleDetailsGroup, lineSpan);
					//ContextValue<string> titleDetails = new ContextValue<string>(GetGroupSpan(titleDetailsGroup, lineSpan), titleDetailsGroup.Value.Trim()); // GetDetails(match.Groups["titledetails"], lineSpan);

					if (titleStyle == "#>") {
						if (currentSubject != null) {
							BuildCurrentSubject();
						}

						if (configStack.Count > 0) {
							//Console.WriteLine($"{i,3}: Card subject from archive. Name: {wholeTitle}");
							if(configStack.Peek().TryGetArchived(wholeTitleValue.Value, out CardSubject? archiveSubject)) {
								////currentSubject = CardSubjectBuilder.Reopen(archiveSubject.WithOrigin(i));
								//parsedSubjects.Last().Add(archiveSubject.WithOrigin(lineSpan)); // Reopen?
								currentParsedSubjectSet.AddSubject(archiveSubject.WithOrigin(currentParsedSubjectSet, titleTextValue.Location)); // TODO Reopen?
							}
							else {
								currentSubject = new CardSubjectBuilder(wholeTitleValue, configStack.Peek());
								BuildCurrentSubject(); // TODO Is this OK? Reopening better?
								errors.Add(new SharpParsingException(wholeTitleValue.Location, $"Could not find \"{wholeTitleValue.Value}\" in archive."));
							}
							initialSetup = CreateNewInitialSetup();
							useLastFeature = false;
							//parents[i] = currentSubject;
						}
						else {
							errors.Add(new SharpParsingException(lineSpan, "No configurations available for archive access."));
							//unusedLines.Add(lineValue.Location.Line);
							usedLines.Remove(lineValue.Location.Line);
						}
					}
					else if (titleLevel == 1) {
						if (currentSubject != null) {
							BuildCurrentSubject();
						}

						//Console.WriteLine($"{i,3}: New card subject. Name: {wholeTitle}");
						if (configStack.Count > 0) {
							currentSubject = new CardSubjectBuilder(wholeTitleValue, configStack.Peek());
							initialSetup = CreateNewInitialSetup();
							useLastFeature = false;
							//parents[i] = currentSubject;
						}
						else {
							errors.Add(new SharpParsingException(lineSpan, "No card configuration specified for this subject."));
							//unusedLines.Add(lineValue.Location.Line);
							usedLines.Remove(lineValue.Location.Line);
						}
					}
					else if (currentSubject != null) {
						if (initialSetup != null) {
							currentSubject.SetProperties(initialSetup.Values);
							initialSetup = null;
						}

						if (titleLevel == 2) {
							//Console.WriteLine($"{i,3}: New segment title. Name: {titleText} ({titleNote}) [{titleDetails.Value}]");
							useLastFeature = false;
							CardSegmentBuilder newSegment = CardSegmentBuilder.Create(titleTextValue.Location, currentSubject, titleTextValue, titleNoteValue, titleDetailsValue);
							currentSubject.segments.Add(newSegment);
							AssignRelation(currentSubject.Location, newSegment.Location);
							//parents[i] = newSegment;
						}
						else if (currentSubject.segments.Count > 0) { // titleLevel > 2 (consider all of these to be equal)
							//Console.WriteLine($"{i,3}: New feature title. Name: {titleText} ({titleNote}) [{titleDetails.Value}]");
							useLastFeature = true;
							CardSegmentBuilder currentSegment = currentSubject.segments.Last();
							CardFeatureBuilder newFeature = currentSegment.AddEmpty(titleTextValue.Location, titleTextValue, titleNoteValue, titleDetailsValue);
							AssignRelation(currentSegment.Location, newFeature.Location);
							//parents[i] = newFeature;
						}
						else {
							errors.Add(new SharpParsingException(lineSpan, "Invalid feature title line: no segment available."));
						}
					}
					else {
						if (configStack.Count > 0) {
							errors.Add(new SharpParsingException(lineSpan, "No subject available for entry."));
						}
						//unusedLines.Add(lineSpan.Line);
						usedLines.Remove(lineSpan.Line);
					}
				}
				else if (currentSubject != null && (match = entryRegex.Match(lineText)).Success) {
					bool isListItem = match.Groups["list"].Success;

					Group entryNameGroup = match.Groups["entryname"];
					ContextValue<string> entryNameValue = GetGroupValue(entryNameGroup, lineSpan)!.Value; // Must exist if match was success
					bool isValidPropertyName = propertyNameRegex.IsMatch(entryNameValue.Value); // (?? "")?

					Group entryNoteGroup = match.Groups["entrynote"];
					ContextValue<string>? entryNoteValue = GetGroupValue(entryNoteGroup, lineSpan);

					Group entryDetailsGroup = match.Groups["entrydetails"];
					ContextValue<string>? entryDetailsValue = GetGroupValue(entryDetailsGroup, lineSpan);

					Group entryTextGroup = match.Groups["entrytext"];
					ContextValue<string>? entryTextValue = GetGroupValue(entryTextGroup, lineSpan);

					if (initialSetup != null && !isListItem && isValidPropertyName && entryTextValue.HasValue && !entryNoteValue.HasValue && !entryDetailsValue.HasValue) {
						//Console.WriteLine($"{i,3}: New setup entry. Name: \"{entryName}\", Note (invalid): \"{entryNote}\", Details (invalid): \"{entryDetails}\", Text: \"{entryText}\"");
						//currentSubject.SetProperty(entryName, entryText);
						if(initialSetup.TryGetValue(entryNameValue.Value, out ContextProperty<string> oldValue)) {
							// New value is overwriting a previous line
							//unusedLines.Add(oldValue.Location.Line);
							usedLines.Remove(oldValue.Location.Line);
						}

						initialSetup[entryNameValue.Value] = new ContextProperty<string>(entryNameValue.Location, entryNameValue.Value, entryTextValue.Value.Location, entryTextValue.Value.Value);
						AssignRelation(currentSubject.Location, lineSpan);
					}
					else if (initialSetup == null && currentSubject.segments.Count > 0) {
						CheckForTerminator(entryTextValue, out entryTextValue, out bool lineTerminated);

						//Console.WriteLine($"{i,3}: New one-line feature. Name: \"{entryName}\", Note: \"{entryNote}\", Details: \"{entryDetails}\", Text: \"{entryText}\"");
						CardSegmentBuilder currentSegment = currentSubject.segments.Last();
						currentSegment.Add(entryNameValue.Location, entryNameValue, entryNoteValue, entryDetailsValue, entryTextValue, isListItem);
						useLastFeature = configStack.Peek().allowFeatureFollowOn && !lineTerminated;
						AssignRelation(currentSegment.Location, lineSpan);
						//parents[i] = newFeature;
					}
					else {
						errors.Add(new SharpParsingException(lineSpan, "Invalid entry line."));
					}

				}
				else if (currentSubject != null) {
					if (initialSetup != null) {
						//Console.WriteLine($"{i,3}: Free line of text provided before setup completed. Text: {lineText}");
						if (configStack.Peek().requireFormalSetupEnd) {
							errors.Add(new SharpParsingException(lineSpan, "Free line of text provided before setup completed."));
							continue; // Skip further processing of this line of config
						}
						else if (currentSubject.segments.Count == 0) {
							currentSubject.SetProperties(initialSetup.Values);
							initialSetup = null;

							CardSegmentBuilder newSegment = CardSegmentBuilder.Create(lineSpan, currentSubject, null, null, null);
							currentSubject.segments.Add(newSegment);
							AssignRelation(currentSubject.Location, lineSpan);
							//parents[i] = newSegment;
							useLastFeature = false;
						}
					}

					CheckForTerminator(new ContextValue<string>(lineSpan, lineText), out ContextValue<string> lineContentValue, out bool lineTerminated);

					if ((match = listItemRegex.Match(lineText)).Success) {
						Group listItemContentGroup = match.Groups["content"];
						ContextValue<string> listItemContentValue = GetGroupValue(listItemContentGroup, lineSpan)!.Value; // Cannot be null if match was successful
						//Console.WriteLine($"{i,3}: List item text provided. Create list item Feature. Text: {listItemContent}" + (lineTerminated ? " (line terminated)" : ""));
						CardSegmentBuilder currentSegment = currentSubject.segments.Last();
						currentSegment.Add(lineSpan, null, null, null, listItemContentValue, true);
						AssignRelation(currentSegment.Location, lineSpan);
						//parents[i] = newFeature;
						useLastFeature = configStack.Peek().allowFeatureFollowOn && !lineTerminated;
					}
					else if (useLastFeature) {
						//Console.WriteLine($"{i,3}: Free line of text provided. Append to last Feature ({currentSubject.segments.Last().Features.Last().Title}). Text: \"{lineContent}\"" + (lineTerminated ? " (line terminated)" : ""));
						currentSubject.segments.Last().AppendToLast(TrimText(lineContentValue));
						useLastFeature = useLastFeature && !lineTerminated;
					}
					else if (currentSubject.segments.Count > 0){
						//Console.WriteLine($"{i,3}: Free line of text provided. Create title-less Feature. Text: {lineContent}" + (lineTerminated ? " (line terminated)" : ""));
						CardSegmentBuilder currentSegment = currentSubject.segments.Last();
						currentSegment.Add(lineSpan, null, null, null, TrimText(lineContentValue), false);
						AssignRelation(currentSegment.Location, lineSpan);
						//parents[i] = newFeature;
						useLastFeature = configStack.Peek().allowFeatureFollowOn && !lineTerminated;
					}
					else {
						errors.Add(new SharpParsingException(lineSpan, "Invalid text line: No segment available."));
					}
				}
				else {
					if (configStack.Count > 0) {
						errors.Add(new SharpParsingException(lineSpan, "No subject available, could not parse line."));
					}
					//unusedLines.Add(lineValue.Location.Line);
					usedLines.Remove(lineValue.Location.Line);
				}
			}

			if (currentSubject != null) {
				BuildCurrentSubject();
			}

			List<FilePath> configDependencies = parsedSubjectDocument.GetConfigurations()
				.Select(cv => cv.Value)
				.SelectMany(d => d.origin.Yield().Concat(d.archivePaths))
				.ToList();

			results = new CompilationResult(parsedSubjectDocument, null, errors, usedLines, lineOwnership, configDependencies);

			return parsedSubjectDocument;
		}

		private static void CheckForTerminator(ContextValue<string> lineText, out ContextValue<string> lineContent, out bool lineTerminated) {
			if (!string.IsNullOrEmpty(lineText.Value)) {
				Match terminatorMatch = lineTerminatorRegex.Match(lineText.Value);

				bool terminator = terminatorMatch.Groups["terminator"].Value.Length > 0;
				bool escaped = terminatorMatch.Groups["escape"].Value.Length > 0;

				if (terminator && !escaped) {
					lineTerminated = true;
					string content = lineText.Value[..terminatorMatch.Index].TrimEnd();
					DocumentSpan location = lineText.Location;
					lineContent = new ContextValue<string>(new DocumentSpan(location.Offset, location.Line, location.Column, content.Length), content);
					//Console.WriteLine($"Line terminator found ({lineTerminated}): \"{lineText.Value}\" -> \"{content}\"");
				}
				else if (terminator && escaped) {
					lineTerminated = false;
					Group escapeGroup = terminatorMatch.Groups["escape"];
					string content = lineText.Value[..escapeGroup.Index] + "\\\\";
					DocumentSpan location = lineText.Location;
					lineContent = new ContextValue<string>(new DocumentSpan(location.Offset, location.Line, location.Column, content.Length), content);
					//Console.WriteLine($"Line terminator found ({lineTerminated}): \"{lineText.Value}\" -> \"{content}\"");
				}
				else {
					lineTerminated = false;
					lineContent = lineText;
				}
			}
			else {
				lineContent = lineText;
				lineTerminated = false;
			}
		}
		private static void CheckForTerminator(ContextValue<string>? lineText, out ContextValue<string>? lineContent, out bool lineTerminated) {
			if (lineText.HasValue) {
				CheckForTerminator(lineText.Value, out ContextValue<string> finalLineContent, out lineTerminated);
				lineContent = finalLineContent;
			}
			else {
				lineContent = lineText;
				lineTerminated = false;
			}
		}

	}

}