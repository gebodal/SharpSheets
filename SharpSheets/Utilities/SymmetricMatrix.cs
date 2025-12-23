namespace SharpSheets.Utilities {

	public class SymmetricMatrix<T> {

		public int Size { get; }

		private readonly T[] content;

		public T this[int i, int j] {
			get {
				return content[GetIndex(i, j)];
			}
			set {
				content[GetIndex(i, j)] = value;
			}
		}

		public SymmetricMatrix(int size) {
			Size = size;

			content = new T[Size * (Size + 1) / 2];
		}

		private int GetIndex(int i, int j) {
			if (i <= j) {
				return i * Size - (i - 1) * i / 2 + j - i;
			}
			else {
				return j * Size - (j - 1) * j / 2 + i - j;
			}
		}

		public SymmetricMatrix<T> Copy() {
			SymmetricMatrix<T> result = new SymmetricMatrix<T>(Size);
			for (int i = 0; i < content.Length; i++) {
				result.content[i] = content[i];
			}
			return result;
		}

	}

}
