#pragma once

#include <jni.h>

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
using OnMarkCrossReferencesCallback = void (*)(MarkCrossReferencesArgs*);

namespace xamarin::android {
	class GCBridge
	{
	public:
		static void initialize_on_onload (JNIEnv *env) noexcept;
		static void initialize_on_runtime_init (JNIEnv *env, jclass runtimeClass) noexcept;

		// Initialize GC bridge for managed processing mode.
		// Takes a callback that will be invoked when mark_cross_references is called by the GC.
		// The callback is expected to queue the args and signal a managed thread to process them.
		// Returns the mark_cross_references function pointer for JavaMarshal.Initialize.
		static BridgeProcessingFtn initialize_for_managed_processing (OnMarkCrossReferencesCallback callback) noexcept
		{
			on_mark_cross_references_callback = callback;
			return mark_cross_references;
		}

	private:
		static inline OnMarkCrossReferencesCallback on_mark_cross_references_callback = nullptr;

		static void mark_cross_references (MarkCrossReferencesArgs *args) noexcept;
		
		static void log_mark_cross_references_args_if_enabled (MarkCrossReferencesArgs *args) noexcept;
		static void log_handle_context (JNIEnv *env, HandleContext *ctx) noexcept;
	};
}
