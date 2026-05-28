#pragma once

#include <cstddef>

#include <jni.h>

#if !defined (XA_HOST_NATIVEAOT)
// NDEBUG causes robin_map.h not to include <iostream> which, in turn, prevents indirect inclusion of <mutex>. <mutex>
// conflicts with our std::mutex definition in cppcompat.hh
#if !defined (NDEBUG)
#define NDEBUG
#define NDEBUG_UNDEFINE
#endif

// hush some compiler warnings
#if defined (__clang__)
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Wunused-parameter"
#endif // __clang__

#include <tsl/robin_map.h>

#if defined (__clang__)
#pragma clang diagnostic pop
#endif // __clang__

#if defined (NDEBUG_UNDEFINE)
#undef NDEBUG
#undef NDEBUG_UNDEFINE
#endif
#endif // !defined (XA_HOST_NATIVEAOT)

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

class BridgeProcessingShared
{
#if defined (XA_HOST_NATIVEAOT)
	using temporary_peer_map = jobject*;
#else
	using temporary_peer_map = tsl::robin_map<size_t, jobject>;
#endif

public:
	explicit BridgeProcessingShared (MarkCrossReferencesArgs *args, const BridgeProcessingCallbacks *callbacks = nullptr) noexcept;
	~BridgeProcessingShared () noexcept;

	static void initialize_on_runtime_init (JNIEnv *jniEnv, jclass runtimeClass) noexcept;
	void process () noexcept;
private:
	JNIEnv* env;
	MarkCrossReferencesArgs *cross_refs;
	temporary_peer_map temporary_peers {};
	BridgeProcessingCallbacks callbacks;

	static inline jclass GCUserPeer_class = nullptr;
	static inline jmethodID GCUserPeer_ctor = nullptr;

	void prepare_for_java_collection () noexcept;
	void prepare_scc_for_java_collection (size_t scc_index, const StronglyConnectedComponent &scc) noexcept;
	bool has_temporary_peer (size_t scc_index) noexcept;
	void add_temporary_peer (size_t scc_index, jobject temporary_peer) noexcept;
	jobject get_temporary_peer (size_t scc_index) noexcept;
	void release_temporary_peers () noexcept;
	void free_temporary_peer_map () noexcept;
	void take_weak_global_ref (const HandleContext &context) noexcept;

	void add_circular_references (const StronglyConnectedComponent &scc) noexcept;
	void add_cross_reference (size_t source_index, size_t dest_index) noexcept;
	CrossReferenceTarget select_cross_reference_target (size_t scc_index) noexcept;
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
