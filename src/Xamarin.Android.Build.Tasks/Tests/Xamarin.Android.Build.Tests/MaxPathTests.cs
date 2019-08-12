using System.IO;
using NUnit.Framework;
using Xamarin.Android.Tools;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	/// <summary>
	/// A set of tests checking MAX_PATH
	/// </summary>
	[TestFixture]
	[Parallelizable (ParallelScope.Children)]
	public class MaxPathTests : BaseTest
	{
		[SetUp]
		public void Setup ()
		{
			if (!IsWindows) {
				Assert.Ignore ("MAX_PATH only applies on Windows");
			}
		}

		static object [] EdgeCases => new object [] {
			new object[] {
				/* limit */         145,
				/* xamarinForms */  true,
			},
			new object[] {
				/* limit */         77,
				/* xamarinForms */  false,
			},
		};

		/// <summary>
		/// Full path length of .\bin\TestDebug\temp\
		/// </summary>
		int BaseLength => Path.Combine (Root, "temp").Length + 1;

		[Test]
		[TestCaseSource (nameof (EdgeCases))]
		public void Edge (int limit, bool xamarinForms)
		{
			var testName = $"{nameof (Edge)}{xamarinForms}"
				.PadRight (Files.MaxPath - BaseLength - limit, 'N');
			var proj = xamarinForms ?
				new XamarinFormsAndroidApplicationProject () :
				new XamarinAndroidApplicationProject ();
			using (var b = CreateApkBuilder (Path.Combine ("temp", testName))) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				Assert.IsTrue (b.Clean (proj), "Clean should have succeeded.");
			}
		}

		static object [] OverTheEdgeCases => new object [] {
			new object[] {
				/* limit */         144,
				/* xamarinForms */  true,
			},
			new object[] {
				/* limit */         76,
				/* xamarinForms */  false,
			},
		};

		[Test]
		[TestCaseSource (nameof (OverTheEdgeCases))]
		public void OverTheEdge (int limit, bool xamarinForms)
		{
			var testName = $"{nameof (Edge)}{xamarinForms}"
				.PadRight (Files.MaxPath - BaseLength - limit, 'N');
			var proj = xamarinForms ?
				new XamarinFormsAndroidApplicationProject () :
				new XamarinAndroidApplicationProject ();
			using (var b = CreateApkBuilder (Path.Combine ("temp", testName))) {
				b.ThrowOnBuildFailure = false;
				Assert.IsFalse (b.Build (proj), "Build should have failed.");
				Assert.IsTrue (StringAssertEx.ContainsText (b.LastBuildOutput, "XA5301"), "Should get MAX_PATH warning");
				b.ThrowOnBuildFailure = true;
				Assert.IsTrue (b.Clean (proj), "Clean should have succeeded.");
			}
		}
	}
}
