using System;
using Android.App;
using Android.Views;
using NUnit.Framework;

namespace Android.ViewsTests;

[TestFixture]
public class LayoutInflaterTest
{
	[Test]
	[Category ("Intune")]
	public void From ()
	{
		AppContext.TryGetSwitch ("Microsoft.Android.Runtime.RuntimeFeature.IsAssignableFromCheck", out bool isAssignableFromCheck);
		Console.WriteLine ($"{nameof (LayoutInflaterTest)}: RuntimeFeature.IsAssignableFromCheck={isAssignableFromCheck}");

		// See: tests\Mono.Android-Tests\Mono.Android-Tests\IsAssignableFromRemaps.xml
		// Remapped to "net/dot/android/test/MyLayoutInflater"
		var from = LayoutInflater.From (Application.Context);
		Assert.IsNotNull (from);
	}
}
