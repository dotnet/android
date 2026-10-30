#nullable enable

using System;
using System.Runtime.Versioning;

using Android.OS;
using Android.Runtime;

using NUnit.Framework;

namespace Java.UtilTests;

[TestFixture]
[Category ("HandwrittenJniBindings")]
public class SpliteratorTests
{
	[Test]
	[SupportedOSPlatform ("android24.0")]
	public void AbstractSpliteratorTryAdvance ()
	{
		if (Build.VERSION.SdkInt < BuildVersionCodes.N) {
			Assert.Ignore ("Spliterators requires Android 24 or later.");
			return;
		}

		var klass = JNIEnv.FindClass ("net/dot/android/test/HandwrittenSpliterator");
		try {
			var constructor = JNIEnv.GetMethodID (klass, "<init>", "()V");
			var handle = JNIEnv.NewObject (klass, constructor);
			using var spliterator = Java.Lang.Object.GetObject<Java.Util.Spliterators.AbstractSpliterator> (
				handle, JniHandleOwnership.TransferLocalRef) ?? throw new InvalidOperationException ("Could not wrap the Java spliterator.");
			using var consumer = new TestConsumer ();

			Assert.IsTrue (spliterator.TryAdvance (consumer));
			Assert.AreEqual ("item", consumer.Value);
			for (int i = 0; i < 2048; ++i) {
				Assert.IsFalse (spliterator.TryAdvance (consumer));
			}
			Assert.AreEqual (1, consumer.Count);
			Assert.Throws<Java.Lang.NullPointerException> (() => spliterator.TryAdvance (null));
		} finally {
			JNIEnv.DeleteGlobalRef (klass);
		}
	}

	[SupportedOSPlatform ("android24.0")]
	sealed class TestConsumer : Java.Lang.Object, Java.Util.Functions.IConsumer
	{
		public string? Value { get; private set; }
		public int Count { get; private set; }

		public void Accept (Java.Lang.Object? value)
		{
			Value = value?.ToString ();
			++Count;
		}
	}
}
