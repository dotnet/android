#pragma once

#include <jni.h>
#include <thread>
#include <semaphore>
#include <shared_mutex>

#include <shared/cpp-util.hh>

struct JniObjectReferenceControlBlock
{
	jobject handle;
	int handle_type;
	jobject weak_handle;
	int refs_added;
};

struct HandleContext
{
	intptr_t gc_handle;
	int32_t is_collected;
	JniObjectReferenceControlBlock* control_block;
};

struct StronglyConnectedComponent
{
	size_t Count;
	HandleContext** Contexts;
};

struct ComponentCrossReference
{
	size_t SourceGroupIndex;
	size_t DestinationGroupIndex;
};

struct MarkCrossReferencesArgs
{
    size_t ComponentsLen;
    StronglyConnectedComponent* Components;
    size_t CrossReferencesLen;
    ComponentCrossReference* CrossReferences;
};

using BridgeProcessingStartedFtn = void (*)();
using BridgeProcessingFinishedFtn = void (*)(MarkCrossReferencesArgs*);
using BridgeProcessingFtn = void (*)(MarkCrossReferencesArgs*);

namespace xamarin::android {
	class GCBridge
	{
	public:
		static void wait_for_bridge_processing () noexcept;
		static void initialize_on_load (JNIEnv *env) noexcept;
		static BridgeProcessingFtn initialize_callback (
			BridgeProcessingStartedFtn bridge_processing_started_callback,
			BridgeProcessingFinishedFtn bridge_processing_finished_callback) noexcept
		{
			abort_if_invalid_pointer_argument (bridge_processing_started_callback, "bridge_processing_started_callback");
			abort_if_invalid_pointer_argument (bridge_processing_finished_callback, "bridge_processing_finished_callback");
			abort_unless (GCBridge::bridge_processing_started_callback == nullptr, "GC bridge processing started callback is already set");
			abort_unless (GCBridge::bridge_processing_finished_callback == nullptr, "GC bridge processing finished callback is already set");

			GCBridge::bridge_processing_started_callback = bridge_processing_started_callback;
			GCBridge::bridge_processing_finished_callback = bridge_processing_finished_callback;

			bridge_processing_thread = new std::thread(GCBridge::bridge_processing);
			bridge_processing_thread->detach ();

			return mark_cross_references;
		}

	private:
		static inline BridgeProcessingStartedFtn bridge_processing_started_callback = nullptr;
		static inline BridgeProcessingFinishedFtn bridge_processing_finished_callback = nullptr;

		static inline MarkCrossReferencesArgs shared_cross_refs;
		static inline std::binary_semaphore bridge_processing_semaphore{0};

		static inline JNIEnv* env = nullptr;
		static inline std::shared_mutex processing_mutex;
		static inline std::thread* bridge_processing_thread = nullptr;

		static void trigger_java_gc () noexcept;
		static void bridge_processing () noexcept;
		static void mark_cross_references (MarkCrossReferencesArgs* cross_refs) noexcept;

		static bool is_bridgeless_scc (StronglyConnectedComponent *scc) noexcept;
		static bool add_reference (HandleContext *from, jobject to) noexcept;
		static bool add_direct_reference (jobject from, jobject to) noexcept; // TODO naming
		static void clear_references (jobject handle) noexcept;
		static int scc_get_stashed_temporary_peer_index (StronglyConnectedComponent *scc) noexcept;
		static void scc_set_stashed_temporary_peer_index (StronglyConnectedComponent *scc, int index) noexcept;
		static jobject get_scc_representative (StronglyConnectedComponent *scc, jobject temporary_peers) noexcept;
		static void maybe_release_scc_representative (StronglyConnectedComponent *scc, jobject handle) noexcept;
		static void prepare_for_java_collection (MarkCrossReferencesArgs* cross_refs) noexcept;
		static void cleanup_after_java_collection (MarkCrossReferencesArgs* cross_refs) noexcept;

		static inline jobject Runtime_instance = nullptr;
		static inline jmethodID Runtime_gc = nullptr;

		static inline jclass GCUserPeer_class = nullptr;
		static inline jmethodID GCUserPeer_ctor = nullptr;

		static inline jclass ArrayList_class = nullptr;
		static inline jmethodID ArrayList_ctor = nullptr;
		static inline jmethodID ArrayList_get = nullptr;
		static inline jmethodID ArrayList_add = nullptr;
	};
}
