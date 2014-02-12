using System;
using System.Reflection;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests
{
	[TestFixture]
	public class JniEnvironmentTests
	{
		[Test]
		public void CheckCurrent ()
		{
			var f = typeof (JniEnvironment).GetField ("current", BindingFlags.NonPublic | BindingFlags.Static);
			var e = JniEnvironment.Current;
			var h = e.SafeHandle.DangerousGetHandle ();
			e.Dispose ();
			Assert.IsNull (f.GetValue (null));
			JniEnvironment.CheckCurrent (h);
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

