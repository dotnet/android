using System;
using Android.App;
using Android.Content;
using Android.Runtime;
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

	[Test]
	[Category ("Intune")]
	public void FromSystemService ()
	{
		Console.WriteLine ($"{nameof (LayoutInflaterTest)}: RuntimeFeature.IsAssignableFromCheck={RuntimeFeature.IsAssignableFromCheck}");

		var service = Application.Context.GetSystemService (Context.LayoutInflaterService);
		Assert.IsNotNull (service);

		var inflater = Java.Lang.Object.GetObject<LayoutInflater> (service.Handle, JniHandleOwnership.DoNotTransfer);
		Assert.IsNotNull (inflater);
	}
}
