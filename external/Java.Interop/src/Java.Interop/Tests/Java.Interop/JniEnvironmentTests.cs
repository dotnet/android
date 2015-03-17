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

			using (var envp = new JniEnvironment (e.SafeHandle.DangerousGetHandle ())) {
				Assert.AreSame (envp, f.GetValue (null));
				Assert.AreNotSame (envp, JniEnvironment.RootEnvironment);
			}

			Assert.AreSame (c, JniEnvironment.Current);
		}

		[Test]
		public void Dispose_ClearsLocalReferences ()
		{
			JniLocalReference lref;
			using (var envp = new JniEnvironment (JniEnvironment.Current.SafeHandle.DangerousGetHandle ())) {
				lref    = (JniLocalReference) new JavaObject ().SafeHandle;
				Assert.IsFalse (lref.IsClosed);
			}
			Assert.IsTrue (lref.IsClosed);
		}


		[Test]
		public void Dispose_ClearsCurrentField ()
		{
			var f = typeof (JniEnvironment).GetField ("current", BindingFlags.NonPublic | BindingFlags.Static);
			var e = JniEnvironment.Current;
			var h = e.SafeHandle.DangerousGetHandle ();
			e.Dispose ();
			Assert.IsNull (f.GetValue (null));
			Assert.IsNotNull (JniEnvironment.Current);
			Assert.AreEqual (h, JniEnvironment.Current.SafeHandle.DangerousGetHandle ());
		}

		[Test]
		public void Types_IsSameObject ()
		{
			using (var t = new JniType ("java/lang/Object")) {
				var c = t.GetConstructor ("()V");
				using (var o = t.NewObject (c)) {
					using (var ot = JniEnvironment.Types.GetTypeFromInstance (o)) {
						Assert.IsTrue (JniEnvironment.Types.IsSameObject (t.SafeHandle, ot.SafeHandle));
					}
				}
			}
		}

		[Test]
		public void Types_GetJniTypeNameFromInstance ()
		{
			using (var o = new JavaObject ())
				Assert.AreEqual ("java/lang/Object", JniEnvironment.Types.GetJniTypeNameFromInstance (o.SafeHandle));
			using (var o = new JavaInt32Array (0))
				Assert.AreEqual ("[I", JniEnvironment.Types.GetJniTypeNameFromInstance (o.SafeHandle));
		}

		[Test]
		public void Handles_NewReturnToJniRef ()
		{
			using (var t = new JniType ("java/lang/Object")) {
				var c = t.GetConstructor ("()V");
				using (var o = t.NewObject (c)) {
					// warning: lref 'leak'
					var r = JniEnvironment.Handles.NewReturnToJniRef (o);
					var h = new JniInvocationHandle (r);
					Assert.AreEqual (JniEnvironment.Handles.GetIdentityHashCode (o), JniEnvironment.Handles.GetIdentityHashCode (h));
				}
			}
		}
	}
}

