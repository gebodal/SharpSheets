using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpSheets.Parsing {

	public interface IFileReader {

		/// <summary></summary>
		/// <param name="path"></param>
		/// <returns></returns>
		/// <exception cref="FileNotFoundException"></exception>
		string ReadAllText(string path);

		/// <summary></summary>
		/// <param name="path"></param>
		/// <returns></returns>
		/// <exception cref="FileNotFoundException"></exception>
		string[] ReadAllLines(string path);

		/// <summary></summary>
		/// <param name="path"></param>
		/// <returns></returns>
		/// <exception cref="FileNotFoundException"></exception>
		StreamReader OpenFile(string path);

	}

	public static class FileReaders {

		public static IFileReader BaseReader() {
			return new BaseFileReader();
		}

		public static IFileReader BaseReader(Encoding encoding) {
			return new BaseFileReader(encoding);
		}

		private class BaseFileReader : IFileReader {

			private readonly Encoding? encoding;

			public BaseFileReader(Encoding encoding) {
				this.encoding = encoding;
			}

			public BaseFileReader() {
				this.encoding = null;
			}

			public string ReadAllText(string path) {
				if (encoding != null) {
					return File.ReadAllText(path, encoding);
				}
				else {
					return File.ReadAllText(path);
				}
			}

			public string[] ReadAllLines(string path) {
				if (encoding != null) {
					return File.ReadAllLines(path, encoding);
				}
				else {
					return File.ReadAllLines(path);
				}
			}

			public StreamReader OpenFile(string path) {
				if (encoding != null) {
					return new StreamReader(path, encoding);
				}
				else {
					return new StreamReader(path, true);
				}
			}

		}

	}

}
