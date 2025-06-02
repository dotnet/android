#include <host/gc-bridge.hh>
#include <host/os-bridge.hh>
#include <shared/helpers.hh>

using namespace xamarin::android;

void GCBridge::wait_for_bridge_processing () noexcept
{
	// log_write (LOG_DEFAULT, LogLevel::Info, "GCBridge: wait_for_bridge_processing - waiting to acquire processing mutex");
	std::shared_lock<std::shared_mutex> lock (processing_mutex);
	// log_write (LOG_DEFAULT, LogLevel::Info, "GCBridge: wait_for_bridge_processing - acquired processing mutex");
}

void GCBridge::initialize_on_load (JNIEnv *env) noexcept
{
	abort_if_invalid_pointer_argument (env, "env");

	jclass lref = env->FindClass ("java/lang/Runtime");
	jmethodID Runtime_getRuntime = env->GetStaticMethodID (lref, "getRuntime", "()Ljava/lang/Runtime;");
	Runtime_gc = env->GetMethodID (lref, "gc", "()V");
	Runtime_instance = OSBridge::lref_to_gref (env, env->CallStaticObjectMethod (lref, Runtime_getRuntime));
	env->DeleteLocalRef (lref);

	abort_unless (
		Runtime_gc != nullptr && Runtime_instance != nullptr,
		"Failed to look up Java GC runtime API."
	);
}

void GCBridge::trigger_java_gc () noexcept
{
	env->CallVoidMethod (Runtime_instance, Runtime_gc);
	if (env->ExceptionCheck ()) [[unlikely]] {
		env->ExceptionDescribe ();
		env->ExceptionClear ();
		log_error (LOG_DEFAULT, "Java GC failed");
	}
}

bool GCBridge::add_reference (HandleContext *from, jobject to) noexcept
{
	abort_if_invalid_pointer_argument (from, "from");
	abort_if_invalid_pointer_argument (to, "to");

	if (add_direct_reference (from->control_block->handle, to)) {
		from->control_block->refs_added++;
		return true;
	} else {
		return false;
	}
}

bool GCBridge::add_direct_reference (jobject from, jobject to) noexcept
{
	abort_if_invalid_pointer_argument (from, "from");
	abort_if_invalid_pointer_argument (to, "to");

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

static bool is_valid_gref (JniObjectReferenceControlBlock *control_block) noexcept
{
	return control_block != nullptr
		&& control_block->handle != nullptr
		&& control_block->handle_type == JNIGlobalRefType;
}

jobject GCBridge::get_scc_representative (StronglyConnectedComponent *scc, jobject temporary_peers) noexcept
{
	abort_if_invalid_pointer_argument (scc, "scc");

	if (scc->Count > 0) {
		return scc->Contexts [0]->control_block->handle; // Return the first valid global reference
	} else {
		abort_unless (temporary_peers != nullptr, "Temporary peers must not be null for bridgeless SCCs");

		int index = scc_get_stashed_temporary_peer_index (scc);
		return env->CallObjectMethod (temporary_peers, ArrayList_get, index);
	}
}

void GCBridge::maybe_release_scc_representative (StronglyConnectedComponent *scc, jobject handle) noexcept
{
	abort_if_invalid_pointer_argument (scc, "scc");

	if (scc->Count < 0) {
		env->DeleteLocalRef (handle); // Release the local ref for bridgeless SCCs returned from get_scc_representative
	}
}

void GCBridge::prepare_for_java_collection (MarkCrossReferencesArgs* cross_refs) noexcept
{
	// Some SCCs might have no IGCUserPeers associated with them, so we must create one
	jobject temporary_peers = nullptr; // This is an ArrayList
	int temporary_peer_count = 0;      // Number of items in temporary_peers

	// Before looking at xrefs, scan the SCCs. During collection, an SCC has to behave like a
	// single object. If the number of objects in the SCC is anything other than 1, the SCC
	// must be doctored to mimic that one-object nature.
	for (int i = 0; i < cross_refs->ComponentsLen; i++)
	{
		StronglyConnectedComponent *scc = &cross_refs->Components [i];

		// Count > 1 case: The SCC contains many objects which must be collected as one.
		// Solution: Make all objects within the SCC directly or indirectly reference each other
		if (scc->Count > 1) {
			HandleContext *first = scc->Contexts [0];
			HandleContext *prev = first;

			for (int j = 1; j < scc->Count; j++) {
				HandleContext *current = scc->Contexts [j];

				add_reference (prev, current->control_block->handle);
				prev = current;
			}

			add_reference (prev, first->control_block->handle);
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
			HandleContext *from = from_scc->Contexts [0];
			jobject to_handle = get_scc_representative (to_scc, temporary_peers);

			add_reference (from, to_handle);

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
			HandleContext *context = scc->Contexts [j];

			jobject handle = context->control_block->handle;
			
			// log_write_fmt (LOG_DEFAULT, LogLevel::Info,
			// 	"Switching global reference to weak for context {:x}, gchandle: {}, handle {:x}, refs added {}",
			// 	(intptr_t)context, (intptr_t)context->gc_handle, (intptr_t)handle, context->control_block->refs_added);
			
			jobject weak = env->NewWeakGlobalRef (handle);

			// log_write_fmt (LOG_DEFAULT, LogLevel::Info,
			// 	"Creating weak global ref for: gc_handle {}, handle {:x}, weak handle {:x}, refs {}",
			// 		(intptr_t)context->gc_handle,
			// 		(intptr_t)handle,
			// 		(intptr_t)weak,
			// 		context->control_block->refs_added);

			env->DeleteGlobalRef (handle); // Delete the strong reference now that we have a weak one

			context->control_block->handle = weak;
			context->control_block->handle_type = JNIWeakGlobalRefType;
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

void GCBridge::cleanup_after_java_collection (MarkCrossReferencesArgs* cross_refs) noexcept
{
	// try to switch back to global refs to analyze what stayed alive
	for (int i = 0; i < cross_refs->ComponentsLen; i++)
	{
		StronglyConnectedComponent *scc = &cross_refs->Components [i];

		for (int j = 0; j < scc->Count; j++) {
			HandleContext *context = scc->Contexts [j];

			jobject weak = context->control_block->handle;

			// TODO remove this log
			if (context->control_block->handle_type != JNIWeakGlobalRefType) {
				log_write_fmt (LOG_DEFAULT, LogLevel::Info, "Skipping non-weak global ref: {:x} ({}), gchandle: {} 0x{:x}", (intptr_t)weak, context->control_block->handle_type, (intptr_t)context->gc_handle, (intptr_t)context->gc_handle);
			}

			abort_unless (context->control_block->handle_type == JNIWeakGlobalRefType,
				"Expected weak global reference type for handle");
			jobject global = env->NewGlobalRef (weak);

			if (global) {
				// log_write_fmt (LOG_DEFAULT, LogLevel::Info, "Object survived Java GC: weak {:x} -> gref {:x}", (intptr_t)weak, (intptr_t)global);
				env->DeleteWeakGlobalRef (weak);

				context->control_block->handle = global;
				context->control_block->handle_type = JNIGlobalRefType;

				if (context->control_block->refs_added > 0) {
					// Clear references
					clear_references (context->control_block->handle);

					// Reset refs_added
					context->control_block->refs_added = 0;
				}
			} else {
				// Object was collected by Java GC
				context->is_collected = 1;

				// log_write_fmt (LOG_DEFAULT, LogLevel::Info,
				// 	"Object was collected by Java GC: weak {:x}, gchandle: {}, control block {:x}, handle type {}",
				// 	(intptr_t)weak, (intptr_t)context->gc_handle, (intptr_t)context->control_block, context->control_block->handle_type);
			}
		}
	}
}

void GCBridge::bridge_processing () noexcept
{
	abort_unless (bridge_processing_started_callback != nullptr, "GC bridge processing started callback is not set");
	abort_unless (bridge_processing_finished_callback != nullptr, "GC bridge processing finished callback is not set");

	env = OSBridge::ensure_jnienv ();

	while (true)
	{
		bridge_processing_semaphore.acquire ();
		std::unique_lock<std::shared_mutex> lock (processing_mutex);

		bridge_processing_started_callback ();
		prepare_for_java_collection (&GCBridge::shared_cross_refs);
		trigger_java_gc ();
		cleanup_after_java_collection (&GCBridge::shared_cross_refs);
		bridge_processing_finished_callback (&GCBridge::shared_cross_refs);
	}
}

static void assert_valid_handles (MarkCrossReferencesArgs* cross_refs) noexcept
{
	for (int i = 0; i < cross_refs->ComponentsLen; i++)
	{
		StronglyConnectedComponent *scc = &cross_refs->Components [i];
		for (int j = 0; j < scc->Count; j++) {
			HandleContext *context = scc->Contexts [j];
			if (!is_valid_gref (context->control_block)) {
				log_error_fmt (LOG_DEFAULT, "Invalid global reference in SCC context {:x}, gchandle: {}, control block {}, handle {}, handle type {}",
					(intptr_t)context, context->gc_handle, (intptr_t)context->control_block, (intptr_t)context->control_block->handle, context->control_block->handle_type);
			}
			abort_unless (is_valid_gref (context->control_block), "Invalid global reference in SCC");
		}
	}
}

void GCBridge::mark_cross_references (MarkCrossReferencesArgs* cross_refs) noexcept
{
	// TODO get rid of this
	assert_valid_handles (cross_refs);

	{
		std::unique_lock<std::shared_mutex> lock (processing_mutex);

		GCBridge::shared_cross_refs.ComponentsLen = cross_refs->ComponentsLen;
		GCBridge::shared_cross_refs.Components = cross_refs->Components;
		GCBridge::shared_cross_refs.CrossReferencesLen = cross_refs->CrossReferencesLen;
		GCBridge::shared_cross_refs.CrossReferences = cross_refs->CrossReferences;
	}

	bridge_processing_semaphore.release ();
}
