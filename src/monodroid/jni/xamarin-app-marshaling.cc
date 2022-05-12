#include <cstdlib>
#include <android/log.h>

#include <jni.h>

#include <mono/metadata/appdomain.h>
#include <mono/metadata/class.h>
#include <mono/metadata/object.h>

#include "xamarin-app.hh"

static get_function_pointer_fn get_function_pointer;

void xamarin_app_init (get_function_pointer_fn fn)
{
	get_function_pointer = fn;
}

using android_app_activity_on_create_bundle_fn = void (*) (JNIEnv *env, jclass klass, jobject savedInstanceState);
static android_app_activity_on_create_bundle_fn android_app_activity_on_create_bundle = nullptr;

JNIEXPORT void
JNICALL Java_helloandroid_MainActivity_n_1onCreate__Landroid_os_Bundle_2 (JNIEnv *env, jclass klass, jobject savedInstanceState)
{
	//	log_info (LOG_DEFAULT, "%s (%p, %p, %p)", __PRETTY_FUNCTION__, env, klass, savedInstanceState);

	if (android_app_activity_on_create_bundle == nullptr) {
		void *fn = get_function_pointer (
			16 /* Mono.Android.dll index */,
			0x020000AF /* Android.App.Activity token */,
			0x0600055B /* n_OnCreate_Landroid_os_Bundle_ */
		);

		android_app_activity_on_create_bundle = reinterpret_cast<android_app_activity_on_create_bundle_fn>(fn);
	}

	android_app_activity_on_create_bundle (env, klass, savedInstanceState);
}

using android_app_activity_on_create_view_fn = jobject (*) (JNIEnv *env, jclass klass, jobject view, jstring name, jobject context, jobject attrs);
static android_app_activity_on_create_view_fn android_app_activity_on_create_view = nullptr;

JNIEXPORT jobject
JNICALL Java_helloandroid_MainActivity_n_1onCreateView__Landroid_view_View_2Ljava_lang_String_2Landroid_content_Context_2Landroid_util_AttributeSet_2 (JNIEnv *env, jclass klass, jobject view, jstring name, jobject context, jobject attrs)
{
	//	log_info (LOG_DEFAULT, "%s (%p, %p, %p, %p, %p, %p)", __PRETTY_FUNCTION__, env, klass, view, name, context, attrs);

	if (android_app_activity_on_create_view == nullptr) {
		void *fn = get_function_pointer (
			16 /* Mono.Android.dll index */,
			0x020000AF /* Android.App.Activity token */,
			0x06000564 /* n_OnCreateView_Landroid_view_View_Ljava_lang_String_Landroid_content_Context_Landroid_util_AttributeSet_ */
		);

		android_app_activity_on_create_view = reinterpret_cast<android_app_activity_on_create_view_fn>(fn);
	}

	return android_app_activity_on_create_view (env, klass, view, name, context, attrs);
}
