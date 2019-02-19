using System;
using System.Xml.Linq;

namespace Xamarin.Android.BuildTools.PrepTasks
{
	public static class ErrorResultsHelper
	{
		public static void CreateErrorResultsFile (string destFile, string testSuiteName, string testCaseName, Exception e, string message, string contents = null)
		{
			var messageElement = new XElement ("message", message);

			if (contents != null)
				messageElement.Add (new XCData (contents));

			var failureElement = new XElement ("failure", messageElement);

			if (e != null)
				failureElement.Add (new XElement ("stack-trace", e.ToString ()));

			var doc = new XDocument (
				new XElement ("test-results",
					new XAttribute ("date", DateTime.Now.ToString ("yyyy-MM-dd")),
					new XAttribute ("errors", "1"),
					new XAttribute ("failures", "0"),
					new XAttribute ("ignored", "0"),
					new XAttribute ("inconclusive", "0"),
					new XAttribute ("invalid", "0"),
					new XAttribute ("name", destFile),
					new XAttribute ("not-run", "0"),
					new XAttribute ("skipped", "0"),
					new XAttribute ("time", DateTime.Now.ToString ("HH:mm:ss")),
					new XAttribute ("total", "1"),
					new XElement ("environment",
						new XAttribute ("nunit-version", "3.6.0.0"),
						new XAttribute ("clr-version", "4.0.30319.42000"),
						new XAttribute ("os-version", "Unix 15.6.0.0"),
						new XAttribute ("platform", "Unix"),
						new XAttribute ("cwd", Environment.CurrentDirectory),
						new XAttribute ("machine-name", Environment.MachineName),
						new XAttribute ("user", Environment.UserName),
						new XAttribute ("user-domain", Environment.MachineName)),
					new XElement ("culture-info",
						new XAttribute ("current-culture", "en-US"),
						new XAttribute ("current-uiculture", "en-US")),
					new XElement ("test-suite",
						new XAttribute ("type", "APK-File"),
						new XAttribute ("name", testSuiteName),
						new XAttribute ("executed", "True"),
						new XAttribute ("result", "Failure"),
						new XAttribute ("success", "False"),
						new XAttribute ("time", "0"),
						new XAttribute ("asserts", "0"),
						new XElement ("results",
							new XElement ("test-case",
								new XAttribute ("name", testCaseName),
								new XAttribute ("executed", "True"),
								new XAttribute ("result", "Error"),
								new XAttribute ("success", "False"),
								new XAttribute ("time", "0.0"),
								new XAttribute ("asserts", "1"),
								failureElement)))));
			doc.Save (destFile);
		}
	}
}
