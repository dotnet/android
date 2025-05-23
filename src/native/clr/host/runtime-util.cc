#include <host/os-bridge.hh>
#include <host/runtime-util.hh>

using namespace xamarin::android;

auto RuntimeUtil::get_class_from_runtime_field (JNIEnv *env, jclass runtime, std::string_view const& name, bool make_gref) noexcept -> jclass
{
	constexpr char java_lang_class_sig[] = "Ljava/lang/Class;";

	jfieldID fieldID = env->GetStaticFieldID (runtime, name.data (), java_lang_class_sig);
	if (fieldID == nullptr)
		return nullptr;

	jobject field = env->GetStaticObjectField (runtime, fieldID);
	if (field == nullptr)
		return nullptr;

	return reinterpret_cast<jclass> (make_gref ? OSBridge::lref_to_gref (env, field) : field);
}
