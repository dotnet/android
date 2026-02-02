#pragma once

#include <atomic>
#include <jni.h>
#include <semaphore>
#include <thread>

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
using BridgeProcessingCallback = void (*)(MarkCrossReferencesArgs*);

namespace xamarin::android {
	class GCBridge
	{
	public:
		// Initialize GC bridge for managed processing mode.
		// Takes a callback that will be invoked from a background thread when mark_cross_references is called.
		// Returns the mark_cross_references function pointer for JavaMarshal.Initialize.
		static BridgeProcessingFtn initialize_for_managed_processing (BridgeProcessingCallback callback) noexcept
		{
			abort_if_invalid_pointer_argument (callback, "callback");
			bridge_processing_callback = callback;

			// Start the background thread that will call into managed code
			bridge_processing_thread = std::thread { bridge_processing };
			bridge_processing_thread.detach ();

			return mark_cross_references;
		}

	private:
		static inline std::thread bridge_processing_thread {};
		static inline std::binary_semaphore shared_args_semaphore{0};
		static inline std::atomic<MarkCrossReferencesArgs*> shared_args;
		static inline BridgeProcessingCallback bridge_processing_callback = nullptr;

		static void bridge_processing () noexcept;
		static void mark_cross_references (MarkCrossReferencesArgs *args) noexcept;
		
		static void log_mark_cross_references_args_if_enabled (MarkCrossReferencesArgs *args) noexcept;
		static void log_handle_context (JNIEnv *env, HandleContext *ctx) noexcept;
	};
}
