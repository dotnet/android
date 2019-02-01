#include <assert.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <dlfcn.h>
#ifdef WINDOWS
#include <memory.h>
#endif

#include "monodroid.h"
#include "dylib-mono.h"
#include "util.h"
#include "globals.h"

using namespace xamarin::android;

/*
  this function is used from JavaInterop and should be treated as public API
  https://github.com/xamarin/java.interop/blob/master/src/java-interop/java-interop-gc-bridge-mono.c#L266

  it should also accept libmono_path = nullptr parameter
*/
int monodroid_dylib_mono_init (DylibMono *mono_imports, const char *libmono_path)
{
	if (mono_imports == nullptr)
		return FALSE;

	/*
	 * We need to use RTLD_GLOBAL so that libmono-profiler-log.so can resolve
	 * symbols against the Mono library we're loading.
	 */

	void* handle = libmono_path ? androidSystem.load_dso (libmono_path, RTLD_LAZY | RTLD_GLOBAL, FALSE) : dlopen (libmono_path, RTLD_LAZY | RTLD_GLOBAL);

	return monoFunctions.init (handle);
}

bool DylibMono::init (void *libmono_handle)
{
	if (initialized)
		return true;

	if (libmono_handle == nullptr)
		return false;

	dl_handle = libmono_handle;
	version   = sizeof (*this);

	log_info (LOG_DEFAULT, "Loading Mono symbols...");

	bool symbols_missing = false;

#define LOAD_SYMBOL_CAST(symbol, cast_type)			\
	symbol = reinterpret_cast<cast_type> (dlsym (dl_handle, #symbol)); \
	if (symbol == nullptr) { \
		log_error (LOG_DEFAULT, "Failed to load Mono symbol: %s", #symbol); \
		symbols_missing = true; \
	}

#define LOAD_SYMBOL(symbol) LOAD_SYMBOL_CAST(symbol, monodroid_ ##symbol ##_fptr)
#define LOAD_SYMBOL_NO_PREFIX(symbol) LOAD_SYMBOL_CAST(symbol, symbol ##_fptr)

	timing_period total_time;
	if (XA_UNLIKELY (utils.should_log (LOG_TIMING))) {
		total_time.mark_start ();
	}

	LOAD_SYMBOL(mono_add_internal_call)
	LOAD_SYMBOL(mono_assembly_get_image)
	LOAD_SYMBOL(mono_assembly_load_from_full)
	LOAD_SYMBOL(mono_assembly_load_full)
	LOAD_SYMBOL(mono_assembly_loaded)
	LOAD_SYMBOL(mono_assembly_name_free)
	LOAD_SYMBOL(mono_assembly_name_get_culture)
	LOAD_SYMBOL(mono_assembly_name_get_name)
	LOAD_SYMBOL(mono_assembly_name_new)
	LOAD_SYMBOL(mono_assembly_open_full)
	LOAD_SYMBOL(mono_check_corlib_version)
	LOAD_SYMBOL(mono_class_from_mono_type)
	LOAD_SYMBOL(mono_class_from_name)
	LOAD_SYMBOL(mono_class_get_fields)
	LOAD_SYMBOL(mono_class_get_field_from_name)
	LOAD_SYMBOL(mono_class_get_method_from_name)
	LOAD_SYMBOL(mono_class_get_name)
	LOAD_SYMBOL(mono_class_get_namespace)
	LOAD_SYMBOL(mono_class_get_property_from_name)
	LOAD_SYMBOL(mono_class_is_subclass_of)
	LOAD_SYMBOL(mono_class_vtable)
	LOAD_SYMBOL(mono_config_for_assembly)
	LOAD_SYMBOL(mono_config_parse_memory)
	LOAD_SYMBOL(mono_counters_dump)
	LOAD_SYMBOL(mono_counters_enable)
	LOAD_SYMBOL(mono_debug_init)
	LOAD_SYMBOL(mono_debug_open_image_from_memory)
	LOAD_SYMBOL(mono_dl_fallback_register)
	LOAD_SYMBOL(mono_domain_assembly_open)
	LOAD_SYMBOL(mono_domain_create_appdomain)
	LOAD_SYMBOL(mono_domain_foreach)
	LOAD_SYMBOL(mono_domain_from_appdomain)
	LOAD_SYMBOL(mono_domain_get)
	LOAD_SYMBOL(mono_domain_get_id)
	LOAD_SYMBOL(mono_domain_get_by_id)
	LOAD_SYMBOL(mono_domain_set)
	LOAD_SYMBOL(mono_domain_unload)
	LOAD_SYMBOL(mono_field_get_type)
	LOAD_SYMBOL(mono_field_get_value)
	LOAD_SYMBOL(mono_field_set_value)
	LOAD_SYMBOL(mono_field_static_set_value)
	LOAD_SYMBOL_CAST(mono_get_root_domain, monodroid_mono_domain_get_fptr)
	LOAD_SYMBOL(mono_gc_register_bridge_callbacks)
	LOAD_SYMBOL(mono_gc_wait_for_bridge_processing)
	LOAD_SYMBOL(mono_image_open_from_data_with_name)
	LOAD_SYMBOL(mono_install_assembly_preload_hook)
	LOAD_SYMBOL(mono_install_assembly_refonly_preload_hook)
	LOAD_SYMBOL(mono_jit_init_version)
	LOAD_SYMBOL(mono_jit_parse_options)
	LOAD_SYMBOL(mono_jit_set_trace_options)
	LOAD_SYMBOL_CAST(mono_jit_thread_attach, monodroid_mono_jit_thread_attach)
	LOAD_SYMBOL_CAST(mono_jit_set_aot_mode, monodroid_mono_jit_set_aot_mode_fptr)
	LOAD_SYMBOL(mono_jit_cleanup)
	LOAD_SYMBOL(mono_method_full_name)
	LOAD_SYMBOL(mono_object_get_class)
	LOAD_SYMBOL(mono_object_new)
	LOAD_SYMBOL(mono_object_unbox)
	LOAD_SYMBOL(mono_profiler_install)
	LOAD_SYMBOL(mono_profiler_install_jit_end)
	LOAD_SYMBOL(mono_profiler_install_thread)
	LOAD_SYMBOL(mono_profiler_set_events)
	LOAD_SYMBOL(mono_property_set_value)
	LOAD_SYMBOL(mono_register_bundled_assemblies)
	LOAD_SYMBOL(mono_register_config_for_assembly)
	LOAD_SYMBOL(mono_register_machine_config)
	LOAD_SYMBOL(mono_register_symfile_for_assembly)
	LOAD_SYMBOL(mono_runtime_invoke)
	LOAD_SYMBOL(mono_runtime_set_main_args)
	LOAD_SYMBOL(mono_set_crash_chaining)
	LOAD_SYMBOL(mono_set_defaults)
	LOAD_SYMBOL(mono_set_signal_chaining)
	LOAD_SYMBOL(mono_string_new)
	LOAD_SYMBOL(mono_thread_attach)
	LOAD_SYMBOL(mono_thread_create)
	LOAD_SYMBOL(mono_thread_current)
	LOAD_SYMBOL_CAST(mono_use_llvm, int*)
	LOAD_SYMBOL_NO_PREFIX(mono_aot_register_module)
	LOAD_SYMBOL(mono_profiler_create)
	LOAD_SYMBOL(mono_profiler_set_jit_begin_callback)
	LOAD_SYMBOL(mono_profiler_set_jit_done_callback)
	LOAD_SYMBOL(mono_profiler_set_thread_started_callback)
	LOAD_SYMBOL(mono_profiler_set_thread_stopped_callback)

	if (XA_UNLIKELY (utils.should_log (LOG_TIMING))) {
		total_time.mark_end ();

		timing_diff diff (total_time);
		log_info_nocheck (LOG_TIMING, "DylibMono.init: end, total time; elapsed: %lis:%lu::%lu", diff.sec, diff.ms, diff.ns);
	}

	if (symbols_missing) {
		log_fatal (LOG_DEFAULT, "Failed to load some Mono symbols, aborting...");
		exit (FATAL_EXIT_MONO_MISSING_SYMBOLS);
	}

	initialized = true;
	return true;
}

void
DylibMono::close ()
{
	if (dl_handle != nullptr)
		dlclose (dl_handle);
}

void
DylibMono::config_parse_memory (const char *buffer)
{
	if (mono_config_parse_memory == nullptr)
		return;
	mono_config_parse_memory (buffer);
}

void
DylibMono::add_internal_call (const char *name, const void *method)
{
	if (mono_add_internal_call == nullptr)
		return;

	mono_add_internal_call (name, method);
}

MonoImage*
DylibMono::assembly_get_image (void *arg0)
{
	if (mono_assembly_get_image == nullptr)
		return nullptr;

	return mono_assembly_get_image (arg0);
}

MonoAssembly*
DylibMono::assembly_load_from_full (MonoImage *image, const char *fname, MonoImageOpenStatus *status, bool refonly)
{
	if (mono_assembly_load_from_full == nullptr)
		return nullptr;

	return mono_assembly_load_from_full (image, fname, status, refonly ? TRUE : FALSE);
}

MonoAssembly*
DylibMono::assembly_load_full (MonoAssemblyName *aname, const char *basedir, MonoImageOpenStatus* status, bool refonly)
{
	if (mono_assembly_load_full == nullptr)
		return nullptr;

	return mono_assembly_load_full (aname, basedir, status, refonly ? TRUE : FALSE);
}

MonoAssembly*
DylibMono::assembly_loaded (MonoAssemblyName *aname)
{
	if (mono_assembly_loaded == nullptr)
		return nullptr;

	return mono_assembly_loaded (aname);
}

const char*
DylibMono::assembly_name_get_culture (MonoAssemblyName *aname)
{
	if (mono_assembly_name_get_culture == nullptr)
		return nullptr;

	return mono_assembly_name_get_culture (aname);
}

const char*
DylibMono::assembly_name_get_name (MonoAssemblyName *aname)
{
	if (mono_assembly_name_get_name == nullptr)
		return nullptr;

	return mono_assembly_name_get_name (aname);
}

MonoAssemblyName*
DylibMono::assembly_name_new (const char *name)
{
	if (mono_assembly_name_new == nullptr)
		return nullptr;

	return mono_assembly_name_new (name);
}

void
DylibMono::assembly_name_free (MonoAssemblyName *aname)
{
	if (mono_assembly_name_free == nullptr)
		return;

	mono_assembly_name_free (aname);
}

MonoAssembly*
DylibMono::assembly_open_full (const char *filename, MonoImageOpenStatus *status, bool refonly)
{
	if (mono_assembly_open_full == nullptr)
		return nullptr;

	return mono_assembly_open_full (filename, status, refonly ? TRUE : FALSE);
}

char*
DylibMono::check_corlib_version ()
{
	if (mono_check_corlib_version == nullptr)
		return nullptr;

	return mono_check_corlib_version ();
}

MonoClass*
DylibMono::class_from_mono_type (void *arg0)
{
	if (mono_class_from_mono_type == nullptr)
		return nullptr;

	return mono_class_from_mono_type (arg0);
}

MonoClass*
DylibMono::class_from_name (MonoImage *image, const char *name_space, const char *name)
{
	if (mono_class_from_name == nullptr)
		return nullptr;

	return mono_class_from_name (image, name_space, name);
}

const char*
DylibMono::class_get_name (MonoClass *arg0)
{
	if (mono_class_get_name == nullptr)
		return nullptr;

	return mono_class_get_name (arg0);
}

const char*
DylibMono::class_get_namespace (MonoClass *arg0)
{
	if (mono_class_get_namespace == nullptr)
		return nullptr;

	return mono_class_get_namespace (arg0);
}

bool
DylibMono::class_is_subclass_of (MonoClass *klass, MonoClass *klassc, bool use_interfaces)
{
	if (mono_class_is_subclass_of == nullptr)
		return false;

	return mono_class_is_subclass_of (klass, klassc, use_interfaces ? TRUE : FALSE) ? true : false;
}

MonoClassField*
DylibMono::class_get_field_from_name (MonoClass *arg0, char *arg1)
{
	if (mono_class_get_field_from_name == nullptr)
		return nullptr;

	return mono_class_get_field_from_name (arg0, arg1);
}

MonoClassField*
DylibMono::class_get_fields (MonoClass *arg0, void **arg1)
{
	if (mono_class_get_fields == nullptr)
		return nullptr;

	return mono_class_get_fields (arg0, arg1);
}

MonoMethod*
DylibMono::class_get_method_from_name (MonoClass *arg0, const char *arg1, int arg2)
{
	if (mono_class_get_method_from_name == nullptr)
		return nullptr;

	return mono_class_get_method_from_name (arg0, arg1, arg2);
}

MonoProperty*
DylibMono::class_get_property_from_name (MonoClass *klass, const char *name)
{
	if (mono_class_get_property_from_name == nullptr)
		return nullptr;

	return mono_class_get_property_from_name (klass, name);
}

MonoVTable*
DylibMono::class_vtable (MonoDomain *domain, MonoClass *klass)
{
	if (mono_class_vtable == nullptr)
		return nullptr;

	return mono_class_vtable (domain, klass);
}

void
DylibMono::config_for_assembly (MonoImage *assembly)
{
	if (mono_config_for_assembly == nullptr)
		return;

	mono_config_for_assembly (assembly);
}

void
DylibMono::counters_dump (int section_mask, FILE* outfile)
{
	if (mono_counters_dump == nullptr)
		return;

	mono_counters_dump (section_mask, outfile);
}

void
DylibMono::counters_enable (int arg0)
{
	if (mono_counters_enable == nullptr)
		return;

	mono_counters_enable (arg0);
}

void
DylibMono::debug_init (int format)
{
	if (mono_debug_init == nullptr)
		return;

	mono_debug_init (format);
}

void
DylibMono::debug_open_image_from_memory (MonoImage *image, const mono_byte *raw_contents, int size)
{
	if (mono_debug_open_image_from_memory == nullptr)
		return;

	mono_debug_open_image_from_memory (image, raw_contents, size);
}

MonoDlFallbackHandler*
DylibMono::dl_fallback_register (MonoDlFallbackLoad load_func, MonoDlFallbackSymbol symbol_func, MonoDlFallbackClose close_func, void *user_data)
{
	if (mono_dl_fallback_register == nullptr)
		return nullptr;

	return mono_dl_fallback_register (load_func, symbol_func, close_func, user_data);
}

MonoAssembly*
DylibMono::domain_assembly_open (MonoDomain *arg0, const char *arg1)
{
	if (mono_domain_assembly_open == nullptr)
		return nullptr;

	return mono_domain_assembly_open (arg0, arg1);
}

MonoDomain*
DylibMono::domain_create_appdomain (char *friendly_name, char *config_file)
{
	if (mono_domain_create_appdomain == nullptr)
		return nullptr;

	return mono_domain_create_appdomain (friendly_name, config_file);
}

void
DylibMono::domain_foreach (MonoDomainFunc func, void *user_data)
{
	if (mono_domain_foreach == nullptr)
		return;

	mono_domain_foreach (func, user_data);
}

MonoDomain*
DylibMono::domain_from_appdomain (MonoObject *appdomain)
{
	if (mono_domain_from_appdomain == nullptr)
		return nullptr;

	return mono_domain_from_appdomain (appdomain);
}

MonoDomain*
DylibMono::domain_get ()
{
	if (mono_domain_get == nullptr)
		return nullptr;

	return mono_domain_get ();
}

MonoDomain*
DylibMono::domain_get_by_id (int ID)
{
	if (mono_domain_get_by_id == nullptr)
		return nullptr;

	return mono_domain_get_by_id (ID);
}

int
DylibMono::domain_get_id (MonoDomain *domain)
{
	if (mono_domain_get_id == nullptr)
		return -1;

	return mono_domain_get_id (domain);
}

bool
DylibMono::domain_set (MonoDomain *domain, bool force)
{
	if (mono_domain_set == nullptr)
		return false;

	return mono_domain_set (domain, force ? TRUE : FALSE) ? true : false;
}

void
DylibMono::domain_unload (MonoDomain *domain)
{
	if (mono_domain_unload == nullptr)
		return;

	mono_domain_unload (domain);
}

MonoType*
DylibMono::field_get_type (MonoClassField *arg0)
{
	if (mono_field_get_type == nullptr)
		return nullptr;

	return mono_field_get_type (arg0);
}

void
DylibMono::field_get_value (MonoObject *arg0, MonoClassField *arg1, void *arg2)
{
	if (mono_field_get_value == nullptr)
		return;

	mono_field_get_value (arg0, arg1, arg2);
}

void
DylibMono::field_set_value (MonoObject *arg0, MonoClassField *arg1, void *arg2)
{
	if (mono_field_set_value == nullptr)
		return;

	mono_field_set_value (arg0, arg1, arg2);
}

void
DylibMono::field_static_set_value (MonoVTable *vtable, MonoClassField *field, void *value)
{
	if (mono_field_static_set_value == nullptr)
		return;

	mono_field_static_set_value (vtable, field, value);
}

void
DylibMono::gc_register_bridge_callbacks (void *callback)
{
	if (mono_gc_register_bridge_callbacks == nullptr)
		return;

	mono_gc_register_bridge_callbacks (callback);
}

void
DylibMono::gc_wait_for_bridge_processing (void)
{
	if (mono_gc_wait_for_bridge_processing == nullptr)
		return;

	mono_gc_wait_for_bridge_processing ();
}

MonoImage*
DylibMono::image_open_from_data_with_name (char *data, uint32_t data_len, bool need_copy, MonoImageOpenStatus *status, bool refonly, const char *name)
{
	if (mono_image_open_from_data_with_name == nullptr)
		return nullptr;

	return mono_image_open_from_data_with_name (data, data_len, need_copy, status, refonly ? TRUE : FALSE, name);
}

void
DylibMono::install_assembly_preload_hook (MonoAssemblyPreLoadFunc func, void *user_data)
{
	if (mono_install_assembly_preload_hook == nullptr)
		return;

	mono_install_assembly_preload_hook (func, user_data);
}

MonoDomain*
DylibMono::jit_init_version (char *arg0, char *arg1)
{
	if (mono_jit_init_version == nullptr)
		return nullptr;

	return mono_jit_init_version (arg0, arg1);
}

void
DylibMono::jit_cleanup (MonoDomain *domain)
{
	if (mono_jit_cleanup == nullptr)
		return;

	mono_jit_cleanup (domain);
}

void
DylibMono::jit_parse_options (int argc, char **argv)
{
	if (mono_jit_parse_options == nullptr)
		return;

	mono_jit_parse_options (argc, argv);
}

bool
DylibMono::jit_set_trace_options (const char *options)
{
	if (mono_jit_set_trace_options == nullptr)
		return false;

	return mono_jit_set_trace_options (options);
}

MonoDomain*
DylibMono::jit_thread_attach (MonoDomain *domain)
{
	if (mono_jit_thread_attach == nullptr)
		return nullptr;

	return mono_jit_thread_attach (domain);
}

void
DylibMono::jit_set_aot_mode (MonoAotMode mode)
{
	if (mono_jit_set_aot_mode == nullptr)
		return;

	mono_jit_set_aot_mode (mode);
}

char*
DylibMono::method_full_name (MonoMethod *method, bool signature)
{
	if (mono_method_full_name == nullptr)
		return nullptr;

	return mono_method_full_name (method, signature ? TRUE : FALSE);
}

MonoClass*
DylibMono::object_get_class (MonoObject *obj)
{
	if (mono_object_get_class == nullptr)
		return nullptr;

	return mono_object_get_class (obj);
}

MonoObject*
DylibMono::object_new (MonoDomain *domain, MonoClass *klass)
{
	if (mono_object_new == nullptr)
		return nullptr;

	return mono_object_new (domain, klass);
}

void*
DylibMono::object_unbox (MonoObject *obj)
{
	if (mono_object_unbox == nullptr)
		return nullptr;

	return mono_object_unbox (obj);
}

MonoProfilerHandle
DylibMono::profiler_create ()
{
	if (mono_profiler_create == nullptr)
		return nullptr;

	return mono_profiler_create (nullptr);
}

void
DylibMono::profiler_install_thread (MonoProfilerHandle handle, MonoThreadStartedEventFunc start_ftn, MonoThreadStoppedEventFunc end_ftn)
{
	if (mono_profiler_set_thread_started_callback == nullptr || mono_profiler_set_thread_stopped_callback == nullptr)
		return;

	mono_profiler_set_thread_started_callback (handle, start_ftn);
	mono_profiler_set_thread_stopped_callback (handle, end_ftn);
}

void
DylibMono::profiler_set_jit_begin_callback (MonoProfilerHandle handle, MonoJitBeginEventFunc begin_ftn)
{
	if (mono_profiler_set_jit_begin_callback == nullptr)
		return;

	mono_profiler_set_jit_begin_callback (handle, begin_ftn);
}

void
DylibMono::profiler_set_jit_done_callback (MonoProfilerHandle handle, MonoJitDoneEventFunc done_ftn)
{
	if (mono_profiler_set_jit_done_callback == nullptr)
		return;

	mono_profiler_set_jit_done_callback (handle, done_ftn);
}

void
DylibMono::property_set_value (MonoProperty *prop, void *obj, void **params, MonoObject **exc)
{
	if (mono_property_set_value == nullptr)
		return;

	mono_property_set_value (prop, obj, params, exc);
}

void
DylibMono::register_bundled_assemblies (const MonoBundledAssembly **assemblies)
{
	if (mono_register_bundled_assemblies == nullptr)
		return;

	mono_register_bundled_assemblies (assemblies);
}

void
DylibMono::register_config_for_assembly (const char* assembly_name, const char* config_xml)
{
	if (mono_register_config_for_assembly == nullptr)
		return;

	mono_register_config_for_assembly (assembly_name, config_xml);
}

void
DylibMono::register_symfile_for_assembly (const char* assembly_name, const mono_byte *raw_contents, int size)
{
	if (mono_register_symfile_for_assembly == nullptr)
		return;

	mono_register_symfile_for_assembly (assembly_name, raw_contents, size);
}

void
DylibMono::register_machine_config (const char *config)
{
	if (mono_register_machine_config == nullptr)
		return;

	mono_register_machine_config (config);
}

MonoObject*
DylibMono::runtime_invoke (MonoMethod *method, void *obj, void **params, MonoObject **exc)
{
	if (mono_runtime_invoke == nullptr)
		return nullptr;

	return mono_runtime_invoke (method, obj, params, exc);
}

void
DylibMono::set_defaults (int arg0, int arg1)
{
	if (mono_set_defaults == nullptr)
		return;

	mono_set_defaults (arg0, arg1);
}

void
DylibMono::set_crash_chaining (bool chain_crashes)
{
	if (mono_set_crash_chaining == nullptr)
		return;

	mono_set_crash_chaining (chain_crashes ? TRUE : FALSE);
}

void
DylibMono::set_signal_chaining (bool chain_signals)
{
	if (mono_set_signal_chaining == nullptr)
		return;

	mono_set_signal_chaining (chain_signals ? TRUE : FALSE);
}

MonoString*
DylibMono::string_new (MonoDomain *domain, const char *text)
{
	if (mono_string_new == nullptr)
		return nullptr;

	return mono_string_new (domain, text);
}

MonoThread*
DylibMono::thread_attach (MonoDomain *domain)
{
	if (mono_thread_attach == nullptr)
		return nullptr;

	return mono_thread_attach (domain);
}

void
DylibMono::thread_create (MonoDomain *domain, void* func, void* arg)
{
	if (mono_thread_create == nullptr)
		return;

	mono_thread_create (domain, func, arg);
}

MonoThread*
DylibMono::thread_current (void)
{
	if (mono_thread_current == nullptr)
		return nullptr;

	return mono_thread_current ();
}

void
DylibMono::gc_disable ()
{
	if (mono_gc_disable == nullptr)
		return;

	mono_gc_disable ();
}

void
DylibMono::install_assembly_refonly_preload_hook (MonoAssemblyPreLoadFunc func, void *user_data)
{
	if (mono_install_assembly_refonly_preload_hook == nullptr)
		return;

	mono_install_assembly_refonly_preload_hook (func, user_data);
}

int
DylibMono::runtime_set_main_args (int argc, char* argv[])
{
	if (mono_runtime_set_main_args == nullptr)
		return -1;

	return mono_runtime_set_main_args (argc, argv);
}

MonoDomain*
DylibMono::get_root_domain ()
{
	if (mono_get_root_domain == nullptr)
		return nullptr;

	return mono_get_root_domain ();
}

void
DylibMono::aot_register_module (void *aot_info)
{
	if (mono_aot_register_module == nullptr)
		return;

	mono_aot_register_module (aot_info);
}
