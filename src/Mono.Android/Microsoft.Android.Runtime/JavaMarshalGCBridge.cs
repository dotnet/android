using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Java;
using Android.Runtime;
using Java.Interop;

namespace Microsoft.Android.Runtime;

static unsafe class JavaMarshalGCBridge
{
	const string WeakToGlobalStackTrace = "   at [[clr-gc:take_global_ref]]";
	const string GlobalToWeakStackTrace = "   at [[clr-gc:take_weak_global_ref]]";

	static readonly Lazy<JavaMarshalGCBridgeJni> jni = new (CreateJni);

	static JavaMarshalGCBridgeJni BridgeJni
		=> jni.Value;

	static JavaMarshalGCBridgeJni CreateJni ()
	{
		ManagedObjectReferenceManager.BeginGCBridgeReferenceOperation (
			RuntimeFeature.ObjectReferenceLogging ? "   at [[gc-bridge:initialize]]" : null);
		try {
			return new JavaMarshalGCBridgeJni ();
		} finally {
			ManagedObjectReferenceManager.EndGCBridgeReferenceOperation ();
		}
	}

	internal static delegate* unmanaged<MarkCrossReferencesArgs*, void> Initialize ()
	{
		return RuntimeNativeMethods.clr_initialize_gc_bridge (&ProcessBridge);
	}

	internal static bool RequiresTemporaryPeer (nuint componentCount)
		=> componentCount == 0;

	[UnmanagedCallersOnly]
	static void ProcessBridge (MarkCrossReferencesArgs* args)
	{
		try {
			ArgumentNullException.ThrowIfNull (args);
			bool gcBridgeEventEnabled = RuntimeFeature.EventSourceSupport && RuntimeEventSource.GCBridgeStart ();
			HandleContext.EnsureAllContextsAreOurs (args);
			Process (args);
			ReadOnlySpan<GCHandle> handlesToFree = ProcessCollectedContexts (args);

			JavaMarshal.FinishCrossReferenceProcessing (args, handlesToFree);

			AndroidRuntimeInternal.NotifyBridgeProcessingFinished ();
			// Keep the switch here so ILLink removes the EventSource when it is disabled.
			if (RuntimeFeature.EventSourceSupport) {
				if (gcBridgeEventEnabled) {
					RuntimeEventSource.GCBridgeStop ();
				}
			}
		} catch (Exception e) {
			Environment.FailFast ("Managed GC bridge processing failed.", e);
		}
	}

	static ReadOnlySpan<GCHandle> ProcessCollectedContexts (MarkCrossReferencesArgs* mcr)
	{
		List<GCHandle> handlesToFree = [];

		for (int i = 0; (nuint)i < mcr->ComponentCount; i++) {
			StronglyConnectedComponent component = mcr->Components [i];
			for (int j = 0; (nuint)j < component.Count; j++) {
				ProcessContext ((HandleContext*)component.Contexts [j]);
			}
		}

		void ProcessContext (HandleContext* context)
		{
			if (context == null) {
				throw new ArgumentNullException (nameof (context), "HandleContext should never be null.");
			}

			// Ignore contexts which were not collected
			if (!context->IsCollected) {
				return;
			}

			GCHandle handle = HandleContext.GetAssociatedGCHandle (context);

			// Note: modifying the RegisteredInstances dictionary while processing the collected contexts
			// is tricky and can lead to deadlocks, so we remember which contexts were collected and we will free
			// them later outside of the bridge processing loop.
			JavaMarshalRegisteredPeers.QueueCollectedContext ((IntPtr)context);

			// important: we must not free the handle before passing it to JavaMarshal.FinishCrossReferenceProcessing
			handlesToFree.Add (handle);
		}

		return CollectionsMarshal.AsSpan (handlesToFree);
	}

	static void Process (MarkCrossReferencesArgs* args)
	{
		ValidateArguments (args);
		if (RuntimeFeature.GCBridgeLogging) {
			LogArguments (args);
		}
		PrepareForJavaCollection (args);
		BridgeJni.TriggerJavaGC ();
		CleanupAfterJavaCollection (args);
		if (RuntimeFeature.GCBridgeLogging) {
			LogSummary (args);
		}
	}

	static void ValidateArguments (MarkCrossReferencesArgs* args)
	{
		if (args->ComponentCount > 0 && args->Components == null)
			throw new InvalidOperationException ("Components must not be null when ComponentCount is greater than zero.");
		if (args->CrossReferenceCount > 0 && args->CrossReferences == null)
			throw new InvalidOperationException ("CrossReferences must not be null when CrossReferenceCount is greater than zero.");
	}

	static void PrepareForJavaCollection (MarkCrossReferencesArgs* args)
	{
		PrepareComponentsAndCrossReferences (args);

		for (nuint i = 0; i < args->ComponentCount; i++) {
			StronglyConnectedComponent component = args->Components [i];
			for (nuint j = 0; j < component.Count; j++) {
				TakeWeakGlobalReference (GetContext (component, j));
			}
		}
	}

	static void PrepareComponentsAndCrossReferences (MarkCrossReferencesArgs* args)
	{
		using var temporaryPeers = new TemporaryPeerMap (args);

		for (nuint i = 0; i < args->ComponentCount; i++) {
			StronglyConnectedComponent component = args->Components [i];
			if (RequiresTemporaryPeer (component.Count)) {
				temporaryPeers.Add (i);
			} else if (component.Count > 1) {
				AddCircularReferences (component);
			}
		}

		for (nuint i = 0; i < args->CrossReferenceCount; i++) {
			ComponentCrossReference crossReference = args->CrossReferences [i];
			AddCrossReference (
				args,
				crossReference.SourceGroupIndex,
				crossReference.DestinationGroupIndex,
				temporaryPeers);
		}
	}

	static void AddCircularReferences (StronglyConnectedComponent component)
	{
		nuint previousIndex = component.Count - 1;
		for (nuint nextIndex = 0; nextIndex < component.Count; nextIndex++) {
			var previous = GetContext (component, previousIndex);
			var next = GetContext (component, nextIndex);
			if (!BridgeJni.AddReference (previous->PeerReference, next->PeerReference, knownGCUserPeer: false)) {
				throw new InvalidOperationException ("Failed to add a reference between objects in a strongly connected component.");
			}
			previous->RefsAdded = 1;
			previousIndex = nextIndex;
		}
	}

	static void AddCrossReference (
			MarkCrossReferencesArgs* args,
			nuint sourceIndex,
			nuint destinationIndex,
			TemporaryPeerMap temporaryPeers)
	{
		var source = SelectCrossReferenceTarget (args, sourceIndex, temporaryPeers);
		var destination = SelectCrossReferenceTarget (args, destinationIndex, temporaryPeers);
		if (BridgeJni.AddReference (source.Reference, destination.Reference, source.IsGCUserPeerKnown)) {
			source.MarkRefsAdded ();
		}
	}

	static CrossReferenceTarget SelectCrossReferenceTarget (
			MarkCrossReferencesArgs* args,
			nuint componentIndex,
			TemporaryPeerMap temporaryPeers)
	{
		if (componentIndex >= args->ComponentCount)
			throw new InvalidOperationException ("A GC bridge cross-reference component index is out of range.");

		JniObjectReference temporaryPeer = temporaryPeers.Get (componentIndex);
		if (temporaryPeer.IsValid)
			return new CrossReferenceTarget (temporaryPeer);

		StronglyConnectedComponent component = args->Components [componentIndex];
		if (component.Count == 0)
			throw new InvalidOperationException ("An empty component does not have a temporary peer.");
		return new CrossReferenceTarget (GetContext (component, 0));
	}

	static void CleanupAfterJavaCollection (MarkCrossReferencesArgs* args)
	{
		for (nuint i = 0; i < args->ComponentCount; i++) {
			StronglyConnectedComponent component = args->Components [i];
			for (nuint j = 0; j < component.Count; j++) {
				var context = GetContext (component, j);
				TakeGlobalReference (context);
				ClearReferencesIfNeeded (context);
			}
			EnsureAllCollectedOrAllAlive (component);
		}
	}

	static void ClearReferencesIfNeeded (HandleContext* context)
	{
		if (context->IsCollected)
			return;
		if (context->PeerReference.Type != JniObjectReferenceType.Global)
			throw new InvalidOperationException ("A live GC bridge peer must have a global reference.");
		if (context->RefsAdded == 0)
			return;

		BridgeJni.ClearReferences (context->PeerReference);
		context->RefsAdded = 0;
	}

	static void TakeWeakGlobalReference (HandleContext* context)
	{
		JniObjectReference global = context->PeerReference;
		if (global.Type != JniObjectReferenceType.Global)
			throw new InvalidOperationException ("Expected a global reference before Java GC bridge processing.");

		if (RuntimeFeature.ObjectReferenceLogging && Logger.LogGlobalRef) {
			WriteReferenceDiagnostic ("take_weak_global_ref handle=0x{0:x}", unchecked((nuint)global.Handle));
		}
		ManagedObjectReferenceManager.BeginGCBridgeReferenceOperation (
			RuntimeFeature.ObjectReferenceLogging ? GlobalToWeakStackTrace : null);
		JniObjectReference weak;
		try {
			weak = global.NewWeakGlobalRef ();
		} finally {
			ManagedObjectReferenceManager.EndGCBridgeReferenceOperation ();
		}
		if (!weak.IsValid) {
			FailFastOnPendingJavaException ("Failed to create a weak global reference during GC bridge processing.");
			Environment.FailFast ("Failed to create a weak global reference during GC bridge processing.");
		}

		context->SetPeerReference (weak);
		ManagedObjectReferenceManager.BeginGCBridgeReferenceOperation (
			RuntimeFeature.ObjectReferenceLogging ? GlobalToWeakStackTrace : null);
		try {
			JniObjectReference.Dispose (ref global);
		} finally {
			ManagedObjectReferenceManager.EndGCBridgeReferenceOperation ();
		}
	}

	static void TakeGlobalReference (HandleContext* context)
	{
		JniObjectReference weak = context->PeerReference;
		if (weak.Type != JniObjectReferenceType.WeakGlobal)
			throw new InvalidOperationException ("Expected a weak global reference after Java GC bridge processing.");

		ManagedObjectReferenceManager.BeginGCBridgeReferenceOperation (
			RuntimeFeature.ObjectReferenceLogging ? WeakToGlobalStackTrace : null);
		JniObjectReference global;
		try {
			global = weak.NewGlobalRef ();
		} finally {
			ManagedObjectReferenceManager.EndGCBridgeReferenceOperation ();
		}

		if (!global.IsValid) {
			FailFastOnPendingJavaException ("Failed to promote a weak global reference during GC bridge processing.");
			if (RuntimeFeature.ObjectReferenceLogging && Logger.LogGlobalRef) {
				WriteReferenceDiagnostic ("handle 0x{0:x}/W; was collected by a Java GC", unchecked((nuint)weak.Handle));
			}
		}

		context->SetPeerReference (global);
		if (RuntimeFeature.ObjectReferenceLogging && Logger.LogGlobalRef) {
			WriteReferenceDiagnostic (
				"take_global_ref wref=0x{0:x} -> handle=0x{1:x}",
				unchecked((nuint)weak.Handle),
				unchecked((nuint)global.Handle));
		}

		ManagedObjectReferenceManager.BeginGCBridgeReferenceOperation (
			RuntimeFeature.ObjectReferenceLogging ? WeakToGlobalStackTrace : null);
		try {
			JniObjectReference.Dispose (ref weak);
		} finally {
			ManagedObjectReferenceManager.EndGCBridgeReferenceOperation ();
		}
	}

	static void EnsureAllCollectedOrAllAlive (StronglyConnectedComponent component)
	{
		if (component.Count == 0)
			return;

		bool collected = GetContext (component, 0)->IsCollected;
		for (nuint i = 1; i < component.Count; i++) {
			if (GetContext (component, i)->IsCollected != collected)
				throw new InvalidOperationException ("Cannot have a mix of collected and alive contexts in a strongly connected component.");
		}
	}

	static HandleContext* GetContext (StronglyConnectedComponent component, nuint index)
	{
		if (component.Contexts == null)
			throw new InvalidOperationException ("A non-empty component must have contexts.");
		var context = (HandleContext*) component.Contexts [index];
		if (context == null)
			throw new InvalidOperationException ("A GC bridge context must not be null.");
		return context;
	}

	static void FailFastOnPendingJavaException (string message)
	{
		IntPtr exception = JNIEnv.ExceptionOccurred ();
		if (exception == IntPtr.Zero)
			return;

		JNIEnv.ExceptionDescribe ();
		JNIEnv.ExceptionClear ();
		JNIEnv.DeleteLocalRef (exception);
		Environment.FailFast (message);
	}

	static void WriteReferenceDiagnostic (string format, params object?[] args)
	{
		if (RuntimeFeature.ObjectReferenceLogging) {
			var manager = JniEnvironment.Runtime.ObjectReferenceManager;
			if (manager.LogGlobalReferenceMessages) {
				manager.WriteGlobalReferenceLine (format, args);
			}
		}
	}

	static void LogArguments (MarkCrossReferencesArgs* args)
	{
		if (!Logger.LogGC)
			return;

		Logger.Log (
			LogLevel.Info,
			"monodroid-gc",
			$"cross references callback invoked with {args->ComponentCount} sccs and {args->CrossReferenceCount} xrefs.");

		for (nuint i = 0; i < args->ComponentCount; i++) {
			StronglyConnectedComponent component = args->Components [i];
			Logger.Log (LogLevel.Info, "monodroid-gc", $"group {i} with {component.Count} objects");
			for (nuint j = 0; j < component.Count; j++) {
				JniObjectReference reference = GetContext (component, j)->PeerReference;
				string? className = reference.IsValid ? JniEnvironment.Types.GetJniTypeNameFromInstance (reference) : null;
				Logger.Log (
					LogLevel.Info,
					"monodroid-gc",
					string.Create (
						CultureInfo.InvariantCulture,
						$"gref 0x{unchecked((nuint)reference.Handle):x} [{className ?? "unknown class"}]"));
			}
		}

		for (nuint i = 0; i < args->CrossReferenceCount; i++) {
			ComponentCrossReference crossReference = args->CrossReferences [i];
			Logger.Log (
				LogLevel.Info,
				"monodroid-gc",
				$"xref [{i}] {crossReference.SourceGroupIndex} -> {crossReference.DestinationGroupIndex}");
		}
	}

	static void LogSummary (MarkCrossReferencesArgs* args)
	{
		if (!Logger.LogGC)
			return;

		nuint total = 0;
		nuint alive = 0;
		for (nuint i = 0; i < args->ComponentCount; i++) {
			StronglyConnectedComponent component = args->Components [i];
			for (nuint j = 0; j < component.Count; j++) {
				total++;
				if (!GetContext (component, j)->IsCollected)
					alive++;
			}
		}
		Logger.Log (LogLevel.Info, "monodroid-gc", $"GC cleanup summary: {total} objects tested - resurrecting {alive}.");
	}

	readonly struct CrossReferenceTarget
	{
		readonly HandleContext* context;

		public CrossReferenceTarget (JniObjectReference temporaryPeer)
		{
			Reference = temporaryPeer;
			context = null;
			IsGCUserPeerKnown = true;
		}

		public CrossReferenceTarget (HandleContext* context)
		{
			this.context = context;
			Reference = context->PeerReference;
			IsGCUserPeerKnown = context->RefsAdded != 0;
		}

		public JniObjectReference Reference { get; }
		public bool IsGCUserPeerKnown { get; }

		public void MarkRefsAdded ()
		{
			if (context != null)
				context->RefsAdded = 1;
		}
	}

	struct TemporaryPeerMap : IDisposable
	{
		const int LocalReferenceSlack = 16;

		readonly JniObjectReference[] peers;

		public TemporaryPeerMap (MarkCrossReferencesArgs* args)
		{
			int count = checked((int)args->ComponentCount);
			int temporaryPeerCount = 0;
			for (nuint i = 0; i < args->ComponentCount; i++) {
				if (RequiresTemporaryPeer (args->Components [i].Count))
					temporaryPeerCount++;
			}
			if (temporaryPeerCount == 0) {
				peers = [];
				return;
			}

			peers = new JniObjectReference [count];

			try {
				JniEnvironment.References.EnsureLocalCapacity (checked(temporaryPeerCount + LocalReferenceSlack));
			} catch (Exception e) when (e is JavaException || e is InvalidOperationException) {
				Logger.Log (
					LogLevel.Warn,
					"monodroid-gc",
					$"Failed to reserve JNI local reference capacity for {temporaryPeerCount} temporary peers: {e.Message}");
			}
		}

		public void Add (nuint componentIndex)
		{
			int index = checked((int)componentIndex);
			if (peers [index].IsValid)
				throw new InvalidOperationException ("A temporary peer already exists for this component.");

			JniObjectReference peer = BridgeJni.CreateTemporaryPeer ();
			if (!peer.IsValid)
				throw new InvalidOperationException ("Failed to create a temporary peer during GC bridge processing.");
			peers [index] = peer;
		}

		public JniObjectReference Get (nuint componentIndex)
		{
			return peers.Length == 0 ? default : peers [checked((int)componentIndex)];
		}

		public void Dispose ()
		{
			for (int i = 0; i < peers.Length; i++) {
				JniObjectReference.Dispose (ref peers [i]);
			}
		}
	}
}
