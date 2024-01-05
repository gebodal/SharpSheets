using System;
using System.Collections.Generic;
using System.Linq;

namespace SharpSheets.Utilities {

	public static class Partitioning {
		public static IEnumerable<T[][]> GetAllPartitions<T>(T[] elements, int k) {
			return GetAllPartitions(0, k, Array.Empty<T[]>(), elements);
		}

		public static IEnumerable<T[][]> GetAllPartitions<T>(IEnumerable<T> elements, int k) {
			return GetAllPartitions(0, k, Array.Empty<T[]>(), elements.ToArray());
		}

		private static IEnumerable<T[][]> GetAllPartitions<T>(int depth, int k, T[][] fixedParts, T[] suffixElements) {
			//Console.WriteLine(new string('\t', depth) + $"Start GetAllPartitions {depth} ({fixedParts.Length})");
			// A trivial partition consists of the fixed parts  followed by all suffix elements as one block
			yield return fixedParts.Concat(new T[][] { suffixElements }).ToArray();
			//Console.WriteLine(new string('\t', depth) + "Yielded trivial");

			if (depth == k - 1) yield break;

			// Get all two-group-partitions of the suffix elements  and sub-divide them recursively
			IEnumerable<Tuple<T[], T[]>> suffixPartitions = GetTuplePartitions(suffixElements);
			foreach (Tuple<T[], T[]> suffixPartition in suffixPartitions) {
				foreach (T[][] subPartition in GetAllPartitions(depth + 1, k, fixedParts.Concat(new T[][] { suffixPartition.Item1 }).ToArray(), suffixPartition.Item2)) {
					//Console.WriteLine($"Returning partition of length {subPartition.Length} at depth {depth}");
					yield return subPartition;
				}
			}
			//Console.WriteLine(new string('\t', depth) + $"End GetAllPartitions {depth}");
		}

		private static IEnumerable<Tuple<T[], T[]>> GetTuplePartitions<T>(T[] elements) {
			// No result if less than 2 elements (i.e. 1 or 0)
			if (elements.Length < 2) yield break;


			for (int i = elements.Length - 1; i > 0; i--) {
				yield return Tuple.Create(elements.Take(i).ToArray(), elements.Skip(i).ToArray());
			}

			/*
			// Generate all 2-part partitions
			for (int pattern = 1; pattern < 1 << (elements.Length - 1); pattern++) {
				// Create the two result sets and
				// assign the first element to the first set
				List<T>[] resultSets = { new List<T> { elements[0] }, new List<T>() };
				// Distribute the remaining elements
				for (int index = 1; index < elements.Length; index++) {
					resultSets[(pattern >> (index - 1)) & 1].Add(elements[index]);
				}

				yield return Tuple.Create(resultSets[0].ToArray(), resultSets[1].ToArray());
			}
			*/
		}
	}

}