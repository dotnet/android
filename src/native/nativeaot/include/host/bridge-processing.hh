#pragma once

#include <jni.h>

#include <host/bridge-processing-shared.hh>

class BridgeProcessing final : public BridgeProcessingShared
{
public:
	explicit BridgeProcessing (MarkCrossReferencesArgs *args) noexcept
		: BridgeProcessingShared (args)
	{}

	static void naot_initialize_on_runtime_init (JNIEnv *env) noexcept;

private:
	auto maybe_call_gc_user_peerable_add_managed_reference (JNIEnv *env, jobject from, jobject to) noexcept -> bool override final;
	auto maybe_call_gc_user_peerable_clear_managed_references (JNIEnv *env, jobject handle) noexcept -> bool override final;

private:
	static inline jclass GCUserPeerable_class = nullptr;
	static inline jmethodID GCUserPeerable_jiAddManagedReference = nullptr;
	static inline jmethodID GCUserPeerable_jiClearManagedReferences = nullptr;
};
