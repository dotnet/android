#include <string>

#include <host/bridge-processing.hh>
#include <runtime-base/logger.hh>
#include <shared/helpers.hh>

using namespace xamarin::android;

void BridgeProcessing::naot_initialize_on_runtime_init (JNIEnv *env) noexcept
{
	GCUserPeerable_class = env->FindClass ("net/dot/jni/GCUserPeerable");
	if (GCUserPeerable_class == nullptr) [[unlikely]] {
		Helpers::abort_application (
			LOG_DEFAULT,
			"Failed to find net/dot/jni/GCUserPeerable class while initializing GC bridge processing."sv
		);
	}

	GCUserPeerable_class = static_cast<jclass>(OSBridge::lref_to_gref (env, GCUserPeerable_class));
	GCUserPeerable_jiAddManagedReference = env->GetMethodID (GCUserPeerable_class, "jiAddManagedReference", "(Ljava/lang/Object;)V");
	GCUserPeerable_jiClearManagedReferences = env->GetMethodID (GCUserPeerable_class, "jiClearManagedReferences", "()V");

	if (GCUserPeerable_jiAddManagedReference == nullptr || GCUserPeerable_jiClearManagedReferences == nullptr) [[unlikely]] {
		constexpr auto ABSENT = "absent"sv;
		constexpr auto PRESENT = "present"sv;

		// This is fugly, but more type safe than printf format parsing
		std::string err_msg { "Failed to find GCUserPeerable method(s): jiAddManagedReference ("sv };
		err_msg.append (GCUserPeerable_jiAddManagedReference == nullptr ? ABSENT : PRESENT);
		err_msg.append ("); jiClearManagedReferences ("sv);
		err_msg.append (GCUserPeerable_jiClearManagedReferences == nullptr ? ABSENT : PRESENT);
		err_msg.append (")"sv);

		Helpers::abort_application (LOG_DEFAULT, err_msg);
	}
}

auto BridgeProcessing::maybe_call_gc_user_peerable_add_managed_reference (JNIEnv *env, jobject from, jobject to) noexcept -> bool
{
	if (!env->IsInstanceOf (from, GCUserPeerable_class)) {
		return false;
	}

	env->CallVoidMethod (from, GCUserPeerable_jiAddManagedReference, to);
	return true;
}

auto BridgeProcessing::maybe_call_gc_user_peerable_clear_managed_references (JNIEnv *env, jobject handle) noexcept -> bool
{
	if (!env->IsInstanceOf (handle, GCUserPeerable_class)) {
		return false;
	}

	env->CallVoidMethod (handle, GCUserPeerable_jiClearManagedReferences);
	return true;
}
