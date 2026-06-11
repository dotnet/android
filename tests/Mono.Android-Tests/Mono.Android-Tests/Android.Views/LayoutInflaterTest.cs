using System;
using Android.App;
using Android.Views;
using Microsoft.Android.Runtime;
using NUnit.Framework;

namespace Android.ViewsTests;

[TestFixture]
public class LayoutInflaterTest
{
	[Test]
	[Category ("Intune")]
	public void From ()
	{
		Console.WriteLine ($"{nameof (LayoutInflaterTest)}: RuntimeFeature.IsAssignableFromCheck={RuntimeFeature.IsAssignableFromCheck}");

		// See: tests\Mono.Android-Tests\Mono.Android-Tests\IsAssignableFromRemaps.xml
		// Remapped to "net/dot/android/test/MyLayoutInflater"
		var from = LayoutInflater.From (Application.Context);
		Assert.IsNotNull (from);
	}
}
