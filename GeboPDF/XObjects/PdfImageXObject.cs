using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Gif;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GeboPdf.Objects;
using GeboPdf.Graphics;

namespace GeboPdf.XObjects {

	public enum DCTDecodeColorTransform : int {
		NoTransformation = 0,
		LuminanceTransformation = 1
	}

	public static class ImageHelpers {

		public static (int width, int height) GetDimensions(string path) {
			ImageInfo imageInfo = Image.Identify(path);

			if(imageInfo is null) {
				throw new NotSupportedException($"No support for images of type \"{Path.GetExtension(path)}\".");
			}

			return (imageInfo.Width, imageInfo.Height);
		}

	}

	public class PdfImageXObject : PdfXObject {

		public int Width => imageInfo.Width;
		public int Height => imageInfo.Height;

		private readonly string path;
		private readonly ImageInfo imageInfo;

		private readonly bool interpolate;
		private readonly int quality;

		//private readonly bool hasTransparency;

		private readonly PdfSMaskXObject? sMask;

		public PdfImageXObject(string path, int quality, bool interpolate) : base() {
			this.path = path;
			imageInfo = Image.Identify(this.path);

			this.interpolate = interpolate;
			this.quality = Math.Max(0, Math.Min(100, quality));

			PngMetadata pngMetadata = imageInfo.Metadata.GetPngMetadata();
			bool hasTransparency = pngMetadata.HasTransparency || pngMetadata.ColorType == PngColorType.GrayscaleWithAlpha || pngMetadata.ColorType == PngColorType.RgbWithAlpha;
			if (hasTransparency) {
				sMask = new PdfSMaskXObject(path, imageInfo.Width, imageInfo.Height, interpolate);
			}
			else {
				sMask = null;
			}
		}

		public override bool AllowEncoding { get; } = false;

		public override MemoryStream GetStream() {
			MemoryStream imageStream = new MemoryStream();

			//Console.WriteLine("Start writing image");
			//Console.Out.Flush();

			using (Image image = Image.Load(path)) {
				image.Save(imageStream, new JpegEncoder() {
					ColorType = JpegEncodingColor.YCbCrRatio420, // JpegColorType.YCbCrRatio420,
					Quality = quality, Interleaved = true
					});
			}

			//Console.WriteLine("Finish writing image");
			//Console.Out.Flush();

			//var png = imageInfo.Metadata.GetPngMetadata();

			return imageStream;
		}

		public override int Count {
			get {
				int count = 9;
				if (sMask is not null) {
					count += 1;
				}
				return count;
			}
		}

		public override IEnumerator<KeyValuePair<PdfName, PdfObject>> GetEnumerator() {
			yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.Type, PdfNames.XObject);
			yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.Subtype, PdfNames.Image);
			yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.Width, new PdfInt(imageInfo.Width));
			yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.Height, new PdfInt(imageInfo.Height));
			yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.ColorSpace, PdfColorSpace.DeviceRGB);
			yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.BitsPerComponent, new PdfInt(8));
			//{ PdfNames.Intent, intent }, // Rendering intent name?
			//{ PdfNames.ImageMask, new PdfBoolean(imageMaskFlag) }, // Image mask flag?
			//{ PdfNames.Mask, ?? }, // Image mask stream/array?
			//{ PdfNames.Decode, new PdfArray(decide) }, // Decode array?
			yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.Interpolate, new PdfBoolean(interpolate));
			//{ PdfNames.SMask, ?? }, // Soft mask stream?
			//{ PdfNames.SMaskInData, new PdfInt(sMaskInData) }, // Soft mask?
			// Metadata?
			//{ PdfNames.OC, ?? }, // Optional content group?
			yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.Filter, new PdfName("DCTDecode"));
			yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.DecodeParms, new PdfDictionary() {
				{ new PdfName("ColorTransform"), new PdfInt((int)DCTDecodeColorTransform.LuminanceTransformation) }
			});


			if (sMask is not null) {
				yield return new KeyValuePair<PdfName, PdfObject>(new PdfName("SMask"), PdfIndirectReference.Create(sMask));
			}
		}

		public override IEnumerable<PdfObject> CollectObjects() {
			yield return this;
			if (sMask is not null) {
				yield return sMask;
			}
		}

		public override int GetHashCode() {
			unchecked {
				int hash = 17;
				hash = hash * 23 + path.GetHashCode();
				//hash = hash * 23 + imageInfo.GetHashCode();
				hash = hash * 23 + interpolate.GetHashCode();
				return hash;
			}
		}

		public override bool Equals(object? obj) {
			if(ReferenceEquals(this, obj)) {
				return true;
			}
			else if (obj is PdfImageXObject other) {
				if(!string.Equals(path, other.path, StringComparison.Ordinal)) {
					return false;
				}

				if(interpolate != other.interpolate) {
					return false;
				}

				/*
				AbstractPdfDictionary myDict = GetDictionary();
				AbstractPdfDictionary otherDict = GetDictionary();
				if (myDict.Count != otherDict.Count) {
					return false;
				}
				*/

				return true;
			}
			return false;
		}

	}

	public class PdfSMaskXObject : PdfXObject {

		private readonly int width;
		private readonly int height;

		private readonly string path;

		private readonly bool interpolate;

		public PdfSMaskXObject(string path, int width, int height, bool interpolate) : base() {
			this.path = path;
			this.width = width;
			this.height = height;
			this.interpolate = interpolate;
		}

		public override bool AllowEncoding { get; } = true;

		public override MemoryStream GetStream() {
			MemoryStream imageStream = new MemoryStream();

			//Console.WriteLine("Start writing SMask");
			//Console.Out.Flush();

			using (Image<Rgba32> image = (Image<Rgba32>)Image<Rgba32>.Load(path)) {
				image.ProcessPixelRows(accessor => {
					for (int y = 0; y < accessor.Height; y++) {
						Span<Rgba32> row = accessor.GetRowSpan(y);

						// Using row.Length helps JIT to eliminate bounds checks when accessing row[x].
						for (int x = 0; x < row.Length; x++) {
							Rgba32 pixel = row[x];
							imageStream.WriteByte(pixel.A);
						}
					}
				});
				/*
				for (int y = 0; y < image.Height; y++) {
					// It's faster to get the row and avoid a per-pixel multiplication using the image[x, y] indexer (Needs higher version of ImageSharp)
					//Span<Rgba32> row = image.GetPixelRowSpan(y);
					//image.ProcessPixelRows(p => p);
					for (int x = 0; x < image.Width; x++) {
						Rgba32 pixel = image[x, y];

						imageStream.WriteByte(pixel.A);
					}
				}
				*/
			}

			//Console.WriteLine("Finish writing SMask");
			//Console.Out.Flush();

			return imageStream;
		}

		public override int Count => 7;

		public override IEnumerator<KeyValuePair<PdfName, PdfObject>> GetEnumerator() {
			yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.Type, PdfNames.XObject);
			yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.Subtype, PdfNames.Image);
			yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.Width, new PdfInt(width));
			yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.Height, new PdfInt(height));
			yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.ColorSpace, PdfColorSpace.DeviceGray);
			yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.BitsPerComponent, new PdfInt(8));
			//{ PdfNames.Intent, intent }, // Rendering intent name?
			//{ PdfNames.ImageMask, new PdfBoolean(imageMaskFlag) }, // Image mask flag?
			//{ PdfNames.Mask, ?? }, // Image mask stream/array?
			//{ PdfNames.Decode, new PdfArray(decide) }, // Decode array?
			yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.Interpolate, new PdfBoolean(interpolate));
			// Metadata?
		}

		public override IEnumerable<PdfObject> CollectObjects() {
			yield return this;
		}

		public override int GetHashCode() {
			return HashCode.Combine(path, width, height, interpolate);
		}

		public override bool Equals(object? obj) {
			if (ReferenceEquals(this, obj)) {
				return true;
			}
			else if (obj is PdfSMaskXObject other) {
				if (!string.Equals(path, other.path, StringComparison.Ordinal)) {
					return false;
				}

				if (interpolate != other.interpolate) {
					return false;
				}

				/*
				AbstractPdfDictionary myDict = GetDictionary();
				AbstractPdfDictionary otherDict = GetDictionary();
				if (myDict.Count != otherDict.Count) {
					return false;
				}
				*/

				return true;
			}
			return false;
		}

	}

}
