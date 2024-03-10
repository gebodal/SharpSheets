using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeboPdf.Utilities {

	public static class StreamUtils {

		public static void CopyTo(this Stream source, int bytesToCopy, Stream destination, int bufferSize = 81920) {
			byte[] buffer = new byte[bufferSize];

			int toRead = bytesToCopy;

			int read;
			while ((read = source.Read(buffer, 0, buffer.Length)) > 0) {
				if (toRead <= read) {
					read = toRead;
				}

				int toWrite = read;
				toRead -= read;

				destination.Write(buffer, 0, toWrite);

				if (toRead == 0) {
					break;
				}
			}

		}

		public static void Write(this Stream destination, byte[] bytes) {
			destination.Write(bytes, 0, bytes.Length);
		}

	}

}
