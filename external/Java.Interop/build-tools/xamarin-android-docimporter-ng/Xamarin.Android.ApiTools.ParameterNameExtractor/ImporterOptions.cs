using System;
using System.IO;

namespace Xamarin.Android.ApiTools
{
	public class ImporterOptions
	{
		public string InputZipArchive { get; set; }
		public string DocumentDirectory { get; set; }
		public string OutputTextFile { get; set; }
		public string OutputXmlFile { get; set; }
		public TextWriter DiagnosticWriter { get; set; }
		public bool FrameworkOnly { get; set; }
	}
}
