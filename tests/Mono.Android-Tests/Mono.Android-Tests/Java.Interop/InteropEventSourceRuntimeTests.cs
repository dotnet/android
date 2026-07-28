using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;

using Android.Runtime;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests
{
	[TestFixture]
	public class InteropEventSourceRuntimeTests
	{
		[Test]
		public void ManagedConstructionAndDispose_EmitLifecycleEvents ()
		{
			using (var listener = new CapturingEventListener ()) {
				using (var instance = new Java.Lang.Object ()) {
				}

				Assert.IsTrue (listener.EventNames.Contains ("JavaWrapperCreated"), "Expected JavaWrapperCreated event.");
				Assert.IsTrue (listener.EventNames.Contains ("DotNetWrapperReleasedJavaReference"), "Expected DotNetWrapperReleasedJavaReference event.");
			}
		}

		[Test]
		public void WrappingRawJavaInstance_EmitsDotNetWrapperCreated ()
		{
			using (var listener = new CapturingEventListener ()) {
				IntPtr klass = JNIEnv.FindClass ("java/lang/Object");
				Assert.AreNotEqual (IntPtr.Zero, klass, "Failed to resolve java/lang/Object class.");
				try {
					IntPtr ctor = JNIEnv.GetMethodID (klass, "<init>", "()V");
					Assert.AreNotEqual (IntPtr.Zero, ctor, "Failed to resolve java/lang/Object constructor.");

					IntPtr handle = JNIEnv.NewObject (klass, ctor);
					Assert.AreNotEqual (IntPtr.Zero, handle, "Failed to create java/lang/Object instance.");

					var wrapper = Java.Lang.Object.GetObject<Java.Lang.Object> (handle, JniHandleOwnership.TransferLocalRef);
					Assert.IsNotNull (wrapper);
					wrapper.Dispose ();
				} finally {
					JNIEnv.DeleteLocalRef (klass);
				}

				Assert.IsTrue (listener.EventNames.Contains ("DotNetWrapperCreated"), "Expected DotNetWrapperCreated event.");
			}
		}

		sealed class CapturingEventListener : EventListener
		{
			public HashSet<string> EventNames { get; } = new HashSet<string> (StringComparer.Ordinal);

			protected override void OnEventSourceCreated (EventSource eventSource)
			{
				if (eventSource.Name == "Java.Interop") {
					EnableEvents (eventSource, EventLevel.Verbose, EventKeywords.All);
				}
			}

			protected override void OnEventWritten (EventWrittenEventArgs eventData)
			{
				if (eventData.EventName != null) {
					EventNames.Add (eventData.EventName);
				}
			}
		}
	}
}
