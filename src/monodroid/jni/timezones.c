#include <stdio.h>
#include <stdint.h>
#include <stdlib.h>

#include "java-interop-util.h"

#include "mono_android_Runtime.h"
#include "monodroid.h"
#include "dylib-mono.h"
#include "debug.h"
#include "embedded-assemblies.h"
#include "util.h"
#include "monodroid-glue.h"

static MonoMethod   *AndroidEnvironment_NotifyTimeZoneChanged;

static void
init (struct DylibMono *mono)
{
	MonoAssembly  *Mono_Android_dll;
	MonoImage     *Mono_Android_image;
	MonoClass     *AndroidEnvironment;

	if (AndroidEnvironment_NotifyTimeZoneChanged)
		return;

	Mono_Android_dll                          = monodroid_load_assembly (mono, mono->mono_domain_get (), "Mono.Android");
	Mono_Android_image                        = mono->mono_assembly_get_image (Mono_Android_dll);
	AndroidEnvironment                        = mono->mono_class_from_name (Mono_Android_image,  "Android.Runtime",  "AndroidEnvironment");
	AndroidEnvironment_NotifyTimeZoneChanged  = mono->mono_class_get_method_from_name (AndroidEnvironment, "NotifyTimeZoneChanged", 0);

	if (AndroidEnvironment_NotifyTimeZoneChanged == NULL) {
		log_fatal (LOG_DEFAULT, "Unable to find Android.Runtime.AndroidEnvironment.NotifyTimeZoneChanged()!");
		exit (FATAL_EXIT_MISSING_ASSEMBLY);
	}
}

static void
clear_time_zone_caches_within_domain (void *user_data)
{
	struct DylibMono *mono = user_data;

	mono->mono_runtime_invoke (
			AndroidEnvironment_NotifyTimeZoneChanged, /* method */
			NULL,                                     /* obj    */
			NULL,                                     /* args   */
			NULL                                      /* exc    */
	);
}

static void
clear_time_zone_caches (MonoDomain *domain, void *user_data)
{
	struct DylibMono *mono  = user_data;

	mono->mono_thread_create (domain, clear_time_zone_caches_within_domain, mono);
}

JNIEXPORT void
JNICALL Java_mono_android_Runtime_notifyTimeZoneChanged (JNIEnv *env, jclass klass)
{
	struct DylibMono *mono  = monodroid_get_dylib ();

	if (mono->mono_domain_foreach == NULL)
		return;

	init (mono);

	mono->mono_domain_foreach (clear_time_zone_caches, mono);
}
