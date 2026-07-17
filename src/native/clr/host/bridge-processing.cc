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

	// Cache the IGCUserPeer interface method IDs once, instead of resolving them per reference edge.
	IGCUserPeer_class = RuntimeUtil::get_class_from_runtime_field (env, runtimeClass, "mono_android_IGCUserPeer", true);
	abort_unless (IGCUserPeer_class != nullptr, "Failed to load mono.android.IGCUserPeer!");

	IGCUserPeer_monodroidAddReference = env->GetMethodID (IGCUserPeer_class, "monodroidAddReference", "(Ljava/lang/Object;)V");
	IGCUserPeer_monodroidClearReferences = env->GetMethodID (IGCUserPeer_class, "monodroidClearReferences", "()V");

	abort_unless (
		IGCUserPeer_monodroidAddReference != nullptr && IGCUserPeer_monodroidClearReferences != nullptr,
		"Failed to load mono.android.IGCUserPeer methods!");
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
	// Each SCC with no IGCUserPeers is represented by a temporary peer held as a JNI local
	// reference that must stay alive until every cross reference has been added. Reserve enough
	// local reference capacity up front so that a large number of such SCCs cannot overflow the
	// JNI local reference table (which only guarantees 16 slots by default).
	size_t temporary_peer_count = 0;
	for (size_t i = 0; i < cross_refs->ComponentCount; i++) {
		if (cross_refs->Components [i].Count == 0) {
			temporary_peer_count = Helpers::add_with_overflow_check<size_t> (temporary_peer_count, 1);
		}
	}

	if (temporary_peer_count > 0) {
		constexpr size_t local_ref_slack = 16;
		constexpr size_t max_jint = static_cast<size_t> (0x7fffffff);
		size_t desired_capacity = Helpers::add_with_overflow_check<size_t> (temporary_peer_count, local_ref_slack);
		jint requested_capacity = static_cast<jint> (desired_capacity > max_jint ? max_jint : desired_capacity);

		if (env->EnsureLocalCapacity (requested_capacity) != JNI_OK) [[unlikely]] {
			env->ExceptionClear ();
			log_warn (LOG_GC, "Failed to reserve JNI local reference capacity for {} temporary peers", temporary_peer_count);
		}
	}

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

	if (!env->IsInstanceOf (from, IGCUserPeer_class)) [[unlikely]] {
		jclass java_class = env->GetObjectClass (from);
		log_missing_add_references_method (java_class);
		env->DeleteLocalRef (java_class);
		return false;
	}

	env->CallVoidMethod (from, IGCUserPeer_monodroidAddReference, to);
	abort_on_pending_java_exception ("A Java exception was thrown by monodroidAddReference during GC bridge processing"sv);
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

	if (!env->IsInstanceOf (handle, IGCUserPeer_class)) [[unlikely]] {
		jclass java_class = env->GetObjectClass (handle);
		log_missing_clear_references_method (java_class);
		env->DeleteLocalRef (java_class);
		return;
	}

	env->CallVoidMethod (handle, IGCUserPeer_monodroidClearReferences);
	abort_on_pending_java_exception ("A Java exception was thrown by monodroidClearReferences during GC bridge processing"sv);
}

void BridgeProcessingShared::abort_on_pending_java_exception (std::string_view message) noexcept
{
	if (!env->ExceptionCheck ()) [[likely]] {
		return;
	}

	env->ExceptionDescribe ();
	env->ExceptionClear ();
	Helpers::abort_application (LOG_GC, message);
}

void BridgeProcessingShared::take_global_ref (HandleContext &context) noexcept
{
	abort_unless (context.control_block != nullptr, "Control block must not be null");
	abort_unless (context.control_block->handle_type == JNIWeakGlobalRefType, "Expected weak global reference type for handle");

	jobject weak = context.control_block->handle;
	jobject handle = env->NewGlobalRef (weak);

	log_weak_to_gref (weak, handle);

	if (handle == nullptr) {
		// A null result normally means the weak reference's target was collected by the Java GC.
		// However, NewGlobalRef can also fail (returning null with a pending exception) when the VM
		// is out of memory or the global reference table is full. Treating that as "collected" would
		// tear down a live peer, so distinguish the two and fail fast on a genuine failure.
		abort_on_pending_java_exception ("Failed to promote a weak global reference to a global reference during GC bridge processing"sv);
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
	if (weak == nullptr) [[unlikely]] {
		// `handle` is a valid strong global reference, so NewWeakGlobalRef only returns null when the
		// VM is out of memory. Continuing would delete the strong reference below and lose the object
		// (a later NewGlobalRef of a null weak reference would look like a collected peer), so fail fast.
		// The OOM failure raises a pending exception; abort unconditionally in case it somehow did not.
		constexpr std::string_view failure = "Failed to create a weak global reference during GC bridge processing"sv;
		abort_on_pending_java_exception (failure);
		Helpers::abort_application (LOG_GC, failure);
	}
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
		if ((log_categories & LOG_GREF) != 0) [[unlikely]] {
			OSBridge::_monodroid_gref_log_new (weak, OSBridge::get_object_ref_type (env, weak),
				handle, OSBridge::get_object_ref_type (env, handle),
				"finalizer", gettid (),
				"   at [[clr-gc:take_global_ref]]");
		} else {
			OSBridge::_monodroid_gref_inc ();
		}
	}

	if (!Logger::gref_log ()) [[likely]] {
		return;
	}

	OSBridge::_monodroid_gref_log (
		std::format ("take_global_ref wref={:#x} -> handle={:#x}\n"sv,
			reinterpret_cast<intptr_t> (weak),
			reinterpret_cast<intptr_t> (handle)).data ());
}

[[gnu::always_inline]]
void BridgeProcessingShared::log_weak_ref_collected (jobject weak) noexcept
{
	if (!Logger::gc_spew_enabled ()) [[likely]] {
		return;
	}

	OSBridge::_monodroid_gref_log (
		std::format ("handle {:#x}/W; was collected by a Java GC"sv, reinterpret_cast<intptr_t> (weak)).data ());
}

[[gnu::always_inline]]
void BridgeProcessingShared::log_take_weak_global_ref (jobject handle) noexcept
{
	if (!Logger::gref_log ()) [[likely]] {
		return;
	}

	OSBridge::_monodroid_gref_log (std::format ("take_weak_global_ref handle={:#x}\n"sv, reinterpret_cast<intptr_t> (handle)).data ());
}

[[gnu::always_inline]]
void BridgeProcessingShared::log_weak_gref_new (jobject handle, jobject weak) noexcept
{
	if ((log_categories & LOG_GREF) != 0) [[unlikely]] {
		OSBridge::_monodroid_weak_gref_new (handle, OSBridge::get_object_ref_type (env, handle),
			weak, OSBridge::get_object_ref_type (env, weak),
			"finalizer", gettid (), "   at [[clr-gc:take_weak_global_ref]]");
	} else {
		OSBridge::_monodroid_weak_gref_inc ();
	}
}

[[gnu::always_inline]]
void BridgeProcessingShared::log_gref_delete (jobject handle) noexcept
{
	if ((log_categories & LOG_GREF) != 0) [[unlikely]] {
		OSBridge::_monodroid_gref_log_delete (handle, OSBridge::get_object_ref_type (env, handle),
			"finalizer", gettid (), "   at [[clr-gc:take_weak_global_ref]]");
	} else {
		OSBridge::_monodroid_gref_dec ();
	}
}

[[gnu::always_inline]]
void BridgeProcessingShared::log_weak_ref_delete (jobject weak) noexcept
{
	if ((log_categories & LOG_GREF) != 0) [[unlikely]] {
		OSBridge::_monodroid_weak_gref_delete (weak, OSBridge::get_object_ref_type (env, weak),
			"finalizer", gettid (), "   at [[clr-gc:take_global_ref]]");
	} else {
		OSBridge::_monodroid_weak_gref_dec ();
	}
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

	log_info (LOG_GC, "GC cleanup summary: {} objects tested - resurrecting {}.", total, alive);
}
