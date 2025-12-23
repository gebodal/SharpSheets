using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeboPDF.Fonts.TrueType {

	public static class TrueTypeUtils {

		public static uint CheckSum(Stream stream, uint length) {
			BinaryReader reader = new BinaryReader(stream);
			uint sum = 0U;
			uint nlongs = (length + 3U) / 4U;
			while (nlongs-- > 0U) {
				sum += reader.ReadUInt32();
			}
			return sum;
		}

	}

}
