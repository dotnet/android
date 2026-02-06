using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Java;
using Android.Runtime;
using Java.Interop;

namespace Microsoft.Android.Runtime;

/// <summary>
/// Handles GC bridge processing between managed and Java objects.
/// This is the C# port of the native bridge-processing.cc logic.
/// </summary>
unsafe class BridgeProcessing
{
	readonly MarkCrossReferencesArgs* crossRefs;

	// Temporary peers created for empty SCCs
	readonly Dictionary<nuint, JniObjectReference> temporaryPeers = new ();

	// Cached Java class and method references
	static JniType? s_GCUserPeerClass;
	static JniMethodInfo? s_GCUserPeerCtor;

	static readonly BridgeProcessingLogger? s_logger;

	static BridgeProcessing ()
	{
		s_GCUserPeerClass = new JniType ("mono/android/GCUserPeer");
		s_GCUserPeerCtor = s_GCUserPeerClass.GetConstructor ("()V");

		if (Logger.LogGC || Logger.LogGlobalRef) {
			s_logger = new BridgeProcessingLogger ();
		}
	}

	public BridgeProcessing (MarkCrossReferencesArgs* args)
	{
		if (args == null) {
			throw new ArgumentNullException (nameof (args), "Cross references argument is a NULL pointer");
		}

		if (args->ComponentCount > 0 && args->Components == null) {
			throw new InvalidOperationException ("Components member of the cross references arguments structure is NULL");
		}

		if (args->CrossReferenceCount > 0 && args->CrossReferences == null) {
			throw new InvalidOperationException ("CrossReferences member of the cross references arguments structure is NULL");
		}

		crossRefs = args;
	}

	/// <summary>
	/// Main processing method - equivalent to BridgeProcessingShared::process()
	/// </summary>
	public void Process ()
	{
		PrepareForJavaCollection ();
		BridgeProcessingJniHelper.TriggerJavaGC ();
		CleanupAfterJavaCollection ();
		s_logger?.LogGcSummary (crossRefs);
	}

	/// <summary>
	/// Prepare objects for Java GC by setting up cross-references and converting to weak refs
	/// </summary>
	void PrepareForJavaCollection ()
	{
		// Before looking at xrefs, scan the SCCs. During collection, an SCC has to behave like a
		// single object. If the number of objects in the SCC is anything other than 1, the SCC
		// must be doctored to mimic that one-object nature.
		for (nuint i = 0; i < crossRefs->ComponentCount; i++) {
			ref StronglyConnectedComponent scc = ref crossRefs->Components[i];
			PrepareSccForJavaCollection (i, ref scc);
		}

		// Add the cross scc refs
		for (nuint i = 0; i < crossRefs->CrossReferenceCount; i++) {
			ref ComponentCrossReference xref = ref crossRefs->CrossReferences[i];
			AddCrossReference (xref.SourceGroupIndex, xref.DestinationGroupIndex);
		}

		// With cross references processed, the temporary peer list can be released
		foreach (var (_, peer) in temporaryPeers) {
			var reference = peer;
			JniObjectReference.Dispose (ref reference);
		}

		// Switch global to weak references
		for (nuint i = 0; i < crossRefs->ComponentCount; i++) {
			ref StronglyConnectedComponent scc = ref crossRefs->Components[i];
			for (nuint j = 0; j < scc.Count; j++) {
				HandleContext* context = (HandleContext*)scc.Contexts[j];
				Debug.Assert (context != null, "Context must not be null");

				TakeWeakGlobalRef (context);
			}
		}
	}

	void PrepareSccForJavaCollection (nuint sccIndex, ref StronglyConnectedComponent scc)
	{
		// Count == 0 case: Some SCCs might have no IGCUserPeers associated with them, so we must create one
		if (scc.Count == 0) {
			var newObject = s_GCUserPeerClass!.NewObject (s_GCUserPeerCtor!, null);
			temporaryPeers[sccIndex] = newObject;
			return;
		}

		// Count == 1 case: The SCC contains a single object, there is no need to do anything special.
		if (scc.Count == 1) {
			return;
		}

		// Count > 1 case: The SCC contains many objects which must be collected as one.
		// Solution: Make all objects within the SCC directly or indirectly reference each other
		AddCircularReferences (ref scc);
	}

	void AddCircularReferences (ref StronglyConnectedComponent scc)
	{
		nuint prevIndex = scc.Count - 1;
		for (nuint nextIndex = 0; nextIndex < scc.Count; nextIndex++) {
			HandleContext* prev = (HandleContext*)scc.Contexts[prevIndex];
			HandleContext* next = (HandleContext*)scc.Contexts[nextIndex];

			Debug.Assert (prev != null && prev->ControlBlock != null, "Previous context or control block is null");
			Debug.Assert (next != null && next->ControlBlock != null, "Next context or control block is null");

			var prevRef = new JniObjectReference (prev->ControlBlock->Handle, JniObjectReferenceType.Global);
			var nextRef = new JniObjectReference (next->ControlBlock->Handle, JniObjectReferenceType.Global);

			bool referenceAdded = BridgeProcessingJniHelper.AddReference (prevRef, nextRef, s_logger);
			if (!referenceAdded) {
				throw new InvalidOperationException ("Failed to add reference between objects in a strongly connected component");
			}

			prev->ControlBlock->RefsAdded = 1;
			prevIndex = nextIndex;
		}
	}

	void AddCrossReference (nuint sourceIndex, nuint destIndex)
	{
		var (fromRef, fromContextPtr) = SelectCrossReferenceTarget (sourceIndex);
		var (toRef, _) = SelectCrossReferenceTarget (destIndex);

		if (BridgeProcessingJniHelper.AddReference (fromRef, toRef, s_logger) && fromContextPtr != IntPtr.Zero) {
			HandleContext* fromContext = (HandleContext*)fromContextPtr;
			fromContext->ControlBlock->RefsAdded = 1;
		}
	}

	/// <summary>
	/// Selects the target for a cross-reference from an SCC.
	/// Returns the JNI reference and the handle context pointer (IntPtr.Zero for temporary peers).
	/// </summary>
	(JniObjectReference Reference, IntPtr ContextPtr) SelectCrossReferenceTarget (nuint sccIndex)
	{
		ref StronglyConnectedComponent scc = ref crossRefs->Components[sccIndex];

		if (scc.Count == 0) {
			if (!temporaryPeers.TryGetValue (sccIndex, out var tempPeer)) {
				throw new InvalidOperationException ("Temporary peer must be found in the map");
			}
			return (tempPeer, IntPtr.Zero);
		}

		HandleContext* context = (HandleContext*)scc.Contexts[0];
		Debug.Assert (context != null && context->ControlBlock != null, "SCC must have at least one valid context");

		var reference = new JniObjectReference (context->ControlBlock->Handle, JniObjectReferenceType.Global);
		return (reference, (IntPtr)context);
	}

	void TakeWeakGlobalRef (HandleContext* context)
	{
		Debug.Assert (context != null && context->ControlBlock != null, "Context or control block is null");
		Debug.Assert (context->ControlBlock->HandleType == (int)JniObjectReferenceType.Global, "Expected global reference type for handle");

		var handle = new JniObjectReference (context->ControlBlock->Handle, JniObjectReferenceType.Global);
		s_logger?.LogTakeWeakGlobalRef (handle);

		var weak = handle.NewWeakGlobalRef ();
		s_logger?.LogWeakGrefNew (handle, weak);

		context->ControlBlock->Handle = weak.Handle;
		context->ControlBlock->HandleType = (int)JniObjectReferenceType.WeakGlobal;

		// Delete the old global ref
		s_logger?.LogGrefDelete (handle);
		JniObjectReference.Dispose (ref handle);
	}

	void CleanupAfterJavaCollection ()
	{
		for (nuint i = 0; i < crossRefs->ComponentCount; i++) {
			ref StronglyConnectedComponent scc = ref crossRefs->Components[i];

			// Try to switch back to global refs to analyze what stayed alive
			for (nuint j = 0; j < scc.Count; j++) {
				HandleContext* context = (HandleContext*)scc.Contexts[j];
				Debug.Assert (context != null, "Context must not be null");

				TakeGlobalRef (context);
				ClearReferencesIfNeeded (context);
			}

			AssertAllCollectedOrAllAlive (ref scc);
		}
	}

	void TakeGlobalRef (HandleContext* context)
	{
		Debug.Assert (context != null && context->ControlBlock != null, "Context or control block is null");
		Debug.Assert (context->ControlBlock->HandleType == (int)JniObjectReferenceType.WeakGlobal, "Expected weak global reference type for handle");

		var weak = new JniObjectReference (context->ControlBlock->Handle, JniObjectReferenceType.WeakGlobal);
		var handle = weak.NewGlobalRef ();
		s_logger?.LogWeakToGref (weak, handle);

		// The weak reference might have been collected
		if (handle.Handle == IntPtr.Zero) {
			s_logger?.LogWeakRefCollected (weak);
		}

		context->ControlBlock->Handle = handle.Handle; // This may be null if collected
		context->ControlBlock->HandleType = (int)JniObjectReferenceType.Global;

		// Delete the old weak ref
		s_logger?.LogWeakRefDelete (weak);
		JniObjectReference.Dispose (ref weak);
	}

	void ClearReferencesIfNeeded (HandleContext* context)
	{
		if (IsCollected (context)) {
			return;
		}

		Debug.Assert (context->ControlBlock != null, "Control block must not be null");
		Debug.Assert (context->ControlBlock->Handle != IntPtr.Zero, "Control block handle must not be null");
		Debug.Assert (context->ControlBlock->HandleType == (int)JniObjectReferenceType.Global, "Control block handle type must be global reference");

		if (context->ControlBlock->RefsAdded == 0) {
			return;
		}

		var handle = new JniObjectReference (context->ControlBlock->Handle, JniObjectReferenceType.Global);
		ClearReferences (handle);
		context->ControlBlock->RefsAdded = 0;
	}

	void ClearReferences (JniObjectReference handle)
	{
		if (!handle.IsValid) {
			return;
		}

		// Try the optimized path for GCUserPeerable (NativeAOT)
		if (!RuntimeFeature.IsCoreClrRuntime) {
			if (BridgeProcessingJniHelper.TryClearManagedReferences (handle)) {
				return;
			}
		}

		// Fall back to reflection-based approach
		var javaClassRef = JniEnvironment.Types.GetObjectClass (handle);
		using var javaClass = new JniType (ref javaClassRef, JniObjectReferenceOptions.CopyAndDispose);

		JniMethodInfo clearMethod;
		try {
			clearMethod = javaClass.GetInstanceMethod ("monodroidClearReferences", "()V");
		} catch (Java.Lang.NoSuchMethodError) {
			s_logger?.LogMissingClearReferencesMethod (javaClass);
			return;
		}

		JniEnvironment.InstanceMethods.CallVoidMethod (handle, clearMethod, null);
	}

	static bool IsCollected (HandleContext* context)
	{
		Debug.Assert (context != null && context->ControlBlock != null, "Context or control block is null");
		return context->ControlBlock->Handle == IntPtr.Zero;
	}

	void AssertAllCollectedOrAllAlive (ref StronglyConnectedComponent scc)
	{
		if (scc.Count == 0) {
			return;
		}

		HandleContext* firstContext = (HandleContext*)scc.Contexts[0];
		Debug.Assert (firstContext != null, "Context must not be null");
		bool isCollected = IsCollected (firstContext);

		for (nuint j = 1; j < scc.Count; j++) {
			HandleContext* context = (HandleContext*)scc.Contexts[j];
			Debug.Assert (context != null, "Context must not be null");

			if (IsCollected (context) != isCollected) {
				throw new InvalidOperationException ("Cannot have a mix of collected and alive contexts in the SCC");
			}
		}
	}

	/// <summary>
	/// Internal representation of the JNI object reference control block.
	/// This must match the layout of Java.Interop.JniObjectReferenceControlBlock and the native struct.
	/// </summary>
	[StructLayout (LayoutKind.Sequential)]
	internal struct JniObjectReferenceControlBlock
	{
		public IntPtr Handle;
		public int HandleType;
		public IntPtr WeakHandle;
		public int RefsAdded;
	}

	/// <summary>
	/// Internal representation of the handle context passed from the GC bridge.
	/// This must match the layout of the native HandleContext struct.
	/// </summary>
	[StructLayout (LayoutKind.Sequential)]
	internal struct HandleContext
	{
		public int IdentityHashCode;
		public JniObjectReferenceControlBlock* ControlBlock;
	}
}
