#pragma once

#include <atomic>
#include <jni.h>
#include <semaphore>

#include <shared/cpp-util.hh>
#include <host/os-bridge.hh>

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

using BridgeProcessingFtn = void (*)(MarkCrossReferencesArgs*);

namespace xamarin::android {
	class GCBridge
	{
	public:
		static void initialize_on_onload (JNIEnv *env) noexcept;
		static void initialize_on_runtime_init (JNIEnv *env, jclass runtimeClass) noexcept;

		// Initialize for managed processing mode
		// Returns the mark_cross_references function pointer for JavaMarshal.Initialize
		static BridgeProcessingFtn initialize_for_managed_processing () noexcept
		{
			return mark_cross_references;
		}

		// Wait for the next set of cross references to process (for managed processing mode)
		// Blocks until mark_cross_references is called by the GC
		static MarkCrossReferencesArgs* wait_for_processing () noexcept
		{
			shared_args_semaphore.acquire ();
			return shared_args.load ();
		}

		static void trigger_java_gc (JNIEnv *env) noexcept;

		// Trigger Java GC using the cached Runtime instance (for managed processing mode)
		static void trigger_java_gc_cached () noexcept
		{
			JNIEnv *env = OSBridge::ensure_jnienv ();
			trigger_java_gc (env);
		}

	private:
		static inline std::binary_semaphore shared_args_semaphore{0};
		static inline std::atomic<MarkCrossReferencesArgs*> shared_args;

		static inline jobject Runtime_instance = nullptr;
		static inline jmethodID Runtime_gc = nullptr;

		static void mark_cross_references (MarkCrossReferencesArgs *args) noexcept;
		
		static void log_mark_cross_references_args_if_enabled (MarkCrossReferencesArgs *args) noexcept;
		static void log_handle_context (JNIEnv *env, HandleContext *ctx) noexcept;
	};
}
