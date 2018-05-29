#include <assert.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <dlfcn.h>
#ifdef WINDOWS
#include <memory.h>
#endif

#include "java-interop-util.h"

#include "monodroid.h"
#include "dylib-mono.h"
#include "util.h"

struct DylibMono* monodroid_dylib_mono_new (const char *libmono_path)
{
	struct DylibMono *imports = calloc (1, sizeof (struct DylibMono));
	if (!imports)
		return NULL;
	if (!monodroid_dylib_mono_init (imports, libmono_path)) {
		free (imports);
		return NULL;
	}
	return imports;
}

void monodroid_dylib_mono_free (struct DylibMono *mono_imports)
{
	if (!mono_imports)
		return;
	dlclose (mono_imports->dl_handle);
	free (mono_imports);
}

int monodroid_dylib_mono_init (struct DylibMono *mono_imports, const char *libmono_path)
{
	int symbols_missing = FALSE;

	if (mono_imports == NULL)
		return FALSE;

	memset (mono_imports, 0, sizeof (*mono_imports));

	/*
	 * We need to use RTLD_GLOBAL so that libmono-profiler-log.so can resolve
	 * symbols against the Mono library we're loading.
	 */
	mono_imports->dl_handle = dlopen (libmono_path, RTLD_LAZY | RTLD_GLOBAL);

	if (!mono_imports->dl_handle) {
		return FALSE;
	}

	mono_imports->version   = sizeof (*mono_imports);

	log_info (LOG_DEFAULT, "Loading Mono symbols...");

#define LOAD_SYMBOL(symbol) \
	mono_imports->symbol = dlsym (mono_imports->dl_handle, #symbol); \
	if (!mono_imports->symbol) { \
		log_error (LOG_DEFAULT, "Failed to load Mono symbol: %s", #symbol); \
		symbols_missing = TRUE; \
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
	LOAD_SYMBOL(mono_get_root_domain)
	LOAD_SYMBOL(mono_gc_register_bridge_callbacks)
	LOAD_SYMBOL(mono_gc_wait_for_bridge_processing)
	LOAD_SYMBOL(mono_image_open_from_data_with_name)
	LOAD_SYMBOL(mono_install_assembly_preload_hook)
	LOAD_SYMBOL(mono_install_assembly_refonly_preload_hook)
	LOAD_SYMBOL(mono_jit_init_version)
	LOAD_SYMBOL(mono_jit_parse_options)
	LOAD_SYMBOL(mono_jit_set_trace_options)
	LOAD_SYMBOL(mono_jit_thread_attach)
	LOAD_SYMBOL(mono_jit_set_aot_mode)
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
	LOAD_SYMBOL(mono_use_llvm)


	if (symbols_missing) {
		log_fatal (LOG_DEFAULT, "Failed to load some Mono symbols, aborting...");
		exit (FATAL_EXIT_MONO_MISSING_SYMBOLS);
	}

	return TRUE;
}
