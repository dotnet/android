using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using Mono.Options;
using Xamarin.Android.ApiTools.DroidDocImporter;
using Xamarin.Android.ApiTools.JavaStubImporter;

namespace Xamarin.Android.ApiTools
{
	public class Driver
	{
		public static void Main (string [] args)
		{
			var options = CreateOptions (args);
			if (options.DocumentDirectory != null)
				new DroidDocScrapingImporter ().Import (options);
			if (options.InputZipArchive != null)
				new JavaStubSourceImporter ().Import (options);
		}

		static ImporterOptions CreateOptions (string [] args)
		{
			var ret = new ImporterOptions ();
			var options = new OptionSet {
				{"droiddoc=", v => ret.DocumentDirectory = v },
				{"source-stub-zip=", v => ret.InputZipArchive = v },
				{"output-text=", v => ret.OutputTextFile = v },
				{"output-xml=", v => ret.OutputXmlFile = v },
				{"verbose", v => ret.DiagnosticWriter = Console.Error },
				{"framework-only", v => ret.FrameworkOnly = true },
				new ResponseFileSource (),
			};
			options.Parse (args);
			return ret;
		}
	}
}
