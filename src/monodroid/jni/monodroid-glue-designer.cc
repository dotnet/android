#if !defined (ANDROID)
//
// Android designer support code, not used on devices
//
#include "globals.hh"
#include "mono_android_Runtime.h"
#include "monodroid-glue-internal.hh"

using namespace xamarin::android::internal;

// DO NOT USE ON NORMAL X.A
// This function only works with the custom TypeManager embedded with the designer process.
force_inline static void
reinitialize_android_runtime_type_manager (JNIEnv *env)
{
	log_info (LOG_DEFAULT, "%s ENTER", __PRETTY_FUNCTION__);
	jclass typeManager = env->FindClass ("mono/android/TypeManager");
	log_info (LOG_DEFAULT, "Location: %s:%u", __FILE__, __LINE__);
	env->UnregisterNatives (typeManager);
	log_info (LOG_DEFAULT, "Location: %s:%u", __FILE__, __LINE__);

	jmethodID resetRegistration = env->GetStaticMethodID (typeManager, "resetRegistration", "()V");
	log_info (LOG_DEFAULT, "Location: %s:%u", __FILE__, __LINE__);
	env->CallStaticVoidMethod (typeManager, resetRegistration);
	log_info (LOG_DEFAULT, "Location: %s:%u", __FILE__, __LINE__);

	env->DeleteLocalRef (typeManager);
	log_info (LOG_DEFAULT, "%s LEAVE", __PRETTY_FUNCTION__);
}

inline void
MonodroidRuntime::shutdown_android_runtime (MonoDomain *domain) noexcept
{
	log_info (LOG_DEFAULT, "%s ENTER", __PRETTY_FUNCTION__);
	MonoClass *runtime = get_android_runtime_class (domain);
	MonoMethod *method = mono_class_get_method_from_name (runtime, "Exit", 0);

	Util::monodroid_runtime_invoke (domain, method, nullptr, nullptr, nullptr);
	log_info (LOG_DEFAULT, "%s LEAVE", __PRETTY_FUNCTION__);
}

inline jint
MonodroidRuntime::Java_mono_android_Runtime_createNewContextWithData (JNIEnv *env, jclass klass, jobjectArray runtimeApksJava, jobjectArray assembliesJava,
                                                                      jobjectArray assembliesBytes, jobjectArray assembliesPaths, jobject loader, jboolean force_preload_assemblies) noexcept
{
	log_info (LOG_DEFAULT, "%s ENTER", __PRETTY_FUNCTION__);
	log_info (LOG_DEFAULT, "CREATING NEW CONTEXT");
	reinitialize_android_runtime_type_manager (env);
	MonoDomain *root_domain = mono_get_root_domain ();
	mono_jit_thread_attach (root_domain);

	jstring_array_wrapper runtimeApks (env, runtimeApksJava);
	jstring_array_wrapper assemblies (env, assembliesJava);
	jstring_array_wrapper assembliePaths (env, assembliesPaths);
	MonoDomain *domain = create_and_initialize_domain (env, klass, runtimeApks, assemblies, assembliesBytes, assembliePaths, loader, /*is_root_domain:*/ false, force_preload_assemblies, /* have_split_apks */ false);
	mono_domain_set (domain, FALSE);
	int domain_id = mono_domain_get_id (domain);
	current_context_id = domain_id;
	log_info (LOG_DEFAULT, "Created new context with id %d\n", domain_id);
	log_info (LOG_DEFAULT, "%s LEAVE", __PRETTY_FUNCTION__);
	return domain_id;
}

inline void
MonodroidRuntime::Java_mono_android_Runtime_switchToContext (JNIEnv *env, jint contextID) noexcept
{
	log_info (LOG_DEFAULT, "%s ENTER", __PRETTY_FUNCTION__);
	log_info (LOG_DEFAULT, "SWITCHING CONTEXT");
	MonoDomain *domain = mono_domain_get_by_id ((int)contextID);
	log_info (LOG_DEFAULT, "Location: %s:%u", __FILE__, __LINE__);
	if (current_context_id != (int)contextID) {
		log_info (LOG_DEFAULT, "Location: %s:%u", __FILE__, __LINE__);
		mono_domain_set (domain, TRUE);
		log_info (LOG_DEFAULT, "Location: %s:%u", __FILE__, __LINE__);
		// Reinitialize TypeManager so that its JNI handle goes into the right domain
		reinitialize_android_runtime_type_manager (env);
		log_info (LOG_DEFAULT, "Location: %s:%u", __FILE__, __LINE__);
	}
	log_info (LOG_DEFAULT, "Location: %s:%u", __FILE__, __LINE__);
	current_context_id = (int)contextID;
	log_info (LOG_DEFAULT, "%s LEAVE", __PRETTY_FUNCTION__);
}

inline void
MonodroidRuntime::Java_mono_android_Runtime_destroyContexts (JNIEnv *env, jintArray array) noexcept
{
	log_info (LOG_DEFAULT, "%s ENTER", __PRETTY_FUNCTION__);
	MonoDomain *root_domain = mono_get_root_domain ();
	mono_jit_thread_attach (root_domain);
	current_context_id = -1;

	jint *contextIDs = env->GetIntArrayElements (array, nullptr);
	jsize count = env->GetArrayLength (array);

	log_info (LOG_DEFAULT, "Cleaning %d domains", count);

	for (jsize i = 0; i < count; i++) {
		int domain_id = contextIDs[i];
		MonoDomain *domain = mono_domain_get_by_id (domain_id);

		if (domain == nullptr)
			continue;
		log_info (LOG_DEFAULT, "Shutting down domain `%d'", contextIDs[i]);
		shutdown_android_runtime (domain);
		OSBridge::remove_monodroid_domain (domain);
		designerAssemblies.clear_for_domain (domain);
	}
	OSBridge::on_destroy_contexts ();
	for (jsize i = 0; i < count; i++) {
		int domain_id = contextIDs[i];
		MonoDomain *domain = mono_domain_get_by_id (domain_id);

		if (domain == nullptr)
			continue;
		log_info (LOG_DEFAULT, "Unloading domain `%d'", contextIDs[i]);
		mono_domain_unload (domain);
	}
	env->ReleaseIntArrayElements (array, contextIDs, JNI_ABORT);

	reinitialize_android_runtime_type_manager (env);

	log_info (LOG_DEFAULT, "All domain cleaned up");
	log_info (LOG_DEFAULT, "%s LEAVE", __PRETTY_FUNCTION__);
}

JNIEXPORT jint
JNICALL Java_mono_android_Runtime_createNewContextWithData (JNIEnv *env, jclass klass, jobjectArray runtimeApksJava, jobjectArray assembliesJava, jobjectArray assembliesBytes, jobjectArray assembliesPaths, jobject loader, jboolean force_preload_assemblies)
{
	log_info (LOG_DEFAULT, "%s ENTER", __PRETTY_FUNCTION__);
	jint ret = MonodroidRuntime::Java_mono_android_Runtime_createNewContextWithData (
		env,
		klass,
		runtimeApksJava,
		assembliesJava,
		assembliesBytes,
		assembliesPaths,
		loader,
		force_preload_assemblies
	);
	log_info (LOG_DEFAULT, "%s LEAVE", __PRETTY_FUNCTION__);
	return ret;
}

/* !DO NOT REMOVE! Used by older versions of the Android Designer (pre-16.4) */
JNIEXPORT jint
JNICALL Java_mono_android_Runtime_createNewContext (JNIEnv *env, jclass klass, jobjectArray runtimeApksJava, jobjectArray assembliesJava, jobject loader)
{
	log_info (LOG_DEFAULT, "%s ENTER", __PRETTY_FUNCTION__);
	jint ret = MonodroidRuntime::Java_mono_android_Runtime_createNewContextWithData (
		env,
		klass,
		runtimeApksJava,
		assembliesJava,
		nullptr, // assembliesBytes
		nullptr, // assembliesPaths
		loader,
		false    // force_preload_assemblies
	);
	log_info (LOG_DEFAULT, "%s LEAVE", __PRETTY_FUNCTION__);
	return ret;
}

JNIEXPORT void
JNICALL Java_mono_android_Runtime_switchToContext (JNIEnv *env, [[maybe_unused]] jclass klass, jint contextID)
{
	log_info (LOG_DEFAULT, "%s ENTER", __PRETTY_FUNCTION__);
	MonodroidRuntime::Java_mono_android_Runtime_switchToContext (env, contextID);
	log_info (LOG_DEFAULT, "%s LEAVE", __PRETTY_FUNCTION__);
}

JNIEXPORT void
JNICALL Java_mono_android_Runtime_destroyContexts (JNIEnv *env, [[maybe_unused]] jclass klass, jintArray array)
{
	log_info (LOG_DEFAULT, "%s ENTER", __PRETTY_FUNCTION__);
	MonodroidRuntime::Java_mono_android_Runtime_destroyContexts (env, array);
	log_info (LOG_DEFAULT, "%s LEAVE", __PRETTY_FUNCTION__);
}
#endif // ndef ANDROID
