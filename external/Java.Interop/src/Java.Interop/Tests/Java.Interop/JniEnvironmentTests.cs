using System;
using System.Reflection;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests
{
	[TestFixture]
	public class JniEnvironmentTests : JavaVMFixture
	{
		[Test]
		public void Constructor_SetsCurrent ()
		{
			var f = typeof (JniEnvironment).GetField ("current", BindingFlags.NonPublic | BindingFlags.Static);
			var e = JniEnvironment.Current;

			var c = f.GetValue (null);
			Assert.IsNotNull (c);
			Assert.AreSame (c, JniEnvironment.Current);
			Assert.AreSame (c, JniEnvironment.RootEnvironment);

			using (var envp = new JniEnvironment (e.EnvironmentPointer)) {
				Assert.AreSame (envp, f.GetValue (null));
				Assert.AreNotSame (envp, JniEnvironment.RootEnvironment);
			}

			Assert.AreSame (c, JniEnvironment.Current);
		}

		[Test]
		public void Dispose_ClearsLocalReferences ()
		{
			if (!HaveSafeHandles) {
				Assert.Ignore ("SafeHandles aren't used, so magical disposal from a distance isn't supported.");
				return;
			}
			JniObjectReference lref;
			using (var envp = new JniEnvironment (JniEnvironment.Current.EnvironmentPointer)) {
				lref    = new JavaObject ().PeerReference;
				Assert.IsTrue (lref.IsValid);
			}
			Assert.IsFalse (lref.IsValid);
		}

		[Test]
		public void Dispose_ClearsCurrentField ()
		{
			var f = typeof (JniEnvironment).GetField ("current", BindingFlags.NonPublic | BindingFlags.Static);
			var e = JniEnvironment.Current;
			var h = e.EnvironmentPointer;
			e.Dispose ();
			Assert.IsNull (f.GetValue (null));
			Assert.IsNotNull (JniEnvironment.Current);
			Assert.AreEqual (h, JniEnvironment.Current.EnvironmentPointer);
		}

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
					JniEnvironment.Handles.Dispose (ref o);
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
					JniEnvironment.Handles.Dispose (ref n);
					// warning: lref 'leak'
					var r = JniEnvironment.Handles.NewReturnToJniRef (o);
					var h = new JniObjectReference (r);
					Assert.AreEqual (JniEnvironment.Handles.GetIdentityHashCode (o), JniEnvironment.Handles.GetIdentityHashCode (h));
				} finally {
					JniEnvironment.Handles.Dispose (ref o);
				}
			}
		}
	}
}

