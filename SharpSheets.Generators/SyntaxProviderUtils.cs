using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using System.Threading;

namespace SharpSheets.Generators {

	public static class SyntaxProviderUtils {

		public static IncrementalValuesProvider<T> ForAttributeWithMetadataName<T>(this SyntaxValueProvider provider,
				string fullyQualifiedMetadataName, Func<SyntaxNode, CancellationToken, bool> predicate,
				Func<GeneratorAttributeSyntaxContext, CancellationToken, IEnumerable<T>> transform
			) {

			return provider.ForAttributeWithMetadataName(fullyQualifiedMetadataName, predicate, transform)
				.SelectMany(static (items, _) => items);
		}

		public static IncrementalValuesProvider<T> ForAttributeWithMetadataNameBatch<T,I>(this SyntaxValueProvider provider,
				string fullyQualifiedMetadataName, Func<SyntaxNode, CancellationToken, bool> predicate,
				Func<GeneratorAttributeSyntaxContext, CancellationToken, I> extractor,
				Func<ImmutableArray<I>, CancellationToken, IEnumerable<T>> transform
			) {

			return provider.ForAttributeWithMetadataName(fullyQualifiedMetadataName, predicate, extractor)
				.Collect()
				.SelectMany(transform);
		}

	}

}
