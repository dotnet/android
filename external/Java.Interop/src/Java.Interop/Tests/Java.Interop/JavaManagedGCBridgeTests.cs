using System;
using System.Collections.Generic;
using System.Threading;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests {

	[TestFixture]
	public class JavaManagedGCBridgeTests : JavaVMFixture {

		// https://github.com/mono/mono/blob/98d2314/mono/tests/sgen-bridge-xref.cs
		[Test]
		public void CrossReferences ()
		{
			using (var array = new JavaObjectArray<CrossReferenceBridge> (2)) {
				WeakReference<CrossReferenceBridge> root = null, child = null;
				var t = new Thread (() => SetupLinks (array, out root, out child));
				t.Start ();
				t.Join ();

				JniEnvironment.Runtime.ValueManager.CollectPeers ();
				CrossReferenceBridge a, b;
				a = b = null;
				Console.WriteLine ("try get A {0}", root.TryGetTarget (out a));
				Console.WriteLine ("try get B {0}", child.TryGetTarget (out b));
				Console.WriteLine ("a is null {0}", a == null);
				Console.WriteLine ("b is null {0}", b == null);

				Assert.IsNotNull (a);
				Assert.IsNotNull (b);
			}
		}

		static void SetupLinks (JavaObjectArray<CrossReferenceBridge> array, out WeakReference<CrossReferenceBridge> root, out WeakReference<CrossReferenceBridge> child)
		{
			var a = new CrossReferenceBridge () {
				id      = "bridge",
			};
			var b = new CrossReferenceBridge () {
				id      = "child",
			};
			a.link.Add (b);

			array [0] = a;
			array [1] = b;

			root    = new WeakReference<CrossReferenceBridge> (a, true);
			child   = new WeakReference<CrossReferenceBridge> (b, true);
		}
	}

	[JniTypeSignature (JniTypeName)]
	public class CrossReferenceBridge : JavaObject {
		internal    const    string         JniTypeName = "com/xamarin/interop/CrossReferenceBridge";

		public  string          id;
		public  List<object>    link        = new List<object> ();

		protected override void Dispose (bool disposing)
		{
		}
	}
}

