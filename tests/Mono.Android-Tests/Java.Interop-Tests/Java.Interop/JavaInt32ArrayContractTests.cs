using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Java.Interop;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Java.InteropTests
{
	[TestClass]
	public class JavaInt32ArrayContractTests : JavaPrimitiveArrayContract<JavaInt32Array, int>
	{
		protected override ICollection<int> CreateCollection (IEnumerable<int> values)
		{
			return new JavaInt32Array (values);
		}

		protected override ICollection<int> CreateCollection (IList<int> values)
		{
			return new JavaInt32Array (values);
		}

		protected override ICollection<int> CreateCollection (int length)
		{
			return new JavaInt32Array (length);
		}
	}

	[TestClass]
	public class JniArrayElementsTests
	{
		readonly TestContext testContext;

		public JniArrayElementsTests (TestContext testContext)
		{
			this.testContext = testContext;
		}

		[TestMethod]
		[Timeout (60_000, CooperativeCancellation = true)]
		public async Task GetElements_RetainsOwnerUntilReleased ()
		{
			WeakReference ownerRef = null;
			JniInt32ArrayElements elements = null;
			FinalizerHelpers.PerformNoPinAction (() => {
				var array = new JavaInt32Array (new[] { 1, 2, 3 });
				ownerRef = new WeakReference (array);
				elements = array.GetElements ();
			});

			try {
				await GCBridgeTestHelpers.CollectUnreachablePeersAsync (testContext.CancellationToken);
				Assert.IsTrue (ownerRef.IsAlive, "Expected JniInt32ArrayElements to keep its owning JavaInt32Array alive before release.");

				elements.CopyToJava ();
				await GCBridgeTestHelpers.CollectUnreachablePeersAsync (testContext.CancellationToken);
				Assert.IsTrue (ownerRef.IsAlive, "Expected JniInt32ArrayElements to keep its owning JavaInt32Array alive after CopyToJava (Commit).");
			} finally {
				elements?.Dispose ();
			}

			await GCBridgeTestHelpers.CollectUnreachablePeersAsync (testContext.CancellationToken);
			Assert.IsFalse (ownerRef.IsAlive, "Expected owning JavaInt32Array to be collected after JniInt32ArrayElements is disposed.");
			GC.KeepAlive (elements);
		}
	}
}

