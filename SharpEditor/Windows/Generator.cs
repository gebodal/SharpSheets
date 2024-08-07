using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using GeboPdf.Documents;
using GeboPdf.IO;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using SharpSheets.Utilities;
using SharpSheets.Parsing;
using SharpSheets.Canvas;
using SharpSheets.PDFs;
using SharpEditor.DataManagers;
using Avalonia.Threading;

namespace SharpEditor.Windows {

	public class GeneratorErrorEventArgs : EventArgs {

		public string Message { get; }
		public string Title { get; }
		public bool IsError { get; }

		public GeneratorErrorEventArgs(string message, string title, bool isError) {
			Message = message;
			Title = title;
			IsError = isError;
		}

	}

	public class Generator {

		private class GeneratorData {
			public readonly IParser parser;
			public readonly string filename;
			public readonly string configuration;
			public readonly string source;
			public readonly string? fieldsSourcePath;

			public GeneratorData(IParser parser, string filename, string configuration, string source, string? fieldsSourcePath) {
				this.parser = parser;
				this.filename = filename;
				this.configuration = configuration;
				this.source = source;
				this.fieldsSourcePath = fieldsSourcePath;
			}
		}

		private readonly Task task;
		private readonly BlockingCollection<GeneratorData> queue;
		private bool running;

		public event EventHandler<GeneratorErrorEventArgs>? GeneratorError;

		public bool OpenOnGenerate { get; set; }

		public Generator() {
			queue = new BlockingCollection<GeneratorData>();
			running = false;

			task = new Task(Run);
		}

		private readonly object taskLock = new object();

		public void Start() {
			Console.WriteLine("Start generator.");
			lock (taskLock) {
				if (!running) {
					running = true;
					task.Start();
				}
			}
			Console.WriteLine("Finished starting generator.");
		}
		public void Stop() {
			Console.WriteLine("Stop generator.");
			lock (taskLock) {
				running = false;
				task.Wait();
			}
			Console.WriteLine("Finished stopping generator.");
		}

		public void Enqueue(IParser parser, string filename, string configuration, string source, string? fieldsSourcePath) {
			Console.WriteLine($"Enqueue {filename}");
			queue.Add(new GeneratorData(parser, filename, configuration, source, fieldsSourcePath));
		}

		private void Run() {
			//Console.WriteLine("Start generator Run.");
			while (running) {
				//Console.WriteLine("Still running.");
				while (queue.TryTake(out GeneratorData? toGenerate, 100)) {
					//Console.WriteLine("Found one in queue.");

					string filename = toGenerate.filename;
					string configuration = toGenerate.configuration;
					string? fieldsSourcePath = toGenerate.fieldsSourcePath;
					IParser parser = toGenerate.parser;
					string source = toGenerate.source;

					if (OpenOnGenerate) { // OpenOnGenerate
						string stub = System.IO.Path.Combine(SharpEditorPathInfo.GetDirectoryPathOrFallback(filename), System.IO.Path.GetFileNameWithoutExtension(filename));
						string extension = System.IO.Path.GetExtension(filename);
						if (extension.StartsWith(".")) { extension = extension.Substring(1); }
						int i = 0;
						while (!IsFileAvailable(filename)) {
							i++;
							filename = string.Format("{0}{1}.{2}", stub, i, extension);
							//Console.WriteLine(filename);
						}
					}

					FileStream? stream = null;

					try {
						FilePath filePath = new FilePath(Path.GetFullPath(filename));
						DirectoryPath sourcePath = new DirectoryPath(Path.GetFullPath(source));
						//string sourceName = Path.GetFileNameWithoutExtension(source);
						IDocumentContent? content = parser.Parse(filePath, sourcePath, configuration) as IDocumentContent;

						if (content == null) {
							GeneratorError?.Invoke(this, new GeneratorErrorEventArgs("Something went wrong while parsing the configuration.", "Parsing Error", true));
							continue;
						}

						if (!content.HasContent) {
							GeneratorError?.Invoke(this, new GeneratorErrorEventArgs("Cannot generate document. Configuration has no content.", "Generator Error", true));
							continue;
						}

						Dictionary<string, GeboPdf.Objects.PdfObject>? fieldValues = null;
						if (!string.IsNullOrWhiteSpace(fieldsSourcePath)) {
							fieldValues = FieldTools.ExtractFields(fieldsSourcePath);
						}

						PdfDocument pdf = new PdfDocument();

						ISharpDocument document = new SharpGeboDocument(pdf, fieldValues); // TODO Should accept fieldValues here
						content.DrawTo(document, out _, default); // TODO Should accept CancellationToken here

						/*
						if (fieldValues != null) {
							FieldTools.PopulateFields(pdf, fieldValues);
						}
						*/

						stream = new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.None, 4096);
						PdfStreamWriter pdfStream = new PdfStreamWriter(stream);
						PdfDocumentWriter writer = new PdfDocumentWriter(pdfStream) { CompressStreams = true };

						writer.WriteDocument(pdf);

						stream.Close();
						stream = null;

						//Console.WriteLine($"OpenOnGenerate = {OpenOnGenerate}");
						if (OpenOnGenerate) { // OpenOnGenerate
							//Console.WriteLine($"Open {filename}");
							Process.Start(new ProcessStartInfo(filename) { UseShellExecute = true });
						}
					}
					catch (IOException) {
						GeneratorError?.Invoke(this, new GeneratorErrorEventArgs($"The file {filename} is unavailable.\n\nIs the file open in another program?", "File Busy", false));
						Console.WriteLine($"The file {filename} is unavailable. Is the file open in another program?");
					}
					catch (Exception e) {
						GeneratorError?.Invoke(this, new GeneratorErrorEventArgs($"Something went wrong while generating the PDF:\n{e.Message}", "Generation Error", true));
						Console.WriteLine($"Something went wrong while generating the PDF: {e.Message}");
					}
					finally {
						// Are these try/catch blocks OK?
						//Console.WriteLine("Finally in generator");
						try {
							if (stream != null) { stream.Close(); }
						}
						catch (Exception) { }
					}
				}
			}
		}

		private static bool IsFileAvailable(string filename) {
			if (File.Exists(filename)) {
				// If the file can be opened for exclusive access it means that the file
				// is no longer locked by another process.
				try {
					using (FileStream inputStream = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.None)) {
						return true;
					}
				}
				catch (Exception) {
					return false;
				}
			}
			else {
				return true;
			}
		}
	}

}