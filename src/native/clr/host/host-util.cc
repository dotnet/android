#include <host/host-util.hh>
#include <host/os-bridge.hh>

using namespace xamarin::android;

auto HostUtil::get_class_from_runtime_field (JNIEnv *env, jclass runtime, const char *name, bool make_gref) noexcept -> jclass
{
	static constexpr char java_lang_class_sig[] = "Ljava/lang/Class;";

	jfieldID fieldID = env->GetStaticFieldID (runtime, name, java_lang_class_sig);
	if (fieldID == nullptr)
		return nullptr;

	jobject field = env->GetStaticObjectField (runtime, fieldID);
	if (field == nullptr)
		return nullptr;

	return reinterpret_cast<jclass> (make_gref ? OSBridge::lref_to_gref (env, field) : field);
}
