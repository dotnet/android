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

	// For NativeAOT: GCUserPeerable interface
	static JniType? s_GCUserPeerableClass;
	static JniMethodInfo? s_GCUserPeerable_jiAddManagedReference;
	static JniMethodInfo? s_GCUserPeerable_jiClearManagedReferences;

	// For triggering Java GC directly
	static JniObjectReference s_RuntimeInstance;
	static JniMethodInfo? s_Runtime_gc;

	// Is NativeAOT mode (uses GCUserPeerable instead of IGCUserPeer)
	static bool s_initialized;
	static bool s_isNativeAOT;

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
	/// Initialize cached Java references. Must be called once during runtime initialization.
	/// </summary>
	public static void Initialize (bool isNativeAOT)
	{
		if (s_initialized)
			return;

		s_isNativeAOT = isNativeAOT;

		// Initialize GCUserPeer class for creating temporary peers
		s_GCUserPeerClass = new JniType ("mono/android/GCUserPeer");
		s_GCUserPeerCtor = s_GCUserPeerClass.GetConstructor ("()V");

		if (isNativeAOT) {
			// For NativeAOT, we also use GCUserPeerable interface for optimized method calls
			s_GCUserPeerableClass = new JniType ("net/dot/jni/GCUserPeerable");
			s_GCUserPeerable_jiAddManagedReference = s_GCUserPeerableClass.GetInstanceMethod ("jiAddManagedReference", "(Ljava/lang/Object;)V");
			s_GCUserPeerable_jiClearManagedReferences = s_GCUserPeerableClass.GetInstanceMethod ("jiClearManagedReferences", "()V");
		}

		// Initialize Java Runtime for triggering GC
		using var runtimeClass = new JniType ("java/lang/Runtime");
		var getRuntimeMethod = runtimeClass.GetStaticMethod ("getRuntime", "()Ljava/lang/Runtime;");
		s_Runtime_gc = runtimeClass.GetInstanceMethod ("gc", "()V");
		var runtimeLocal = JniEnvironment.StaticMethods.CallStaticObjectMethod (runtimeClass.PeerReference, getRuntimeMethod, null);
		s_RuntimeInstance = runtimeLocal.NewGlobalRef ();
		JniObjectReference.Dispose (ref runtimeLocal);

		s_initialized = true;
	}

	/// <summary>
	/// Main processing method - equivalent to BridgeProcessingShared::process()
	/// </summary>
	public void Process ()
	{
		PrepareForJavaCollection ();
		TriggerJavaGC ();
		CleanupAfterJavaCollection ();
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
		foreach (var (_, temporaryPeer) in temporaryPeers) {
			var peer = temporaryPeer;
			JniObjectReference.Dispose (ref peer);
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

			bool referenceAdded = AddReference (prevRef, nextRef);
			if (!referenceAdded) {
				throw new InvalidOperationException ("Failed to add reference between objects in a strongly connected component");
			}

			prev->ControlBlock->RefsAdded = 1;
			prevIndex = nextIndex;
		}
	}

	void AddCrossReference (nuint sourceIndex, nuint destIndex)
	{
		CrossReferenceTarget from = SelectCrossReferenceTarget (sourceIndex);
		CrossReferenceTarget to = SelectCrossReferenceTarget (destIndex);

		if (AddReference (from.Reference, to.Reference)) {
			from.MarkRefsAddedIfNeeded ();
		}
	}

	readonly struct CrossReferenceTarget
	{
		public readonly JniObjectReference Reference;
		public readonly HandleContext* Context;
		public readonly bool IsTemporaryPeer;

		public CrossReferenceTarget (JniObjectReference reference, HandleContext* context, bool isTemporaryPeer)
		{
			Reference = reference;
			Context = context;
			IsTemporaryPeer = isTemporaryPeer;
		}

		public void MarkRefsAddedIfNeeded ()
		{
			if (IsTemporaryPeer || Context == null) {
				return;
			}
			Context->ControlBlock->RefsAdded = 1;
		}
	}

	CrossReferenceTarget SelectCrossReferenceTarget (nuint sccIndex)
	{
		ref StronglyConnectedComponent scc = ref crossRefs->Components[sccIndex];

		if (scc.Count == 0) {
			if (!temporaryPeers.TryGetValue (sccIndex, out var tempPeer)) {
				throw new InvalidOperationException ("Temporary peer must be found in the map");
			}
			return new CrossReferenceTarget (tempPeer, null, isTemporaryPeer: true);
		}

		HandleContext* context = (HandleContext*)scc.Contexts[0];
		Debug.Assert (context != null && context->ControlBlock != null, "SCC must have at least one valid context");

		var reference = new JniObjectReference (context->ControlBlock->Handle, JniObjectReferenceType.Global);
		return new CrossReferenceTarget (reference, context, isTemporaryPeer: false);
	}

	bool AddReference (JniObjectReference from, JniObjectReference to)
	{
		if (!from.IsValid || !to.IsValid) {
			return false;
		}

		// Try the optimized path for GCUserPeerable (NativeAOT)
		if (s_isNativeAOT && TryCallGCUserPeerableAddManagedReference (from, to)) {
			return true;
		}

		// Fall back to reflection-based approach
		var fromClassRef = JniEnvironment.Types.GetObjectClass (from);
		try {
			using var fromClass = new JniType (ref fromClassRef, JniObjectReferenceOptions.CopyAndDispose);
			JniMethodInfo addMethod;
			try {
				addMethod = fromClass.GetInstanceMethod ("monodroidAddReference", "(Ljava/lang/Object;)V");
			} catch (Java.Lang.NoSuchMethodError) {
				Logger.Log (LogLevel.Error, "monodroid-gc", "Failed to find monodroidAddReference method");
				return false;
			}

			JniArgumentValue* args = stackalloc JniArgumentValue[1];
			args[0] = new JniArgumentValue (to);
			JniEnvironment.InstanceMethods.CallVoidMethod (from, addMethod, args);
			return true;
		} catch {
			return false;
		}
	}

	bool TryCallGCUserPeerableAddManagedReference (JniObjectReference from, JniObjectReference to)
	{
		if (s_GCUserPeerableClass == null || s_GCUserPeerable_jiAddManagedReference == null) {
			return false;
		}

		if (!JniEnvironment.Types.IsInstanceOf (from, s_GCUserPeerableClass.PeerReference)) {
			return false;
		}

		JniArgumentValue* args = stackalloc JniArgumentValue[1];
		args[0] = new JniArgumentValue (to);
		JniEnvironment.InstanceMethods.CallVoidMethod (from, s_GCUserPeerable_jiAddManagedReference, args);
		return true;
	}

	/// <summary>
	/// Trigger Java garbage collection using the cached Runtime instance directly through JNI.
	/// </summary>
	static void TriggerJavaGC ()
	{
		if (s_Runtime_gc == null) {
			throw new InvalidOperationException ("BridgeProcessing.Initialize must be called before TriggerJavaGC");
		}

		try {
			JniEnvironment.InstanceMethods.CallVoidMethod (s_RuntimeInstance, s_Runtime_gc, null);
		} catch (Exception ex) {
			Logger.Log (LogLevel.Error, "monodroid-gc", $"Java GC failed: {ex.Message}");
		}
	}

	void TakeWeakGlobalRef (HandleContext* context)
	{
		Debug.Assert (context != null && context->ControlBlock != null, "Context or control block is null");
		Debug.Assert (context->ControlBlock->HandleType == (int)JniObjectReferenceType.Global, "Expected global reference type for handle");

		var handle = new JniObjectReference (context->ControlBlock->Handle, JniObjectReferenceType.Global);
		var weak = handle.NewWeakGlobalRef ();

		context->ControlBlock->Handle = weak.Handle;
		context->ControlBlock->HandleType = (int)JniObjectReferenceType.WeakGlobal;

		// Delete the old global ref
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

		// The weak reference might have been collected
		context->ControlBlock->Handle = handle.Handle; // This may be null if collected
		context->ControlBlock->HandleType = (int)JniObjectReferenceType.Global;

		// Delete the old weak ref
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
		if (s_isNativeAOT && TryCallGCUserPeerableClearManagedReferences (handle)) {
			return;
		}

		// Fall back to reflection-based approach
		var javaClassRef = JniEnvironment.Types.GetObjectClass (handle);
		try {
			using var javaClass = new JniType (ref javaClassRef, JniObjectReferenceOptions.CopyAndDispose);
			JniMethodInfo clearMethod;
			try {
				clearMethod = javaClass.GetInstanceMethod ("monodroidClearReferences", "()V");
			} catch (Java.Lang.NoSuchMethodError) {
				Logger.Log (LogLevel.Error, "monodroid-gc", "Failed to find monodroidClearReferences method");
				return;
			}

			JniEnvironment.InstanceMethods.CallVoidMethod (handle, clearMethod, null);
		} catch {
			// Ignore exceptions during cleanup
		}
	}

	bool TryCallGCUserPeerableClearManagedReferences (JniObjectReference handle)
	{
		if (s_GCUserPeerableClass == null || s_GCUserPeerable_jiClearManagedReferences == null) {
			return false;
		}

		if (!JniEnvironment.Types.IsInstanceOf (handle, s_GCUserPeerableClass.PeerReference)) {
			return false;
		}

		JniEnvironment.InstanceMethods.CallVoidMethod (handle, s_GCUserPeerable_jiClearManagedReferences, null);
		return true;
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
