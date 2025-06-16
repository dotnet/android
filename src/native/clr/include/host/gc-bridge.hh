#pragma once

#include <jni.h>
#include <thread>
#include <semaphore>
#include <shared_mutex>
#include <unordered_map>

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
    size_t ComponentCount;
    StronglyConnectedComponent* Components;
    size_t CrossReferenceCount;
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
			BridgeProcessingStartedFtn bridge_processing_started,
			BridgeProcessingFinishedFtn bridge_processing_finished) noexcept
		{
			abort_if_invalid_pointer_argument (bridge_processing_started, "bridge_processing_started");
			abort_if_invalid_pointer_argument (bridge_processing_finished, "bridge_processing_finished");
			abort_unless (GCBridge::bridge_processing_started_callback == nullptr, "GC bridge processing started callback is already set");
			abort_unless (GCBridge::bridge_processing_finished_callback == nullptr, "GC bridge processing finished callback is already set");

			GCBridge::bridge_processing_started_callback = bridge_processing_started;
			GCBridge::bridge_processing_finished_callback = bridge_processing_finished;

			bridge_processing_thread = new std::thread(GCBridge::bridge_processing);
			bridge_processing_thread->detach ();

			return mark_cross_references;
		}

	private:
		static inline std::binary_semaphore bridge_processing_semaphore{0};
		static inline std::shared_mutex processing_mutex;
		static inline std::thread* bridge_processing_thread = nullptr;

		static inline JNIEnv* env = nullptr;
		static inline jobject Runtime_instance = nullptr;
		static inline jmethodID Runtime_gc = nullptr;
		static inline jclass GCUserPeer_class = nullptr;
		static inline jmethodID GCUserPeer_ctor = nullptr;

		static inline MarkCrossReferencesArgs cross_refs;

		static inline BridgeProcessingStartedFtn bridge_processing_started_callback = nullptr;
		static inline BridgeProcessingFinishedFtn bridge_processing_finished_callback = nullptr;

		static void bridge_processing () noexcept;
		static void mark_cross_references (MarkCrossReferencesArgs *cross_refs) noexcept;

		static void prepare_for_java_collection () noexcept;
		static void trigger_java_gc () noexcept;
		static void cleanup_after_java_collection () noexcept;
		static bool cleanup_strongly_connected_component (size_t i, const StronglyConnectedComponent &scc) noexcept;

		static void take_weak_global_ref (HandleContext *context) noexcept;
		static void take_global_ref (HandleContext *context) noexcept;

		static bool add_reference (jobject from, jobject to) noexcept;
		static void clear_references (jobject handle) noexcept;
		static void add_references (const StronglyConnectedComponent &scc) noexcept;
		static void add_cross_reference (
			size_t xref_index,
			const std::unordered_map<size_t, jobject> &temporary_peers) noexcept;
		static jobject pick_representative (
			size_t scc_index,
			const StronglyConnectedComponent &scc,
			const std::unordered_map<size_t, jobject> &temporary_peers) noexcept;

		static void log_mark_cross_references_args_if_enabled () noexcept;
	};
}
