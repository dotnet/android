#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;
using NUnit.Framework;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	public class GetAndroidDefineConstantsTests
	{
		[TestCase ("36", null, 36, "")]
		[TestCase ("36.1", null, 36, "__ANDROID_36_1__")]
		[TestCase ("38.2", "11.0-rc+test", 38, "__ANDROID_36_1__;__ANDROID_37_1__;__ANDROID_37_2__;__ANDROID_37_3__;__ANDROID_37_4__;__ANDROID_38_1__;__ANDROID_38_2__")]
		public void Constants (string apiLevel, string? productVersion, int major, string minorConstants)
		{
			var task = new GetAndroidDefineConstants {
				AndroidApiLevel = apiLevel,
				ProductVersion = productVersion,
			};
			Assert.IsTrue (task.Execute ());
			var constants = task.AndroidDefineConstants ?? throw new InvalidOperationException ("No constants returned.");
			var expected = new List<string> ();
			if (productVersion != null)
				expected.Add ("__XAMARIN_ANDROID_11_0_rc_test__");
			expected.AddRange (["__MOBILE__", "__ANDROID__"]);
			expected.AddRange (Enumerable.Range (1, major).Select (i => $"__ANDROID_{i}__"));
			expected.AddRange (minorConstants.Split (';', StringSplitOptions.RemoveEmptyEntries));
			CollectionAssert.AreEqual (expected, constants.Select (i => i.ItemSpec));
		}

		[TestCase ("")]
		[TestCase ("invalid")]
		public void InvalidApiLevel (string apiLevel)
		{
			var errors = new List<BuildErrorEventArgs> ();
			var task = new GetAndroidDefineConstants {
				AndroidApiLevel = apiLevel,
				BuildEngine = new MockBuildEngine (TestContext.Out, errors: errors),
			};
			Assert.IsFalse (task.Execute ());
			Assert.IsNull (task.AndroidDefineConstants);
			Assert.IsEmpty (errors);
		}
	}
}
