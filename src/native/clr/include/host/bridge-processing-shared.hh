#pragma once

#include <cstddef>

#include <jni.h>

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

struct BridgeProcessingCallbacks
{
	void *context;
	bool (*maybe_call_gc_user_peerable_add_managed_reference) (void *context, JNIEnv *env, jobject from, jobject to) noexcept;
	bool (*maybe_call_gc_user_peerable_clear_managed_references) (void *context, JNIEnv *env, jobject handle) noexcept;
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
	// Count is unsigned, so encode the temporary peer index as ~index.  This stores the same bit
	// pattern as -(index + 1), giving us a sign bit marker while preserving index 0.
	// The .NET 11 GC bridge implementation appears safe to temporarily mutate here: once it
	// hands us MarkCrossReferencesArgs, it does not inspect Count again before freeing the data.
	// The destructor always resets temporary markers before returning cross_refs to the runtime.
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
	explicit BridgeProcessingShared (MarkCrossReferencesArgs *args, const BridgeProcessingCallbacks *callbacks = nullptr) noexcept;

	void process () noexcept;
private:
	JNIEnv* env;
	MarkCrossReferencesArgs *cross_refs;
	BridgeProcessingCallbacks callbacks;

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
	bool maybe_call_gc_user_peerable_add_managed_reference (JNIEnv *env, jobject from, jobject to) noexcept;
	bool maybe_call_gc_user_peerable_clear_managed_references (JNIEnv *env, jobject handle) noexcept;

	void log_missing_add_references_method (jclass java_class) noexcept;
	void log_missing_clear_references_method (jclass java_class) noexcept;
	void log_weak_to_gref (jobject weak, jobject handle) noexcept;
	void log_weak_ref_collected (jobject weak) noexcept;
	void log_take_weak_global_ref (jobject handle) noexcept;
	void log_weak_gref_new (jobject handle, jobject weak) noexcept;
	void log_gref_delete (jobject handle) noexcept;
	void log_weak_ref_delete (jobject weak) noexcept;
	void log_gc_summary () noexcept;
};
