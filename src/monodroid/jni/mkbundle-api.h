#ifndef __MKBUNDLE_API_H
#define __MKBUNDLE_API_H

#ifdef __cplusplus
using namespace xamarin::android;
#endif

typedef struct BundleMonoAPI
{
	void (*mono_register_bundled_assemblies) (const MonoBundledAssembly **assemblies);
	void (*mono_register_config_for_assembly) (const char* assembly_name, const char* config_xml);
	void (*mono_jit_set_aot_mode) (int mode);
	void (*mono_aot_register_module) (void* aot_info);
	void (*mono_config_parse_memory) (const char *buffer);
	void (*mono_register_machine_config) (const char *config_xml);
} BundleMonoAPI;

#if ANDROID
#include <stdarg.h>
#include <android/log.h>

static void
mkbundle_log_error (const char *format, ...)
{
	va_list ap;

	va_start (ap, format);
	__android_log_vprint (ANDROID_LOG_ERROR, "mkbundle", format, ap);
	va_end (ap);
}
#endif // ANDROID
#endif // __MKBUNDLE_API_H
