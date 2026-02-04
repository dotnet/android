using Android.Runtime;
using Java.Interop;

namespace Microsoft.Android.Runtime;

/// <summary>
/// Logger for GC bridge processing operations.
/// Mirrors the logging from the original C++ bridge-processing.cc.
/// </summary>
class BridgeProcessingLogger
{
	const string LogTag = "monodroid-gc";

	public void LogMissingAddReferencesMethod (JniType javaClass)
	{
		Logger.Log (LogLevel.Error, LogTag, "Failed to find monodroidAddReference method");
		if (Logger.LogGC) {
			var className = GetClassName (javaClass);
			Logger.Log (LogLevel.Error, LogTag, $"Missing monodroidAddReference method for object of class {className}");
		}
	}

	public void LogMissingClearReferencesMethod (JniType javaClass)
	{
		Logger.Log (LogLevel.Error, LogTag, "Failed to find monodroidClearReferences method");
		if (Logger.LogGC) {
			var className = GetClassName (javaClass);
			Logger.Log (LogLevel.Error, LogTag, $"Missing monodroidClearReferences method for object of class {className}");
		}
	}

	public void LogWeakToGref (JniObjectReference weak, JniObjectReference handle)
	{
		if (!Logger.LogGlobalRef) {
			return;
		}
		Logger.Log (LogLevel.Info, LogTag, $"take_global_ref wref=0x{weak.Handle:x} -> handle=0x{handle.Handle:x}");
	}

	public void LogWeakRefCollected (JniObjectReference weak)
	{
		if (!Logger.LogGC) {
			return;
		}
		Logger.Log (LogLevel.Info, LogTag, $"handle 0x{weak.Handle:x}/W; was collected by a Java GC");
	}

	public void LogTakeWeakGlobalRef (JniObjectReference handle)
	{
		if (!Logger.LogGlobalRef) {
			return;
		}
		Logger.Log (LogLevel.Info, LogTag, $"take_weak_global_ref handle=0x{handle.Handle:x}");
	}

	public void LogWeakGrefNew (JniObjectReference handle, JniObjectReference weak)
	{
		if (!Logger.LogGlobalRef) {
			return;
		}
		Logger.Log (LogLevel.Info, LogTag, $"weak_gref_new handle=0x{handle.Handle:x} -> weak=0x{weak.Handle:x}");
	}

	public void LogGrefDelete (JniObjectReference handle)
	{
		if (!Logger.LogGlobalRef) {
			return;
		}
		Logger.Log (LogLevel.Info, LogTag, $"gref_delete handle=0x{handle.Handle:x}   at [[clr-gc:take_weak_global_ref]]");
	}

	public void LogWeakRefDelete (JniObjectReference weak)
	{
		if (!Logger.LogGlobalRef) {
			return;
		}
		Logger.Log (LogLevel.Info, LogTag, $"weak_ref_delete weak=0x{weak.Handle:x}   at [[clr-gc:take_global_ref]]");
	}

	public unsafe void LogGcSummary (MarkCrossReferencesArgs* crossRefs)
	{
		if (!Logger.LogGC) {
			return;
		}

		nuint total = 0;
		nuint alive = 0;

		for (nuint i = 0; i < crossRefs->ComponentCount; i++) {
			ref StronglyConnectedComponent scc = ref crossRefs->Components[i];

			for (nuint j = 0; j < scc.Count; j++) {
				BridgeProcessing.HandleContext* context = (BridgeProcessing.HandleContext*)scc.Contexts[j];
				if (context == null) {
					continue;
				}

				total++;
				if (context->ControlBlock != null && context->ControlBlock->Handle != System.IntPtr.Zero) {
					alive++;
				}
			}
		}

		Logger.Log (LogLevel.Info, LogTag, $"GC cleanup summary: {total} objects tested - resurrecting {alive}.");
	}

	static string GetClassName (JniType javaClass)
	{
		try {
			return javaClass.PeerReference.IsValid ? javaClass.Name : "(unknown)";
		} catch {
			return "(unknown)";
		}
	}
}
