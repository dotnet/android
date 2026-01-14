#include <host/bridge-processing.hh>
#include <host/host.hh>
#include <host/runtime-util.hh>
#include <runtime-base/logger.hh>
#include <shared/helpers.hh>

using namespace xamarin::android;

void BridgeProcessingShared::initialize_on_runtime_init (JNIEnv *env, jclass runtimeClass) noexcept
{
	abort_if_invalid_pointer_argument (env, "env");
	abort_if_invalid_pointer_argument (runtimeClass, "runtimeClass");

	GCUserPeer_class = RuntimeUtil::get_class_from_runtime_field (env, runtimeClass, "mono_android_GCUserPeer", true);
	GCUserPeer_ctor = env->GetMethodID (GCUserPeer_class, "<init>", "()V");

	abort_unless (GCUserPeer_class != nullptr && GCUserPeer_ctor != nullptr, "Failed to load mono.android.GCUserPeer!");
}

BridgeProcessingShared::BridgeProcessingShared (MarkCrossReferencesArgs *args) noexcept
	: env{ OSBridge::ensure_jnienv () },
	  cross_refs{ args }
{
	if (args == nullptr) [[unlikely]] {
		Helpers::abort_application (LOG_GC, "Cross references argument is a NULL pointer"sv);
	}

	if (args->ComponentCount > 0 && args->Components == nullptr) [[unlikely]] {
		Helpers::abort_application (LOG_GC, "Components member of the cross references arguments structure is NULL"sv);
	}

	if (args->CrossReferenceCount > 0 && args->CrossReferences == nullptr) [[unlikely]] {
		Helpers::abort_application (LOG_GC, "CrossReferences member of the cross references arguments structure is NULL"sv);
	}
}

void BridgeProcessingShared::process () noexcept
{
	prepare_for_java_collection ();
	GCBridge::trigger_java_gc (env);
	cleanup_after_java_collection ();
	log_gc_summary ();
}

void BridgeProcessingShared::prepare_for_java_collection () noexcept
{
	// Before looking at xrefs, scan the SCCs. During collection, an SCC has to behave like a
	// single object. If the number of objects in the SCC is anything other than 1, the SCC
	// must be doctored to mimic that one-object nature.
	for (size_t i = 0; i < cross_refs->ComponentCount; i++) {
		const StronglyConnectedComponent &scc = cross_refs->Components [i];
		prepare_scc_for_java_collection (i, scc);
	}

	// Add the cross scc refs
	for (size_t i = 0; i < cross_refs->CrossReferenceCount; i++) {
		const ComponentCrossReference &xref = cross_refs->CrossReferences [i];
		add_cross_reference (xref.SourceGroupIndex, xref.DestinationGroupIndex);
	}

	// With cross references processed, the temporary peer list can be released
	for (const auto& [scc, temporary_peer] : temporary_peers) {
		env->DeleteLocalRef (temporary_peer);
	}

	// Switch global to weak references
	for (size_t i = 0; i < cross_refs->ComponentCount; i++) {
		const StronglyConnectedComponent &scc = cross_refs->Components [i];
		for (size_t j = 0; j < scc.Count; j++) {
			const HandleContext *context = scc.Contexts [j];
			abort_unless (context != nullptr, "Context must not be null");

			take_weak_global_ref (*context);
		}
	}
}

void BridgeProcessingShared::prepare_scc_for_java_collection (size_t scc_index, const StronglyConnectedComponent &scc) noexcept
{
	// Count == 0 case: Some SCCs might have no IGCUserPeers associated with them, so we must create one
	if (scc.Count == 0) {
		temporary_peers [scc_index] = env->NewObject (GCUserPeer_class, GCUserPeer_ctor);
		return;
	}

	// Count == 1 case: The SCC contains a single object, there is no need to do anything special.
	if (scc.Count == 1) {
		return;
	}

	// Count > 1 case: The SCC contains many objects which must be collected as one.
	// Solution: Make all objects within the SCC directly or indirectly reference each other
	add_circular_references (scc);
}

CrossReferenceTarget BridgeProcessingShared::select_cross_reference_target (size_t scc_index) noexcept
{
	const StronglyConnectedComponent &scc = cross_refs->Components [scc_index];

	if (scc.Count == 0) {
		const auto temporary_peer = temporary_peers.find (scc_index);
		abort_unless (temporary_peer != temporary_peers.end(), "Temporary peer must be found in the map");
		return { .is_temporary_peer = true, .temporary_peer = temporary_peer->second };
	}

	abort_unless (scc.Contexts [0] != nullptr, "SCC must have at least one context");
	return { .is_temporary_peer = false, .context = scc.Contexts [0] };
}

// caller must ensure that scc.Count > 1
void BridgeProcessingShared::add_circular_references (const StronglyConnectedComponent &scc) noexcept
{
	auto get_control_block = [&scc](size_t index) -> JniObjectReferenceControlBlock& {
		abort_unless (scc.Contexts [index] != nullptr, "Context in SCC must not be null");
		JniObjectReferenceControlBlock *control_block = scc.Contexts [index]->control_block;
		abort_unless (control_block != nullptr, "Control block in SCC must not be null");
		return *control_block;
	};

	size_t prev_index = scc.Count - 1;
	for (size_t next_index = 0; next_index < scc.Count; next_index++) {
		JniObjectReferenceControlBlock &prev = get_control_block (prev_index);
		const JniObjectReferenceControlBlock &next = get_control_block (next_index);

		bool reference_added = add_reference (prev.handle, next.handle);

		abort_unless (reference_added, [this, &prev, &next] {
			jclass prev_java_class = env->GetObjectClass (prev.handle);
			const char *prev_class_name = Host::get_java_class_name_for_TypeManager (prev_java_class);

			jclass next_java_class = env->GetObjectClass (next.handle);
			const char *next_class_name = Host::get_java_class_name_for_TypeManager (next_java_class);

			return detail::_format_message (
				"Failed to add reference between objects in a strongly connected component: %s -> %s.",
				prev_class_name,
				next_class_name);
		});

		prev.refs_added = 1;
		prev_index = next_index;
	}
}

void BridgeProcessingShared::add_cross_reference (size_t source_index, size_t dest_index) noexcept
{
	CrossReferenceTarget from = select_cross_reference_target (source_index);
	CrossReferenceTarget to = select_cross_reference_target (dest_index);

	if (add_reference (from.get_handle(), to.get_handle())) {
		from.mark_refs_added_if_needed ();
	}
}

bool BridgeProcessingShared::add_reference (jobject from, jobject to) noexcept
{
	abort_if_invalid_pointer_argument (from, "from");
	abort_if_invalid_pointer_argument (to, "to");

	if (maybe_call_gc_user_peerable_add_managed_reference (env, from, to)) {
		return true;
	}

	jclass java_class = env->GetObjectClass (from);
	jmethodID add_method_id = env->GetMethodID (java_class, "monodroidAddReference", "(Ljava/lang/Object;)V");

	if (add_method_id == nullptr) [[unlikely]] {
		// TODO: is this a fatal error?
		env->ExceptionClear ();
		log_missing_add_references_method (java_class);
		env->DeleteLocalRef (java_class);
		return false;
	}

	env->DeleteLocalRef (java_class);
	env->CallVoidMethod (from, add_method_id, to);
	return true;
}

void BridgeProcessingShared::clear_references_if_needed (const HandleContext &context) noexcept
{
	if (context.is_collected ()) {
		return;
	}

	JniObjectReferenceControlBlock *control_block = context.control_block;

	abort_unless (control_block != nullptr, "Control block must not be null");
	abort_unless (control_block->handle != nullptr, "Control block handle must not be null");
	abort_unless (control_block->handle_type == JNIGlobalRefType, "Control block handle type must be global reference");

	if (control_block->refs_added == 0) {
		return;
	}

	clear_references (control_block->handle);
	control_block->refs_added = 0;
}

void BridgeProcessingShared::clear_references (jobject handle) noexcept
{
	abort_if_invalid_pointer_argument (handle, "handle");

	if (maybe_call_gc_user_peerable_clear_managed_references (env, handle)) {
		return;
	}

	jclass java_class = env->GetObjectClass (handle);
	jmethodID clear_method_id = env->GetMethodID (java_class, "monodroidClearReferences", "()V");

	if (clear_method_id == nullptr) [[unlikely]] {
		env->ExceptionClear ();
		log_missing_clear_references_method (java_class);
		env->DeleteLocalRef (java_class);
		return;
	}

	env->DeleteLocalRef (java_class);
	env->CallVoidMethod (handle, clear_method_id);
}

void BridgeProcessingShared::take_global_ref (HandleContext &context) noexcept
{
	abort_unless (context.control_block != nullptr, "Control block must not be null");
	abort_unless (context.control_block->handle_type == JNIWeakGlobalRefType, "Expected weak global reference type for handle");

	jobject weak = context.control_block->handle;
	jobject handle = env->NewGlobalRef (weak);
	log_weak_to_gref (weak, handle);

	if (handle == nullptr) {
		log_weak_ref_collected (weak);
	}

	context.control_block->handle = handle; // by doing this, the weak reference won't be deleted AGAIN in managed code
	context.control_block->handle_type = JNIGlobalRefType;

	log_weak_ref_delete (weak);
	env->DeleteWeakGlobalRef (weak);
}

void BridgeProcessingShared::take_weak_global_ref (const HandleContext &context) noexcept
{
	abort_unless (context.control_block != nullptr, "Control block must not be null");
	abort_unless (context.control_block->handle_type == JNIGlobalRefType, "Expected global reference type for handle");

	jobject handle = context.control_block->handle;
	log_take_weak_global_ref (handle);

	jobject weak = env->NewWeakGlobalRef (handle);
	log_weak_gref_new (handle, weak);

	context.control_block->handle = weak;
	context.control_block->handle_type = JNIWeakGlobalRefType;

	log_gref_delete (handle);
	env->DeleteGlobalRef (handle);
}

void BridgeProcessingShared::cleanup_after_java_collection () noexcept
{
	for (size_t i = 0; i < cross_refs->ComponentCount; i++) {
		const StronglyConnectedComponent &scc = cross_refs->Components [i];

		// try to switch back to global refs to analyze what stayed alive
		for (size_t j = 0; j < scc.Count; j++) {
			HandleContext *context = scc.Contexts [j];
			abort_unless (context != nullptr, "Context must not be null");

			take_global_ref (*context);
			clear_references_if_needed (*context);
		}

		abort_unless_all_collected_or_all_alive (scc);
	}
}

void BridgeProcessingShared::abort_unless_all_collected_or_all_alive (const StronglyConnectedComponent &scc) noexcept
{
	if (scc.Count == 0) {
		return;
	}

	abort_unless (scc.Contexts [0] != nullptr, "Context must not be null");
	bool is_collected = scc.Contexts [0]->is_collected ();

	for (size_t j = 1; j < scc.Count; j++) {
		const HandleContext *context = scc.Contexts [j];
		abort_unless (context != nullptr, "Context must not be null");
		abort_unless (context->is_collected () == is_collected, "Cannot have a mix of collected and alive contexts in the SCC");
	}
}

jobject CrossReferenceTarget::get_handle () const noexcept
{
	if (is_temporary_peer) {
		return temporary_peer;
	}

	abort_unless (context != nullptr, "Context must not be null");
	abort_unless (context->control_block != nullptr, "Control block must not be null");
	return context->control_block->handle;
}

void CrossReferenceTarget::mark_refs_added_if_needed () noexcept
{
	if (is_temporary_peer) {
		return;
	}

	abort_unless (context != nullptr, "Context must not be null");
	abort_unless (context->control_block != nullptr, "Control block must not be null");
	context->control_block->refs_added = 1;
}

[[gnu::always_inline]]
void BridgeProcessingShared::log_missing_add_references_method ([[maybe_unused]] jclass java_class) noexcept
{
	log_error (LOG_DEFAULT, "Failed to find monodroidAddReferences method");
#if DEBUG
	abort_if_invalid_pointer_argument (java_class, "java_class");
	if (!Logger::gc_spew_enabled ()) [[likely]] {
		return;
	}

	char *class_name = Host::get_java_class_name_for_TypeManager (java_class);
	log_error (LOG_GC, "Missing monodroidAddReferences method for object of class {}", optional_string (class_name));
	free (class_name);
#endif
}

[[gnu::always_inline]]
void BridgeProcessingShared::log_missing_clear_references_method ([[maybe_unused]] jclass java_class) noexcept
{
	log_error (LOG_DEFAULT, "Failed to find monodroidClearReferences method");
#if DEBUG
	abort_if_invalid_pointer_argument (java_class, "java_class");
	if (!Logger::gc_spew_enabled ()) [[likely]] {
		return;
	}

	char *class_name = Host::get_java_class_name_for_TypeManager (java_class);
	log_error (LOG_GC, "Missing monodroidClearReferences method for object of class {}", optional_string (class_name));
	free (class_name);
#endif
}

[[gnu::always_inline]]
void BridgeProcessingShared::log_weak_to_gref (jobject weak, jobject handle) noexcept
{
	if (handle != nullptr) {
		OSBridge::_monodroid_gref_log_new (weak, OSBridge::get_object_ref_type (env, weak),
			handle, OSBridge::get_object_ref_type (env, handle),
			"finalizer", gettid (),
			"   at [[clr-gc:take_global_ref]]");
	}

	if (!Logger::gref_log ()) [[likely]] {
		return;
	}

	auto weak_p = reinterpret_cast<intptr_t> (weak);
	auto handle_p = reinterpret_cast<intptr_t> (handle);

	if constexpr (Constants::is_nativeaot) {
		constexpr size_t BufferSize = 128;
		dynamic_local_string<BufferSize> message;
		bool formatted = format_printf (
			message,
			"take_global_ref wref={:#x} -> handle={:#x}\n",
			weak_p, handle_p
		);

		if (formatted) [[likely]] {
			OSBridge::_monodroid_gref_log (message.get ());
		} else {
			OSBridge::_monodroid_gref_log ("take_global_ref: failed to format log message\n");
		}
	} else {
		OSBridge::_monodroid_gref_log (
			std::format (
				"take_global_ref wref={:#x} -> handle={:#x}\n"sv,
				weak_p,
				handle_p
			).c_str ()
		);
	}
}

[[gnu::always_inline]]
void BridgeProcessingShared::log_weak_ref_collected (jobject weak) noexcept
{
	if (Logger::gc_spew_enabled ()) [[likely]] {
		return;
	}

	auto weak_p = reinterpret_cast<intptr_t> (weak);
	if constexpr (Constants::is_nativeaot) {
		constexpr size_t BufferSize = 128;
		dynamic_local_string<BufferSize> message;
		bool formatted = format_printf (
			message,
			"handle 0x%x/W; was collected by a Java GC",
			weak_p
		);

		if (formatted) [[likely]] {
			OSBridge::_monodroid_gref_log (message.get ());
		} else {
			OSBridge::_monodroid_gref_log ("handle was collected by a Java GC, failed to format full message");
		}
	} else {
		OSBridge::_monodroid_gref_log (
			std::format ("handle {:#x}/W; was collected by a Java GC"sv, weak_p).c_str ()
		);
	}
}

[[gnu::always_inline]]
void BridgeProcessingShared::log_take_weak_global_ref (jobject handle) noexcept
{
	if (!Logger::gref_log ()) [[likely]] {
		return;
	}

	auto handle_p = reinterpret_cast<intptr_t> (handle);
	if constexpr (Constants::is_nativeaot) {
		constexpr size_t BufferSize = 128;
		dynamic_local_string<BufferSize> message;
		bool formatted = format_printf (
			message,
			"take_weak_global_ref handle={:#x}\n",
			handle_p
		);

		if (formatted) [[likely]] {
			OSBridge::_monodroid_gref_log (message.get ());
		} else {
			OSBridge::_monodroid_gref_log ("take_weak_global_ref handle taken, failed to format full message");
		}
	} else {
		OSBridge::_monodroid_gref_log (std::format ("take_weak_global_ref handle={:#x}\n"sv, handle_p).c_str ());
	}
}

[[gnu::always_inline]]
void BridgeProcessingShared::log_weak_gref_new (jobject handle, jobject weak) noexcept
{
	OSBridge::_monodroid_weak_gref_new (handle, OSBridge::get_object_ref_type (env, handle),
		weak, OSBridge::get_object_ref_type (env, weak),
		"finalizer", gettid (), "   at [[clr-gc:take_weak_global_ref]]");
}

[[gnu::always_inline]]
void BridgeProcessingShared::log_gref_delete (jobject handle) noexcept
{
	OSBridge::_monodroid_gref_log_delete (handle, OSBridge::get_object_ref_type (env, handle),
		"finalizer", gettid (), "   at [[clr-gc:take_weak_global_ref]]");
}

[[gnu::always_inline]]
void BridgeProcessingShared::log_weak_ref_delete (jobject weak) noexcept
{
	OSBridge::_monodroid_weak_gref_delete (weak, OSBridge::get_object_ref_type (env, weak),
		"finalizer", gettid (), "   at [[clr-gc:take_global_ref]]");
}

[[gnu::always_inline]]
void BridgeProcessingShared::log_gc_summary () noexcept
{
	if (!Logger::gc_spew_enabled ()) [[likely]] {
		return;
	}

	size_t total = 0;
	size_t alive = 0;

	for (size_t i = 0; i < cross_refs->ComponentCount; i++) {
		const StronglyConnectedComponent &scc = cross_refs->Components [i];

		for (size_t j = 0; j < scc.Count; j++) {
			HandleContext *context = scc.Contexts [j];
			abort_unless (context != nullptr, "Context must not be null");

			total = Helpers::add_with_overflow_check<size_t> (total, 1);
			if (!context->is_collected ()) {
				alive = Helpers::add_with_overflow_check<size_t> (alive, 1);
			}
		}
	}

	log_info (
		LOG_GC,
#if defined(XA_HOST_NATIVEAOT)
		"GC cleanup summary: %z objects tested - resurrecting %z.",
#else
		"GC cleanup summary: {} objects tested - resurrecting {}."sv,
#endif
		total,
		alive
	);
}
