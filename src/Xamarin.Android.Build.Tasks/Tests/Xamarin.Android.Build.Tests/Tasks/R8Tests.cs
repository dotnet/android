using System;
using System.IO;
using NUnit.Framework;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	public class R8Tests
	{
		[Test]
		public void WritePrivateMemberObfuscationRules ()
		{
			using var writer = new StringWriter ();
			R8.WriteObfuscationRules (writer, "private-members");

			var expected = """
				-keep,allowshrinking,allowoptimization class **
				-keepclassmembers,allowshrinking,allowoptimization class ** {
				   public protected *;
				}
				-keep,allowoptimization interface ** {
				   public protected *;
				}
				-keep,allowshrinking class * implements **

				""";
			Assert.AreEqual (expected.ReplaceLineEndings (Environment.NewLine), writer.ToString ());
		}

		[Test]
		public void WriteDisabledObfuscationRules ()
		{
			using var writer = new StringWriter ();
			R8.WriteObfuscationRules (writer, "disabled");

			Assert.AreEqual ("-dontobfuscate" + Environment.NewLine, writer.ToString ());
		}

		[Test]
		public void WriteInvalidObfuscationModeThrows ()
		{
			using var writer = new StringWriter ();

			Assert.Throws<ArgumentException> (() => R8.WriteObfuscationRules (writer, "private-member"));
		}
	}
}
