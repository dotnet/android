#include <host/gc-bridge.hh>
#include <host/os-bridge.hh>
#include <shared/helpers.hh>

using namespace xamarin::android;

void GCBridge::wait_for_bridge_processing () noexcept
{
	log_write (LOG_DEFAULT, LogLevel::Info, "GCBridge: wait_for_bridge_processing...");
	std::shared_lock<std::shared_mutex> lock (processing_mutex);
	log_write (LOG_DEFAULT, LogLevel::Info, "GCBridge: wait_for_bridge_processing done");
}

void GCBridge::initialize_on_load (JNIEnv *env) noexcept
{
	// abort_if_invalid_pointer_argument ("env", env);

	log_write (LOG_DEFAULT, LogLevel::Info, "Initializing GC bridge");

	jclass lref = env->FindClass ("java/lang/Runtime");
	jmethodID Runtime_getRuntime = env->GetStaticMethodID (lref, "getRuntime", "()Ljava/lang/Runtime;");
	Runtime_gc = env->GetMethodID (lref, "gc", "()V");
	Runtime_instance = OSBridge::lref_to_gref (env, env->CallStaticObjectMethod (lref, Runtime_getRuntime));
	env->DeleteLocalRef (lref);

	abort_unless (
		Runtime_gc != nullptr && Runtime_instance != nullptr,
		"Failed to look up Java GC runtime API."
	);

	log_write (LOG_DEFAULT, LogLevel::Info, "Initialized GC bridge");
}

void GCBridge::trigger_java_gc () noexcept
{
	log_write (LOG_DEFAULT, LogLevel::Info, "Triggering Java GC");

	env->CallVoidMethod (Runtime_instance, Runtime_gc);

	if (env->ExceptionCheck ()) [[unlikely]] {
		env->ExceptionDescribe ();
		env->ExceptionClear ();
		log_error (LOG_DEFAULT, "Java GC failed");
	} else {
		log_write (LOG_DEFAULT, LogLevel::Info, "Java GC triggered");
	}
}

bool GCBridge::add_reference (JniObjectReferenceControlBlock *from, jobject to) noexcept
{
	if (add_direct_reference (from->handle, to)) {
		from->refs_added++;
		return true;
	} else {
		return false;
	}
}

bool GCBridge::add_direct_reference (jobject from, jobject to) noexcept
{
	jclass java_class = env->GetObjectClass (from);
	jmethodID add_method_id = env->GetMethodID (java_class, "monodroidAddReference", "(Ljava/lang/Object;)V");

	env->DeleteLocalRef (java_class); // TODO do I also need to delete the jmethodID? or that's not necessary?
	if (add_method_id) {
		env->CallVoidMethod (from, add_method_id, to);
		return true;
	}

	env->ExceptionClear ();
	return false;
}

int GCBridge::scc_get_stashed_temporary_peer_index (StronglyConnectedComponent *scc) noexcept
{
	abort_if_invalid_pointer_argument (scc, "scc");
	abort_unless (scc->Count < 0, "Attempted to load stashed index from an object which does not contain one.");

	return -scc->Count - 1;
}

void GCBridge::scc_set_stashed_temporary_peer_index (StronglyConnectedComponent *scc, int index) noexcept
{
	abort_if_invalid_pointer_argument (scc, "scc");
	scc->Count = -index - 1; // Store the index as a negative value
}

bool GCBridge::is_bridgeless_scc (StronglyConnectedComponent *scc) noexcept
{
	abort_if_invalid_pointer_argument (scc, "scc");
	return scc->Count < 0; // If Count is negative, it's a bridgeless SCC
}

jobject GCBridge::get_scc_representative (StronglyConnectedComponent *scc, jobject temporary_peers) noexcept
{
	abort_if_invalid_pointer_argument (scc, "scc");

	if (scc->Count > 0) {
		return scc->ContextMemory [0]->handle; // Return the first context's handle
	} else {
		abort_unless (temporary_peers != nullptr, "Temporary peers must not be null for bridgeless SCCs");

		int index = scc_get_stashed_temporary_peer_index (scc);
		return env->CallObjectMethod (temporary_peers, ArrayList_get, index);
	}
}

void GCBridge::maybe_release_scc_representative (StronglyConnectedComponent *scc, jobject handle) noexcept
{
	abort_if_invalid_pointer_argument (scc, "scc");
	abort_if_invalid_pointer_argument (handle, "handle");

	if (scc->Count < 0) {
		env->DeleteLocalRef (handle); // Release the local ref for bridgeless SCCs returned from get_scc_representative
	}
}

void GCBridge::prepare_for_java_collection (MarkCrossReferences* cross_refs) noexcept
{
	log_write_fmt (LOG_DEFAULT, LogLevel::Info, "GCBridge::prepare_for_java_collection called: {} components", cross_refs->ComponentsLen);

	// Some SCCs might have no IGCUserPeers associated with them, so we must create one
	jobject temporary_peers = nullptr; // This is an ArrayList
	int temporary_peer_count = 0;      // Number of items in temporary_peers

	// Before looking at xrefs, scan the SCCs. During collection, an SCC has to behave like a
	// single object. If the number of objects in the SCC is anything other than 1, the SCC
	// must be doctored to mimic that one-object nature.
	for (int i = 0; i < cross_refs->ComponentsLen; i++)
	{
		StronglyConnectedComponent *scc = &cross_refs->Components [i];

		log_write_fmt (LOG_DEFAULT, LogLevel::Info,
			"Processing SCC at index {} with {} objects", i, scc->Count);

		// Count > 1 case: The SCC contains many objects which must be collected as one.
		// Solution: Make all objects within the SCC directly or indirectly reference each other
		if (scc->Count > 1) {
			JniObjectReferenceControlBlock *first = scc->ContextMemory [0];
			JniObjectReferenceControlBlock *prev = first;

			for (int j = 1; j < scc->Count; j++) {
				JniObjectReferenceControlBlock *current = scc->ContextMemory [j];

				// TODO are those handles correct? in the mono GC bridge, there is a more complex logic that gets the handles
				// from the context block data... maybe in that case the handles are GCHandles?
				add_reference (prev, current->handle);
				prev = current;
			}

			// ref the first from the final
			add_reference (prev, first->handle);
		} else if (scc->Count == 0) {
			log_write_fmt (LOG_DEFAULT, LogLevel::Info,
				"Creating temporary peer for SCC at index {} with no bridge objects", i);

			// Once per process boot, look up JNI metadata we need to make temporary objects
			if (ArrayList_class == nullptr) {
				ArrayList_class = reinterpret_cast<jclass> (OSBridge::lref_to_gref (env, env->FindClass ("java/util/ArrayList")));
				ArrayList_ctor = env->GetMethodID (ArrayList_class, "<init>", "()V");
				ArrayList_add = env->GetMethodID (ArrayList_class, "add", "(Ljava/lang/Object;)Z");
				ArrayList_get = env->GetMethodID (ArrayList_class, "get", "(I)Ljava/lang/Object;");

				abort_unless (
					ArrayList_class != nullptr && ArrayList_ctor != nullptr && ArrayList_get != nullptr,
					"Failed to load classes required for JNI"
				);
			}

			// Once per prepare_for_java_collection call, create a list to hold the temporary
			// objects we create. This will protect them from collection while we build the list.
			if (!temporary_peers) {
				temporary_peers = env->NewObject (ArrayList_class, ArrayList_ctor);
			}

			// Create this SCC's temporary object
			jobject peer = env->NewObject (GCUserPeer_class, GCUserPeer_ctor);
			env->CallBooleanMethod (temporary_peers, ArrayList_add, peer);
			env->DeleteLocalRef (peer);

			scc_set_stashed_temporary_peer_index (scc, temporary_peer_count);
			temporary_peer_count++;
		}
	}

	// Add the cross scc refs
	for (int i = 0; i < cross_refs->CrossReferencesLen; i++)
	{
		ComponentCrossReference *xref = &cross_refs->CrossReferences [i];
		log_write_fmt (LOG_DEFAULT, LogLevel::Info,
			"Processing cross-reference at index {}: {} -> {}", i, xref->SourceGroupIndex, xref->DestinationGroupIndex);

		StronglyConnectedComponent *from_scc = &cross_refs->Components [xref->SourceGroupIndex];
		StronglyConnectedComponent *to_scc = &cross_refs->Components [xref->DestinationGroupIndex];

		if (is_bridgeless_scc (from_scc)) {
			jobject from_handle = get_scc_representative (from_scc, temporary_peers);
			jobject to_handle = get_scc_representative (to_scc, temporary_peers);

			add_direct_reference (from_handle, to_handle);

			maybe_release_scc_representative (from_scc, from_handle);
			maybe_release_scc_representative (to_scc, to_handle);
		} else {
			JniObjectReferenceControlBlock *from_cb = from_scc->ContextMemory [0];
			jobject to_handle = get_scc_representative (to_scc, temporary_peers);

			// log_write_fmt (LOG_DEFAULT, LogLevel::Info,
			// 	"Adding cross-reference from SCC {} to SCC {}: {} -> {}",
			// 	xref->SourceGroupIndex, xref->DestinationGroupIndex, from_handle, to_handle);
			add_reference (from_cb, to_handle);

			maybe_release_scc_representative (to_scc, to_handle);
		}
	}

	// With xrefs processed, the temporary peer list can be released
	env->DeleteLocalRef (temporary_peers);

	// Switch global to weak references
	for (int i = 0; i < cross_refs->ComponentsLen; i++)
	{
		StronglyConnectedComponent *scc = &cross_refs->Components [i];

		if (scc->Count < 0) {
			scc->Count = 0; // Reset any temporary peer index stored in Count
		}

		for (int j = 0; j < scc->Count; j++) {
			JniObjectReferenceControlBlock *context = scc->ContextMemory [j];

			log_write_fmt (LOG_DEFAULT, LogLevel::Info,
				"Creating weak global ref for: handle {}, type {}, weak handle {}, refs {}",
					(intptr_t)context->handle,
					context->handle_type,
					(intptr_t)context->weak_handle,
					context->refs_added);

			jobject handle = context->handle;
			jobject weak = env->NewWeakGlobalRef (handle);

			// log_write_fmt (LOG_DEFAULT, LogLevel::Info,
			// 	"Creating weak global ref for handle {:p} in context {:p}: weak {:p}",
			// 	handle, context, weak);

			env->DeleteGlobalRef (handle); // Delete the strong reference after creating the weak one

			context->handle = weak;
			context->handle_type = JNIWeakGlobalRefType;
		}
	}
}

void GCBridge::clear_references (jobject handle) noexcept
{
	// Clear references from the object
	jclass java_class = env->GetObjectClass (handle);
	jmethodID clear_method_id = env->GetMethodID (java_class, "monodroidClearReferences", "()V");

	if (clear_method_id) {
		env->CallVoidMethod (handle, clear_method_id);
	} else {
		log_error (LOG_DEFAULT, "Failed to find monodroidClearReferences method");
	}

	env->DeleteLocalRef (java_class); // Clean up the local reference to the class
}

void GCBridge::cleanup_after_java_collection (MarkCrossReferences* cross_refs) noexcept
{
	// try to switch back to global refs to analyze what stayed alive
	for (int i = 0; i < cross_refs->ComponentsLen; i++)
	{
		StronglyConnectedComponent *scc = &cross_refs->Components [i];

		for (int j = 0; j < scc->Count; j++) {
			JniObjectReferenceControlBlock *context = scc->ContextMemory [j];

			jobject weak = context->handle;
			jobject global = env->NewGlobalRef (weak);

			if (global) {
				// Object survived Java GC, so we need to update the handle
				// log_write_fmt (LOG_DEFAULT, LogLevel::Info,
				// 	"Object survived Java GC: handle {:p}, weak {:p}, global {:p}",
				// 	context->handle, weak, global);

				context->handle = global;
				context->handle_type = JNIGlobalRefType;

				if (context->refs_added > 0) {
					// Clear references
					clear_references (context->handle);

					// Reset refs_added
					context->refs_added = 0;
				}
			} else {
				// Object was collected by Java GC
				// log_write_fmt (LOG_DEFAULT, LogLevel::Info,
				// 	"Object was collected by Java GC: weak {:p}, handle {:p}",
				// 	weak, context->handle);

				context->handle = nullptr;
				context->handle_type = JNIInvalidRefType;
			}

			env->DeleteWeakGlobalRef (weak);
		}
	}
}

void GCBridge::bridge_processing () noexcept
{
	env = OSBridge::ensure_jnienv ();

	while (true)
	{
		log_write (LOG_DEFAULT, LogLevel::Info, "GCBridge: waiting for semaphore...");
		bridge_processing_semaphore.acquire ();
		log_write (LOG_DEFAULT, LogLevel::Info, "GCBridge: processing started");
		bridge_processing_started_callback ();
		log_write (LOG_DEFAULT, LogLevel::Info, "GCBridge: taking unique lock...");
		std::unique_lock<std::shared_mutex> lock (processing_mutex);
		log_write (LOG_DEFAULT, LogLevel::Info, "GCBridge: got unique lock");
		MarkCrossReferences cross_refs = GCBridge::shared_cross_refs;

		intptr_t gchandles = collect_gchandles_callback (&cross_refs);

		log_write (LOG_DEFAULT, LogLevel::Info, "GCBridge: prepare_for_java_collection");
		prepare_for_java_collection (&cross_refs);
		log_write (LOG_DEFAULT, LogLevel::Info, "GCBridge: trigger_java_gc");
		trigger_java_gc ();
		log_write (LOG_DEFAULT, LogLevel::Info, "GCBridge: cleanup_after_java_collection");
		cleanup_after_java_collection (&cross_refs);
		log_write (LOG_DEFAULT, LogLevel::Info, "GCBridge: bridge_processing_finished_callback");
		bridge_processing_finished_callback (&cross_refs, gchandles);
		log_write (LOG_DEFAULT, LogLevel::Info, "GCBridge: done processing");
	}
}

void GCBridge::mark_cross_references (MarkCrossReferences* cross_refs) noexcept
{
	log_write (LOG_DEFAULT, LogLevel::Info, "GCBridge: mark cross references");

	for (int i = 0; i < cross_refs->ComponentsLen; i++)
	{
		StronglyConnectedComponent *scc = &cross_refs->Components [i];
		log_write_fmt (LOG_DEFAULT, LogLevel::Info,
			"Processing SCC at index {} with {} objects", i, scc->Count);

		for (int j = 0; j < scc->Count; j++) {
			JniObjectReferenceControlBlock *context = scc->ContextMemory [j];
			log_write_fmt (LOG_DEFAULT, LogLevel::Info,
				"Context at index {}: handle {}, type {}, weak handle {}, refs {}",
					j, (intptr_t)context->handle, context->handle_type,
					(intptr_t)context->weak_handle, context->refs_added);
		}
	}

	GCBridge::shared_cross_refs.ComponentsLen = cross_refs->ComponentsLen;
	GCBridge::shared_cross_refs.Components = cross_refs->Components;
	GCBridge::shared_cross_refs.CrossReferencesLen = cross_refs->CrossReferencesLen;
	GCBridge::shared_cross_refs.CrossReferences = cross_refs->CrossReferences;

	log_write (LOG_DEFAULT, LogLevel::Info, "GCBridge: set semaphore");
	bridge_processing_semaphore.release ();
	log_write (LOG_DEFAULT, LogLevel::Info, "GCBridge: exitting mark_cross_references");
}
