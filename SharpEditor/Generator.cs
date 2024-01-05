// Copyright (c) 2009 Daniel Grunwald
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of this
// software and associated documentation files (the "Software"), to deal in the Software
// without restriction, including without limitation the rights to use, copy, modify, merge,
// publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons
// to whom the Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all copies or
// substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
// PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
// FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
// OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using GeboPdf.Documents;
using GeboPdf.IO;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Windows.Threading;
using SharpSheets.Utilities;
using SharpSheets.Parsing;
using SharpSheets.Canvas;
using SharpSheets.PDFs;
using SharpEditor.DataManagers;

namespace SharpEditor {

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

		private readonly Dispatcher uiDispatcher;

		public bool OpenOnGenerate { get; set; }

		public Generator(Dispatcher uiDispatcher) {
			this.uiDispatcher = uiDispatcher;

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
							uiDispatcher.Invoke(delegate {
								MessageBox.Show("Something went wrong while parsing the configuration.", "Parsing Error", MessageBoxButton.OK, MessageBoxImage.Error);
							});
							continue;
						}

						if (!content.HasContent) {
							uiDispatcher.Invoke(delegate {
								MessageBox.Show("Cannot generate document. Configuration has no content.", "Generator Error", MessageBoxButton.OK, MessageBoxImage.Error);
							});
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
						uiDispatcher.Invoke(delegate {
							MessageBox.Show($"The file {filename} is unavailable.\n\nIs the file open in another program?", "File Busy", MessageBoxButton.OK, MessageBoxImage.Warning);
						});
						Console.WriteLine($"The file {filename} is unavailable. Is the file open in another program?");
					}
					catch (Exception e) {
						uiDispatcher.Invoke(delegate {
							MessageBox.Show($"Something went wrong while generating the PDF:\n{e.Message}", "Generation Error", MessageBoxButton.OK, MessageBoxImage.Error);
						});
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