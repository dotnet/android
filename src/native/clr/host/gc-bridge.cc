#include <host/gc-bridge.hh>
#include <host/os-bridge.hh>
#include <shared/helpers.hh>
#include <limits>

using namespace xamarin::android;

void GCBridge::wait_for_bridge_processing () noexcept
{
	std::shared_lock<std::shared_mutex> lock (processing_mutex);
}

void GCBridge::initialize_on_load (JNIEnv *jniEnv) noexcept
{
	abort_if_invalid_pointer_argument (jniEnv, "jniEnv");

	jclass lref = jniEnv->FindClass ("java/lang/Runtime");
	jmethodID Runtime_getRuntime = jniEnv->GetStaticMethodID (lref, "getRuntime", "()Ljava/lang/Runtime;");
	Runtime_gc = jniEnv->GetMethodID (lref, "gc", "()V");
	Runtime_instance = OSBridge::lref_to_gref (jniEnv, jniEnv->CallStaticObjectMethod (lref, Runtime_getRuntime));
	jniEnv->DeleteLocalRef (lref);

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

void GCBridge::add_inner_reference (HandleContext *from, HandleContext *to) noexcept
{
	abort_if_invalid_pointer_argument (from, "from");
	abort_if_invalid_pointer_argument (to, "to");

	if (add_reference (from->control_block->handle, to->control_block->handle)) {
		from->control_block->refs_added++;
	}
}

void GCBridge::add_cross_reference (GCBridge::CrossReferenceComponent from, GCBridge::CrossReferenceComponent to) noexcept
{
	jobject from_object;
	if (from.is_bridgeless_scc) {
		from_object = from.target;
	} else {
		from_object = from.handle_context->control_block->handle;
	}

	jobject to_object;
	if (to.is_bridgeless_scc) {
		to_object = to.target;
	} else {
		to_object = to.handle_context->control_block->handle;
	}

	if (add_reference (from_object, to_object) && !from.is_bridgeless_scc) {
		from.handle_context->control_block->refs_added++;
	}
}

bool GCBridge::add_reference (jobject from, jobject to) noexcept
{
	jclass java_class = env->GetObjectClass (from);
	jmethodID add_method_id = env->GetMethodID (java_class, "monodroidAddReference", "(Ljava/lang/Object;)V");
	env->DeleteLocalRef (java_class);

	if (add_method_id) {
		env->CallVoidMethod (from, add_method_id, to);
		return true;
	} else {
		env->ExceptionClear ();
		return false;
	}
}

int GCBridge::scc_get_stashed_temporary_peer_index (StronglyConnectedComponent *scc) noexcept
{
	abort_if_invalid_pointer_argument (scc, "scc");
	abort_unless (scc->Count < 0, "Attempted to load stashed index from an object which does not contain one.");
	abort_unless (scc->Count >= static_cast<ssize_t> (std::numeric_limits<int>::min ()), "Count cannot fit in an int.");

	return static_cast<int> (-scc->Count - 1);
}

void GCBridge::scc_set_stashed_temporary_peer_index (StronglyConnectedComponent *scc, ssize_t index) noexcept
{
	abort_if_invalid_pointer_argument (scc, "scc");
	abort_unless (index >= 0, "Index must be non-negative");

	scc->Count = -index - 1;
}

bool GCBridge::is_bridgeless_scc (StronglyConnectedComponent *scc) noexcept
{
	abort_if_invalid_pointer_argument (scc, "scc");
	return scc->Count < 0; // If Count is negative, it's a bridgeless SCC
}

GCBridge::CrossReferenceComponent GCBridge::get_target (StronglyConnectedComponent *scc, jobject temporary_peers) noexcept
{
	abort_if_invalid_pointer_argument (scc, "scc");

	if (is_bridgeless_scc (scc)) {
		abort_unless (temporary_peers != nullptr, "Temporary peers must not be null for bridgeless SCCs");

		int index = scc_get_stashed_temporary_peer_index (scc);
		jobject target = env->CallObjectMethod (temporary_peers, ArrayList_get, index);

		return GCBridge::CrossReferenceComponent {
			.is_bridgeless_scc = true,
			.target = target,
		};
	} else {
		return GCBridge::CrossReferenceComponent {
			.is_bridgeless_scc = false,
			.handle_context = scc->Contexts [0],
		};
	}
}

void GCBridge::release_target (GCBridge::CrossReferenceComponent target) noexcept
{
	if (target.is_bridgeless_scc) {
		// Release the local ref for bridgeless SCCs returned from get_scc_representative
		env->DeleteLocalRef (target.target);
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
	for (size_t i = 0; i < cross_refs->ComponentCount; i++)
	{
		StronglyConnectedComponent *scc = &cross_refs->Components [i];

		abort_unless (scc->Count >= 0,
			"Attempted to prepare for GC bridge processing where the number of strongly connected components is negative (likely due to overflow of ssize_t).");

		// Count > 1 case: The SCC contains many objects which must be collected as one.
		// Solution: Make all objects within the SCC directly or indirectly reference each other
		if (scc->Count > 1) {
			HandleContext *first = scc->Contexts [0];
			HandleContext *prev = first;

			for (ssize_t j = 1; j < scc->Count; j++) {
				HandleContext *current = scc->Contexts [j];
				add_inner_reference (prev, current);
				prev = current;
			}

			add_inner_reference (prev, first);
		} else if (scc->Count == 0) {
			ensure_array_list ();
			
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
	for (size_t i = 0; i < cross_refs->CrossReferenceCount; i++)
	{
		ComponentCrossReference *xref = &cross_refs->CrossReferences [i];
		
		GCBridge::CrossReferenceComponent from = get_target (&cross_refs->Components [xref->SourceGroupIndex], temporary_peers);
		GCBridge::CrossReferenceComponent to = get_target (&cross_refs->Components [xref->DestinationGroupIndex], temporary_peers);

		add_cross_reference (from, to);

		release_target (from);
		release_target (to);
	}

	// With xrefs processed, the temporary peer list can be released
	env->DeleteLocalRef (temporary_peers);

	// Switch global to weak references
	for (size_t i = 0; i < cross_refs->ComponentCount; i++)
	{
		StronglyConnectedComponent *scc = &cross_refs->Components [i];

		if (scc->Count < 0) {
			scc->Count = 0; // Reset any temporary peer index stored in Count
		}

		for (ssize_t j = 0; j < scc->Count; j++) {
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
	for (size_t i = 0; i < cross_refs->ComponentCount; i++)
	{
		StronglyConnectedComponent *scc = &cross_refs->Components [i];

		for (ssize_t j = 0; j < scc->Count; j++) {
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

void GCBridge::mark_cross_references (MarkCrossReferencesArgs* cross_refs) noexcept
{
	std::unique_lock<std::shared_mutex> lock (processing_mutex);

	GCBridge::shared_cross_refs.ComponentCount = cross_refs->ComponentCount;
	GCBridge::shared_cross_refs.Components = cross_refs->Components;
	GCBridge::shared_cross_refs.CrossReferenceCount = cross_refs->CrossReferenceCount;
	GCBridge::shared_cross_refs.CrossReferences = cross_refs->CrossReferences;

	bridge_processing_semaphore.release ();
}

[[gnu::always_inline]]
void GCBridge::ensure_array_list () noexcept
{
	if (ArrayList_class == nullptr) [[unlikely]] {
		ArrayList_class = env->FindClass ("java/util/ArrayList");
		abort_unless (ArrayList_class != nullptr, "Failed to find java/util/ArrayList class");

		ArrayList_ctor = env->GetMethodID (ArrayList_class, "<init>", "()V");
		abort_unless (ArrayList_ctor != nullptr, "Failed to find ArrayList constructor");
		
		ArrayList_get = env->GetMethodID (ArrayList_class, "get", "(I)Ljava/lang/Object;");
		abort_unless (ArrayList_get != nullptr, "Failed to find ArrayList get method");
		
		ArrayList_add = env->GetMethodID (ArrayList_class, "add", "(Ljava/lang/Object;)Z");
		abort_unless (ArrayList_add != nullptr, "Failed to find ArrayList add method");
	}
}
