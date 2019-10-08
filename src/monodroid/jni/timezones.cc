#include <stdio.h>
#include <stdint.h>
#include <stdlib.h>

#include <mono/metadata/appdomain.h>
#include <mono/metadata/assembly.h>
#include <mono/metadata/class.h>
#include <mono/metadata/object.h>
#include <mono/metadata/threads.h>

#include "java-interop-util.h"

#include "mono_android_Runtime.h"
#include "monodroid.h"
#include "debug.hh"
#include "embedded-assemblies.hh"
#include "util.hh"
#include "monodroid-glue.hh"
#include "globals.hh"

using namespace xamarin::android;
using namespace xamarin::android::internal;

static MonoMethod   *AndroidEnvironment_NotifyTimeZoneChanged;

static void
init ()
{
	MonoAssembly  *Mono_Android_dll;
	MonoImage     *Mono_Android_image;
	MonoClass     *AndroidEnvironment;

	if (AndroidEnvironment_NotifyTimeZoneChanged)
		return;

	Mono_Android_dll                          = utils.monodroid_load_assembly (mono_domain_get (), "Mono.Android");
	Mono_Android_image                        = mono_assembly_get_image (Mono_Android_dll);
	AndroidEnvironment                        = mono_class_from_name (Mono_Android_image,  "Android.Runtime",  "AndroidEnvironment");
	AndroidEnvironment_NotifyTimeZoneChanged  = mono_class_get_method_from_name (AndroidEnvironment, "NotifyTimeZoneChanged", 0);

	if (AndroidEnvironment_NotifyTimeZoneChanged == nullptr) {
		log_fatal (LOG_DEFAULT, "Unable to find Android.Runtime.AndroidEnvironment.NotifyTimeZoneChanged()!");
		exit (FATAL_EXIT_MISSING_ASSEMBLY);
	}
}

static void
clear_time_zone_caches_within_domain (void *user_data)
{
	mono_runtime_invoke (
			AndroidEnvironment_NotifyTimeZoneChanged, /* method */
			nullptr,                                  /* obj    */
			nullptr,                                  /* args   */
			nullptr                                   /* exc    */
	);
}

static void
clear_time_zone_caches (MonoDomain *domain, void *user_data)
{
	mono_thread_create (domain, reinterpret_cast<void*> (clear_time_zone_caches_within_domain), user_data);
}

extern "C" JNIEXPORT void
JNICALL Java_mono_android_Runtime_notifyTimeZoneChanged (JNIEnv *env, jclass klass)
{
	init ();
	mono_domain_foreach (clear_time_zone_caches, nullptr);
}
