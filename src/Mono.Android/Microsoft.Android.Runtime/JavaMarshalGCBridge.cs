using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Java;
using System.Threading;
using Android.Runtime;
using Java.Interop;

namespace Microsoft.Android.Runtime;

/// <summary>
/// Processes GC bridges between managed and Java peers.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Initialize"/> performs process-global, one-shot GC-bridge initialization
/// through the native initializer supplied in <c>JNIEnvInit.JnienvInitializeArgs</c>.
/// The native bridge starts a detached processing thread and aborts the process if it is
/// initialized more than once. <see cref="Initialize"/> is idempotent: subsequent calls
/// return immediately. It must be called after the <c>JniRuntime</c> has been created and
/// published, because bridge processing uses its JNI reference manager.
/// </para>
/// <para>
/// The GC-bridge registration lives for the entire lifetime of the process and is never torn
/// down: stopping the detached bridge-processing thread is not supported by the runtime.
/// </para>
/// </remarks>
static class JavaMarshalGCBridge
{
	static readonly object initializeLock = new ();
	static bool initialized;
	static int bridgeProcessingGeneration;
	static readonly Lazy<JavaMarshalGCBridgeJni> jni = new (CreateJni);

	internal static int BridgeProcessingGeneration => Volatile.Read (ref bridgeProcessingGeneration);

	static JavaMarshalGCBridgeJni BridgeJni
		=> jni.Value;

	static JavaMarshalGCBridgeJni CreateJni ()
	{
		ManagedObjectReferenceManager.BeginGCBridgeReferenceOperation (GCBridgeReferenceOperation.Initialize);
		try {
			return new JavaMarshalGCBridgeJni ();
		} finally {
			ManagedObjectReferenceManager.EndGCBridgeReferenceOperation ();
		}
	}

	/// <safety>
	/// <paramref name="args"/> must contain the native host's process-lifetime initializer with the declared
	/// unmanaged ABI. The JNI runtime must already be published; its bridge graphs remain live for each callback.
	/// The supplied initializer must not have been invoked outside this one-shot initialization path.
	/// Retain its native callback and all CLR-supplied descriptor/array storage for each full callback, including
	/// failure. Initialization can start the native processing thread before a later managed operation throws;
	/// an exception does not undo that registration or authorize releasing its code/storage.
	/// </safety>
	internal static unsafe void Initialize (JNIEnvInit.JnienvInitializeArgs args)
	{
		lock (initializeLock) {
			if (initialized)
				return;

			unsafe {
				// SAFETY: the host retains the initializer and ProcessBridge for the process lifetime; the lock and guard prevent duplicate initialization.
				var initializeGCBridge = args.initializeGCBridge;
				ArgumentNullException.ThrowIfNull (initializeGCBridge);

				if (RuntimeFeature.EventSourceSupport) {
					RuntimeEventSource.Initialize ();
				}

				var mark_cross_references_ftn = initializeGCBridge (&ProcessBridge);
				if (mark_cross_references_ftn == null)
					throw new InvalidOperationException ("The native GC bridge initializer did not return a bridge callback.");
				JavaMarshal.Initialize (mark_cross_references_ftn);
			}

			initialized = true;
		}
	}

	internal static bool RequiresTemporaryPeer (nuint componentCount)
		=> componentCount == 0;

	[UnmanagedCallersOnly]
	static void ProcessBridge (MarkCrossReferencesArgs* args)
	{
		unsafe {
			// SAFETY: the registered CLR callback supplies args and all counted arrays until FinishCrossReferenceProcessing; identity checks do not validate their native bounds, and failures terminate the process.
			try {
				ArgumentNullException.ThrowIfNull (args);
				bool gcBridgeEventEnabled = RuntimeFeature.EventSourceSupport && RuntimeEventSource.GCBridgeStart ();
				HandleContext.EnsureAllContextsAreOurs (args);
				Process (args);
				ReadOnlySpan<GCHandle> handlesToFree = ProcessCollectedContexts (args);

				JavaMarshal.FinishCrossReferenceProcessing (args, handlesToFree);

				Interlocked.Increment (ref bridgeProcessingGeneration);
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
	}

	static ReadOnlySpan<GCHandle> ProcessCollectedContexts (MarkCrossReferencesArgs* mcr)
	{
		unsafe {
			// SAFETY: the CLR retains mcr's counted components and contexts for this callback; collected contexts are queued for later free, after their associated handles are passed back to the CLR.
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
	}

	static void Process (MarkCrossReferencesArgs* args)
	{
		unsafe {
			// SAFETY: the CLR owns args and its arrays throughout this synchronous prepare/GC/cleanup sequence; validation checks null/count consistency, not arbitrary native allocation provenance.
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
	}

	static void ValidateArguments (MarkCrossReferencesArgs* args)
	{
		unsafe {
			// SAFETY: args is the CLR-supplied readable descriptor; these null/count checks do not dereference its component arrays or establish their bounds.
			if (args->ComponentCount > 0 && args->Components == null)
				throw new InvalidOperationException ("Components must not be null when ComponentCount is greater than zero.");
			if (args->CrossReferenceCount > 0 && args->CrossReferences == null)
				throw new InvalidOperationException ("CrossReferences must not be null when CrossReferenceCount is greater than zero.");
		}
	}

	static void PrepareForJavaCollection (MarkCrossReferencesArgs* args)
	{
		unsafe {
			// SAFETY: the CLR retains the graph for its declared counts; references are linked before each existing global is replaced with a weak global.
			PrepareComponentsAndCrossReferences (args);

			for (nuint i = 0; i < args->ComponentCount; i++) {
				StronglyConnectedComponent component = args->Components [i];
				for (nuint j = 0; j < component.Count; j++) {
					TakeWeakGlobalReference (GetContext (component, j));
				}
			}
		}
	}

	static void PrepareComponentsAndCrossReferences (MarkCrossReferencesArgs* args)
	{
		unsafe {
			// SAFETY: component and cross-reference arrays are CLR-owned for this callback; TemporaryPeerMap owns its extra JNI locals until the using scope exits, including on failure.
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
	}

	static void AddCircularReferences (StronglyConnectedComponent component)
	{
		unsafe {
			// SAFETY: only components with Count > 1 reach this path; previousIndex and nextIndex stay within the CLR-provided context array while the cycle is linked.
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
	}

	static void AddCrossReference (
			MarkCrossReferencesArgs* args,
			nuint sourceIndex,
			nuint destinationIndex,
			TemporaryPeerMap temporaryPeers)
	{
		unsafe {
			// SAFETY: SelectCrossReferenceTarget checks each component index before access; source/destination references and temporary peers remain owned by this bridge callback.
			var source = SelectCrossReferenceTarget (args, sourceIndex, temporaryPeers);
			var destination = SelectCrossReferenceTarget (args, destinationIndex, temporaryPeers);
			if (BridgeJni.AddReference (source.Reference, destination.Reference, source.IsGCUserPeerKnown)) {
				source.MarkRefsAdded ();
			}
		}
	}

	static CrossReferenceTarget SelectCrossReferenceTarget (
			MarkCrossReferencesArgs* args,
			nuint componentIndex,
			TemporaryPeerMap temporaryPeers)
	{
		unsafe {
			// SAFETY: componentIndex is checked before array access; the CLR retains its components and TemporaryPeerMap retains any borrowed temporary peer.
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
	}

	static void CleanupAfterJavaCollection (MarkCrossReferencesArgs* args)
	{
		unsafe {
			// SAFETY: the CLR retains the counted contexts while weak references are promoted, links cleared and component liveness compared; no context is freed here.
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
	}

	static void ClearReferencesIfNeeded (HandleContext* context)
	{
		unsafe {
			// SAFETY: context remains registered during bridge processing; only a live global reference with added links is passed to monodroidClearReferences.()V.
			if (context->IsCollected)
				return;
			if (context->PeerReference.Type != JniObjectReferenceType.Global)
				throw new InvalidOperationException ("A live GC bridge peer must have a global reference.");
			if (context->RefsAdded == 0)
				return;

			BridgeJni.ClearReferences (context->PeerReference);
			context->RefsAdded = 0;
		}
	}

	static void TakeWeakGlobalReference (HandleContext* context)
	{
		unsafe {
			// SAFETY: the registered context owns global; weak is created before global is released, and failure to create a valid weak reference terminates rather than publishing it.
			JniObjectReference global = context->PeerReference;
			if (global.Type != JniObjectReferenceType.Global)
				throw new InvalidOperationException ("Expected a global reference before Java GC bridge processing.");

			ManagedObjectReferenceManager.BeginGCBridgeReferenceOperation (GCBridgeReferenceOperation.GlobalToWeak);
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
			ManagedObjectReferenceManager.BeginGCBridgeReferenceOperation (GCBridgeReferenceOperation.GlobalToWeak);
			try {
				JniObjectReference.Dispose (ref global);
			} finally {
				ManagedObjectReferenceManager.EndGCBridgeReferenceOperation ();
			}
		}
	}

	static void TakeGlobalReference (HandleContext* context)
	{
		unsafe {
			// SAFETY: the registered context owns weak; promotion supplies global or the collected/null result before weak is released, and unexpected JNI failure terminates.
			JniObjectReference weak = context->PeerReference;
			if (weak.Type != JniObjectReferenceType.WeakGlobal)
				throw new InvalidOperationException ("Expected a weak global reference after Java GC bridge processing.");

			ManagedObjectReferenceManager.BeginGCBridgeReferenceOperation (GCBridgeReferenceOperation.WeakToGlobal);
			JniObjectReference global;
			try {
				global = weak.NewGlobalRef ();
			} finally {
				ManagedObjectReferenceManager.EndGCBridgeReferenceOperation ();
			}

			if (!global.IsValid) {
				FailFastOnPendingJavaException ("Failed to promote a weak global reference during GC bridge processing.");
				if (RuntimeFeature.EventSourceSupport) {
					RuntimeEventSource.WeakGlobalReferenceCollected (weak.Handle);
				}
			}

			context->SetPeerReference (global);
			ManagedObjectReferenceManager.BeginGCBridgeReferenceOperation (GCBridgeReferenceOperation.WeakToGlobal);
			try {
				JniObjectReference.Dispose (ref weak);
			} finally {
				ManagedObjectReferenceManager.EndGCBridgeReferenceOperation ();
			}
		}
	}

	static void EnsureAllCollectedOrAllAlive (StronglyConnectedComponent component)
	{
		unsafe {
			// SAFETY: the CLR retains component.Contexts for Count entries; zero count returns before access, and the loop checks every remaining context against the first.
			if (component.Count == 0)
				return;

			bool collected = GetContext (component, 0)->IsCollected;
			for (nuint i = 1; i < component.Count; i++) {
				if (GetContext (component, i)->IsCollected != collected)
					throw new InvalidOperationException ("Cannot have a mix of collected and alive contexts in a strongly connected component.");
			}
		}
	}

	static HandleContext* GetContext (StronglyConnectedComponent component, nuint index)
	{
		unsafe {
			// SAFETY: callers bound index by component.Count in the CLR-owned context array; the checks reject null arrays/elements but cannot validate arbitrary pointer extents.
			if (component.Contexts == null)
				throw new InvalidOperationException ("A non-empty component must have contexts.");
			var context = (HandleContext*) component.Contexts [index];
			if (context == null)
				throw new InvalidOperationException ("A GC bridge context must not be null.");
			return context;
		}
	}

	static void FailFastOnPendingJavaException (string message)
	{
		IntPtr exception = JNIEnv.ExceptionOccurred ();
		if (exception == IntPtr.Zero)
			return;

		JNIEnv.ExceptionDescribe ();
		JNIEnv.ExceptionClear ();
		unsafe {
			// SAFETY: ExceptionOccurred returned this owned local throwable reference, used only before its release.
			JNIEnv.DeleteLocalRef (exception);
		}
		Environment.FailFast (message);
	}

	static void LogArguments (MarkCrossReferencesArgs* args)
	{
		unsafe {
			// SAFETY: the CLR retains args and its counted arrays during diagnostic iteration; class-name lookup borrows only references reported live by their registered contexts.
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
	}

	static void LogSummary (MarkCrossReferencesArgs* args)
	{
		unsafe {
			// SAFETY: the CLR retains args and its counted contexts during this summary; only IsCollected is read before the callback releases or queues contexts.
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
	}

	readonly struct CrossReferenceTarget
	{
		readonly HandleContext* context;

		public CrossReferenceTarget (JniObjectReference temporaryPeer)
		{
			unsafe {
				// SAFETY: only a null context address is stored here; Reference borrows the TemporaryPeerMap-owned local until that map is disposed.
				Reference = temporaryPeer;
				context = null;
				IsGCUserPeerKnown = true;
			}
		}

		public CrossReferenceTarget (HandleContext* context)
		{
			unsafe {
				// SAFETY: context comes from the current CLR graph and remains registered; Reference snapshots its peer without taking ownership or extending its lifetime.
				this.context = context;
				Reference = context->PeerReference;
				IsGCUserPeerKnown = context->RefsAdded != 0;
			}
		}

		public JniObjectReference Reference { get; }
		public bool IsGCUserPeerKnown { get; }

		public void MarkRefsAdded ()
		{
			unsafe {
				// SAFETY: a nonnull context is still registered for this bridge callback; only its refs_added marker is written, and temporary-peer targets have no context to update.
				if (context != null)
					context->RefsAdded = 1;
			}
		}
	}

	struct TemporaryPeerMap : IDisposable
	{
		const int LocalReferenceSlack = 16;

		readonly JniObjectReference[] peers;

		public TemporaryPeerMap (MarkCrossReferencesArgs* args)
		{
			unsafe {
				// SAFETY: ProcessBridge rejects null args and Process validates the component array/count pair;
				// the CLR retains that storage through this callback, and checked conversion bounds the managed peers array.
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
