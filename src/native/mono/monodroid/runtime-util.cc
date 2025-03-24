#include "globals.hh"
#include "runtime-util.hh"

using namespace xamarin::android::internal;

jclass
RuntimeUtil::get_class_from_runtime_field (JNIEnv *env, jclass runtime, const char *name, bool make_gref)
{
	static constexpr char java_lang_class_sig[] = "Ljava/lang/Class;";

	jfieldID fieldID = env->GetStaticFieldID (runtime, name, java_lang_class_sig);
	if (fieldID == nullptr)
		return nullptr;

	jobject field = env->GetStaticObjectField (runtime, fieldID);
	if (field == nullptr)
		return nullptr;

	return reinterpret_cast<jclass> (make_gref ? osBridge.lref_to_gref (env, field) : field);
}
