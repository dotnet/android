using System;

using Java.Interop;

using NUnit.Framework;

namespace Java.BaseTests {

	[TestFixture]
	public class NestedTypeTests : JavaVMFixture {

		[Test]
		public void Create_AbstractQueuedSynchronizer_ConditionObject ()
		{
			using var outer = new MyQueuedSynchronizer ();
			using var inner = new MyQueuedSynchronizer.MyConditionObject (outer);
		}
	}

	[JniTypeSignature (JniTypeName)]
	class MyQueuedSynchronizer : Java.Util.Concurrent.Locks.AbstractQueuedSynchronizer {
		internal const string JniTypeName = "example/MyQueuedSynchronizer";

		public MyQueuedSynchronizer ()
		{
		}

		public class MyConditionObject : Java.Util.Concurrent.Locks.AbstractQueuedSynchronizer.ConditionObject {

			public MyConditionObject (MyQueuedSynchronizer outer)
				: base (outer)
			{
			}
		}
	}
}
