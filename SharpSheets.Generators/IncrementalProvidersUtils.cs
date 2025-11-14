using Microsoft.CodeAnalysis;
using System.Linq;

namespace SharpSheets.Generators {
	public static class IncrementalProvidersUtils {

		public static IncrementalValuesProvider<(T1, T2, T3)> Flatten<T1, T2, T3>(this IncrementalValuesProvider<((T1, T2), T3)> source) {
			return source.Select((i, _) => (i.Item1.Item1, i.Item1.Item2, i.Item2));
		}
		public static IncrementalValuesProvider<(T1, T2, T3)> Flatten<T1, T2, T3>(this IncrementalValuesProvider<(T1, (T2, T3))> source) {
			return source.Select((i, _) => (i.Item1, i.Item2.Item1, i.Item2.Item2));
		}

		public static IncrementalValueProvider<(T1, T2, T3)> Flatten<T1, T2, T3>(this IncrementalValueProvider<((T1, T2), T3)> source) {
			return source.Select((i, _) => (i.Item1.Item1, i.Item1.Item2, i.Item2));
		}
		public static IncrementalValueProvider<(T1, T2, T3)> Flatten<T1, T2, T3>(this IncrementalValueProvider<(T1, (T2, T3))> source) {
			return source.Select((i, _) => (i.Item1, i.Item2.Item1, i.Item2.Item2));
		}

		public static IncrementalValuesProvider<(T1, T2, T3, T4)> Flatten<T1, T2, T3, T4>(this IncrementalValuesProvider<(((T1, T2), T3), T4)> source) {
			return source.Select((i, _) => (i.Item1.Item1.Item1, i.Item1.Item1.Item2, i.Item1.Item2, i.Item2));
		}

		public static IncrementalValueProvider<(T1, T2, T3, T4)> Flatten<T1, T2, T3, T4>(this IncrementalValueProvider<(((T1, T2), T3), T4)> source) {
			return source.Select((i, _) => (i.Item1.Item1.Item1, i.Item1.Item1.Item2, i.Item1.Item2, i.Item2));
		}

		public static IncrementalValuesProvider<(T1, T2, T3, T4, T5)> Flatten<T1, T2, T3, T4, T5>(this IncrementalValuesProvider<((((T1, T2), T3), T4), T5)> source) {
			return source.Select((i, _) => (i.Item1.Item1.Item1.Item1, i.Item1.Item1.Item1.Item2, i.Item1.Item1.Item2, i.Item1.Item2, i.Item2));
		}

		public static IncrementalValueProvider<(T1, T2, T3, T4, T5)> Flatten<T1, T2, T3, T4, T5>(this IncrementalValueProvider<((((T1, T2), T3), T4), T5)> source) {
			return source.Select((i, _) => (i.Item1.Item1.Item1.Item1, i.Item1.Item1.Item1.Item2, i.Item1.Item1.Item2, i.Item1.Item2, i.Item2));
		}

		public static (IncrementalValueProvider<T1>, IncrementalValueProvider<T2>) Split<T1, T2>(this IncrementalValueProvider<(T1 a, T2 b)> source) {
			return (
					source.Select((i, _) => i.a),
					source.Select((i, _) => i.b)
				);
		}

		public static (IncrementalValueProvider<T1>, IncrementalValueProvider<T2>, IncrementalValueProvider<T3>) Split<T1, T2, T3>(this IncrementalValueProvider<(T1 a, T2 b, T3 c)> source) {
			return (
					source.Select((i, _) => i.a),
					source.Select((i, _) => i.b),
					source.Select((i, _) => i.c)
				);
		}

		public static (IncrementalValueProvider<T1>, IncrementalValueProvider<T2>, IncrementalValueProvider<T3>, IncrementalValueProvider<T4>) Split<T1, T2, T3, T4>(this IncrementalValueProvider<(T1 a, T2 b, T3 c, T4 d)> source) {
			return (
					source.Select((i, _) => i.a),
					source.Select((i, _) => i.b),
					source.Select((i, _) => i.c),
					source.Select((i, _) => i.d)
				);
		}

		public static (IncrementalValueProvider<T1>, IncrementalValueProvider<T2>, IncrementalValueProvider<T3>, IncrementalValueProvider<T4>, IncrementalValueProvider<T5>) Split<T1, T2, T3, T4, T5>(this IncrementalValueProvider<(T1 a, T2 b, T3 c, T4 d, T5 e)> source) {
			return (
					source.Select((i, _) => i.a),
					source.Select((i, _) => i.b),
					source.Select((i, _) => i.c),
					source.Select((i, _) => i.d),
					source.Select((i, _) => i.e)
				);
		}

		public static IncrementalValueProvider<(T1, T2, T3)> Combine<T1, T2, T3>(this IncrementalValueProvider<T1> source, IncrementalValueProvider<T2> other1, IncrementalValueProvider<T3> other2) {
			return source.Combine(other1).Combine(other2).Flatten();
		}

		public static IncrementalValueProvider<(T1, T2, T3, T4)> Combine<T1, T2, T3, T4>(this IncrementalValueProvider<T1> source, IncrementalValueProvider<T2> other1, IncrementalValueProvider<T3> other2, IncrementalValueProvider<T4> other3) {
			return source.Combine(other1).Combine(other2).Combine(other3).Flatten();
		}

		public static IncrementalValueProvider<(T1, T2, T3, T4, T5)> Combine<T1, T2, T3, T4, T5>(this IncrementalValueProvider<T1> source, IncrementalValueProvider<T2> other1, IncrementalValueProvider<T3> other2, IncrementalValueProvider<T4> other3, IncrementalValueProvider<T5> other4) {
			return source.Combine(other1).Combine(other2).Combine(other3).Combine(other4).Flatten();
		}

		public static IncrementalValuesProvider<T> WhereNotNull<T>(this IncrementalValuesProvider<T?> source) {
			return source.Where(static e => e is not null).Select(static (e, _) => e!);
		}

	}

}
