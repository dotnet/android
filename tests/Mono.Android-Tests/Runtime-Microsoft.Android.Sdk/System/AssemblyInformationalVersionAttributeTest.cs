using System;
using System.Threading;

using NUnit.Framework;

namespace Xamarin.Android.RuntimeTests {

	[TestFixture]
	public class AssemblyInformationalVersionAttributeTest {

		[Test]
		public void VersionInformationExists ()
		{
			var attrs = (System.Reflection.AssemblyInformationalVersionAttribute[])
			    typeof (Java.Lang.Object)
			    .Assembly
			    .GetCustomAttributes (typeof (System.Reflection.AssemblyInformationalVersionAttribute), inherit: true);
			Assert.IsTrue (attrs != null,     "No AssemblyInformationalVersionAttribute values found: `null`!");
			Assert.IsTrue (attrs.Length > 0,  "No AssemblyInformationalVersionAttribute values found: 0 length!");
			string version = attrs [0].InformationalVersion;
			Assert.IsFalse (string.IsNullOrWhiteSpace (version),  "No InformationalVersion provided!");
			Assert.IsTrue (version.Contains ("; git-rev-head:"),  "No commit info!");
			Assert.IsTrue (version.Contains ("; git-branch:"),    "No branch info!");
		}
	}
}
