using System;
using System.Runtime.InteropServices;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests
{
	[TestFixture]
	public class JniEnvironmentTests : JavaVMFixture
	{
		[Test]
		public unsafe void Types_IsSameObject ()
		{
			using (var t = new JniType ("java/lang/Object")) {
				var c = t.GetConstructor ("()V");
				var o = t.NewObject (c, null);
				try {
					using (var ot = JniEnvironment.Types.GetTypeFromInstance (o)) {
						Assert.IsTrue (JniEnvironment.Types.IsSameObject (t.PeerReference, ot.PeerReference));
					}
				} finally {
					JniObjectReference.Dispose (ref o);
				}
			}
		}

		[Test]
		public void Types_GetJniTypeNameFromInstance ()
		{
			using (var o = new JavaObject ())
				Assert.AreEqual ("java/lang/Object", JniEnvironment.Types.GetJniTypeNameFromInstance (o.PeerReference));
			using (var o = new JavaInt32Array (0))
				Assert.AreEqual ("[I", JniEnvironment.Types.GetJniTypeNameFromInstance (o.PeerReference));
		}

		[Test]
		public unsafe void Handles_NewReturnToJniRef ()
		{
			using (var t = new JniType ("java/lang/Object")) {
				var c = t.GetConstructor ("()V");
				var o = t.NewObject (c, null);
				try {
					var n = o.NewLocalRef ();
					JniObjectReference.Dispose (ref n);
					// warning: lref 'leak'
					var r = JniEnvironment.References.NewReturnToJniRef (o);
					var h = new JniObjectReference (r);
					Assert.AreEqual (JniEnvironment.References.GetIdentityHashCode (o), JniEnvironment.References.GetIdentityHashCode (h));
				} finally {
					JniObjectReference.Dispose (ref o);
				}
			}
		}

		[Test]
		public void References_CreatedReference_InvalidRef ()
		{
			var c = JniEnvironment.LocalReferenceCount;
			var r = new JniObjectReference ();
			JniEnvironment.References.CreatedReference (r);
			Assert.AreEqual (c, JniEnvironment.LocalReferenceCount);
		}

		[Test]
		public void References_CreatedReference_LocalRef ()
		{
			var NewLocalRef = GetNewRefFunc ("java_interop_jnienv_new_local_ref");
			if (NewLocalRef == null) {
				Assert.Ignore ("This version of Java.Interop.dll doesn't use P/Invoke-based JNI invokes.");
				return;
			}
			using (var t = new JniType ("java/lang/Object")) {
				var c = JniEnvironment.LocalReferenceCount;
				var r = NewLocalRef (JniEnvironment.EnvironmentPointer, t.PeerReference.Handle);

				// This is the "problem": direct use of JNI functions don't contribute to
				// our reference count accounting
				Assert.AreEqual (c, JniEnvironment.LocalReferenceCount);

				var o = new JniObjectReference (r, JniObjectReferenceType.Local);
				JniEnvironment.References.CreatedReference (o);

				Assert.AreEqual (c + 1, JniEnvironment.LocalReferenceCount);
				JniObjectReference.Dispose (ref o);
				Assert.AreEqual (c, JniEnvironment.LocalReferenceCount);
			}
		}

		static readonly Type NativeMethods_type =
			typeof (JniEnvironment).Assembly.GetType ("Java.Interop.NativeMethods", throwOnError: false) ??
			typeof (JniEnvironment).Assembly.GetType ("Java.Interop.JIPinvokes.NativeMethods", throwOnError: false);

		static Func<IntPtr, IntPtr, IntPtr> GetNewRefFunc (string method)
		{
			if (NativeMethods_type == null)
				return null;
			return (Func<IntPtr, IntPtr, IntPtr>)Delegate.CreateDelegate (
					typeof (Func<IntPtr, IntPtr, IntPtr>),
					NativeMethods_type,
					method,
					ignoreCase: false,
					throwOnBindFailure: true);
		}

		static Action<IntPtr, IntPtr> GetDeleteRefFunc (string method)
		{
			if (NativeMethods_type == null)
				return null;
			return (Action<IntPtr, IntPtr>)Delegate.CreateDelegate (
					typeof (Action<IntPtr, IntPtr>),
					NativeMethods_type,
					method,
					ignoreCase: false,
					throwOnBindFailure: true);
		}

		[Test]
		public void References_CreatedReference_GlobalRef ()
		{
			var NewGlobalRef = GetNewRefFunc ("java_interop_jnienv_new_global_ref");
			if (NewGlobalRef == null) {
				Assert.Ignore ("This version of Java.Interop.dll doesn't use P/Invoke-based JNI invokes.");
				return;
			}
			using (var t = new JniType ("java/lang/Object")) {
				var c = JniEnvironment.Runtime.GlobalReferenceCount;
				var r = NewGlobalRef (JniEnvironment.EnvironmentPointer, t.PeerReference.Handle);

				// This is the "problem": direct use of JNI functions don't contribute to
				// our reference count accounting
				Assert.AreEqual (c, JniEnvironment.Runtime.GlobalReferenceCount);

				var o = new JniObjectReference (r, JniObjectReferenceType.Global);
				Assert.Throws<ArgumentException> (() => JniEnvironment.References.CreatedReference (o));

				Assert.AreEqual (c, JniEnvironment.Runtime.GlobalReferenceCount);

				GetDeleteRefFunc ("java_interop_jnienv_delete_global_ref")?.Invoke (JniEnvironment.EnvironmentPointer, o.Handle);
			}
		}
	}
}

