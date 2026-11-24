using System;
using Android.App;
using Android.Views;
using Microsoft.Android.Runtime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Android.ViewsTests;

[TestClass]
public class LayoutInflaterTest
{
	[TestMethod]
	[TestCategory ("Intune")]
	public void From ()
	{
		Console.WriteLine ($"{nameof (LayoutInflaterTest)}: RuntimeFeature.IsAssignableFromCheck={RuntimeFeature.IsAssignableFromCheck}");

		// See: tests\Mono.Android-Tests\Mono.Android-Tests\IsAssignableFromRemaps.xml
		// Remapped to "net/dot/android/test/MyLayoutInflater"
		var from = LayoutInflater.From (Application.Context);
		Assert.IsNotNull (from);
	}
}
