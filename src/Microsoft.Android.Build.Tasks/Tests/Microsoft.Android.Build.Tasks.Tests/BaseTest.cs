using System.IO;
using NUnit.Framework;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests;

public class BaseTest
{
	public string Root => Path.Combine (TestContext.CurrentContext.WorkDirectory, "Microsoft.Android.Build.Tasks.Tests");

	public string TestName => TestNameUtilities.GetDirectoryName (TestContext.CurrentContext.Test.Name);

	[TearDown]
	public void Cleanup ()
	{
		var testDirectory = Path.Combine (Root, "temp", TestName);
		if (Directory.Exists (testDirectory)) {
			Directory.Delete (testDirectory, recursive: true);
		}
	}
}
