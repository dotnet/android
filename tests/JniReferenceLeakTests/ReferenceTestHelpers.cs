using Java.Interop;

namespace JniReferenceLeakTests;

static class ReferenceTestHelpers
{
	public static void AssertNoGlobalReferenceLeak (Action action, int iterations = 100, int allowedIncrease = 5)
	{
		for (int i = 0; i < iterations; i++) {
			action ();
		}
		CollectGarbage ();

		int before = JniEnvironment.Runtime.GlobalReferenceCount;
		for (int i = 0; i < iterations; i++) {
			action ();
		}
		CollectGarbage ();
		int after = JniEnvironment.Runtime.GlobalReferenceCount;

		Assert.IsTrue (
			after - before <= allowedIncrease,
			$"Global reference count increased by more than {allowedIncrease} after {iterations} iterations. Before={before}, After={after}, Delta={after - before}");
	}

	public static void CollectGarbage ()
	{
		for (int i = 0; i < 3; i++) {
			GC.Collect ();
			GC.WaitForPendingFinalizers ();
		}

		JniEnvironment.Runtime.ValueManager.CollectPeers ();
		JniEnvironment.Runtime.ValueManager.WaitForGCBridgeProcessing ();
	}
}
