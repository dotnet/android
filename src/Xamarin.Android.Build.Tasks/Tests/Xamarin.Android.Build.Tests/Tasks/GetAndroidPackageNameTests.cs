using System.Collections.Generic;
using Microsoft.Build.Framework;
using NUnit.Framework;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests {

	[TestFixture]
	[Parallelizable (ParallelScope.Children)]
	public class GetAndroidPackageNameTests {

		[TestCase ("com.example.for", "for")]
		[TestCase ("com.class.example", "class")]
		[TestCase ("com.example.true", "true")]
		public void ReservedJavaIdentifier_FailsWithXA4258 (string packageName, string invalidIdentifier)
		{
			var errors = new List<BuildErrorEventArgs> ();
			var task = new GetAndroidPackageName {
				BuildEngine = new MockBuildEngine (TestContext.Out, errors),
				AssemblyName = "Example",
				PackageName = packageName,
			};

			Assert.IsFalse (task.Execute ());
			Assert.That (errors, Has.Exactly (1).Matches<BuildErrorEventArgs> (error =>
				error.Code == "XA4258" &&
				error.Message.Contains (packageName) &&
				error.Message.Contains (invalidIdentifier)));
		}

		[Test]
		public void ContextualKeywordInPackage_Succeeds ()
		{
			var errors = new List<BuildErrorEventArgs> ();
			var task = new GetAndroidPackageName {
				BuildEngine = new MockBuildEngine (TestContext.Out, errors),
				AssemblyName = "Example",
				PackageName = "com.example.record",
			};

			Assert.IsTrue (task.Execute ());
			Assert.IsEmpty (errors);
		}
	}
}
