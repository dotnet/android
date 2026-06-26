#pragma once

#include <pthread.h>
#include <semaphore.h>

#include <jni.h>

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
	int32_t identity_hash_code;
	JniObjectReferenceControlBlock *control_block;

	bool is_collected () const noexcept
	{
		abort_unless (control_block != nullptr, "Control block must not be null");
		return control_block->handle == nullptr;
	}
};

struct StronglyConnectedComponent
{
	size_t Count;
	HandleContext **Contexts;
};

struct ComponentCrossReference
{
	size_t SourceGroupIndex;
	size_t DestinationGroupIndex;
};

struct MarkCrossReferencesArgs
{
    size_t ComponentCount;
    StronglyConnectedComponent *Components;
    size_t CrossReferenceCount;
    ComponentCrossReference *CrossReferences;
};

using BridgeProcessingStartedFtn = void (*)(MarkCrossReferencesArgs*);
using BridgeProcessingFinishedFtn = void (*)(MarkCrossReferencesArgs*);
using BridgeProcessingFtn = void (*)(MarkCrossReferencesArgs*);

namespace xamarin::android {
	class GCBridge
	{
	public:
		static void initialize_on_onload (JNIEnv *env) noexcept;
		static void initialize_on_runtime_init (JNIEnv *env, jclass runtimeClass) noexcept;

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

			int ret = sem_init (&shared_args_semaphore, 0, 0);
			abort_unless (ret == 0, "Failed to initialize GC bridge semaphore");

			ret = pthread_create (&bridge_processing_thread, nullptr, GCBridge::bridge_processing_thread_entry, nullptr);
			abort_unless (ret == 0, "Failed to create GC bridge processing thread");

			ret = pthread_detach (bridge_processing_thread);
			abort_unless (ret == 0, "Failed to detach GC bridge processing thread");

			return mark_cross_references;
		}

		static void trigger_java_gc (JNIEnv *env) noexcept;

	private:
		static inline pthread_t bridge_processing_thread {};
		static inline sem_t shared_args_semaphore {};
		static inline MarkCrossReferencesArgs *shared_args = nullptr;

		static inline jobject Runtime_instance = nullptr;
		static inline jmethodID Runtime_gc = nullptr;

		static inline BridgeProcessingStartedFtn bridge_processing_started_callback = nullptr;
		static inline BridgeProcessingFinishedFtn bridge_processing_finished_callback = nullptr;

		static void bridge_processing () noexcept;
		static auto bridge_processing_thread_entry (void *arg) noexcept -> void*;
		static void mark_cross_references (MarkCrossReferencesArgs *args) noexcept;
		static MarkCrossReferencesArgs* enter_bridge_processing () noexcept;
		static void send_and_signal (MarkCrossReferencesArgs *args) noexcept;
		static void process_bridge_args (MarkCrossReferencesArgs *args) noexcept;
		
		static void log_mark_cross_references_args_if_enabled (MarkCrossReferencesArgs *args) noexcept;
		static void log_handle_context (JNIEnv *env, HandleContext *ctx) noexcept;
	};
}
