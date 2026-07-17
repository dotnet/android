#pragma once

#include <cstddef>
#include <jni.h>
#include <string_view>

#include <host/gc-bridge.hh>
#include <host/os-bridge.hh>
#include <shared/cpp-util.hh>

struct CrossReferenceTarget
{
	bool is_temporary_peer;
	union
	{
		jobject temporary_peer;
		HandleContext* context;
	};

	jobject get_handle () const noexcept;
	void mark_refs_added_if_needed () noexcept;
};

class TemporaryPeerMap
{
public:
	explicit TemporaryPeerMap (JNIEnv *env, MarkCrossReferencesArgs *cross_refs) noexcept;
	~TemporaryPeerMap () noexcept;

	static void initialize_on_runtime_init (JNIEnv *env, jclass runtimeClass) noexcept;

	void add (StronglyConnectedComponent &scc) noexcept;
	bool has_temporary_peer (const StronglyConnectedComponent &scc) const noexcept;
	jobject get (const StronglyConnectedComponent &scc) const noexcept;

private:
	// Count is unsigned, so encode the temporary peer index as ~index. This stores the same bit
	// pattern as -(index + 1), giving us a sign bit marker while preserving index 0.
	// The destructor resets every marker before returning cross_refs to the runtime.
	static constexpr size_t temporary_peer_index_sign_bit = ~(~size_t { 0 } >> 1);

	static bool is_temporary_peer_index (size_t count) noexcept;
	static size_t encode_temporary_peer_index (size_t index) noexcept;
	static size_t decode_temporary_peer_index (size_t count) noexcept;

	static inline jclass peer_class = nullptr;
	static inline jmethodID peer_ctor = nullptr;

	JNIEnv *env;
	MarkCrossReferencesArgs *cross_refs;
	jobject *peers {};
	size_t count {};
	size_t capacity {};
};

class BridgeProcessingShared
{
public:
	explicit BridgeProcessingShared (MarkCrossReferencesArgs *args) noexcept;
	static void initialize_on_runtime_init (JNIEnv *jniEnv, jclass runtimeClass) noexcept;
	void process () noexcept;
private:
	JNIEnv* env;
	MarkCrossReferencesArgs *cross_refs;

	// Cached `mono.android.IGCUserPeer` interface and its methods. The method IDs are looked up
	// once from the interface class and are valid for virtual dispatch on every implementing peer,
	// so we avoid a per-edge GetObjectClass + GetMethodID lookup during bridge processing.
	static inline jclass IGCUserPeer_class = nullptr;
	static inline jmethodID IGCUserPeer_monodroidAddReference = nullptr;
	static inline jmethodID IGCUserPeer_monodroidClearReferences = nullptr;

	void prepare_for_java_collection () noexcept;
	void prepare_sccs_and_cross_references_for_java_collection () noexcept;
	void prepare_scc_for_java_collection (size_t scc_index, const StronglyConnectedComponent &scc, TemporaryPeerMap &temporary_peers) noexcept;
	void take_weak_global_ref (const HandleContext &context) noexcept;

	void add_circular_references (const StronglyConnectedComponent &scc) noexcept;
	void add_cross_reference (size_t source_index, size_t dest_index, TemporaryPeerMap &temporary_peers) noexcept;
	CrossReferenceTarget select_cross_reference_target (size_t scc_index, TemporaryPeerMap &temporary_peers) noexcept;
	bool add_reference (jobject from, jobject to) noexcept;

	void cleanup_after_java_collection () noexcept;
	void cleanup_scc_for_java_collection (const StronglyConnectedComponent &scc) noexcept;
	void abort_unless_all_collected_or_all_alive (const StronglyConnectedComponent &scc) noexcept;
	void take_global_ref (HandleContext &context) noexcept;

	void clear_references_if_needed (const HandleContext &context) noexcept;
	void clear_references (jobject handle) noexcept;

	// If a Java exception is pending on `env`, describe it, clear it, and abort. Bridge
	// processing has no safe way to recover from an exception thrown by a peer's reference
	// callbacks, and leaving an exception pending would make subsequent JNI calls undefined.
	void abort_on_pending_java_exception (std::string_view message) noexcept;

	void log_missing_add_references_method (jclass java_class) noexcept;
	void log_missing_clear_references_method (jclass java_class) noexcept;
	void log_weak_to_gref (jobject weak, jobject handle) noexcept;
	void log_weak_ref_collected (jobject weak) noexcept;
	void log_take_weak_global_ref (jobject handle) noexcept;
	void log_weak_gref_new (jobject handle, jobject weak) noexcept;
	void log_gref_delete (jobject handle) noexcept;
	void log_weak_ref_delete (jobject weak) noexcept;
	void log_gc_summary () noexcept;

	// These methods must be implemented by every host individually
	// Both methods below return `true` if they processed the call
	virtual auto maybe_call_gc_user_peerable_add_managed_reference (JNIEnv *env, jobject from, jobject to) noexcept -> bool = 0;
	virtual auto maybe_call_gc_user_peerable_clear_managed_references (JNIEnv *env, jobject handle) noexcept -> bool = 0;
};
