﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using SharpSheets.Widgets;
using SharpSheets.Utilities;
using SharpSheets.Layouts;
using SharpSheets.Evaluations;
using SharpSheets.Parsing;
using SharpSheets.Cards.Definitions;
using SharpSheets.Documentation;
using SharpSheets.Fonts;
using SharpSheets.Evaluations.Nodes;
using SharpSheets.Cards.CardConfigs;
using SharpEditor.DataManagers;

namespace SharpEditor.CodeHelpers {

	public abstract class SharpConfigCodeHelper<TSpan> : ICodeHelper where TSpan : SharpConfigSpan {

		protected readonly TextEditor textEditor;
		protected readonly SharpConfigParsingState<TSpan> parsingState;

		protected TextDocument Document { get { return textEditor.Document; } }

		protected SharpConfigCodeHelper(
				TextEditor textEditor,
				SharpConfigParsingState<TSpan> parsingState,
				ITypeDetailsCollection divTypes,
				ITypeDetailsCollection impliedConstructors,
				ConstructorDetails fallbackType,
				ConstructorArgumentDetails[] fallbackArguments
			) {

			this.textEditor = textEditor;
			this.parsingState = parsingState;

			this.divTypes = divTypes;
			this.impliedConstructors = impliedConstructors;
			this.fallbackType = fallbackType;
			this.fallbackArguments = fallbackArguments;
		}

		private bool StringEquals(string a, string b) {
			return SharpDocuments.StringEquals(a, b);
			//return string.Equals(a, b, StringComparison.InvariantCultureIgnoreCase);
		}

		#region Constructor Details

		private readonly ITypeDetailsCollection divTypes;
		private readonly ITypeDetailsCollection impliedConstructors; // Constructors which can appear inside property values
		private readonly ConstructorDetails fallbackType;
		private readonly ConstructorArgumentDetails[] fallbackArguments; // rectSetupConstructor.Arguments

		protected ConstructorDetails? GetConstructor(Type? type) {
			if(type is null) {
				return null;
			}
			else if(divTypes.TryGetValue(type, out ConstructorDetails? divConstructor)) {
				return divConstructor;
			}
			else if(impliedConstructors.TryGetValue(type, out ConstructorDetails? impliedConstructor)) {
				return impliedConstructor;
			}
			else if(fallbackType.DeclaringType == type) {
				return fallbackType;
			}
			else {
				return null;
			}
		}
		protected ConstructorDetails? GetConstructor(string name) {
			if (divTypes.TryGetValue(name, out ConstructorDetails? divConstructor)) {
				return divConstructor;
			}
			else if (impliedConstructors.TryGetValue(name, out ConstructorDetails? impliedConstructor)) {
				return impliedConstructor;
			}
			else if (string.Equals(fallbackType.Name, name, StringComparison.InvariantCultureIgnoreCase)) {
				return fallbackType;
			}
			else {
				return null;
			}
		}

		// TODO Do these need to be reset occasionally?
		private readonly Dictionary<string, ConstructorDetails> _registeredConstructorDetails = new Dictionary<string, ConstructorDetails>(StringComparer.InvariantCultureIgnoreCase);
		private readonly Dictionary<ConstructorDetails, FrameworkElement[]> _constructorDescriptions = new Dictionary<ConstructorDetails, FrameworkElement[]>();
		protected FrameworkElement[]? GetConstructorDescription(ConstructorDetails constructorDetails) {
			if(constructorDetails == null) { return null; }
			else if(ReferenceEquals(_registeredConstructorDetails.GetValueOrFallback(constructorDetails.FullName, null), constructorDetails)) {
				return _constructorDescriptions.GetValueOrFallback(constructorDetails, null);
			}
			else {
				if (_registeredConstructorDetails.TryGetValue(constructorDetails.FullName, out ConstructorDetails? alreadyRegistered)) {
					_constructorDescriptions.Remove(alreadyRegistered);
				}

				FrameworkElement[] description = TooltipBuilder.MakeConstructorEntry(constructorDetails, null, false, impliedConstructors).ToArray();
				_registeredConstructorDetails[constructorDetails.FullName] = constructorDetails;
				_constructorDescriptions[constructorDetails] = description;
				return description;
			}
		}

		#endregion

		#region Information Getters

		protected IEnumerable<TSpan> GetParseSpans(int offset) {
			return parsingState
				.GetSpansAtOffset(offset)
				.OfType<TSpan>()
				.Where(s => s.Type != SharpConfigSpanType.DRAWING_ERROR && s.Type != SharpConfigSpanType.NONE)
				.OrderBy(s => s.Length);
		}

		private int PropertyDepth(string name) {
			return name.Split('.').Length;
		}

		protected List<ConstructorDetails> GetOwnerConstructors(TSpan span) {
			//IContext spanContext = parsingState.GetContext(span.StartOffset);
			////Type ownerType = (GetConstructor(spanContext?.SimpleName ?? "") ?? fallbackType).DeclaringType;
			////ConstructorDetails ownerConstructor = ownerType != null ? GetConstructor(ownerType) : fallbackType;

			//ConstructorDetails ownerConstructor = GetConstructor(spanContext?.SimpleName ?? "") ?? fallbackType;
			//List<ConstructorDetails> ownerConstructors = new List<ConstructorDetails>() { ownerConstructor };

			List<ConstructorDetails> ownerConstructors = new List<ConstructorDetails>();
			foreach (TSpan ownerSpan in span.Owners.OfType<TSpan>().OrderBy(s => s.StartOffset)) {
				if (ownerSpan.Type == SharpConfigSpanType.DIV && ownerSpan.Name != null && divTypes.Get(ownerSpan.Name) is ConstructorDetails divConstructor) {
					ownerConstructors.Add(divConstructor);

					// If we've been redirected to this division because we're using a default implied constructor (i.e. no span)
					//if (divConstructor.Arguments.Where(a => span.Name != a.Name && (span.Name?.StartsWith(a.Name) ?? false) && CodeHelpers.DefaultType(a.Type) != null).OrderByDescending(a => a.Name.Split('.').Length - 1).FirstOrDefault() is ArgumentDetails correspondingArg) {
					if (span.Name != null && divConstructor.Arguments.Where(a => span.Name.StartsWith(a.Name) && CodeHelpers.DefaultType(a.Type) != null).OrderByDescending(a => PropertyDepth(a.Name)).FirstOrDefault() is ArgumentDetails correspondingArg) {
						ConstructorDetails? correspondingConstructor = GetConstructor(CodeHelpers.DefaultType(correspondingArg.Type));
						if (correspondingConstructor != null) { ownerConstructors.Add(correspondingConstructor.Prefixed(correspondingArg.Name)); }
					}
				}
				else if (span.Name != null && ownerSpan.Type == SharpConfigSpanType.PROPERTY && ownerSpan.Name != null) {
					int ownerNameOverlap = StringUtils.PrefixOverlapLength(span.Name, ownerSpan.Name);
					if (ownerNameOverlap > 0 && ownerSpan.Value != null && impliedConstructors.Get(ownerSpan.Value) is ConstructorDetails impliedConstructor) {
						// Found a constructor for the value of this property
						string prefix = string.Join(".", ownerSpan.Name.Split('.').SkipLastN(1));
						ownerConstructors.Add(impliedConstructor.Prefixed(prefix));
					}

					foreach(TSpan parentSpan in ownerSpan.Owners.OfType<TSpan>().OrderBy(s => s.StartOffset)) {
						if (parentSpan.Type == SharpConfigSpanType.DIV && parentSpan.Name != null && divTypes.Get(parentSpan.Name) is ConstructorDetails parentDivConstructor) {
							// This div is the parent of the property the current span belongs to, and might be the actual source of the constructor (e.g. title styles)
							ArgumentDetails? correspondingArg = parentDivConstructor.Arguments
								.Where(a => span.Name.StartsWith(a.Name) && StringUtils.PrefixOverlapLength(span.Name, a.Name) > ownerNameOverlap)
								.OrderByDescending(a => PropertyDepth(a.Name))
								.FirstOrDefault();
							if (correspondingArg != null) {
								ConstructorDetails? correspondingConstructor = GetConstructor(CodeHelpers.DefaultType(correspondingArg.Type));
								if (correspondingConstructor != null) {
									ownerConstructors.Add(correspondingConstructor.Prefixed(correspondingArg.Name));
								}
							}
						}
					}
				}
			}

			if (ownerConstructors.Count == 0) {
				ownerConstructors.Add(fallbackType);
			}

			return ownerConstructors;
		}

		protected ConstructorArgumentDetails[] GetAllArguments(TSpan span, IEnumerable<ConstructorDetails> parentConstructors) {
			if(span.Name is null) {
				return Array.Empty<ConstructorArgumentDetails>();
			}

			bool ArgIsMatch(ConstructorArgumentDetails arg) {

				bool isNumbered = arg.ArgumentType.DisplayType.IsNumbered(out _);

				Regex argRegex = new Regex(@"^" + Regex.Escape(arg.ArgumentName) + (isNumbered ? @"[0-9]+" : @"") + (arg.Implied is not null ? Regex.Escape("." + arg.Implied) : @"") + @"$", RegexOptions.IgnoreCase);

				return argRegex.IsMatch(span.Name);

				/*
				if (arg.Implied != null) {
					if (SharpDocuments.StringEquals(span.Name, $"{arg.ArgumentName}.{arg.Implied}")) {
						return true; // If the implied arg matches, we can just accept
					}
				}
				else if (SharpDocuments.StringEquals(span.Name, arg.ArgumentName)) {
					return true;
				}
				return false;
				*/
			}

			return parentConstructors
				.SelectMany(p => p.ConstructorArguments)
				.Where(ArgIsMatch)
				.ToArray();
		}

		protected class ConfigLineInfo {
			public readonly int line;
			public readonly string text;
			public readonly int indent;
			public readonly int? caratIndex;
			public readonly IContext? context;
			public readonly ConstructorDetails directParent;
			public readonly ArgumentDetails? argument;
			public readonly ConstructorDetails[] applicableConstructors;

			public ConfigLineInfo(int line, string text, int indent, int? caratIndex, IContext? context, ConstructorDetails directParent, ArgumentDetails? argument, ConstructorDetails[] applicableConstructors) {
				this.line = line;
				this.text = text;
				this.indent = indent;
				this.caratIndex = caratIndex;
				this.context = context;
				this.directParent = directParent;
				this.argument = argument;
				this.applicableConstructors = applicableConstructors;
			}

			public IEnumerable<ConstructorArgumentDetails> GetApplicableConstructorArgs() {
				return applicableConstructors.SelectMany(c => c.ConstructorArguments);
			}
			public IEnumerable<ConstructorArgumentDetails> GetApplicableConstructorArgs(string startsWith) {
				return applicableConstructors.SelectMany(c => c.ConstructorArguments).Where(a => a.ArgumentName.StartsWith(startsWith, SharpDocuments.StringComparison));
			}
		}

		protected int GetLineIndent(DocumentLine line) {
			return TextUtilities.GetWhitespaceAfter(Document, line.Offset).Length;
		}

		static readonly Regex argRegex = new Regex(@"^\@?(?<arg>[a-z][a-z0-9\.]+)\s*:", RegexOptions.IgnoreCase);
		protected ConfigLineInfo GetLineInfo(DocumentLine line) {
			int currentIndent = GetLineIndent(line);
			string currentLineText = Document.GetText(line).Trim();

			int? caratOffset = textEditor.CaretOffset;
			if (caratOffset.Value < line.Offset || caratOffset.Value >= line.EndOffset) {
				caratOffset = null;
			}
			else {
				caratOffset = Math.Min(caratOffset.Value - line.Offset - currentIndent, currentLineText.Length - 1);
			}


			Match argMatch = argRegex.Match(currentLineText);
			string? argName = argMatch.Success ? argMatch.Groups["arg"].Value : null;

			IContext? ownerContext = parsingState.GetContext(line);

			// TODO These can probably be done better
			/*
			Type ownerType = (GetConstructor(ownerContext?.SimpleName ?? "") ?? fallbackType).DeclaringType;
			ConstructorDetails ownerConstructor = ownerType != null ? GetConstructor(ownerType) : fallbackType;
			*/
			ConstructorDetails ownerConstructor = GetConstructor(ownerContext?.SimpleName ?? "") ?? fallbackType;

			// Finding the owner types is necessary, as we might be on an empty line with no owner spans
			ConstructorDetails[] ownerTypes = ownerContext != null ? ownerContext.TraverseChildren(true).Select(c => GetConstructor(c.SimpleName ?? "") ?? fallbackType).DistinctPreserveOrder(ConstructorComparer.Instance).ToArray() : Array.Empty<ConstructorDetails>(); // new Type[] { ownerType };
			List<ConstructorDetails> applicableDivConstructors =
				ownerConstructor.Yield()
				.Concat(ownerTypes)
				.Concat(
					parsingState.FindOverlappingSpans(line)
						.Where(s => s.Type != SharpConfigSpanType.DRAWING_ERROR && s.Type != SharpConfigSpanType.NONE)
						.OrderBy(s => s.Length)
						.SelectMany(s => GetOwnerConstructors(s))
					)
				.DistinctPreserveOrder(ConstructorComparer.Instance)
				.ToList();

			ArgumentDetails? argumentDetails = null;

			List<ArgumentDetails> impliedConstructorArguments;
			if (argName != null) {
				impliedConstructorArguments =
					applicableDivConstructors.SelectMany(c => c.Arguments)
					.Where(a => a.Implied != null && argName.StartsWith(a.Name, SharpDocuments.StringComparison))
					.DistinctPreserveOrder(ArgumentComparer.Instance)
					.ToList();
			}
			else {
				impliedConstructorArguments = new List<ArgumentDetails>();
			}
			List<ConstructorDetails> applicableImpliedConstructors = new List<ConstructorDetails>();
			foreach (ArgumentDetails impliedConArg in impliedConstructorArguments) {
				foreach (ConstructorDetails impliedConstructor in (ownerContext ?? Context.Empty).TraverseChildren(true).Select(c => c.GetProperty($"{impliedConArg.Name}.{impliedConArg.Implied}", true, c, null)).WhereNotNull().Select(p => GetConstructor(p)).WhereNotNull().DistinctPreserveOrder(ConstructorComparer.Instance)) {
					foreach (ArgumentDetails impliedArg in impliedConstructor.Arguments) {
						string impliedArgName = $"{impliedConArg.Name}.{impliedArg.Name}";
						if (SharpDocuments.StringComparer.Equals(argName, impliedArgName)) {
							applicableImpliedConstructors.Add(impliedConstructor);
							if (argumentDetails == null) argumentDetails = impliedArg.Prefixed(impliedConArg.Name);
						}
					}
				}
			}
			if (argumentDetails == null && argName != null) {
				foreach (ArgumentDetails arg in ownerConstructor.Arguments.Concat(applicableDivConstructors.SelectMany(c => c.Arguments).Where(a => !a.UseLocal).Distinct(ArgumentComparer.Instance))) {
					if (arg.Name == argName || (arg.Implied != null && StringEquals($"{arg.Name}.{arg.Implied}", argName))) {
						argumentDetails = arg;
						break;
					}
				}
			}

			ConstructorDetails[] allApplicableConstructors = applicableDivConstructors.Concat(applicableImpliedConstructors).ToArray();

			return new ConfigLineInfo(line.LineNumber, currentLineText, currentIndent, caratOffset, ownerContext, ownerConstructor, argumentDetails, allApplicableConstructors);
		}

		protected ConfigLineInfo GetCurrentLineInfo() {
			DocumentLine currentLine = Document.GetLineByOffset(textEditor.CaretOffset);
			return GetLineInfo(currentLine);
		}

		#endregion

		#region Completion Window

		public int GetCompletionStartOffset() {
			return textEditor.CaretOffset;
		}

		IEnumerable<ICompletionData> GetArgumentNameCompletionEntries(IEnumerable<ConstructorArgumentDetails> arguments, IContext? context, string prefix = "", string? existingText = null) {
			string prepend = prefix.Length > 0 ? prefix + "." : "";

			string FinalText(string text) {
				string final = prepend + text;
				if (existingText != null) {
					//final = final.Replace(existingText, ""); // Need to ensure this is only taken from beginning
					final = Regex.Replace(final, @"^" + Regex.Escape(existingText), "");
				}
				return final;
			}

			foreach (ConstructorArgumentDetails argument in arguments.Distinct(ArgumentComparer.Instance)) {
				if (argument.ArgumentType.IsList) {
					// Ignore entries arguments for completion
					continue;
				}
				else if (argument.Implied != null) {
					string fullArg = $"{argument.ArgumentName.ToLowerInvariant()}.{argument.Implied.ToLowerInvariant()}";
					yield return new CompletionEntry(FinalText(fullArg) + ":") {
						DescriptionElements = TooltipBuilder.GetArgumentDescription(argument, context),
						Append = " "
					};

					if (context != null) {
						string? style = context.GetProperty(fullArg, argument.UseLocal, context, null);
						if (!impliedConstructors.TryGetValue(style ?? "!", out ConstructorDetails? argConstructor)) {
							argConstructor = GetConstructor(CodeHelpers.DefaultType(argument.ArgumentType));
						}
						if (argConstructor!=null) {
							IContext argContext = new NamedContext(context, argument.ArgumentName.ToLowerInvariant());
							foreach (ICompletionData completionData in GetArgumentNameCompletionEntries(argConstructor.ConstructorArguments, argContext, prefix: argument.ArgumentName.ToLowerInvariant(), existingText: existingText)) {
								yield return completionData;
							}
						}
					}
				}
				else if (argument.ArgumentType.DisplayType == typeof(bool)) {
					yield return new CompletionEntry(FinalText(argument.ArgumentName.ToLowerInvariant())) {
						DescriptionElements = TooltipBuilder.GetArgumentDescription(argument, context)
					};
				}
				else if (argument.ArgumentType.DisplayType == typeof(Margins) || argument.ArgumentType.DisplayType == typeof(Position)) {
					yield return new CompletionEntry(FinalText(argument.ArgumentName.ToLowerInvariant()) + ":") {
						DescriptionElements = TooltipBuilder.GetArgumentDescription(argument, context),
						Append = " {",
						AfterCaretAppend = "}"
					};
				}
				else if (argument.ArgumentType.DisplayType == typeof(ChildHolder)) {
					yield return new CompletionEntry(FinalText(argument.ArgumentName.ToLowerInvariant()) + ":") {
						DescriptionElements = TooltipBuilder.GetArgumentDescription(argument, context),
						Append = Environment.NewLine
					};
				}
				else {
					yield return new CompletionEntry(FinalText(argument.ArgumentName.ToLowerInvariant()) + ":") {
						DescriptionElements = TooltipBuilder.GetArgumentDescription(argument, context),
						Append = " "
					};
				}
			}
		}

		protected abstract bool GetCustomCompletionData(ConfigLineInfo currentLine, List<ICompletionData> data); // TODO This is unused?

		public IList<ICompletionData> GetCompletionData() {
			List<ICompletionData> data = new List<ICompletionData>();

			ConfigLineInfo currentLine = GetCurrentLineInfo();

			if (GetCustomCompletionData(currentLine, data)) {
				// If subclass has returned all necessary completion data
				// then just return that data, otherwise continue.
				return data;
			}

			if (string.IsNullOrWhiteSpace(currentLine.text)) {
				// Blank line (suggest arguments and new rect types)
				if (currentLine.applicableConstructors.Length > 0) {
					data.AddRange(GetArgumentNameCompletionEntries(currentLine.GetApplicableConstructorArgs(), currentLine.context));
				}
				else {
					data.AddRange(GetArgumentNameCompletionEntries(fallbackArguments, currentLine.context));
				}

				if (string.IsNullOrWhiteSpace(currentLine.text)) {
					// Only suggest rect types with an empty line
					foreach (ConstructorDetails divConstructor in divTypes) {
						data.Add(new CompletionEntry($"{divConstructor.Name}:") {
							DescriptionElements = GetConstructorDescription(divConstructor),
							Append = Environment.NewLine
						});
					}
				}
			}
			else if (currentLine.text == "@" && currentLine.directParent != null) {
				// Start of local parameter (only suggest properties of current constructor - or default if current unknown)
				if (currentLine.directParent != null) {
					data.AddRange(GetArgumentNameCompletionEntries(currentLine.directParent.ConstructorArguments, currentLine.context));
				}
				else {
					data.AddRange(GetArgumentNameCompletionEntries(fallbackArguments, currentLine.context));
				}
			}
			else if(currentLine.text == "!") {
				// Start of negative flag
				data.AddRange(GetArgumentNameCompletionEntries(currentLine.GetApplicableConstructorArgs().Where(a => a.ArgumentType.DisplayType == typeof(bool)), currentLine.context));
			}
			else if (currentLine.text.EndsWith(":") && currentLine.argument != null && currentLine.argument.Type.DisplayType is Type argType) {
				// Previous character is colon, so check to see if this is an argument where we can suggest values (enum or implied constructor)
				if (Nullable.GetUnderlyingType(argType) is Type nulledType) {
					argType = nulledType;
				}

				if (argType.IsEnum) {
					if (SharpDocumentation.GetEnumDoc(argType) is EnumDoc enumDoc) {
						// If documentation is available, provide full details
						foreach (EnumValDoc valueDoc in enumDoc.values) {
							data.Add(new CompletionEntry(valueDoc.name.ToLowerInvariant()) {
								DescriptionElements = TooltipBuilder.MakeEnumBlocks(enumDoc, valueDoc).ToArray()
							});
						}
					}
					else {
						// If documentation is not available, just provide names of enum values
						foreach (object enumVal in Enum.GetValues(argType)) {
							if (enumVal.ToString() is string enumValStr) {
								data.Add(new CompletionEntry(enumValStr.ToLowerInvariant()));
							}
						}
					}
				}
				else if (argType == typeof(FontPath)) {
					foreach (string fontname in FontPathRegistry.GetAllRegisteredFonts()) {
						data.Add(new CompletionEntry(fontname));
					}
				}
				else if (argType == typeof(FontPathGrouping)) {
					foreach (string fontname in FontPathRegistry.GetAllRegisteredFamilies().Concat(FontPathRegistry.GetAllRegisteredFonts())) {
						data.Add(new CompletionEntry(fontname));
					}
				}
				else {
					// If the argument is an implied constructor (i.e. a Shape which can then refer to other arguments), give that constructor's name
					foreach (KeyValuePair<string, ConstructorDetails> impliedConstructorName in impliedConstructors.GetConstructorNames(argType).OrderBy(kv => kv.Value.Name)) {
						data.Add(new CompletionEntry(impliedConstructorName.Key) {
							DescriptionElements = GetConstructorDescription(impliedConstructorName.Value),
						});
					}
				}
			}
			else if (currentLine.text.EndsWith(".")) {
				// Previous character is "."
				string withoutDot = currentLine.text.Substring(0, currentLine.text.Length - 1);
				data.AddRange(GetArgumentNameCompletionEntries(currentLine.GetApplicableConstructorArgs(withoutDot), currentLine.context, existingText: currentLine.text));
			}

			return data;
		}

		protected abstract bool CustomTextEnteredTriggerCompletion(TextCompositionEventArgs e, string currentLineText);

		public virtual bool TextEnteredTriggerCompletion(TextCompositionEventArgs e) {
			string currentLineText = Document.GetText(Document.GetLineByOffset(textEditor.CaretOffset)).Trim();
			if (e.Text.EndsWith("@") && currentLineText == "@") {
				return true;
			}
			else if (e.Text.EndsWith("!") && currentLineText == "!") {
				return true;
			}
			else if (e.Text.EndsWith(",") || currentLineText.EndsWith(",")) {
				return true; // This OK?
			}
			else if (textEditor.CaretOffset > 2) {
				//Console.WriteLine($"e.Text: {e.Text.Replace("\n", "\\n")}, Previous char: {Document.GetCharAt(textEditor.CaretOffset - 2).ToString().Replace("\n", "\\n")}");
				if (e.Text.EndsWith(".") && char.IsLetterOrDigit(Document.GetCharAt(textEditor.CaretOffset - 2))) {
					//Console.WriteLine("Met criteria");
					return true;
				}
				else if (e.Text.EndsWith(" ") && Document.GetCharAt(textEditor.CaretOffset - 2) == ':') {
					return true;
				}
			}
			else if(CustomTextEnteredTriggerCompletion(e, currentLineText)) {
				return true;
			}
			return false;
		}

		#endregion

		#region Tooltip

		protected abstract bool GetCustomToolTipContent(TSpan span, string word, List<UIElement> elements, IContext? currentContext, ConstructorDetails? constructor, IContext? constructorContext);
		protected abstract void GetAdditionalToolTipContent(TSpan span, string word, List<UIElement> elements, IContext? currentContext, ConstructorDetails? constructor, IContext? constructorContext);

		// TODO This is in progress?
		/*
		protected ArgumentDetails GetArgument(TSpan span, IContext context) {
			parsingState.GetOwningType

			return null;
		}
		*/

		public IList<UIElement> GetToolTipContent(int offset, string word) {
			List<UIElement> content = new List<UIElement>();

			foreach (TSpan span in GetParseSpans(offset)) { // Must be inside registered span

				IContext? currentContext = parsingState.GetContext(span.StartOffset);

				ConstructorDetails? constructor = null;
				IContext? constructorContext = null;
				if (span.Value == word && currentContext != null && impliedConstructors.TryGetValue(word, out constructor)) { // Check if span value is shape
					string[]? nameParts = span.Name?.Split('.');
					if (nameParts != null && nameParts.Length > 1 && string.Equals(nameParts[^1], "style", StringComparison.InvariantCultureIgnoreCase)) {
						constructorContext = new NamedContext(currentContext, string.Join(".", nameParts.Take(nameParts.Length - 1)));
					}
					else {
						constructorContext = currentContext;
					}
				}
				else if (span.Type == SharpConfigSpanType.DIV) { // See if we're over a div
					constructor = divTypes.Get(span.Name ?? "");
					constructorContext = span.Context;
				}

				if (!GetCustomToolTipContent(span, word, content, currentContext, constructor, constructorContext)) {
					// Only continue if subclass indicates that it didn't get all necessary tips
					if (constructor != null) {
						content.AddRange(TooltipBuilder.MakeConstructorEntry(constructor, constructorContext, true, impliedConstructors));
					}
					else if (span.Type == SharpConfigSpanType.DIV) {
						content.AddRange(MakeNamedChildEntry(span));
					}
					else { // if(span.Type == ConfigSpanType.PROPERTY || span.Type == ConfigSpanType.FLAG) // or Named child
						content.AddRange(MakePropertyEntry(span, word, currentContext));
					}
				}

				GetAdditionalToolTipContent(span, word, content, currentContext, constructor, constructorContext);

				if (content.Count > 0) {
					break; // Stop querying spans if one provides any content
				}
			}

			return content;
		}

		protected ConstructorArgumentDetails? GetNamedChildArg(TSpan span, ConstructorDetails parentConstructor) {
			if (span.Name is null) {
				return null;
			}

			bool ArgIsMatch(ConstructorArgumentDetails arg) {
				if(arg.ArgumentType.DisplayType == typeof(ChildHolder)) {
					return SharpDocuments.StringEquals(span.Name, arg.ArgumentName); // TODO Need to deal with stray underscores here?
				}
				else if(arg.ArgumentType.DisplayType.IsNumbered(out Type? numberedType) && numberedType == typeof(ChildHolder)) {
					Regex childRegex = new Regex(@"^" + Regex.Escape(arg.ArgumentName) + @"[0-9]+$", RegexOptions.IgnoreCase);
					return childRegex.IsMatch(span.Name);
				}
				else {
					return false;
				}
			}

			return parentConstructor.ConstructorArguments
				.Where(ArgIsMatch)
				.FirstOrDefault();
		}

		protected IEnumerable<FrameworkElement> MakeNamedChildEntry(TSpan span) {
			string? parentConstructorName = parsingState.GetContext(Document.GetLineByOffset(span.StartOffset))?.Parent?.SimpleName;

			if (parentConstructorName is null) { yield break; }

			ConstructorDetails? parentConstructor = divTypes.Get(parentConstructorName);

			if (parentConstructor is null) { yield break; }

			ConstructorArgumentDetails? argument = GetNamedChildArg(span, parentConstructor);

			if (argument is null) { yield break; }

			yield return TooltipBuilder.MakeArgumentHeaderBlock(argument, null, true);

			if (TooltipBuilder.MakeDescriptionTextBlock(argument.ArgumentDescription) is TextBlock descriptionBlock) {
				//yield return TooltipBuilder.MakeIndentedBlock(argument.ArgumentDescription);
				yield return descriptionBlock;
			}
		}

		protected IEnumerable<FrameworkElement> MakePropertyEntry(TSpan span, string word, IContext? context) {

			ConstructorArgumentDetails[] allArgs = GetAllArguments(span, GetOwnerConstructors(span));

			Type[] enumTypes = allArgs.Select(t => t.ArgumentType.DisplayType.GetUnderlyingType()).Where(t => t.IsEnum).ToArray();
			EnumDoc[] enumDocs = enumTypes
				.Select(t => SharpDocumentation.GetEnumDoc(t))
				.WhereNotNull()
				.Where(t => t.values.Any(v => StringEquals(v.name, word)))
				.ToArray();

			if (enumDocs.Length > 0) {
				foreach (EnumDoc enumDoc in enumDocs.GroupBy(d => d.type).Select(g => g.First())) {
					EnumValDoc? enumValDoc = enumDoc.values.Where(v => StringEquals(v.name, word)).FirstOrDefault();
					foreach (TextBlock block in TooltipBuilder.MakeEnumBlocks(enumDoc, enumValDoc)) yield return block;
				}
			}
			else {
				foreach (IGrouping<DocumentationString?, ConstructorArgumentDetails> descriptionGrouping in allArgs.GroupBy(t => t.ArgumentDescription)) {
					StackPanel argumentList = new StackPanel() {
						Orientation = Orientation.Vertical,
						Margin = TooltipBuilder.TextBlockMargin
					};
					foreach (ConstructorArgumentDetails constructorArg in descriptionGrouping.GroupBy(t => $"{SharpValueHandler.GetTypeName(t.ArgumentType)} {t.ConstructorName}.{t.ArgumentName}").Select(g => g.First())) {
						argumentList.Children.Add(TooltipBuilder.MakeArgumentHeaderBlock(constructorArg, context, false));
					}
					yield return argumentList;

					if (TooltipBuilder.MakeDescriptionTextBlock(descriptionGrouping.Key) is TextBlock descriptionBlock) {
						//yield return TooltipBuilder.MakeIndentedBlock(descriptionGrouping.Key);
						yield return descriptionBlock;
					}

					if (descriptionGrouping.Select(a => a.ArgumentType.DisplayType).First() is Type argType && argType.IsEnum && SharpDocumentation.GetEnumDoc(argType) is EnumDoc enumDoc) {
						yield return TooltipBuilder.MakeEnumOptionsBlock(enumDoc, true);
					}
				}
			}
		}

		#endregion

		#region Context Menu

		public virtual IList<Control> GetContextMenuItems(int offset, string? word) {
			List<Control> items = new List<Control>();

			ConstructorDetails? contextConstructor = null;
			if (word != null && impliedConstructors.TryGetValue(word, out ConstructorDetails? implied)) {
				contextConstructor = implied;
			}
			else if (GetLineInfo(Document.GetLineByOffset(offset)) is ConfigLineInfo lineInfo && lineInfo.applicableConstructors.Length > 0) {
				contextConstructor = lineInfo.applicableConstructors.First();
			}

			if (contextConstructor != null) {
				MenuItem item = new MenuItem() { Header = contextConstructor.Name + " Documentation..." };
				item.Click += delegate { SharpEditorWindow.Instance?.controller.ActivateDocumentationWindow().NavigateTo(contextConstructor, null); };
				items.Add(item);
			}

			return items;
		}

		#endregion

		#region Text Entered

		public void TextEntered(TextCompositionEventArgs e) { }

		#endregion

		#region Commenting

		public bool SupportsIncrementComment { get; } = true;

		public void IncrementComment(int offset, int length) {
			DocumentLine startLine = Document.GetLineByOffset(offset);
			DocumentLine endLine = Document.GetLineByOffset(offset + length);

			int minIndent = int.MaxValue;
			for (int i = startLine.LineNumber; i <= endLine.LineNumber; i++) {
				DocumentLine line = Document.GetLineByNumber(i);
				int currentIndent = GetLineIndent(line);
				if (currentIndent < minIndent) { minIndent = currentIndent; }
			}

			if(minIndent > startLine.Length) {
				return;
			}

			Document.BeginUpdate();

			for (int i = startLine.LineNumber; i <= endLine.LineNumber; i++) {
				DocumentLine line = Document.GetLineByNumber(i);
				Document.Insert(line.Offset + minIndent, "#");
			}

			Document.EndUpdate();
		}

		public void DecrementComment(int offset, int length) {
			DocumentLine startLine = Document.GetLineByOffset(offset);
			DocumentLine endLine = Document.GetLineByOffset(offset + length);

			Document.BeginUpdate();

			for (int i = startLine.LineNumber; i <= endLine.LineNumber; i++) {
				DocumentLine line = Document.GetLineByNumber(i);
				string lineText = Document.GetText(line);
				for (int c = 0; c < lineText.Length; c++) {
					if (!char.IsWhiteSpace(lineText[c])) {
						if (lineText[c] == '#') {
							Document.Remove(line.Offset + c, 1);
						}
						break;
					}
				}
			}

			Document.EndUpdate();
		}

		#endregion

	}

	public class CharacterSheetCodeHelper : SharpConfigCodeHelper<SharpConfigSpan> {

		public static ICodeHelper GetCodeHelper(TextEditor textEditor, CharacterSheetParsingState parsingState) {
			ITypeDetailsCollection divTypes = SharpEditorRegistries.WidgetFactoryInstance;
			ITypeDetailsCollection impliedConstructors = SharpEditorRegistries.ShapeFactoryInstance;

			ConstructorDetails fallbackType = SharpEditorRegistries.WidgetFactoryInstance.Get(typeof(SharpSheets.Widgets.Page))!;
			ConstructorArgumentDetails[] fallbackArguments = WidgetFactory.WidgetSetupConstructor.ConstructorArguments.ToArray();

			return new CharacterSheetCodeHelper(textEditor, parsingState, divTypes, impliedConstructors, fallbackType, fallbackArguments);
		}

		private CharacterSheetCodeHelper(
				TextEditor textEditor,
				SharpConfigParsingState<SharpConfigSpan> parsingState,
				ITypeDetailsCollection divTypes,
				ITypeDetailsCollection impliedConstructors,
				ConstructorDetails fallbackType,
				ConstructorArgumentDetails[] fallbackArguments
			) : base(textEditor, parsingState, divTypes, impliedConstructors, fallbackType, fallbackArguments) {

		}

		protected override bool GetCustomCompletionData(ConfigLineInfo currentLine, List<ICompletionData> data) {
			return false;
		}

		protected override bool CustomTextEnteredTriggerCompletion(TextCompositionEventArgs e, string currentLineText) {
			return false;
		}

		protected override bool GetCustomToolTipContent(SharpConfigSpan span, string word, List<UIElement> elements, IContext? currentContext, ConstructorDetails? constructor, IContext? constructorContext) {
			return false;
		}

		protected override void GetAdditionalToolTipContent(SharpConfigSpan span, string word, List<UIElement> elements, IContext? currentContext, ConstructorDetails? constructor, IContext? constructorContext) {
			return;
		}
	}

	public class CardConfigCodeHelper : SharpConfigCodeHelper<CardConfigSpan> {

		public static ICodeHelper GetCodeHelper(TextEditor textEditor, CardConfigParsingState parsingState) {
			/*
			TypeDetailsCollection divTypes = new TypeDetailsCollection();
			divTypes.UnionWith(SharpEditorDetails.CardRectConstructors);
			divTypes.Include(CardDefinitionFactory.GetSectionDefinitionConstructor());
			divTypes.Include(CardDefinitionFactory.GetFeatureDefinitionConstructor());
			divTypes.UnionWith(CardDefinitionFactory.GetAllPartConstructors(SharpEditorDetails.WidgetFactoryInstance));
			*/

			ITypeDetailsCollection divTypes = SharpEditorRegistries.CardSetConfigFactoryInstance;
			ITypeDetailsCollection impliedConstructors = SharpEditorRegistries.ShapeFactoryInstance;

			ConstructorDetails fallbackType = CardSetConfigFactory.CardSetConfigConstructor;
			ConstructorArgumentDetails[] fallbackArguments = fallbackType.ConstructorArguments
				.Concat(WidgetFactory.WidgetSetupConstructor.ConstructorArguments).ToArray();

			return new CardConfigCodeHelper(textEditor, parsingState, divTypes, impliedConstructors, fallbackType, fallbackArguments);
		}

		protected new readonly CardConfigParsingState parsingState;

		private CardConfigCodeHelper(
				TextEditor textEditor,
				CardConfigParsingState parsingState,
				ITypeDetailsCollection divTypes,
				ITypeDetailsCollection impliedConstructors,
				ConstructorDetails fallbackType,
				ConstructorArgumentDetails[] fallbackArguments
			) : base(textEditor, parsingState, divTypes, impliedConstructors, fallbackType, fallbackArguments) {
			this.parsingState = parsingState;
		}

		CardConfigParsingState ParsingState { get { return parsingState; } }

		protected override bool GetCustomCompletionData(ConfigLineInfo currentLine, List<ICompletionData> data) {
			//Console.WriteLine(currentLine.text + " " + currentLine.indent);
			if (currentLine.text != null && currentLine.caratIndex.HasValue && currentLine.text.Substring(0, currentLine.caratIndex.Value).Contains('{')) {
				// TODO This seriously needs improving
				//Console.WriteLine("Inside expression");
				if (parsingState.GetVariableDefinitionBox(currentLine.context) is IVariableDefinitionBox variables) {
					foreach (KeyValuePair<EvaluationName, EvaluationType> variableReturn in variables.GetReturnTypes()) {
						data.Add(new CompletionEntry(variableReturn.Key.ToString()) {
							DescriptionElements = new FrameworkElement[] { new TextBlock() { Text = SharpValueHandler.GetTypeName(variableReturn.Value) } },
						});
					}
					foreach (IEnvironmentFunctionInfo functionInfo in variables.GetFunctionInfos()) {
						data.Add(new CompletionEntry(functionInfo.Name.ToString()) {
							DescriptionElements = new FrameworkElement[] { new TextBlock() { Text = functionInfo.Description ?? "Environment function." } },
						});
					}
				}
			}

			return false;
		}

		protected override bool CustomTextEnteredTriggerCompletion(TextCompositionEventArgs e, string currentLineText) {
			return false;
		}

		private static readonly Regex variableRegex = new Regex(@"[a-z][a-z0-9]+", RegexOptions.IgnoreCase);
		private string GetVariableNameFromWord(string word) {
			// We need this in case there are stray underscores lying around
			Match match = variableRegex.Match(word);
			if (match.Success) { return match.Value; }
			else { return word; }
		}

		protected override bool GetCustomToolTipContent(CardConfigSpan span, string word, List<UIElement> elements, IContext? currentContext, ConstructorDetails? constructor, IContext? constructorContext) {
			DocumentLine currentLine = Document.GetLineByOffset(span.StartOffset);
			string variableWord = GetVariableNameFromWord(word);
			if ((span.IsExpression || Document.GetText(span).Contains(variableWord.StartsWith("$") ? variableWord : "$" + variableWord)) && ParsingState != null) {
				if (ParsingState.GetVariableDefinitionBox(currentLine) is IVariableDefinitionBox variableBox && variableBox.IsVariable(variableWord) && variableBox.TryGetDefinition(variableWord, out Definition? variableWordDefinition)) {
					elements.Add(TooltipBuilder.MakeCardDefinitionBlocks(variableWordDefinition, null));
					return true;
				}
			}
			else if (span.Type == SharpConfigSpanType.DEFINITION && span.Content is Definition definition) {
				// TODO What to do in case of error where span.Content is null?
				elements.Add(TooltipBuilder.MakeCardDefinitionBlocks(definition, null));
				return true;
			}
			return false;
		}

		protected override void GetAdditionalToolTipContent(CardConfigSpan span, string word, List<UIElement> elements, IContext? currentContext, ConstructorDetails? constructor, IContext? constructorContext) {
			if (constructor != null) {
				if (constructor.FullName == CardSetConfigFactory.OutlineConstructor.FullName) {
					if ((constructorContext ?? Context.Empty).TraverseParents().Any(c => CardSetConfigFactory.SegmentConfigConstructors.ContainsKey(c.SimpleName))) {
						elements.AddRange(TooltipBuilder.MakeDefinitionEntries(CardSubjectEnvironments.BaseDefinitions.Concat(CardSegmentEnvironments.BaseDefinitions).Concat(CardSegmentOutlineEnvironments.BaseDefinitions), null));
					}
					else {
						elements.AddRange(TooltipBuilder.MakeDefinitionEntries(CardSubjectEnvironments.BaseDefinitions.Concat(CardOutlinesEnvironments.BaseDefinitions), null));
					}
				}
				else if (constructor.FullName == CardSetConfigFactory.HeaderConstructor.FullName) {
					elements.AddRange(TooltipBuilder.MakeDefinitionEntries(CardSubjectEnvironments.BaseDefinitions.Concat(CardOutlinesEnvironments.BaseDefinitions), null));
				}
				/*
				else if (constructor.Name == CardDefinitionFactory.SectionDefinitionConstructor.Name) {
					elements.AddRange(TooltipBuilder.MakeDefinitionEntries(CardSectionEnvironments.BaseDefinitions, null));
				}
				*/
				else if (constructor.FullName == CardSetConfigFactory.FeatureConfigConstructor.FullName) {
					elements.AddRange(TooltipBuilder.MakeDefinitionEntries(CardFeatureEnvironments.BaseDefinitions, null));
				}
				else if (CardSetConfigFactory.cardConfigConstructorsByName.ContainsKey(constructor.FullName)) { // This one last so we've already checked other possibilities from this collection
					elements.AddRange(TooltipBuilder.MakeDefinitionEntries(CardSegmentEnvironments.BaseDefinitions, null));
				}
			}
		}
	}
}