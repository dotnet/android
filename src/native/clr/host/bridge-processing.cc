#include <cinttypes>
#include <cstdlib>

#include <host/bridge-processing.hh>
#include <host/host.hh>
#include <host/runtime-util.hh>
#include <runtime-base/logger.hh>
#include <shared/helpers.hh>

using namespace xamarin::android;

TemporaryPeerMap::TemporaryPeerMap (JNIEnv *jni_env, MarkCrossReferencesArgs *args) noexcept
	: env{ jni_env },
	  cross_refs{ args }
{
	size_t map_capacity = 0;
	for (size_t i = 0; i < cross_refs->ComponentCount; i++) {
		const StronglyConnectedComponent &scc = cross_refs->Components [i];
		abort_unless (!is_temporary_peer_index (scc.Count), "SCC count must not use the temporary peer marker bit");
		if (scc.Count == 0) {
			map_capacity = Helpers::add_with_overflow_check<size_t> (map_capacity, 1);
		}
	}

	if (map_capacity == 0) {
		return;
	}

	constexpr size_t local_ref_slack = 16;
	constexpr size_t max_jint = static_cast<size_t> (0x7fffffff);
	size_t desired_capacity = Helpers::add_with_overflow_check<size_t> (map_capacity, local_ref_slack);
	jint requested_capacity = static_cast<jint> (desired_capacity > max_jint ? max_jint : desired_capacity);

	if (env->EnsureLocalCapacity (requested_capacity) != JNI_OK) [[unlikely]] {
		env->ExceptionClear ();
		log_warnf (LOG_GC, "Failed to reserve JNI local reference capacity for %zu temporary peers", map_capacity);
	}

	capacity = map_capacity;
	peers = static_cast<jobject*> (std::calloc (capacity, sizeof (jobject)));
	abort_unless (peers != nullptr, "Failed to allocate GC bridge temporary peer map");
}

TemporaryPeerMap::~TemporaryPeerMap () noexcept
{
	if (peers == nullptr) {
		return;
	}

	for (size_t i = 0; i < count; i++) {
		jobject temporary_peer = peers [i];
		if (temporary_peer != nullptr) {
			env->DeleteLocalRef (temporary_peer);
			peers [i] = nullptr;
		}
	}

	for (size_t i = 0; i < cross_refs->ComponentCount; i++) {
		StronglyConnectedComponent &scc = cross_refs->Components [i];
		if (is_temporary_peer_index (scc.Count)) {
			scc.Count = 0;
		}
	}

	count = 0;
	std::free (peers);
	peers = nullptr;
	capacity = 0;
}

void TemporaryPeerMap::initialize_on_runtime_init (JNIEnv *env, jclass runtimeClass) noexcept
{
	abort_if_invalid_pointer_argument (env, "env");
	abort_if_invalid_pointer_argument (runtimeClass, "runtimeClass");

	peer_class = RuntimeUtil::get_class_from_runtime_field (env, runtimeClass, "mono_android_GCUserPeer", true);
	abort_unless (peer_class != nullptr, "Failed to load mono.android.GCUserPeer!");

	peer_ctor = env->GetMethodID (peer_class, "<init>", "()V");
	abort_unless (peer_ctor != nullptr, "Failed to load mono.android.GCUserPeer constructor!");
}

void TemporaryPeerMap::add (StronglyConnectedComponent &scc) noexcept
{
	abort_unless (peers != nullptr, "Temporary peer map must not be null");
	abort_unless (count < capacity, "Temporary peer map must not be full");

	jobject temporary_peer = env->NewObject (peer_class, peer_ctor);
	abort_unless (temporary_peer != nullptr, "Failed to create GC bridge temporary peer");

	size_t temporary_peer_index = count++;
	peers [temporary_peer_index] = temporary_peer;
	scc.Count = encode_temporary_peer_index (temporary_peer_index);
}

bool TemporaryPeerMap::has_temporary_peer (const StronglyConnectedComponent &scc) const noexcept
{
	return is_temporary_peer_index (scc.Count);
}

jobject TemporaryPeerMap::get (const StronglyConnectedComponent &scc) const noexcept
{
	size_t temporary_peer_index = decode_temporary_peer_index (scc.Count);
	abort_unless (temporary_peer_index < count, "Temporary peer index must be in range");

	return peers [temporary_peer_index];
}

bool TemporaryPeerMap::is_temporary_peer_index (size_t count) noexcept
{
	return (count & temporary_peer_index_sign_bit) != 0;
}

size_t TemporaryPeerMap::encode_temporary_peer_index (size_t index) noexcept
{
	abort_unless (!is_temporary_peer_index (index), "Temporary peer index is too large");
	return ~index;
}

size_t TemporaryPeerMap::decode_temporary_peer_index (size_t count) noexcept
{
	abort_unless (is_temporary_peer_index (count), "Temporary peer index must be negative");
	return ~count;
}

void BridgeProcessingShared::initialize_on_runtime_init (JNIEnv *env, jclass runtimeClass) noexcept
{
	abort_if_invalid_pointer_argument (env, "env");
	abort_if_invalid_pointer_argument (runtimeClass, "runtimeClass");

	TemporaryPeerMap::initialize_on_runtime_init (env, runtimeClass);

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
	prepare_sccs_and_cross_references_for_java_collection ();

	// Temporary peer indexes have been reset, so SCC counts are safe to use normally again.
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

void BridgeProcessingShared::prepare_sccs_and_cross_references_for_java_collection () noexcept
{
	TemporaryPeerMap temporary_peers { env, cross_refs };

	// Before looking at xrefs, scan the SCCs. During collection, an SCC has to behave like a
	// single object. If the number of objects in the SCC is anything other than 1, the SCC
	// must be doctored to mimic that one-object nature.
	for (size_t i = 0; i < cross_refs->ComponentCount; i++) {
		const StronglyConnectedComponent &scc = cross_refs->Components [i];
		prepare_scc_for_java_collection (i, scc, temporary_peers);
	}

	// Add the cross scc refs
	for (size_t i = 0; i < cross_refs->CrossReferenceCount; i++) {
		const ComponentCrossReference &xref = cross_refs->CrossReferences [i];
		add_cross_reference (xref.SourceGroupIndex, xref.DestinationGroupIndex, temporary_peers);
	}
}

void BridgeProcessingShared::prepare_scc_for_java_collection (size_t scc_index, const StronglyConnectedComponent &scc, TemporaryPeerMap &temporary_peers) noexcept
{
	// Count == 0 case: Some SCCs might have no IGCUserPeers associated with them, so we must create one
	if (scc.Count == 0) {
		temporary_peers.add (cross_refs->Components [scc_index]);
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

CrossReferenceTarget BridgeProcessingShared::select_cross_reference_target (size_t scc_index, TemporaryPeerMap &temporary_peers) noexcept
{
	const StronglyConnectedComponent &scc = cross_refs->Components [scc_index];

	if (temporary_peers.has_temporary_peer (scc)) {
		jobject temporary_peer = temporary_peers.get (scc);
		abort_unless (temporary_peer != nullptr, "Temporary peer must not be null");
		return { .is_temporary_peer = true, .temporary_peer = temporary_peer };
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

void BridgeProcessingShared::add_cross_reference (size_t source_index, size_t dest_index, TemporaryPeerMap &temporary_peers) noexcept
{
	CrossReferenceTarget from = select_cross_reference_target (source_index, temporary_peers);
	CrossReferenceTarget to = select_cross_reference_target (dest_index, temporary_peers);

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
	log_errorf (LOG_GC, "Missing monodroidAddReferences method for object of class %s", optional_string (class_name));
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
	log_errorf (LOG_GC, "Missing monodroidClearReferences method for object of class %s", optional_string (class_name));
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

	OSBridge::_monodroid_gref_logf (
		"take_global_ref wref=0x%" PRIxPTR " -> handle=0x%" PRIxPTR "\n",
		reinterpret_cast<uintptr_t> (weak),
		reinterpret_cast<uintptr_t> (handle)
	);
}

[[gnu::always_inline]]
void BridgeProcessingShared::log_weak_ref_collected (jobject weak) noexcept
{
	if (!Logger::gc_spew_enabled ()) [[likely]] {
		return;
	}

	OSBridge::_monodroid_gref_logf (
		"handle 0x%" PRIxPTR "/W; was collected by a Java GC",
		reinterpret_cast<uintptr_t> (weak)
	);
}

[[gnu::always_inline]]
void BridgeProcessingShared::log_take_weak_global_ref (jobject handle) noexcept
{
	if (!Logger::gref_log ()) [[likely]] {
		return;
	}

	OSBridge::_monodroid_gref_logf ("take_weak_global_ref handle=0x%" PRIxPTR "\n", reinterpret_cast<uintptr_t> (handle));
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

	log_infof (LOG_GC, "GC cleanup summary: %zu objects tested - resurrecting %zu.", total, alive);
}
