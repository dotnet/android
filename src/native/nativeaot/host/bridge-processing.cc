#include <cstdio>

#include <host/bridge-processing.hh>
#include <runtime-base/logger.hh>
#include <shared/helpers.hh>

using namespace xamarin::android;

const BridgeProcessingCallbacks BridgeProcessing::bridge_processing_callbacks {
	.context = nullptr,
	.maybe_call_gc_user_peerable_add_managed_reference = &BridgeProcessing::maybe_call_gc_user_peerable_add_managed_reference,
	.maybe_call_gc_user_peerable_clear_managed_references = &BridgeProcessing::maybe_call_gc_user_peerable_clear_managed_references,
};

BridgeProcessing::BridgeProcessing (MarkCrossReferencesArgs *args) noexcept
	: BridgeProcessingShared (args, &bridge_processing_callbacks)
{}

void BridgeProcessing::naot_initialize_on_runtime_init (JNIEnv *env) noexcept
{
	GCUserPeerable_class = env->FindClass ("net/dot/jni/GCUserPeerable");
	if (GCUserPeerable_class == nullptr) [[unlikely]] {
		Helpers::abort_application (
			LOG_DEFAULT,
			"Failed to find net/dot/jni/GCUserPeerable class while initializing GC bridge processing."
		);
	}

	GCUserPeerable_class = static_cast<jclass>(OSBridge::lref_to_gref (env, GCUserPeerable_class));
	GCUserPeerable_jiAddManagedReference = env->GetMethodID (GCUserPeerable_class, "jiAddManagedReference", "(Ljava/lang/Object;)V");
	GCUserPeerable_jiClearManagedReferences = env->GetMethodID (GCUserPeerable_class, "jiClearManagedReferences", "()V");

	if (GCUserPeerable_jiAddManagedReference == nullptr || GCUserPeerable_jiClearManagedReferences == nullptr) [[unlikely]] {
		constexpr char ABSENT[] = "absent";
		constexpr char PRESENT[] = "present";
		char message[128];
		snprintf (
			message,
			sizeof (message),
			"Failed to find GCUserPeerable method(s): jiAddManagedReference (%s); jiClearManagedReferences (%s)",
			GCUserPeerable_jiAddManagedReference == nullptr ? ABSENT : PRESENT,
			GCUserPeerable_jiClearManagedReferences == nullptr ? ABSENT : PRESENT
		);

		Helpers::abort_application (
			LOG_DEFAULT,
			message
		);
	}
}

bool BridgeProcessing::maybe_call_gc_user_peerable_add_managed_reference ([[maybe_unused]] void *context, JNIEnv *env, jobject from, jobject to) noexcept
{
	if (!env->IsInstanceOf (from, GCUserPeerable_class)) {
		return false;
	}

	env->CallVoidMethod (from, GCUserPeerable_jiAddManagedReference, to);
	return true;
}

bool BridgeProcessing::maybe_call_gc_user_peerable_clear_managed_references ([[maybe_unused]] void *context, JNIEnv *env, jobject handle) noexcept
{
	if (!env->IsInstanceOf (handle, GCUserPeerable_class)) {
		return false;
	}

	env->CallVoidMethod (handle, GCUserPeerable_jiClearManagedReferences);
	return true;
}
