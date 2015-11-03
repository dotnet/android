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
	}
}

