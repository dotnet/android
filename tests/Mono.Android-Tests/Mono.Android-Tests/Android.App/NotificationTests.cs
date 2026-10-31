#nullable enable

using System;

using Android.Runtime;

using NUnit.Framework;

namespace Android.AppTests;

[TestFixture]
[Category ("HandwrittenJniBindings")]
public class NotificationTests
{
	[Test]
	public void VibrateRoundTripsWithoutLeakingLocalReferences ()
	{
		using var notification = new Android.App.Notification ();
		long [] pattern = [0, 100, 50, 200];
		notification.Vibrate = pattern;
		CollectionAssert.AreEqual (pattern, notification.Vibrate);
		var localReferences = Java.Interop.JniEnvironment.LocalReferenceCount;

		for (int i = 0; i < 16; ++i) {
			notification.Vibrate = pattern;
			CollectionAssert.AreEqual (pattern, notification.Vibrate);
			notification.Vibrate = [];
			Assert.IsEmpty (notification.Vibrate);
		}

		Assert.AreEqual (localReferences, Java.Interop.JniEnvironment.LocalReferenceCount);
	}

	[Test]
	public void VibrateReleasesArrayWhenSetFieldThrows ()
	{
		using var notification = new Android.App.Notification ();
		notification.Vibrate = [];
		notification.Dispose ();
		Assert.AreEqual (IntPtr.Zero, notification.Handle);
		var localReferences = Java.Interop.JniEnvironment.LocalReferenceCount;

		for (int i = 0; i < 16; ++i) {
			// A disposed receiver is rejected by the managed JNI guard, before native dispatch.
			var exception = Assert.Throws<ArgumentException> (() => notification.Vibrate = [0, 100]);
			Assert.AreEqual ("instance", exception?.ParamName);
			Assert.AreEqual (localReferences, Java.Interop.JniEnvironment.LocalReferenceCount,
				"The temporary vibration array must be released even when SetField throws.");
		}
	}
}
