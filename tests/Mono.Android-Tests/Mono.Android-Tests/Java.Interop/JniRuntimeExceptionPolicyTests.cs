#nullable enable

using System;
using System.Runtime.InteropServices;

using Android.Runtime;
using Java.Interop;

using NUnit.Framework;

namespace Xamarin.Android.RuntimeTests;

// JNI transport coverage without an attached debugger, not debugger stop/Continue behavior.
[TestFixture]
[NonParallelizable]
[Category ("JniRuntimeExceptionPolicy")]
public class JniRuntimeExceptionPolicyTests {
	static Exception? callbackException;
	static int callbackInvocations;

	static int Callback (IntPtr env, IntPtr klass)
	{
		callbackInvocations++;
		if (callbackException is Exception exception)
			throw exception;
		return 42;
	}

	static void Action (IntPtr env, IntPtr klass)
	{
		Callback (env, klass);
	}

	[SetUp]
	public void ResetCallbacks ()
	{
		callbackException = null;
		callbackInvocations = 0;
		TestContext.WriteLine ($"JNI runtime: {JniEnvironment.Runtime.GetType ().FullName}");
	}

	[TearDown]
	public void ClearCallbacks ()
	{
		JniEnvironment.Exceptions.ExceptionClear ();
		callbackException = null;
	}

	[TestCase (false)]
	[TestCase (true)]
	public void FailedFunctionReturnsDefault (bool handled)
	{
		var expected = new InvalidOperationException ("JNI function exception");
		callbackException = expected;
		var notification = new PolicyNotification (expected, handled);
		AndroidEnvironment.UnhandledExceptionRaiser += notification.OnException;
		JniObjectReference pending = default;
		try {
			Assert.AreEqual (0, InvokeManagedInt (JniEnvironment.EnvironmentPointer, IntPtr.Zero));
			pending = JniEnvironment.Exceptions.ExceptionOccurred ();
			JniEnvironment.Exceptions.ExceptionClear ();
			Assert.AreEqual (!handled, pending.IsValid);
			if (!handled) {
				Assert.AreSame (expected, JniEnvironment.Runtime.GetExceptionForThrowable (
					ref pending, JniObjectReferenceOptions.CopyAndDispose));
			}
			AssertNotification (notification);
		} finally {
			JniEnvironment.Exceptions.ExceptionClear ();
			JniObjectReference.Dispose (ref pending);
			AndroidEnvironment.UnhandledExceptionRaiser -= notification.OnException;
		}
	}

	[TestCase (false, false)]
	[TestCase (false, true)]
	[TestCase (true, false)]
	[TestCase (true, true)]
	public void JavaCatchAndFinallyRespectPolicy (bool action, bool handled)
	{
		using var type = RegisterCallbacks ();
		var expected = new InvalidOperationException ("Java catch/finally callback exception");
		callbackException = expected;
		var notification = new PolicyNotification (expected, handled);
		AndroidEnvironment.UnhandledExceptionRaiser += notification.OnException;
		try {
			int result = InvokeJava (type, action, swallow: true);
			Assert.AreEqual (handled ? (action ? 42 : 0) : -1, result);
			Assert.AreEqual (handled ? 0 : 1, GetCounter (type, "caught"));
			Assert.AreEqual (1, GetCounter (type, "finished"));
			Assert.AreEqual (handled ? 1 : 0, GetCounter (type, "continued"));
			AssertNotification (notification);
			Assert.IsFalse (JniEnvironment.Exceptions.ExceptionCheck ());
		} finally {
			AndroidEnvironment.UnhandledExceptionRaiser -= notification.OnException;
		}
	}

	[TestCase (false)]
	[TestCase (true)]
	public void JavaRethrowPreservesIdentityAndRunsFinally (bool action)
	{
		using var type = RegisterCallbacks ();
		var expected = new InvalidOperationException ("Java rethrow callback exception");
		callbackException = expected;
		var notification = new PolicyNotification (expected, handled: false);
		AndroidEnvironment.UnhandledExceptionRaiser += notification.OnException;
		try {
			var actual = Assert.Throws<InvalidOperationException> (() => InvokeJava (type, action, swallow: false));
			Assert.AreSame (expected, actual);
			Assert.AreEqual (1, GetCounter (type, "caught"));
			Assert.AreEqual (1, GetCounter (type, "finished"));
			Assert.AreEqual (0, GetCounter (type, "continued"));
			AssertNotification (notification);
			Assert.IsFalse (JniEnvironment.Exceptions.ExceptionCheck ());
		} finally {
			AndroidEnvironment.UnhandledExceptionRaiser -= notification.OnException;
		}
	}

	[TestCase (false)]
	[TestCase (true)]
	public void SuccessfulCallbacksPreserveReturnValue (bool action)
	{
		using var type = RegisterCallbacks ();
		var notification = new PolicyNotification (new InvalidOperationException (), handled: false);
		AndroidEnvironment.UnhandledExceptionRaiser += notification.OnException;
		try {
			Assert.AreEqual (42, InvokeJava (type, action, swallow: false));
			Assert.AreEqual (0, GetCounter (type, "caught"));
			Assert.AreEqual (1, GetCounter (type, "finished"));
			Assert.AreEqual (1, GetCounter (type, "continued"));
			Assert.AreEqual (1, callbackInvocations);
			Assert.AreEqual (0, notification.Count);
			Assert.IsFalse (JniEnvironment.Exceptions.ExceptionCheck ());
		} finally {
			AndroidEnvironment.UnhandledExceptionRaiser -= notification.OnException;
		}
	}

	static void AssertNotification (PolicyNotification notification)
	{
		Assert.AreEqual (1, callbackInvocations);
		Assert.AreEqual (1, notification.Count);
		Assert.IsTrue (notification.SameException);
		Assert.IsFalse (notification.PendingException);
		Assert.Greater (notification.JavaTime, 0, "Java calls must remain usable during policy notification.");
	}

	sealed class PolicyNotification (Exception expected, bool handled) {
		public int Count;
		public bool SameException = true;
		public bool PendingException;
		public long JavaTime;

		public void OnException (object? sender, RaiseThrowableEventArgs args)
		{
			Count++;
			SameException &= ReferenceEquals (expected, args.Exception);
			bool pending = JniEnvironment.Exceptions.ExceptionCheck ();
			PendingException |= pending;
			if (!pending)
				JavaTime = Java.Lang.JavaSystem.CurrentTimeMillis ();
			args.Handled = handled;
		}
	}

	static int GetCounter (JniType type, string name)
		=> JniEnvironment.StaticFields.GetStaticIntField (type.PeerReference, type.GetStaticField (name, "I"));

	static unsafe int InvokeJava (JniType type, bool action, bool swallow)
	{
		JniArgumentValue* args = stackalloc JniArgumentValue [2];
		args [0] = new JniArgumentValue (action);
		args [1] = new JniArgumentValue (swallow);
		return JniEnvironment.StaticMethods.CallStaticIntMethod (
			type.PeerReference, type.GetStaticMethod ("invoke", "(ZZ)I"), args);
	}

	static unsafe JniType RegisterCallbacks ()
	{
		var type = new JniType ("net/dot/android/test/JniRuntimeExceptionPolicy");
		fixed (byte* callbackName = "callback\0"u8)
		fixed (byte* callbackSignature = "()I\0"u8)
		fixed (byte* actionName = "action\0"u8)
		fixed (byte* actionSignature = "()V\0"u8) {
			JniNativeMethod* methods = stackalloc JniNativeMethod [2];
			methods [0] = new JniNativeMethod (callbackName, callbackSignature,
				(IntPtr) (delegate* unmanaged<IntPtr, IntPtr, int>) &InvokeInt);
			methods [1] = new JniNativeMethod (actionName, actionSignature,
				(IntPtr) (delegate* unmanaged<IntPtr, IntPtr, void>) &InvokeVoid);
			JniEnvironment.Types.RegisterNatives (type.PeerReference, new ReadOnlySpan<JniNativeMethod> (methods, 2));
		}
		return type;
	}

	static unsafe int InvokeManagedInt (IntPtr env, IntPtr klass)
		=> JniMarshal.SafeInvokeFunc<int> (env, klass, &Callback);

	[UnmanagedCallersOnly]
	static int InvokeInt (IntPtr env, IntPtr klass)
		=> InvokeManagedInt (env, klass);

	[UnmanagedCallersOnly]
	static unsafe void InvokeVoid (IntPtr env, IntPtr klass)
		=> JniMarshal.SafeInvokeAction (env, klass, &Action);
}
