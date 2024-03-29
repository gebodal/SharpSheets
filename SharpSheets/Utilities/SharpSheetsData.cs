﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpSheets.Utilities {

	public static class SharpSheetsData {

		public static string GetCreatorName() {
			//return typeof(SharpSheetsData).Assembly.GetName().Name;
			return "SharpSheets";
		}

		public static string GetCreatorString() {
			Version sharpSheetsVersion = typeof(SharpSheetsData).Assembly.GetName().Version ?? new Version(0, 0, 0, 0);
			return $"{GetCreatorName()} {sharpSheetsVersion.Major}.{sharpSheetsVersion.Minor}.{sharpSheetsVersion.Build}.{sharpSheetsVersion.Revision}";
		}

		public static string GetAssemblyDataDir() {
			string appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
			string sharpSheetsDataFolder = Path.Combine(appDataFolder, "SharpSheets"); // Is it OK that this doesn't use version numbers?
			return sharpSheetsDataFolder;
		}

	}

}
