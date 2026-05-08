#pragma once

#include <jni.h>

#include <host/bridge-processing-shared.hh>

class BridgeProcessing final : public BridgeProcessingShared
{
public:
	explicit BridgeProcessing (MarkCrossReferencesArgs *args) noexcept;

	static void naot_initialize_on_runtime_init (JNIEnv *env) noexcept;

private:
	static bool maybe_call_gc_user_peerable_add_managed_reference ([[maybe_unused]] void *context, JNIEnv *env, jobject from, jobject to) noexcept;
	static bool maybe_call_gc_user_peerable_clear_managed_references ([[maybe_unused]] void *context, JNIEnv *env, jobject handle) noexcept;

private:
	static const BridgeProcessingCallbacks bridge_processing_callbacks;
	static inline jclass GCUserPeerable_class = nullptr;
	static inline jmethodID GCUserPeerable_jiAddManagedReference = nullptr;
	static inline jmethodID GCUserPeerable_jiClearManagedReferences = nullptr;
};
