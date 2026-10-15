#nullable enable

using System;
using System.Diagnostics;
using Android.Runtime;
using Java.Interop;
using NUnit.Framework;

namespace Xamarin.Android.RuntimeTests;

[TestFixture]
[NonParallelizable]
[Category ("JniCallbackBoundaryOwnership")]
public class JniCallbackBoundaryOwnershipTests
{
	internal static Exception? CallbackException;
	internal static int Invocations;

	internal static int Callback (IntPtr env, IntPtr self)
	{
		Invocations++;
		if (CallbackException is Exception exception)
			throw exception;
		return 42;
	}

	[TearDown]
	public void ClearCallback ()
	{
		CallbackException = null;
		JniEnvironment.Exceptions.ExceptionClear ();
	}

	[TestCase (0, false)]
	[TestCase (0, true)]
	[TestCase (1, false)]
	[TestCase (1, true)]
	[TestCase (2, false)]
	[TestCase (2, true)]
	public unsafe void GeneratedUcoPublishesAfterPolicyAndJavaRunsCatchFinally (int shape, bool handled)
	{
		using var peer = CreatePeer (shape);
		using var type = new JniType ("net/dot/android/test/JniCallbackBoundaryOwnership");
		var expected = new InvalidOperationException ("generated callback boundary");
		CallbackException = expected;
		Invocations = 0;
		int notifications = 0;
		bool pendingInPolicy = true;
		EventHandler<RaiseThrowableEventArgs> handler = (_, args) => {
			notifications++;
			Assert.AreSame (expected, args.Exception);
			pendingInPolicy = JniEnvironment.Exceptions.ExceptionCheck ();
			Assert.Greater (Java.Lang.JavaSystem.CurrentTimeMillis (), 0, "Policy runs before the Java exception is published.");
			args.Handled = handled;
		};
		AndroidEnvironment.UnhandledExceptionRaiser += handler;
		try {
			JniArgumentValue* args = stackalloc JniArgumentValue [2];
			args [0] = new JniArgumentValue (peer.Handle);
			args [1] = new JniArgumentValue (true);
			int result = JniEnvironment.StaticMethods.CallStaticIntMethod (type.PeerReference,
				type.GetStaticMethod ("invoke", "(Lnet/dot/android/test/JniCallbackBoundaryOwnership$Callback;Z)I"), args);
			Assert.AreEqual (handled ? 0 : -1, result);
			Assert.AreEqual (handled ? 0 : 1, Counter (type, "caught"));
			Assert.AreEqual (1, Counter (type, "finished"));
			Assert.AreEqual (handled ? 1 : 0, Counter (type, "continued"));
			Assert.AreEqual (1, notifications);
			Assert.AreEqual (1, Invocations, "Java must reach the scanner-selected generated forwarding UCO.");
			Assert.IsFalse (pendingInPolicy);
			Assert.IsFalse (JniEnvironment.Exceptions.ExceptionCheck ());
		} finally {
			AndroidEnvironment.UnhandledExceptionRaiser -= handler;
		}
	}

	[TestCase (0)]
	[TestCase (1)]
	[TestCase (2)]
	public unsafe void GeneratedUcoJavaRethrowPreservesExceptionIdentity (int shape)
	{
		using var peer = CreatePeer (shape);
		using var type = new JniType ("net/dot/android/test/JniCallbackBoundaryOwnership");
		var expected = new InvalidOperationException ("generated callback rethrow");
		CallbackException = expected;
		Invocations = 0;
		int notifications = 0;
		EventHandler<RaiseThrowableEventArgs> handler = (_, args) => {
			notifications++;
			Assert.AreSame (expected, args.Exception);
		};
		AndroidEnvironment.UnhandledExceptionRaiser += handler;
		try {
			var actual = Assert.Throws<InvalidOperationException> (() => {
				JniArgumentValue* args = stackalloc JniArgumentValue [2];
				args [0] = new JniArgumentValue (peer.Handle);
				args [1] = new JniArgumentValue (false);
				JniEnvironment.StaticMethods.CallStaticIntMethod (type.PeerReference,
					type.GetStaticMethod ("invoke", "(Lnet/dot/android/test/JniCallbackBoundaryOwnership$Callback;Z)I"), args);
			});
			Assert.AreSame (expected, actual);
			Assert.AreEqual (1, Counter (type, "caught"));
			Assert.AreEqual (1, Counter (type, "finished"));
			Assert.AreEqual (0, Counter (type, "continued"));
			Assert.AreEqual (1, notifications);
			Assert.AreEqual (1, Invocations);
			Assert.IsFalse (JniEnvironment.Exceptions.ExceptionCheck ());
		} finally {
			AndroidEnvironment.UnhandledExceptionRaiser -= handler;
		}
	}

	[TestCase (0)]
	[TestCase (1)]
	[TestCase (2)]
	public unsafe void GeneratedUcoPreservesSuccessfulReturn (int shape)
	{
		using var peer = CreatePeer (shape);
		using var type = new JniType ("net/dot/android/test/JniCallbackBoundaryOwnership");
		Invocations = 0;
		JniArgumentValue* args = stackalloc JniArgumentValue [2];
		args [0] = new JniArgumentValue (peer.Handle);
		args [1] = new JniArgumentValue (false);
		Assert.AreEqual (42, JniEnvironment.StaticMethods.CallStaticIntMethod (type.PeerReference,
			type.GetStaticMethod ("invoke", "(Lnet/dot/android/test/JniCallbackBoundaryOwnership$Callback;Z)I"), args));
		Assert.AreEqual (1, Invocations);
		Assert.AreEqual (1, Counter (type, "continued"));
		Assert.AreEqual (1, Counter (type, "finished"));
	}

	static CallbackOwnershipPeer CreatePeer (int shape) => shape switch {
		0 => new RawCallbackOwnershipPeer (),
		1 => new HelperCallbackOwnershipPeer (),
		2 => new InlineCallbackOwnershipPeer (),
		_ => throw new ArgumentOutOfRangeException (nameof (shape)),
	};
	static int Counter (JniType type, string name)
		=> JniEnvironment.StaticFields.GetStaticIntField (type.PeerReference, type.GetStaticField (name, "I"));
}

[Register ("net/dot/android/test/JniCallbackBoundaryOwnership$Callback", DoNotGenerateAcw = true)]
public abstract class CallbackOwnershipPeer : Java.Lang.Object
{
	protected CallbackOwnershipPeer () { }
	protected CallbackOwnershipPeer (IntPtr handle, JniHandleOwnership ownership) : base (handle, ownership) { }
	public abstract int Invoke ();
}

[Register ("net/dot/android/test/RawCallbackOwnershipPeer")]
public sealed class RawCallbackOwnershipPeer : CallbackOwnershipPeer
{
	public RawCallbackOwnershipPeer () { }
	public RawCallbackOwnershipPeer (IntPtr handle, JniHandleOwnership ownership) : base (handle, ownership) { }
	[Register ("invoke", "()I", "GetInvokeHandler:Xamarin.Android.RuntimeTests.RawCallbackOwnershipPeer, Mono.Android.NET-Tests")]
	public override int Invoke () => JniCallbackBoundaryOwnershipTests.Callback (IntPtr.Zero, IntPtr.Zero);
	static Delegate GetInvokeHandler () => throw new InvalidOperationException ("The scanner must not invoke a connector factory.");
	static int n_Invoke (IntPtr env, IntPtr self) => JniCallbackBoundaryOwnershipTests.Callback (env, self);
}

[Register ("net/dot/android/test/HelperCallbackOwnershipPeer")]
public sealed class HelperCallbackOwnershipPeer : CallbackOwnershipPeer
{
	public HelperCallbackOwnershipPeer () { }
	public HelperCallbackOwnershipPeer (IntPtr handle, JniHandleOwnership ownership) : base (handle, ownership) { }
	[Register ("invoke", "()I", "GetInvokeHandler:Xamarin.Android.RuntimeTests.HelperCallbackOwnershipPeer, Mono.Android.NET-Tests")]
	public override int Invoke () => JniCallbackBoundaryOwnershipTests.Callback (IntPtr.Zero, IntPtr.Zero);
	static Delegate GetInvokeHandler () => throw new InvalidOperationException ("The scanner must not invoke a connector factory.");
	static unsafe int n_Invoke (IntPtr env, IntPtr self)
		=> JniMarshal.SafeInvokeFunc (env, self, &JniCallbackBoundaryOwnershipTests.Callback);
}

[Register ("net/dot/android/test/InlineCallbackOwnershipPeer")]
public sealed class InlineCallbackOwnershipPeer : CallbackOwnershipPeer
{
	public InlineCallbackOwnershipPeer () { }
	public InlineCallbackOwnershipPeer (IntPtr handle, JniHandleOwnership ownership) : base (handle, ownership) { }
	[Register ("invoke", "()I", "GetInvokeHandler:Xamarin.Android.RuntimeTests.InlineCallbackOwnershipPeer, Mono.Android.NET-Tests")]
	public override int Invoke () => JniCallbackBoundaryOwnershipTests.Callback (IntPtr.Zero, IntPtr.Zero);
	static Delegate GetInvokeHandler () => throw new InvalidOperationException ("The scanner must not invoke a connector factory.");
	[DebuggerDisableUserUnhandledExceptions]
	static int n_Invoke (IntPtr env, IntPtr self)
	{
		if (!JniEnvironment.BeginMarshalMethod (env, out var transition, out var runtime))
			return default;
		try {
			return JniCallbackBoundaryOwnershipTests.Callback (env, self);
		} catch (Exception exception) {
			runtime.OnUserUnhandledException (ref transition, exception);
			return default;
		} finally {
			JniEnvironment.EndMarshalMethod (ref transition);
		}
	}
}
