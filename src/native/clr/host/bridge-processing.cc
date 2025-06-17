#include <host/bridge-processing.hh>
#include <host/host.hh>
#include <host/runtime-util.hh>
#include <runtime-base/logger.hh>

#include <thread>

using namespace xamarin::android;

void BridgeProcessing::initialize_on_runtime_init (JNIEnv *env, jclass runtimeClass) noexcept
{
	abort_if_invalid_pointer_argument (env, "env");
	abort_if_invalid_pointer_argument (runtimeClass, "runtimeClass");

	GCUserPeer_class = RuntimeUtil::get_class_from_runtime_field (env, runtimeClass, "mono_android_GCUserPeer", true);
	GCUserPeer_ctor = env->GetMethodID (GCUserPeer_class, "<init>", "()V");

	abort_unless (GCUserPeer_class != nullptr && GCUserPeer_ctor != nullptr, "Failed to load mono.android.GCUserPeer!");
}

BridgeProcessing::BridgeProcessing (MarkCrossReferencesArgs args) noexcept
	: env (OSBridge::ensure_jnienv ()),
	  cross_refs (args)
{
}

void BridgeProcessing::process () noexcept
{
	prepare_for_java_collection ();
	GCBridge::trigger_java_gc (env);
	cleanup_after_java_collection ();
}

void BridgeProcessing::prepare_for_java_collection () noexcept
{
	// Before looking at xrefs, scan the SCCs. During collection, an SCC has to behave like a
	// single object. If the number of objects in the SCC is anything other than 1, the SCC
	// must be doctored to mimic that one-object nature.
	for (size_t i = 0; i < cross_refs.ComponentCount; i++) {
		const StronglyConnectedComponent &scc = cross_refs.Components [i];

		// Count > 1 case: The SCC contains many objects which must be collected as one.
		// Solution: Make all objects within the SCC directly or indirectly reference each other
		if (scc.Count > 1) {
			add_circular_references (scc);
		} else if (scc.Count == 0) {
			// Some SCCs might have no IGCUserPeers associated with them, so we must create one
			temporary_peers [i] = env->NewObject (GCUserPeer_class, GCUserPeer_ctor);
		}
	}

	// Add the cross scc refs
	for (size_t i = 0; i < cross_refs.CrossReferenceCount; i++) {
		const ComponentCrossReference &xref = cross_refs.CrossReferences [i];
		add_cross_reference (xref.SourceGroupIndex, xref.DestinationGroupIndex);
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

CrossReferenceTarget BridgeProcessing::select_cross_reference_target (size_t scc_index) noexcept
{
	const StronglyConnectedComponent &scc = cross_refs.Components [scc_index];
	if (scc.Count > 0) {
		abort_unless (scc.Contexts [0] != nullptr, "SCC must have at least one context");
		abort_unless (scc.Contexts [0]->control_block != nullptr, "SCC must have at least one context with valid control block");
		return { .is_temporary_peer = false, .context = scc.Contexts [0] };
	} else {
		const auto temporary_peer = temporary_peers.find (scc_index);
		abort_unless (temporary_peer != temporary_peers.end(), "Temporary peer must be found in the map");
		return { .is_temporary_peer = true, .temporary_peer = temporary_peer->second };
	}
}

void BridgeProcessing::add_circular_references (const StronglyConnectedComponent &scc) noexcept
{
	abort_unless (scc.Count > 1, "SCC must have at least two items to add inner references");

	JniObjectReferenceControlBlock *prev = scc.Contexts [scc.Count - 1]->control_block;

	for (size_t j = 1; j < scc.Count; j++) {
		JniObjectReferenceControlBlock *next = scc.Contexts [j]->control_block;
		bool reference_added = add_reference (prev->handle, next->handle);
	
		// TODO this doesn't seem to be handled in the Mono code but if we don't handle this case, then the component _might not be_ strongly connected on the Java side.
		// What is the case when this fails though? All bridge objects should have the `monodroidAddReference` method, so this is unexpected.
		abort_unless (reference_added, [this, &prev, &next] {
			jclass prev_java_class = env->GetObjectClass (prev->handle);
			const char *prev_class_name = Host::get_java_class_name_for_TypeManager (prev_java_class);

			jclass next_java_class = env->GetObjectClass (next->handle);
			const char *next_class_name = Host::get_java_class_name_for_TypeManager (next_java_class);

			return detail::_format_message (
				"Failed to add reference between objects in a strongly connected component: %s -> %s.",
				prev_class_name,
				next_class_name);
		});
	
		prev->refs_added = 1;
		prev = next;
	}
}

void BridgeProcessing::add_cross_reference (size_t source_index, size_t dest_index) noexcept
{
	CrossReferenceTarget from = select_cross_reference_target (source_index);
	CrossReferenceTarget to = select_cross_reference_target (dest_index);

	if (add_reference (from.get_handle(), to.get_handle())) {
		from.mark_refs_added_if_needed ();
	}
}

bool BridgeProcessing::add_reference (jobject from, jobject to) noexcept
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

void BridgeProcessing::clear_references_if_needed (JniObjectReferenceControlBlock *control_block) noexcept
{
	abort_if_invalid_pointer_argument (control_block, "control_block");
	abort_unless (control_block->handle != nullptr, "Control block handle must not be null");
	abort_unless (control_block->handle_type == JNIGlobalRefType, "Control block handle type must be global reference");

	if (control_block->refs_added == 0) {
		return;
	}

	// Clear references from the object
	jclass java_class = env->GetObjectClass (control_block->handle);
	jmethodID clear_method_id = env->GetMethodID (java_class, "monodroidClearReferences", "()V");
	env->DeleteLocalRef (java_class); // Clean up the local reference to the class

	if (clear_method_id != nullptr) [[likely]] {
		env->CallVoidMethod (control_block->handle, clear_method_id);
		control_block->refs_added = 0;
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

void BridgeProcessing::take_global_ref (HandleContext *context) noexcept
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
	
	if (Logger::gref_log ()) [[unlikely]] {
		OSBridge::_monodroid_gref_log (
			std::format ("take_global_ref wref={:#x} -> handle={:#x}\n"sv,
				reinterpret_cast<intptr_t> (weak),
				reinterpret_cast<intptr_t> (handle)).data ());
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
			OSBridge::_monodroid_gref_log (
				std::format ("handle {:#x}/W; was collected by a Java GC"sv, reinterpret_cast<intptr_t> (weak)).data ());
		}
	}
}

void BridgeProcessing::take_weak_global_ref (HandleContext *context) noexcept
{
	abort_if_invalid_pointer_argument (context, "context");
	abort_unless (context->control_block != nullptr, "Control block must not be null");
	abort_unless (context->control_block->handle_type == JNIGlobalRefType, "Expected global reference type for handle");

	jobject handle = context->control_block->handle;
	if (Logger::gref_log ()) [[unlikely]] {
		OSBridge::_monodroid_gref_log (std::format ("take_weak_global_ref handle={:#x}\n"sv, reinterpret_cast<intptr_t> (handle)).data ());
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

void BridgeProcessing::cleanup_after_java_collection () noexcept
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

bool BridgeProcessing::cleanup_strongly_connected_component (size_t i, const StronglyConnectedComponent &scc) noexcept
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
			clear_references_if_needed (control_block);
		} else {
			abort_unless (!is_alive, [&i] { return detail::_format_message ("Bridge SCC at index %d must NOT be alive", i); });
		}
	}

	return is_alive;
}

jobject CrossReferenceTarget::get_handle () const noexcept
{
	return is_temporary_peer ? temporary_peer : context->control_block->handle;
}

void CrossReferenceTarget::mark_refs_added_if_needed () noexcept
{
	if (!is_temporary_peer) {
		abort_unless (context != nullptr, "Context must not be null");
		abort_unless (context->control_block != nullptr, "Control block must not be null");
		context->control_block->refs_added = 1;
	}
}
