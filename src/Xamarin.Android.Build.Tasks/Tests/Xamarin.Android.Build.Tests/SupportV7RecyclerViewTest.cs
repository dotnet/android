using System;
using Microsoft.Build.Framework;
using NUnit.Framework;
using Xamarin.ProjectTools;
using System.IO;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	[Parallelizable (ParallelScope.Fixtures)]
	public class SupportV7RecyclerViewTest : BaseTest
	{
		[Test]
		[Category ("Minor")]
		public void Build ()
		{
			var app = new XamarinAndroidApplicationProject ();
			app.Packages.Add (KnownPackages.AndroidSupportV4Beta);
			app.Packages.Add (KnownPackages.SupportV7RecyclerView);
			using (var b = CreateDllBuilder (Path.Combine ("temp", GetType ().Name))) {
				b.Verbosity = LoggerVerbosity.Diagnostic;
				Assert.IsTrue (b.Build (app), "Build should have succeeded.");
			}
		}
	}
}

