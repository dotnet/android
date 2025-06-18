#pragma once

#include <jni.h>
#include <unordered_map>

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

class BridgeProcessing
{
public:
	BridgeProcessing (MarkCrossReferencesArgs *args) noexcept;
	static void initialize_on_runtime_init (JNIEnv *jniEnv, jclass runtimeClass) noexcept;
	void process () noexcept;
private:
	JNIEnv* env;
	MarkCrossReferencesArgs *cross_refs;
	std::unordered_map<size_t, jobject> temporary_peers;

	static inline jclass GCUserPeer_class = nullptr;
	static inline jmethodID GCUserPeer_ctor = nullptr;

	void prepare_for_java_collection () noexcept;
	void prepare_scc_for_java_collection (size_t scc_index, const StronglyConnectedComponent &scc) noexcept;
	void take_weak_global_ref (HandleContext *context) noexcept;

	void add_circular_references (const StronglyConnectedComponent &scc) noexcept;
	void add_cross_reference (size_t source_index, size_t dest_index) noexcept;
	CrossReferenceTarget select_cross_reference_target (size_t scc_index) noexcept;
	bool add_reference (jobject from, jobject to) noexcept;

	void cleanup_after_java_collection () noexcept;
	bool cleanup_strongly_connected_component (const StronglyConnectedComponent &scc) noexcept;
	void take_global_ref (HandleContext *context) noexcept;

	void clear_references_if_needed (JniObjectReferenceControlBlock *control_block) noexcept;
	void clear_references (jobject handle) noexcept;

	void log_missing_add_references_method (jclass java_class) noexcept;
	void log_missing_clear_references_method (jclass java_class) noexcept;
	void log_weak_to_gref (jobject weak, jobject handle) noexcept;
	void log_weak_ref_survived (jobject weak, jobject handle) noexcept;
	void log_weak_ref_collected (jobject weak) noexcept;
	void log_take_weak_global_ref (jobject handle) noexcept;
	void log_weak_gref_new (jobject handle, jobject weak) noexcept;
	void log_gref_delete (jobject handle) noexcept;
};
