#nullable enable

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

using Android.Runtime;
using Java.Interop;

using NUnit.Framework;

namespace Xamarin.Android.RuntimeTests {

	[TestFixture]
	public class ExceptionTest {

		[TestCase (false)]
		[TestCase (true)]
		public void ExceptionProxyLookupPreservesRequestedType (bool generic)
		{
			var expected = new InvalidOperationException ("proxy value");
			using var proxy = JavaProxyThrowable.Create (expected);
			AssertProxyLookup<Java.Lang.Throwable> (proxy, proxy, generic);
			AssertProxyLookup<JavaException> (proxy, proxy, generic);
			AssertProxyLookup<IJavaPeerable> (proxy, proxy, generic);
			AssertProxyLookup<Exception> (proxy, expected, generic);
			AssertProxyLookup<object> (proxy, expected, generic);
		}

		static void AssertProxyLookup<[DynamicallyAccessedMembers (
			DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)] T> (
			Java.Lang.Throwable proxy, object expected, bool generic)
		{
			var reference = proxy.PeerReference.NewLocalRef ();
			try {
				var manager = JniEnvironment.Runtime.ValueManager;
				object? actual = generic
					? manager.GetValue<T> (ref reference, JniObjectReferenceOptions.CopyAndDispose)
					: manager.GetValue (ref reference, JniObjectReferenceOptions.CopyAndDispose, typeof (T));
				Assert.AreSame (expected, actual, $"Lookup as {typeof (T)} should preserve the requested peer/value identity.");
				Assert.IsFalse (reference.IsValid);
			} finally {
				JniObjectReference.Dispose (ref reference);
			}
		}

		[TestCase (false)]
		[TestCase (true)]
		public void ExceptionProxyCreateValueReturnsOriginalManagedException (bool generic)
		{
			var expected = new InvalidOperationException ("created proxy value");
			using var proxy = JavaProxyThrowable.Create (expected);
			var reference = proxy.PeerReference.NewLocalRef ();
			try {
				var manager = JniEnvironment.Runtime.ValueManager;
				object? actual = generic
					? manager.CreateValue<Exception> (ref reference, JniObjectReferenceOptions.CopyAndDispose)
					: manager.CreateValue (ref reference, JniObjectReferenceOptions.CopyAndDispose, typeof (Exception));
				Assert.AreSame (expected, actual);
				Assert.IsFalse (reference.IsValid);
			} finally {
				JniObjectReference.Dispose (ref reference);
			}
		}

		[Test]
		public void ThrowableArrayLookupPreservesProxyPeer ()
		{
			var expected = new InvalidOperationException ("array proxy value");
			using var proxy = JavaProxyThrowable.Create (expected);
			using var peers = new JavaObjectArray<Java.Lang.Throwable> (1);
			peers [0] = proxy;
			Assert.AreSame (proxy, peers [0]);
		}

		[TestCase (false)]
		[TestCase (true)]
		public void ManagedExceptionRoundTripPreservesIdentity (bool directThrow)
		{
			var expected = new InvalidOperationException ("original managed exception");
			var reference = GetPendingThrowable (expected, directThrow);
			try {
				Assert.IsTrue (reference.IsValid);
				Assert.AreEqual ("android/runtime/JavaProxyThrowable", JNIEnv.GetClassNameFromInstance (reference.Handle));
				Assert.IsTrue (Java.Interop.Runtime.IsGCUserPeer (reference.Handle));
				Assert.AreSame (expected, JniEnvironment.Runtime.ValueManager.PeekValue (reference));
				var actual = JniEnvironment.Runtime.GetExceptionForThrowable (
					ref reference, JniObjectReferenceOptions.CopyAndDispose);
				Assert.AreSame (expected, actual);
				Assert.IsFalse (reference.IsValid, "The owned local throwable reference should be consumed.");
			} finally {
				JniObjectReference.Dispose (ref reference);
			}
		}

		[TestCase (false)]
		[TestCase (true)]
		public void JavaThrowableRoundTripPreservesIdentity (bool directThrow)
		{
			using var expected = new Java.Lang.IllegalStateException ("original Java exception");
			var reference = GetPendingThrowable (expected, directThrow);
			try {
				Assert.IsTrue (JniEnvironment.Types.IsSameObject (expected.PeerReference, reference));
				Assert.AreSame (expected, JniEnvironment.Runtime.GetExceptionForThrowable (
					ref reference, JniObjectReferenceOptions.CopyAndDispose));
				Assert.IsFalse (reference.IsValid);
			} finally {
				JniObjectReference.Dispose (ref reference);
			}
		}

		[Test]
		public unsafe void UnregisteredJavaThrowableCreatesAndroidException ()
		{
			using var type = new JniType ("java/lang/IllegalStateException");
			var message = JniEnvironment.Strings.NewString ("unregistered throwable");
			JniObjectReference reference = default;
			try {
				JniArgumentValue* arguments = stackalloc JniArgumentValue [1];
				arguments [0] = new JniArgumentValue (message);
				reference = type.NewObject (type.GetConstructor ("(Ljava/lang/String;)V"), arguments);
				Assert.IsNull (JniEnvironment.Runtime.ValueManager.PeekPeer (reference));
				var actual = JniEnvironment.Runtime.GetExceptionForThrowable (
					ref reference, JniObjectReferenceOptions.CopyAndDispose);
				Assert.IsFalse (reference.IsValid);
				Assert.IsInstanceOf<Java.Lang.IllegalStateException> (actual);
				if (actual is not Java.Lang.IllegalStateException throwable)
					throw new AssertionException ("The Java throwable should use its Android exception binding.");
				using (throwable) {
					Assert.AreEqual ("unregistered throwable", throwable.Message);
				}
			} finally {
				JniObjectReference.Dispose (ref reference);
				JniObjectReference.Dispose (ref message);
			}
		}

		[Test]
		public void InnerExceptionIsNotAProxy ()
		{
			var expected = new InvalidOperationException ("managed cause");
			using var proxy = JavaProxyThrowable.Create (expected);
			using var outer = new Java.Lang.Throwable ("outer exception", proxy);
			var reference = outer.PeerReference.NewLocalRef ();
			try {
				using var actual = new JavaException (ref reference, JniObjectReferenceOptions.CopyAndDispose);
				Assert.AreEqual ("outer exception", actual.Message);
				Assert.AreSame (expected, actual.InnerException);
				using var cause = outer.Cause;
				Assert.IsNotNull (cause);
				if (cause == null)
					throw new AssertionException ("The Java cause is missing.");
				var causeReference = cause.PeerReference.NewLocalRef ();
				try {
					Assert.AreSame (expected, JniEnvironment.Runtime.GetExceptionForThrowable (
						ref causeReference, JniObjectReferenceOptions.CopyAndDispose));
				} finally {
					JniObjectReference.Dispose (ref causeReference);
				}
			} finally {
				JniObjectReference.Dispose (ref reference);
			}
		}

		static JniObjectReference GetPendingThrowable (Exception exception, bool directThrow)
		{
			try {
				if (directThrow)
					JniEnvironment.Exceptions.Throw (exception);
				else
					JniEnvironment.Runtime.RaisePendingException (exception);
				return JniEnvironment.Exceptions.ExceptionOccurred ();
			} finally {
				JniEnvironment.Exceptions.ExceptionClear ();
			}
		}

		[Test]
		[Category ("NativeAOTIgnore")] // NativeAOT has very limited stack traces
		[RequiresUnreferencedCode ("Tests trimming unsafe features")]
		public void InnerExceptionIsSet ()
		{
			var ex = CreateThrownException ();

			using Java.Lang.Throwable proxy = JavaProxyThrowable.Create (ex);
			using var source = new Java.Lang.Throwable ("detailMessage", proxy);
			using var alias  = new Java.Lang.Throwable (source.Handle, JniHandleOwnership.DoNotTransfer);

			CompareStackTraces (ex, proxy);
			Assert.AreEqual ("detailMessage", alias.Message);
			Assert.AreSame (ex, alias.InnerException);
		}

		[Test]
		public void ManagedExceptionProxyContainsManagedStackTrace ()
		{
			var exception = CreateThrownException ();
			using var proxy = JavaProxyThrowable.Create (exception);
			var frames = proxy.GetStackTrace ();
			try {
				Assert.IsNotNull (frames);
				if (frames == null)
					throw new AssertionException ("The proxy stack trace is missing.");
				Assert.IsTrue (frames.Length > 0);
				Assert.That (frames [0].MethodName, Does.Contain (nameof (CreateThrownException)));
				Assert.That (frames [0].ClassName, Does.Contain (nameof (ExceptionTest)));
			} finally {
				if (frames != null) {
					foreach (var frame in frames)
						frame.Dispose ();
				}
			}
		}

		[MethodImpl (MethodImplOptions.NoInlining)]
		static Exception CreateThrownException ()
		{
			try {
				throw new InvalidOperationException ("boo!");
			} catch (Exception exception) {
				return exception;
			}
		}

		[RequiresUnreferencedCode ("Tests trimming unsafe features")]
		void CompareStackTraces (Exception ex, Java.Lang.Throwable throwable)
		{
			var managedTrace = new StackTrace (ex, fNeedFileInfo: true);
			StackFrame[] managedFrames = managedTrace.GetFrames ();
			Java.Lang.StackTraceElement[] javaFrames = throwable.GetStackTrace ();

			// Java
			Assert.IsTrue (javaFrames.Length >= managedFrames.Length,
					$"Java should have at least as many frames as .NET does; java({javaFrames.Length}) < managed({managedFrames.Length})");
			for (int i = 0; i < managedFrames.Length; i++) {
				var mf = managedFrames[i];
				var jf = javaFrames[i];

				// Unknown line locations are -1 on the Java side if they're managed, -2 if they're native
				int managedLine = mf.GetFileLineNumber ();
				if (managedLine == 0) {
					managedLine = mf.HasNativeImage () ? -2 :  -1;
				}

				Console.WriteLine ("# jonp: CompareStackTraces: managedFrame[{0}]: {1}", i, mf);
				Console.WriteLine ("# jonp: CompareStackTraces:    javaFrame[{0}]: {1}", i, jf);
				var managedMethod = mf.GetMethod ();
				var managedDeclaringType = managedMethod?.DeclaringType;
				if (managedMethod != null && managedDeclaringType == null)
					throw new AssertionException ($"Frame {i}: managed declaring type is missing");
				var javaMethodName = jf.MethodName;
				if (managedLine > 0) {
					Assert.AreEqual (managedMethod?.Name,                    javaMethodName, $"Frame {i}: method names differ");
				} else {
					string managedMethodName = managedMethod?.Name ?? "";
					if (javaMethodName == null)
						throw new AssertionException ($"Frame {i}: Java method name is missing");
					Assert.IsTrue (javaMethodName.StartsWith ($"{managedMethodName} + 0x"),
						$"Frame {i}: method name should start with: '{managedMethodName} + 0x'; was `{javaMethodName}`; ");
				}
				Assert.AreEqual (managedDeclaringType?.FullName,          jf.ClassName,
					$"Frame {i}: class names differ: `{managedDeclaringType?.FullName}` != `{jf.ClassName}`");
				Assert.AreEqual (mf.GetFileName (),                       jf.FileName,
					$"Frame {i}: file names differ: `{mf.GetFileName ()}` != `{jf.FileName}`");
				Assert.AreEqual (managedLine,                             jf.LineNumber,
					$"Frame {i}: line numbers differ: {managedLine} != {jf.LineNumber}");
			}
		}
	}
}
