using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Xamarin.Android.Build.Tests;

public class BaseTest
{
	static readonly char [] InvalidChars = ['{', '}', '(', ')', '$', ':', ';', '"', '\'', ',', '=', '|'];

	public string Root => Path.Combine (TestContext.CurrentContext.WorkDirectory, "Microsoft.Android.Build.Tasks.Tests");

	public string TestName {
		get {
			var result = TestContext.CurrentContext.Test.Name;
			foreach (var c in InvalidChars.Concat (Path.GetInvalidPathChars ()).Concat (Path.GetInvalidFileNameChars ())) {
				result = result.Replace (c, '_');
			}
			return result.Replace ("_", "");
		}
	}

	[TearDown]
	public void Cleanup ()
	{
		var testDirectory = Path.Combine (Root, "temp", TestName);
		if (Directory.Exists (testDirectory)) {
			Directory.Delete (testDirectory, recursive: true);
		}
	}
}
