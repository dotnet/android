#nullable enable annotations

using System;
using System.Threading;

using Android.Runtime;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests
{
	[TestFixture]
	[NonParallelizable]
	[Category ("VirtualCallbackConstructorParity")]
	public class VirtualCallbackConstructorTests
	{
		[Test]
		public void ManagedFirstConstructionUsesFinalPeerThroughoutCallback ()
		{
			VirtualCallbackConstructorDerived.Reset ();
			VirtualCallbackConstructorDerived.RunConcurrentLookup = true;

			using var javaClass = VirtualCallbackConstructorDerived.GetJavaClass ();
			using (var instance = new VirtualCallbackConstructorDerived (42)) {
				Assert.AreEqual (1, VirtualCallbackConstructorDerived.ConstructorInvocations);
				Assert.AreEqual (0, VirtualCallbackConstructorDerived.ActivationConstructorInvocations);
				Assert.AreEqual (1, VirtualCallbackConstructorDerived.CallbackInvocations);
				Assert.AreEqual (42, instance.CallbackValue);
				Assert.AreSame (instance, VirtualCallbackConstructorDerived.CallbackPeer);
				Assert.AreSame (instance, VirtualCallbackConstructorDerived.ReentrantPeer);
				Assert.AreSame (instance, VirtualCallbackConstructorDerived.ConstructorPeer);
				AssertConcurrentPeers (instance);
				AssertRegisteredSame (instance);
			}

			VirtualCallbackConstructorDerived.ClearReferences ();
		}

		[Test]
		public void JavaFirstConstructionPromotesOneProvisionalPeer ()
		{
			VirtualCallbackConstructorDerived.Reset ();
			VirtualCallbackConstructorDerived.RunConcurrentLookup = true;

			using (var instance = CreateFromJava (42)) {
				Assert.AreEqual (1, VirtualCallbackConstructorDerived.ConstructorInvocations);
				Assert.AreEqual (1, VirtualCallbackConstructorDerived.ActivationConstructorInvocations);
				Assert.AreEqual (1, VirtualCallbackConstructorDerived.CallbackInvocations);
				Assert.AreEqual (42, instance.CallbackValue);
				Assert.AreSame (instance, VirtualCallbackConstructorDerived.ActivationPeer);
				Assert.AreSame (instance, VirtualCallbackConstructorDerived.CallbackPeer);
				Assert.AreSame (instance, VirtualCallbackConstructorDerived.ReentrantPeer);
				Assert.AreSame (instance, VirtualCallbackConstructorDerived.ConstructorPeer);
				AssertConcurrentPeers (instance);
				AssertRegisteredSame (instance);
			}

			VirtualCallbackConstructorDerived.ClearReferences ();
		}

		[Test]
		public void JavaFirstVirtualCallbackExceptionAbortsConstruction ()
		{
			VirtualCallbackConstructorDerived.Reset ();
			VirtualCallbackConstructorDerived.ThrowFromCallback = true;

			try {
				var exception = Assert.Catch<Exception> (() => CreateFromJavaExpectingException (42));

				Assert.IsNotNull (exception);
				Assert.IsTrue (exception.ToString ().Contains (VirtualCallbackConstructorDerived.CallbackExceptionMessage));
				Assert.AreEqual (0, VirtualCallbackConstructorDerived.ConstructorInvocations);
				Assert.AreEqual (1, VirtualCallbackConstructorDerived.ActivationConstructorInvocations);
				Assert.AreEqual (1, VirtualCallbackConstructorDerived.CallbackInvocations);
				Assert.AreSame (VirtualCallbackConstructorDerived.ActivationPeer, VirtualCallbackConstructorDerived.CallbackPeer);
				Assert.AreSame (VirtualCallbackConstructorDerived.CallbackPeer, VirtualCallbackConstructorDerived.ReentrantPeer);
			} finally {
				VirtualCallbackConstructorDerived.CallbackPeer?.Dispose ();
				VirtualCallbackConstructorDerived.ClearReferences ();
			}
		}

		static VirtualCallbackConstructorDerived CreateFromJava (int value)
		{
			using var javaClass = VirtualCallbackConstructorDerived.GetJavaClass ();
			var constructor = JNIEnv.GetMethodID (javaClass.Handle, "<init>", "(I)V");
			var instance = JNIEnv.StartCreateInstance (javaClass.Handle, constructor, new JValue (value));
			JNIEnv.FinishCreateInstance (instance, javaClass.Handle, constructor, new JValue (value));
			var result = Java.Lang.Object.GetObject<VirtualCallbackConstructorDerived> (
				instance,
				JniHandleOwnership.TransferLocalRef);
			Assert.IsNotNull (result);
			return result;
		}

		static void CreateFromJavaExpectingException (int value)
		{
			IntPtr instance = IntPtr.Zero;
			try {
				using var javaClass = VirtualCallbackConstructorDerived.GetJavaClass ();
				var constructor = JNIEnv.GetMethodID (javaClass.Handle, "<init>", "(I)V");
				instance = JNIEnv.StartCreateInstance (javaClass.Handle, constructor, new JValue (value));
				JNIEnv.FinishCreateInstance (instance, javaClass.Handle, constructor, new JValue (value));
			} finally {
				if (instance != IntPtr.Zero) {
					JNIEnv.DeleteLocalRef (instance);
				}
			}
		}

		static void AssertRegisteredSame (VirtualCallbackConstructorDerived instance)
		{
			var registered = Java.Lang.Object.GetObject<VirtualCallbackConstructorDerived> (
				instance.Handle,
				JniHandleOwnership.DoNotTransfer);
			Assert.AreSame (instance, registered);
		}

		static void AssertConcurrentPeers (VirtualCallbackConstructorDerived instance)
		{
			Assert.IsNull (VirtualCallbackConstructorDerived.ConcurrentLookupException);
			Assert.IsNotNull (VirtualCallbackConstructorDerived.ConcurrentPeers);
			foreach (var peer in VirtualCallbackConstructorDerived.ConcurrentPeers) {
				Assert.AreSame (instance, peer);
			}
		}
	}

	[Register ("net/dot/android/test/VirtualCallbackConstructorDerived")]
	public class VirtualCallbackConstructorDerived : global::Net.Dot.Android.Test.VirtualCallbackConstructorBase
	{
		public const string CallbackExceptionMessage = "virtual constructor callback throw";
		const string JavaClassName = "net.dot.android.test.VirtualCallbackConstructorDerived";

		public static int ConstructorInvocations;
		public static int ActivationConstructorInvocations;
		public static int CallbackInvocations;
		public static bool RunConcurrentLookup;
		public static bool ThrowFromCallback;
		public static VirtualCallbackConstructorDerived? ActivationPeer;
		public static VirtualCallbackConstructorDerived? CallbackPeer;
		public static VirtualCallbackConstructorDerived? ReentrantPeer;
		public static VirtualCallbackConstructorDerived? ConstructorPeer;
		public static VirtualCallbackConstructorDerived? []? ConcurrentPeers;
		public static Exception? ConcurrentLookupException;

		public int CallbackValue;

		[Register (".ctor", "(I)V", "")]
		public VirtualCallbackConstructorDerived (int value)
			: base (value)
		{
			ConstructorInvocations++;
			ConstructorPeer = this;
		}

		public VirtualCallbackConstructorDerived (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
			RecordActivation (this);
		}

		public override void OnConstructed (int value)
		{
			CallbackInvocations++;
			CallbackValue = value;
			CallbackPeer = this;
			ReentrantPeer = Java.Lang.Object.GetObject<VirtualCallbackConstructorDerived> (
				Handle,
				JniHandleOwnership.DoNotTransfer);

			if (RunConcurrentLookup) {
				RunConcurrentLookups ();
			}

			if (ThrowFromCallback) {
				throw new InvalidOperationException (CallbackExceptionMessage);
			}
		}

		internal static void RecordActivation (VirtualCallbackConstructorDerived peer)
		{
			ActivationConstructorInvocations++;
			ActivationPeer = peer;
		}

		internal static Java.Lang.Class GetJavaClass ()
			=> Java.Lang.Class.ForName (
				JavaClassName,
				initialize: true,
				Android.App.Application.Context.ClassLoader);

		static void RunConcurrentLookups ()
		{
			const int threadCount = 2;
			var timeout = TimeSpan.FromSeconds (10);
			var peers = new VirtualCallbackConstructorDerived? [threadCount];
			var exceptions = new Exception? [threadCount];
			var ready = new CountdownEvent (threadCount);
			var start = new ManualResetEventSlim ();
			var threads = new Thread [threadCount];

			for (int i = 0; i < threads.Length; i++) {
				int index = i;
				threads [i] = new Thread (() => {
					try {
						ready.Signal ();
						start.Wait ();
						peers [index] = Java.Lang.Object.GetObject<VirtualCallbackConstructorDerived> (
							CallbackPeer?.Handle ?? IntPtr.Zero,
							JniHandleOwnership.DoNotTransfer);
					} catch (Exception e) {
						exceptions [index] = e;
					}
				}) {
					IsBackground = true,
				};
				threads [i].Start ();
			}

			bool allReady = ready.Wait (timeout);
			start.Set ();
			bool allJoined = true;
			foreach (var thread in threads) {
				allJoined &= thread.Join (timeout);
			}

			if (allJoined) {
				ready.Dispose ();
				start.Dispose ();
			}
			if (!allReady) {
				Assert.Fail ($"Concurrent constructor lookup workers did not become ready within {timeout}.");
			}
			if (!allJoined) {
				Assert.Fail ($"Concurrent constructor lookup workers did not complete within {timeout}.");
			}

			ConcurrentPeers = peers;
			foreach (var exception in exceptions) {
				if (exception != null) {
					ConcurrentLookupException = exception;
					break;
				}
			}
		}

		public static void Reset ()
		{
			ConstructorInvocations = 0;
			ActivationConstructorInvocations = 0;
			CallbackInvocations = 0;
			RunConcurrentLookup = false;
			ThrowFromCallback = false;
			ClearReferences ();
		}

		public static void ClearReferences ()
		{
			ActivationPeer = null;
			CallbackPeer = null;
			ReentrantPeer = null;
			ConstructorPeer = null;
			ConcurrentPeers = null;
			ConcurrentLookupException = null;
		}
	}
}
