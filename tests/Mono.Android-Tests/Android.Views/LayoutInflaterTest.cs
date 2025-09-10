using System;
using Android.App;
using Android.Views;
using Java.Interop;

using NUnit.Framework;

namespace Android.ViewsTests;

[TestFixture]
public class LayoutInflaterTest
{
	[Test]
	[Category ("Intune")]
	public void From ()
	{
		Console.WriteLine ($"{nameof (LayoutInflaterTest)}: TypeManager.IsAssignableFromCheck={TypeManager.IsAssignableFromCheck}");

		// See: tests\Mono.Android-Tests\Runtime-Microsoft.Android.Sdk\IsAssignableFromRemaps.xml
		// Remapped to "net/dot/android/test/MyLayoutInflater"
		var from = LayoutInflater.From (Application.Context);
		Assert.IsNotNull (from);
	}
}
