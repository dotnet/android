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
	}
}

