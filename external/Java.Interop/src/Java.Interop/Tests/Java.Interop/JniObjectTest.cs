using System;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests
{
	[TestFixture]
	public class JniObjectTest
	{
		[Test]
		public void IsSameInstance ()
		{
			using (var t = new JniType ("java/lang/Object")) {
				var c = t.GetConstructor ("()V");
				using (var o = t.NewObject (c)) {
					using (var ot = JniObject.GetTypeFromInstance (o)) {
						Assert.IsTrue (JniObject.IsSameInstance (t.SafeHandle, ot.SafeHandle));
					}
				}
			}
		}
	}
}

