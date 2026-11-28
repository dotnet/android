using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Xamarin.Android.Build.Tests;

public class BaseTest
{
	static readonly char [] InvalidChars = ['{', '}', '(', ')', '$', ':', ';', '"', '\'', ',', '=', '|'];

	public TestContext TestContext { get; set; }

	public static string Root => Path.Combine (AppContext.BaseDirectory, "Microsoft.Android.Build.Tasks.Tests");

	public string TestName {
		get {
			var result = TestContext.TestDisplayName.Replace (" (", "(");
			foreach (var c in InvalidChars.Concat (Path.GetInvalidPathChars ()).Concat (Path.GetInvalidFileNameChars ())) {
				result = result.Replace (c, '_');
			}
			return result.Replace ("_", "");
		}
	}

	[TestCleanup]
	public void Cleanup ()
	{
		var testDirectory = Path.Combine (Root, "temp", TestName);
		if (Directory.Exists (testDirectory)) {
			Directory.Delete (testDirectory, recursive: true);
		}
	}
}
