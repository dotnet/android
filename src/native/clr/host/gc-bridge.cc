#include <host/gc-bridge.hh>
#include <host/os-bridge.hh>
#include <host/host.hh>
#include <runtime-base/util.hh>
#include <shared/helpers.hh>
#include <limits>

using namespace xamarin::android;

void GCBridge::initialize_on_load (JNIEnv *jniEnv) noexcept
{
	abort_if_invalid_pointer_argument (jniEnv, "jniEnv");

	jclass lref = jniEnv->FindClass ("java/lang/Runtime");
	abort_unless (lref != nullptr, "Failed to look up java/lang/Runtime class.");

	jmethodID Runtime_getRuntime = jniEnv->GetStaticMethodID (lref, "getRuntime", "()Ljava/lang/Runtime;");
	abort_unless (Runtime_getRuntime != nullptr, "Failed to look up the Runtime.getRuntime() method.");

	Runtime_gc = jniEnv->GetMethodID (lref, "gc", "()V");
	abort_unless (Runtime_gc != nullptr, "Failed to look up the Runtime.gc() method.");

	Runtime_instance = OSBridge::lref_to_gref (jniEnv, jniEnv->CallStaticObjectMethod (lref, Runtime_getRuntime));
	abort_unless (Runtime_instance != nullptr, "Failed to obtain Runtime instance.");

	jniEnv->DeleteLocalRef (lref);
}

void GCBridge::trigger_java_gc () noexcept
{
	env->CallVoidMethod (Runtime_instance, Runtime_gc);

	if (!env->ExceptionCheck ()) [[likely]] {
		return;
	}

	env->ExceptionDescribe ();
	env->ExceptionClear ();
	log_error (LOG_DEFAULT, "Java GC failed");
}

void GCBridge::prepare_for_java_collection () noexcept
{
	// Some SCCs might have no IGCUserPeers associated with them, so we must create one
	std::unordered_map<size_t, jobject> temporary_peers;

	// Before looking at xrefs, scan the SCCs. During collection, an SCC has to behave like a
	// single object. If the number of objects in the SCC is anything other than 1, the SCC
	// must be doctored to mimic that one-object nature.
	for (size_t i = 0; i < cross_refs.ComponentCount; i++) {
		const StronglyConnectedComponent *scc = &cross_refs.Components [i];

		// Count > 1 case: The SCC contains many objects which must be collected as one.
		// Solution: Make all objects within the SCC directly or indirectly reference each other
		if (scc->Count > 1) {
			add_references (*scc);
		} else if (scc->Count == 0) {
			temporary_peers [i] = env->NewObject (GCUserPeer_class, GCUserPeer_ctor);
		}
	}

	// Add the cross scc refs
	for (size_t i = 0; i < cross_refs.CrossReferenceCount; i++) {
		add_cross_reference (i, temporary_peers);
	}

	// With cross references processed, the temporary peer list can be released
	for (const auto& [scc, temporary_peer] : temporary_peers) {
		env->DeleteLocalRef (temporary_peer);
	}

	// Switch global to weak references
	for (size_t i = 0; i < cross_refs.ComponentCount; i++) {
		const StronglyConnectedComponent &scc = cross_refs.Components [i];
		for (size_t j = 0; j < scc.Count; j++) {
			take_weak_global_ref (scc.Contexts [j]);
		}
	}
}

jobject GCBridge::pick_representative (size_t scc_index, const StronglyConnectedComponent &scc, const std::unordered_map<size_t, jobject> &temporary_peers) noexcept
{
	if (scc.Count > 0) {
		abort_unless (scc.Contexts [0] != nullptr, "SCC must have at least one context");
		abort_unless (scc.Contexts [0]->control_block != nullptr, "SCC must have at least one context with valid control block");
		return scc.Contexts [0]->control_block->handle;
	} else {
		const auto temporary_peer = temporary_peers.find (scc_index);
		abort_unless (temporary_peer != temporary_peers.end(), "Temporary peer must be found in the map");
		return temporary_peer->second;
	}
}

void GCBridge::add_references (const StronglyConnectedComponent &scc) noexcept
{
	abort_unless (scc.Count > 1, "SCC must have at least two items to add inner references");

	JniObjectReferenceControlBlock *prev = scc.Contexts [scc.Count - 1]->control_block;

	for (size_t j = 1; j < scc.Count; j++) {
		JniObjectReferenceControlBlock *current = scc.Contexts [j]->control_block;
		if (add_reference (prev->handle, current->handle)) {
			prev->refs_added = 1;
		} else {
			// TODO
		}

		prev = current;
	}
}

void GCBridge::add_cross_reference (size_t xref_index, const std::unordered_map<size_t, jobject> &temporary_peers) noexcept
{
	const ComponentCrossReference &xref = cross_refs.CrossReferences [xref_index];

	const StronglyConnectedComponent &source = cross_refs.Components [xref.SourceGroupIndex];
	jobject from = pick_representative (xref.SourceGroupIndex, source, temporary_peers);

	const StronglyConnectedComponent &dest = cross_refs.Components [xref.DestinationGroupIndex];
	jobject to = pick_representative (xref.DestinationGroupIndex, dest, temporary_peers);

	if (add_reference (from, to) && source.Count > 0) {
		// If the source is a SCC with at least one item, we need to mark the first item it as having added a reference
		source.Contexts [0]->control_block->refs_added = 1;
	}
}

bool GCBridge::add_reference (jobject from, jobject to) noexcept
{
	jclass java_class = env->GetObjectClass (from);
	jmethodID add_method_id = env->GetMethodID (java_class, "monodroidAddReference", "(Ljava/lang/Object;)V");
	env->DeleteLocalRef (java_class);

	if (add_method_id != nullptr) {
		env->CallVoidMethod (from, add_method_id, to);
		return true;
	} else {
		env->ExceptionClear ();
		return false;
	}
}

void GCBridge::clear_references (jobject handle) noexcept
{
	// Clear references from the object
	jclass java_class = env->GetObjectClass (handle);
	jmethodID clear_method_id = env->GetMethodID (java_class, "monodroidClearReferences", "()V");
	env->DeleteLocalRef (java_class); // Clean up the local reference to the class

	if (clear_method_id != nullptr) {
		env->CallVoidMethod (handle, clear_method_id);
	} else {
		log_error (LOG_DEFAULT, "Failed to find monodroidClearReferences method");
		env->ExceptionClear ();
#if DEBUG
		if (Logger::gc_spew_enabled ()) {
			char *class_name = Host::get_java_class_name_for_TypeManager (java_class);
			log_error (LOG_GC, "Missing monodroidClearReferences method for object of class {}", class_name);
			free (class_name);
		}
#endif
	}
}

void GCBridge::take_global_ref (HandleContext *context) noexcept
{
	abort_if_invalid_pointer_argument (context, "context");
	abort_unless (context->control_block != nullptr, "Control block must not be null");
	if (context->control_block->handle_type != JNIWeakGlobalRefType) [[unlikely]] {
		log_error (LOG_DEFAULT, "Expected weak global reference type for handle, but got {} - handle: {:#x}", context->control_block->handle_type, reinterpret_cast<intptr_t> (context->control_block->handle));
		return;
	}
	abort_unless (context->control_block->handle_type == JNIWeakGlobalRefType, "Expected weak global reference type for handle");

	jobject weak = context->control_block->handle;
	jobject handle = env->NewGlobalRef (weak);
	
	// if (Logger::gref_log ()) [[unlikely]] {
	// 	OSBridge::_monodroid_gref_log (
	// 		std::format ("take_global_ref gchandle={:#x} -> wref={:#x} handle={:#x}\n"sv,
	// 			reinterpret_cast<intptr_t> (context->gc_handle),
	// 			reinterpret_cast<intptr_t> (weak),
	// 			reinterpret_cast<intptr_t> (handle)).data ());
	// }

	if (handle != nullptr) {
		context->control_block->handle = handle;
		context->control_block->handle_type = JNIGlobalRefType;

		OSBridge::_monodroid_gref_log_new (weak, OSBridge::get_object_ref_type (env, weak),
				handle, OSBridge::get_object_ref_type (env, handle),
				"finalizer", gettid (),
				"   at [[clr-gc:take_global_ref]]", 0);

		OSBridge::_monodroid_weak_gref_delete (weak, OSBridge::get_object_ref_type (env, weak),
			"finalizer", gettid (), "   at [[clr-gc:take_global_ref]]", 0);
		env->DeleteWeakGlobalRef (weak);
	} else {
		// The native memory of the control block will be freed in managed code as well as the weak global ref
		context->control_block = nullptr;

		// if (Logger::gc_spew_enabled ()) [[unlikely]] {
		// 	OSBridge::_monodroid_gref_log (
		// 		std::format ("handle {:#x}/W; was collected by a Java GC"sv, reinterpret_cast<intptr_t> (weak)).data ());
		// }
	}
}

void GCBridge::take_weak_global_ref (HandleContext *context) noexcept
{
	abort_if_invalid_pointer_argument (context, "context");
	abort_unless (context->control_block != nullptr, "Control block must not be null");
	abort_unless (context->control_block->handle_type == JNIGlobalRefType, "Expected global reference type for handle");

	jobject handle = context->control_block->handle;
	if (Logger::gref_log ()) [[unlikely]] {
		OSBridge::_monodroid_gref_log (
			std::format ("take_weak_global_ref gchandle={:#x}; handle={:#x}\n"sv,
				reinterpret_cast<intptr_t> (context->gc_handle),
				reinterpret_cast<intptr_t> (handle)).data ());
	}

	jobject weak = env->NewWeakGlobalRef (handle);
	OSBridge::_monodroid_weak_gref_new (handle, OSBridge::get_object_ref_type (env, handle),
			weak, OSBridge::get_object_ref_type (env, weak),
			"finalizer", gettid (), "   at [[clr-gc:take_weak_global_ref]]", 0);

	context->control_block->handle = weak;
	context->control_block->handle_type = JNIWeakGlobalRefType;

	log_error_fmt (LOG_GC, "take_weak_global_ref: gchandle={:#x} -> wref={:#x} handle={:#x} -> new type {}",
		reinterpret_cast<intptr_t> (context->gc_handle),
		reinterpret_cast<intptr_t> (weak),
		reinterpret_cast<intptr_t> (handle),
		context->control_block->handle_type);

	OSBridge::_monodroid_gref_log_delete (handle, OSBridge::get_object_ref_type (env, handle),
			"finalizer", gettid (), "   at [[clr-gc:take_weak_global_ref]]", 0);
	env->DeleteGlobalRef (handle);
}

void GCBridge::cleanup_after_java_collection () noexcept
{
#if DEBUG
	int total = 0;
	int alive = 0;
#endif

	// try to switch back to global refs to analyze what stayed alive
	for (size_t i = 0; i < cross_refs.ComponentCount; i++) {
		const StronglyConnectedComponent &scc = cross_refs.Components [i];
		for (size_t j = 0; j < scc.Count; j++) {
			take_global_ref (scc.Contexts [j]);
		}
	}

	// clear the cross references on any remaining items
	for (size_t i = 0; i < cross_refs.ComponentCount; i++) {
		const StronglyConnectedComponent &scc = cross_refs.Components [i];
		[[maybe_unused]] bool is_alive = cleanup_strongly_connected_component (i, scc);

#if DEBUG
		total += scc.Count;
		if (is_alive) {
			alive += scc.Count;
		}
#endif
	}

#if DEBUG
	log_info (LOG_GC, "GC cleanup summary: {} objects tested - resurrecting {}.", total, alive);
#endif
}

bool GCBridge::cleanup_strongly_connected_component (size_t i, const StronglyConnectedComponent &scc) noexcept
{
	// all contexts in the SCC must either be alive, or collected
	bool is_alive = false;

	for (size_t j = 0; j < scc.Count; j++) {
		abort_unless (scc.Contexts [j] != nullptr, "Context must not be null");
		JniObjectReferenceControlBlock *control_block = scc.Contexts [j]->control_block;

		if (control_block != nullptr) {
			if (j > 0) {
				abort_unless (is_alive, [&i] { return detail::_format_message ("Bridge SCC at index %d must be alive", i); });
			}

			is_alive = true;
			if (control_block->refs_added > 0) {
				clear_references (control_block->handle);
				control_block->refs_added = 0;
			}
		} else {
			abort_unless (!is_alive, [&i] { return detail::_format_message ("Bridge SCC at index %d must NOT be alive", i); });
		}
	}

	return is_alive;
}

void GCBridge::bridge_processing () noexcept
{
	abort_unless (bridge_processing_started_callback != nullptr, "GC bridge processing started callback is not set");
	abort_unless (bridge_processing_finished_callback != nullptr, "GC bridge processing finished callback is not set");

	env = OSBridge::ensure_jnienv ();

	while (true) {
		bridge_processing_semaphore.acquire ();
		std::unique_lock<std::shared_mutex> lock (processing_mutex);

		log_mark_cross_references_args_if_enabled ();

		bridge_processing_started_callback ();
		prepare_for_java_collection ();
		trigger_java_gc ();
		cleanup_after_java_collection ();
		bridge_processing_finished_callback (&cross_refs);
	}
}

void GCBridge::mark_cross_references (MarkCrossReferencesArgs *args) noexcept
{
	static int ctr = 0;
	log_error (LOG_DEFAULT, "mark_cross_references called for the {} time", ++ctr);
	
	abort_if_invalid_pointer_argument (args, "cross_refs");
	abort_unless (args->Components != nullptr || args->ComponentCount == 0, "Components must not be null if ComponentCount is greater than 0");
	abort_unless (args->CrossReferences != nullptr || args->CrossReferenceCount == 0, "CrossReferences must not be null if CrossReferenceCount is greater than 0");

	std::unique_lock<std::shared_mutex> lock (processing_mutex);

	log_error (LOG_DEFAULT, "setting cross_refs");
	cross_refs.ComponentCount = args->ComponentCount;
	cross_refs.Components = args->Components;
	cross_refs.CrossReferenceCount = args->CrossReferenceCount;
	cross_refs.CrossReferences = args->CrossReferences;

	bridge_processing_semaphore.release ();
}

void GCBridge::wait_for_bridge_processing () noexcept
{
	std::shared_lock<std::shared_mutex> lock (processing_mutex);
}

[[gnu::always_inline]]
void GCBridge::log_mark_cross_references_args_if_enabled () noexcept
{
	if (!Logger::gc_spew_enabled ()) [[likely]] {
		return;
	}

	log_info (LOG_GC, "cross references callback invoked with {} sccs and {} xrefs.", cross_refs.ComponentCount, cross_refs.CrossReferenceCount);

	for (size_t i = 0; i < cross_refs.ComponentCount; ++i) {
		log_info (LOG_GC, "group {} with {} objects", i, cross_refs.Components [i].Count);
		for (size_t j = 0; j < cross_refs.Components [i].Count; ++j) {
			HandleContext *ctx = cross_refs.Components [i].Contexts [j];
			abort_unless (ctx != nullptr, "Context must not be null");
			abort_unless (ctx->control_block != nullptr, "Control block must not be null");

			jobject handle = ctx->control_block->handle;
			jclass java_class = env->GetObjectClass (handle);
			if (java_class != nullptr) {
				char *class_name = Host::get_java_class_name_for_TypeManager (java_class);
				log_info (LOG_GC, "\tgchandle {:#x} gref {:#x} [{}]", reinterpret_cast<intptr_t> (ctx->gc_handle), reinterpret_cast<intptr_t> (handle), class_name);
				free (class_name);
			} else {
				log_info (LOG_GC, "\tgchandle {:#x} gref {:#x} [unknown class]", reinterpret_cast<intptr_t> (ctx->gc_handle), reinterpret_cast<intptr_t> (handle));
			}
		}
	}

	if (!Util::should_log (LOG_GC)) {
		return;
	}

	for (size_t i = 0; i < cross_refs.CrossReferenceCount; ++i) {
		size_t source_index = cross_refs.CrossReferences [i].SourceGroupIndex;
		size_t dest_index = cross_refs.CrossReferences [i].DestinationGroupIndex;
		log_info_nocheck_fmt (LOG_GC, "xref [{}] {} -> {}", i, source_index, dest_index);
	}
}
