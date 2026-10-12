using System;
using System.Collections.Generic;
using System.Runtime.Versioning;

using Android.Runtime;
using Java.Util;
using Java.Util.Functions;
using NUnit.Framework;

namespace Java.UtilTests;

[TestFixture]
[Category ("JniSafetyPreparation")]
[SupportedOSPlatform ("android24.0")]
public class SpliteratorTests
{
	[Test]
	public void AbstractInvokerTryAdvanceUsesInstanceBooleanAbi ()
	{
		if (!OperatingSystem.IsAndroidVersionAtLeast (24))
			Assert.Ignore ("Spliterators requires Android API 24.");

		IntPtr klass = JNIEnv.FindClass ("net/dot/android/test/JniSpliteratorFixture");
		Spliterators.AbstractSpliterator iterator;
		try {
			IntPtr constructor = JNIEnv.GetMethodID (klass, "<init>", "()V");
			iterator = Java.Lang.Object.GetObject<Spliterators.AbstractSpliterator> (
				JNIEnv.NewObject (klass, constructor), JniHandleOwnership.TransferLocalRef);
		} finally {
			JNIEnv.DeleteGlobalRef (klass);
		}
		if (iterator is null)
			throw new InvalidOperationException ("Unable to marshal the Java spliterator.");

		using (iterator)
		using (var consumer = new RecordingConsumer ()) {
			Assert.IsTrue (iterator.TryAdvance (consumer));
			Assert.IsTrue (iterator.TryAdvance (consumer));
			Assert.IsFalse (iterator.TryAdvance (consumer));
			CollectionAssert.AreEqual (new [] { "first", "second" }, consumer.Values);
		}
	}

	[Register ("net/dot/android/test/SpliteratorRecordingConsumer")]
	sealed class RecordingConsumer : Java.Lang.Object, IConsumer
	{
		public List<string> Values { get; } = new ();

		public void Accept (Java.Lang.Object value)
		{
			Values.Add (value?.ToString () ?? throw new InvalidOperationException ("The fixture must supply a string."));
		}
	}
}
