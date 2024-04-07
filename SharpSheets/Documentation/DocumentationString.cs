using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpSheets.Documentation {

	public class DocumentationString : IEquatable<DocumentationString> {

		public static readonly DocumentationString Empty = new DocumentationString(Enumerable.Empty<IDocumentationSpan>());

		public IDocumentationSpan[] Spans { get; }

		public DocumentationString(IEnumerable<IDocumentationSpan> spans) {
			Spans = spans.ToArray();
		}

		public DocumentationString(params IDocumentationSpan[] spans) {
			Spans = spans;
		}

		public DocumentationString(string text) {
			Spans = new IDocumentationSpan[] { new TextSpan(text) };
		}

		public T[] Process<T>(IDocumentationSpanVisitor<T> visitor) {
			T[] result = new T[Spans.Length];
			for(int i=0; i<Spans.Length; i++) {
				if (Spans[i] is TextSpan textSpan) {
					result[i] = visitor.Visit(textSpan);
				}
				else if (Spans[i] is LineBreakSpan lineBreakSpan) {
					result[i] = visitor.Visit(lineBreakSpan);
				}
				else if (Spans[i] is TypeSpan typeSpan) {
					result[i] = visitor.Visit(typeSpan);
				}
				else if (Spans[i] is ParameterSpan parameterSpan) {
					result[i] = visitor.Visit(parameterSpan);
				}
				else if (Spans[i] is EnumValueSpan valueSpan) {
					result[i] = visitor.Visit(valueSpan);
				}
				else {
					throw new InvalidOperationException($"Unrecognized {nameof(IDocumentationSpan)} type.");
				}
			}
			return result;
		}

		public DocumentationString Convert(IDocumentationSpanVisitor<IDocumentationSpan> visitor) {
			return new DocumentationString(Process(visitor));
		}

		public bool Equals(DocumentationString? other) {
			if (other is null) { return false; }
			if(Spans.Length != other.Spans.Length) { return false; }
			for (int i = 0; i < Spans.Length; i++) {
				if (!Spans[i].Equals(other.Spans[i])) { return false; }
			}
			return true;
		}

		public override bool Equals(object? obj) {
			return obj is DocumentationString docString && Equals(docString);
		}

		public override int GetHashCode() {
			HashCode hash = new HashCode();
			for (int i = 0; i < Spans.Length; i++) {
				hash.Add(Spans[i]);
			}
			return hash.ToHashCode();
		}
		
	}

	public interface IDocumentationSpanVisitor<TResult> {
		// This isn't proper Visitor Pattern, is it...?
		TResult Visit(TextSpan span);
		TResult Visit(LineBreakSpan span);
		TResult Visit(TypeSpan span);
		TResult Visit(ParameterSpan span);
		TResult Visit(EnumValueSpan span);
	}

	public interface IDocumentationSpan { }

	public class TextSpan : IDocumentationSpan {
		public string Text { get; }
		public TextSpan(string text) {
			this.Text = text;
		}
		public override int GetHashCode() {
			return Text.GetHashCode();
		}
		public override bool Equals(object? obj) {
			return obj is TextSpan textSpan && Text == textSpan.Text;
		}
	}

	public class LineBreakSpan : IDocumentationSpan {
		public LineBreakSpan() { }
		public override int GetHashCode() {
			return "LineBreak".GetHashCode();
		}
		public override bool Equals(object? obj) {
			return obj is LineBreakSpan;
		}
	}

	public class TypeSpan : IDocumentationSpan {
		public string Name { get; }
		public Type? Type { get; }
		public TypeSpan(string name, Type? type) {
			this.Name = name;
			this.Type = type;
		}
		public override int GetHashCode() {
			return Name.GetHashCode();
		}
		public override bool Equals(object? obj) {
			return obj is TypeSpan typeSpan && Name == typeSpan.Name && Type == typeSpan.Type;
		}
	}

	public class ParameterSpan : IDocumentationSpan {
		public string Parameter { get; }
		public ParameterSpan(string parameter) {
			this.Parameter = parameter;
		}
		public override int GetHashCode() {
			return Parameter.GetHashCode();
		}
		public override bool Equals(object? obj) {
			return obj is ParameterSpan paramSpan && Parameter == paramSpan.Parameter;
		}
	}

	public class EnumValueSpan : IDocumentationSpan {
		public string Type { get; }
		public string Value { get; }

		public EnumValueSpan(string type, string value) {
			Type = type;
			Value = value;
		}

		public override int GetHashCode() {
			return HashCode.Combine(Type, Value);
		}
		public override bool Equals(object? obj) {
			return obj is EnumValueSpan enumSpan && Type == enumSpan.Type && Value == enumSpan.Value;
		}
	}

}
