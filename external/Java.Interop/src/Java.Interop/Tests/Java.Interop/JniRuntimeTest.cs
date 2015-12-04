using System;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests
{
	[TestFixture]
	public class JniRuntimeTest : JavaVMFixture
	{
		[Test]
		public void CreateJavaVM ()
		{
			Assert.AreSame (JniRuntime.CurrentRuntime, JniRuntime.CurrentRuntime);
			Assert.IsTrue (JniRuntime.CurrentRuntime.InvocationPointer != IntPtr.Zero);
			Assert.IsTrue (JniEnvironment.EnvironmentPointer != IntPtr.Zero);
		}

#if !__ANDROID__
		[Test]
		public void JDK_OnlySupportsOneVM ()
		{
			try {
				var second = new JreRuntimeOptions ().CreateJreVM ();
				// If we reach here, we're in a JVM that supports > 1 VM
				second.Dispose ();
				Assert.Ignore ();
			} catch (NotSupportedException) {
			} catch (Exception e){
				Assert.Fail ("Expected NotSupportedException; got: {0}", e);
			}
		}
#endif  // !__ANDROID__

		[Test, ExpectedException (typeof (ArgumentNullException))]
		public void CreateJavaVMWithNullBuilder ()
		{
			new JavaVMWithNullBuilder ();
		}

		class JavaVMWithNullBuilder : JniRuntime {
			public JavaVMWithNullBuilder ()
				: base ((JniRuntime.CreationOptions) null)
			{
			}
		}

		[Test]
		public void GetRegisteredJavaVM_ExistingInstance ()
		{
			Assert.AreEqual (JniRuntime.CurrentRuntime, JniRuntime.GetRegisteredRuntime (JniRuntime.CurrentRuntime.InvocationPointer));
		}

		[Test]
		public void GetObject_ReturnsAlias ()
		{
			var local   = new JavaObject ();
			local.UnregisterFromRuntime ();
			Assert.IsNull (JniRuntime.CurrentRuntime.ValueManager.PeekObject (local.PeerReference));
			// GetObject must always return a value (unless handle is null, etc.).
			// However, since we called local.UnregisterFromRuntime(),
			// JniRuntime.PeekObject() is null (asserted above), but GetObject() must
			// **still** return _something_.
			// In this case, it returns an _alias_.
			// TODO: "most derived type" alias generation. (Not relevant here, but...)
			var p       = local.PeerReference;
			var alias   = JniRuntime.CurrentRuntime.ValueManager.GetObject (ref p, JniObjectReferenceOptions.Copy);
			Assert.AreNotSame (local, alias);
			alias.Dispose ();
			local.Dispose ();
		}

		[Test]
		public void GetObject_ReturnsNullWithNullHandle ()
		{
			var o = JniRuntime.CurrentRuntime.ValueManager.GetObject (IntPtr.Zero);
			Assert.IsNull (o);
		}

		[Test]
		public void GetObject_ReturnsRegisteredInstance ()
		{
			JniObjectReference lref;
			using (var o = new JavaObject ()) {
				lref = o.PeerReference.NewLocalRef ();
				Assert.AreSame (o, JniRuntime.CurrentRuntime.ValueManager.PeekObject (lref));
			}
			// At this point, the Java-side object is kept alive by `lref`,
			// but the wrapper instance has been disposed, and thus should
			// be unregistered, and thus unfindable.
			Assert.IsNull (JniRuntime.CurrentRuntime.ValueManager.PeekObject (lref));
			JniObjectReference.Dispose (ref lref);
		}

		[Test]
		public void GetObject_ReturnsNullWithInvalidSafeHandle ()
		{
			var invalid = new JniObjectReference ();
			Assert.IsNull (JniRuntime.CurrentRuntime.ValueManager.GetObject (ref invalid, JniObjectReferenceOptions.CopyAndDispose));
		}

		[Test]
		public unsafe void GetObject_FindBestMatchType ()
		{
			using (var t = new JniType (TestType.JniTypeName)) {
				var c = t.GetConstructor ("()V");
				var o = t.NewObject (c, null);
				using (var w = JniRuntime.CurrentRuntime.ValueManager.GetObject (ref o, JniObjectReferenceOptions.CopyAndDispose)) {
					Assert.AreEqual (typeof (TestType), w.GetType ());
				}
			}
		}
	}
}

