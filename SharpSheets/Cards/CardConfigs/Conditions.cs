using SharpSheets.Evaluations;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace SharpSheets.Cards.CardConfigs {

	public class Conditional<T> {
		public BoolExpression Condition { get; }
		public T Value { get; }

		public Conditional(BoolExpression condition, T value) {
			this.Condition = condition;
			this.Value = value;
		}

		/// <summary></summary>
		/// <exception cref="EvaluationCalculationException"></exception>
		/// <exception cref="EvaluationTypeException"></exception>
		public bool Evaluate(IEnvironment environment) {
			return Condition.Evaluate(environment);
		}
	}

	public class ConditionalCollection<T> : IEnumerable<Conditional<T>> {

		private readonly List<Conditional<T>> entries;

		public int Count { get { return entries.Count; } }

		public ConditionalCollection() {
			this.entries = new List<Conditional<T>>();
		}
		public ConditionalCollection(IEnumerable<Conditional<T>> entries) {
			this.entries = entries.ToList();
		}

		public bool HasDefault {
			get {
				return entries.Any(e => e.Condition.IsTrue);
			}
		}

		/// <summary></summary>
		/// <exception cref="EvaluationCalculationException"></exception>
		/// <exception cref="EvaluationTypeException"></exception>
		public T? GetValue(IEnvironment environment) {
			Conditional<T>? conditional = entries.FirstOrDefault(e => e.Evaluate(environment));
			if (conditional != null) {
				return conditional.Value;
			}
			else {
				return default;
			}
		}

		/// <summary></summary>
		/// <exception cref="EvaluationCalculationException"></exception>
		/// <exception cref="EvaluationTypeException"></exception>
		public T? GetValue(IEnvironment environment, Func<T, bool> predicate) {
			Conditional<T>? conditional = entries.Where(c => predicate(c.Value)).FirstOrDefault(e => e.Evaluate(environment));
			if (conditional != null) {
				return conditional.Value;
			}
			else {
				return default;
			}
		}

		public void Add(Conditional<T> entry) {
			entries.Add(entry);
		}

		public void Add(BoolExpression condition, T value) {
			entries.Add(new Conditional<T>(condition, value));
		}

		public IEnumerator<Conditional<T>> GetEnumerator() {
			foreach (Conditional<T> entry in entries) { yield return entry; }
		}

		IEnumerator IEnumerable.GetEnumerator() {
			return GetEnumerator();
		}
	}
}