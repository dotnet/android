#include <stdio.h>
#include <stdint.h>
#include <stdlib.h>

extern "C" {
#include "java-interop-util.h"
}

#include "mono_android_Runtime.h"
#include "monodroid.h"
#include "dylib-mono.h"
#include "debug.h"
#include "embedded-assemblies.h"
#include "util.h"
#include "monodroid-glue.h"
#include "globals.h"

using namespace xamarin::android;
using namespace xamarin::android::internal;

static MonoMethod   *AndroidEnvironment_NotifyTimeZoneChanged;

static void
init (DylibMono *mono)
{
	MonoAssembly  *Mono_Android_dll;
	MonoImage     *Mono_Android_image;
	MonoClass     *AndroidEnvironment;

	if (AndroidEnvironment_NotifyTimeZoneChanged)
		return;

	Mono_Android_dll                          = utils.monodroid_load_assembly (mono->domain_get (), "Mono.Android");
	Mono_Android_image                        = mono->assembly_get_image (Mono_Android_dll);
	AndroidEnvironment                        = mono->class_from_name (Mono_Android_image,  "Android.Runtime",  "AndroidEnvironment");
	AndroidEnvironment_NotifyTimeZoneChanged  = mono->class_get_method_from_name (AndroidEnvironment, "NotifyTimeZoneChanged", 0);

	if (AndroidEnvironment_NotifyTimeZoneChanged == nullptr) {
		log_fatal (LOG_DEFAULT, "Unable to find Android.Runtime.AndroidEnvironment.NotifyTimeZoneChanged()!");
		exit (FATAL_EXIT_MISSING_ASSEMBLY);
	}
}

static void
clear_time_zone_caches_within_domain (void *user_data)
{
	DylibMono *mono = reinterpret_cast <DylibMono*> (user_data);

	mono->runtime_invoke (
			AndroidEnvironment_NotifyTimeZoneChanged, /* method */
			nullptr,                                  /* obj    */
			nullptr,                                  /* args   */
			nullptr                                   /* exc    */
	);
}

static void
clear_time_zone_caches (MonoDomain *domain, void *user_data)
{
	DylibMono *mono  = reinterpret_cast<DylibMono*> (user_data);

	mono->thread_create (domain, reinterpret_cast<void*> (clear_time_zone_caches_within_domain), mono);
}

extern "C" JNIEXPORT void
JNICALL Java_mono_android_Runtime_notifyTimeZoneChanged (JNIEnv *env, jclass klass)
{
	DylibMono *mono  = &monoFunctions;

	init (mono);
	mono->domain_foreach (clear_time_zone_caches, mono);
}
