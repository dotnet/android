#include <host/gc-bridge.hh>
#include <host/os-bridge.hh>
#include <host/host.hh>
#include <runtime-base/util.hh>
#include <shared/helpers.hh>
#include <limits>
#include <unordered_map>

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
	if (env->ExceptionCheck ()) [[unlikely]] {
		env->ExceptionDescribe ();
		env->ExceptionClear ();
		log_error (LOG_DEFAULT, "Java GC failed");
	}
}

void GCBridge::prepare_for_java_collection (MarkCrossReferencesArgs* cross_refs) noexcept
{
	// Some SCCs might have no IGCUserPeers associated with them, so we must create one
	std::unordered_map<StronglyConnectedComponent*, jobject> temporary_peers;

	// Before looking at xrefs, scan the SCCs. During collection, an SCC has to behave like a
	// single object. If the number of objects in the SCC is anything other than 1, the SCC
	// must be doctored to mimic that one-object nature.
	for (size_t i = 0; i < cross_refs->ComponentCount; i++) {
		StronglyConnectedComponent *scc = &cross_refs->Components [i];

		// Count > 1 case: The SCC contains many objects which must be collected as one.
		// Solution: Make all objects within the SCC directly or indirectly reference each other
		if (scc->Count > 1) {
			add_references (scc);
		} else if (scc->Count == 0) {
			temporary_peers [scc] = env->NewObject (GCUserPeer_class, GCUserPeer_ctor);
		}
	}

	// Add the cross scc refs
	for (size_t i = 0; i < cross_refs->CrossReferenceCount; i++) {
		ComponentCrossReference *xref = &cross_refs->CrossReferences [i];

		StronglyConnectedComponent *source = &cross_refs->Components [xref->SourceGroupIndex];
		jobject from = source->Count > 0 ? source->Contexts [0]->control_block->handle : temporary_peers [source];
		
		StronglyConnectedComponent *destination = &cross_refs->Components [xref->DestinationGroupIndex];
		jobject to = destination->Count > 0 ? destination->Contexts [0]->control_block->handle : temporary_peers [destination];

		add_reference (from, to);
	}

	// With cross references processed, the temporary peer list can be released
	for (const auto& [scc, temporary_peer] : temporary_peers) {
		env->DeleteLocalRef (temporary_peer);
	}

	// Switch global to weak references
	for (size_t i = 0; i < cross_refs->ComponentCount; i++) {
		StronglyConnectedComponent *scc = &cross_refs->Components [i];
		for (size_t j = 0; j < scc->Count; j++) {
			take_weak_global_ref (scc->Contexts [j]);
		}
	}
}

void GCBridge::add_references (StronglyConnectedComponent *scc) noexcept
{
	abort_if_invalid_pointer_argument (scc, "scc");
	abort_unless (scc->Count > 1, "SCC must have at least two items to add inner references");

	JniObjectReferenceControlBlock *prev = scc->Contexts [scc->Count - 1]->control_block;

	for (size_t j = 1; j < scc->Count; j++) {
		JniObjectReferenceControlBlock *current = scc->Contexts [j]->control_block;
		if (add_reference (prev->handle, current->handle)) {
			prev->refs_added++;
		}

		prev = current;
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

void GCBridge::clear_references (jobject handle) noexcept
{
	// Clear references from the object
	jclass java_class = env->GetObjectClass (handle);
	jmethodID clear_method_id = env->GetMethodID (java_class, "monodroidClearReferences", "()V");
	env->DeleteLocalRef (java_class); // Clean up the local reference to the class

	if (clear_method_id) {
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
	abort_unless (context->control_block->handle_type == JNIWeakGlobalRefType, "Expected weak global reference type for handle");

	jobject weak = context->control_block->handle;
	jobject handle = env->NewGlobalRef (weak);
	
	if (Logger::gref_log ()) [[unlikely]] {
		char *message = Util::monodroid_strdup_printf ("take_global_ref gchandle=%p -> wref=%p handle=%p\n", context->gc_handle, weak, handle);
		OSBridge::_monodroid_gref_log (message);
		free (message);
	}

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

		if (Logger::gc_spew_enabled ()) [[unlikely]] {
			char *message = Util::monodroid_strdup_printf ("handle %p/W; was collected by a Java GC", weak);
			OSBridge::_monodroid_gref_log (message);
			free (message);
		}
	}
}

void GCBridge::take_weak_global_ref (HandleContext *context) noexcept
{
	jobject handle = context->control_block->handle;
	if (Logger::gref_log ()) [[unlikely]] {
		char *message = Util::monodroid_strdup_printf ("take_weak_global_ref gchandle=%p; handle=%p\n", context->gc_handle, handle);
		OSBridge::_monodroid_gref_log (message);
		free (message);
	}

	jobject weak = env->NewWeakGlobalRef (handle);
	OSBridge::_monodroid_weak_gref_new (handle, OSBridge::get_object_ref_type (env, handle),
			weak, OSBridge::get_object_ref_type (env, weak),
			"finalizer", gettid (), "   at [[clr-gc:take_weak_global_ref]]", 0);

	context->control_block->handle = weak;
	context->control_block->handle_type = JNIWeakGlobalRefType;

	OSBridge::_monodroid_gref_log_delete (handle, OSBridge::get_object_ref_type (env, handle),
			"finalizer", gettid (), "   at [[clr-gc:take_weak_global_ref]]", 0);
	env->DeleteGlobalRef (handle);
}

void GCBridge::cleanup_after_java_collection (MarkCrossReferencesArgs* cross_refs) noexcept
{
#if DEBUG
	int total = 0;
	int alive = 0;
#endif

	// try to switch back to global refs to analyze what stayed alive
	for (size_t i = 0; i < cross_refs->ComponentCount; i++) {
		StronglyConnectedComponent *scc = &cross_refs->Components [i];
		for (size_t j = 0; j < scc->Count; j++) {
#if DEBUG
				total++;
#endif
				take_global_ref (scc->Contexts [j]);
			}
	}

	// clear the cross references on any remaining items
	for (size_t i = 0; i < cross_refs->ComponentCount; i++) {
		StronglyConnectedComponent *scc = &cross_refs->Components [i];
		bool is_alive = false;

		for (size_t j = 0; j < scc->Count; j++) {
			JniObjectReferenceControlBlock *control_block = scc->Contexts [j]->control_block;

			if (control_block != nullptr) {
#if DEBUG
				alive++;
#endif
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
	}

#if DEBUG
	log_info (LOG_GC, "GC cleanup summary: {} objects tested - resurrecting {}.", total, alive);
#endif
}

void GCBridge::bridge_processing () noexcept
{
	abort_unless (bridge_processing_started_callback != nullptr, "GC bridge processing started callback is not set");
	abort_unless (bridge_processing_finished_callback != nullptr, "GC bridge processing finished callback is not set");

	env = OSBridge::ensure_jnienv ();

	while (true) {
		bridge_processing_semaphore.acquire ();
		std::unique_lock<std::shared_mutex> lock (processing_mutex);

		MarkCrossReferencesArgs* args = &GCBridge::shared_cross_refs;
		log_mark_cross_references_args_if_enabled (args);

		bridge_processing_started_callback ();
		prepare_for_java_collection (args);
		trigger_java_gc ();
		cleanup_after_java_collection (args);
		bridge_processing_finished_callback (args);
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

void GCBridge::wait_for_bridge_processing () noexcept
{
	std::shared_lock<std::shared_mutex> lock (processing_mutex);
}

[[gnu::always_inline]]
void GCBridge::log_mark_cross_references_args_if_enabled (MarkCrossReferencesArgs* args) noexcept
{
	if (Logger::gc_spew_enabled ()) [[unlikely]] {
		log_info (LOG_GC, "cross references callback invoked with {} sccs and {} xrefs.", args->ComponentCount, args->CrossReferenceCount);

		for (size_t i = 0; i < args->ComponentCount; ++i) {
			log_info (LOG_GC, "group {} with {} objects", i, args->Components [i].Count);
			for (size_t j = 0; j < args->Components [i].Count; ++j) {
				HandleContext *ctx = args->Components [i].Contexts [j];
				jobject handle = ctx->control_block->handle;
				jclass java_class = env->GetObjectClass (handle);
				char *class_name = Host::get_java_class_name_for_TypeManager (java_class);
				log_info (LOG_GC, "\tgchandle {:p} gref {:p} [{}]", reinterpret_cast<void*> (ctx->gc_handle), reinterpret_cast<void*> (handle), class_name);
				free (class_name);
			}
		}

		if (Util::should_log (LOG_GC)) {
			for (size_t i = 0; i < args->CrossReferenceCount; ++i) {
				size_t source_index = args->CrossReferences [i].SourceGroupIndex;
				size_t dest_index = args->CrossReferences [i].DestinationGroupIndex;
				log_info_nocheck_fmt (LOG_GC, "xref [{}] {} -> {}", i, source_index, dest_index);
			}
		}
	}
}
