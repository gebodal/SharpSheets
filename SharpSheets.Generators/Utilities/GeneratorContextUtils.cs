using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Text;
using System.Threading;

namespace SharpSheets.Generators.Utilities {

	public static class GeneratorContextUtils {

		public static void ExecuteGenerator<T>(this IncrementalGeneratorInitializationContext context, IncrementalValueProvider<T> source, Func<T, CancellationToken, string?> generator, Func<T, string> filename) {
			context.RegisterSourceOutput(source,
				(spc, value) => {
					// generate the source code
					string? result = generator(value, spc.CancellationToken);
					// Create a separate file and add it to the output
					if (!string.IsNullOrEmpty(result)) {
						string name = filename(value);
						spc.AddSource(name, SourceText.From(result!, Encoding.UTF8));
					}
				});
		}

		public static void ExecuteGenerator<T>(this IncrementalGeneratorInitializationContext context, IncrementalValueProvider<T> source, Func<T, Compilation, CancellationToken, string?> generator, Func<T, string> filename) {
			ExecuteGenerator(context, source.Combine(context.CompilationProvider), (src, ct) => generator(src.Left, src.Right, ct), src => filename(src.Left));
		}

		// Only provided for IncrementalValueProvider, as if there are multiple values, filename must be dynamic
		public static void ExecuteGenerator<T>(this IncrementalGeneratorInitializationContext context, IncrementalValueProvider<T> source, Func<T, CancellationToken, string?> generator, string filename) {
			ExecuteGenerator(context, source, generator, _ => filename);
		}
		public static void ExecuteGenerator<T>(this IncrementalGeneratorInitializationContext context, IncrementalValueProvider<T> source, Func<T, Compilation, CancellationToken, string?> generator, string filename) {
			ExecuteGenerator(context, source, generator, _ => filename);
		}

		public static void ExecuteGenerator<T>(this IncrementalGeneratorInitializationContext context, IncrementalValuesProvider<T> source, Func<T, CancellationToken, string?> generator, Func<T, string> filename) {
			context.RegisterSourceOutput(source,
				(spc, value) => {
					// generate the source code
					string? result = generator(value, spc.CancellationToken);
					// Create a separate file and add it to the output
					if (!string.IsNullOrEmpty(result)) {
						string name = filename(value);
						spc.AddSource(name, SourceText.From(result!, Encoding.UTF8));
					}
				});
		}

		public static void ExecuteGenerator<T>(this IncrementalGeneratorInitializationContext context, IncrementalValuesProvider<T> source, Func<T, Compilation, CancellationToken, string?> generator, Func<T, string> filename) {
			ExecuteGenerator(context, source.Combine(context.CompilationProvider), (src, ct) => generator(src.Left, src.Right, ct), src => filename(src.Left));
		}

		public static void RegisterSourceOutput<T>(this IncrementalGeneratorInitializationContext context, IncrementalValueProvider<T> source, Action<SourceProductionContext, T, Compilation> action) {
			context.RegisterSourceOutput(source.Combine(context.CompilationProvider),
				(spc, source) => action(spc, source.Left, source.Right));
		}

		public static void RegisterSourceOutput<T>(this IncrementalGeneratorInitializationContext context, IncrementalValuesProvider<T> source, Action<SourceProductionContext, T, Compilation> action) {
			context.RegisterSourceOutput(source.Combine(context.CompilationProvider),
				(spc, source) => action(spc, source.Left, source.Right));
		}

	}

}
