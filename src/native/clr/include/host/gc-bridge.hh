#pragma once

#include <atomic>
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

			bridge_processing_thread = std::thread { GCBridge::bridge_processing };
			bridge_processing_thread.detach ();

			return mark_cross_references;
		}

		static void trigger_java_gc (JNIEnv *env) noexcept;

	private:
		static inline std::thread bridge_processing_thread {};

		static inline std::binary_semaphore shared_args_semaphore{0};
		static inline std::atomic<MarkCrossReferencesArgs*> shared_args;

		static inline jobject Runtime_instance = nullptr;
		static inline jmethodID Runtime_gc = nullptr;

		static inline BridgeProcessingStartedFtn bridge_processing_started_callback = nullptr;
		static inline BridgeProcessingFinishedFtn bridge_processing_finished_callback = nullptr;

		static void bridge_processing () noexcept;
		static void mark_cross_references (MarkCrossReferencesArgs *args) noexcept;
		
		static void log_mark_cross_references_args_if_enabled (MarkCrossReferencesArgs *args) noexcept;
		static void log_handle_context (JNIEnv *env, HandleContext *ctx) noexcept;
	};
}
