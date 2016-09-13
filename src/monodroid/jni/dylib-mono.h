#ifndef INC_MONODROID_DYLIB_MONO_H
#define INC_MONODROID_DYLIB_MONO_H

#include <stdint.h>

enum {
	MONO_COUNTER_INT,    /* 32 bit int */
	MONO_COUNTER_UINT,    /* 32 bit uint */
	MONO_COUNTER_WORD,   /* pointer-sized int */
	MONO_COUNTER_LONG,   /* 64 bit int */
	MONO_COUNTER_ULONG,   /* 64 bit uint */
	MONO_COUNTER_DOUBLE,
	MONO_COUNTER_STRING, /* char* */
	MONO_COUNTER_TIME_INTERVAL, /* 64 bits signed int holding usecs. */
	MONO_COUNTER_TYPE_MASK = 0xf,
	MONO_COUNTER_CALLBACK = 128, /* ORed with the other values */
	MONO_COUNTER_SECTION_MASK = 0x00ffff00,
	/* Sections, bits 8-23 (16 bits) */
	MONO_COUNTER_JIT      = 1 << 8,
	MONO_COUNTER_GC       = 1 << 9,
	MONO_COUNTER_METADATA = 1 << 10,
	MONO_COUNTER_GENERICS = 1 << 11,
	MONO_COUNTER_SECURITY = 1 << 12,
	MONO_COUNTER_RUNTIME  = 1 << 13,
	MONO_COUNTER_SYSTEM   = 1 << 14,
	MONO_COUNTER_LAST_SECTION,

	/* Unit, bits 24-27 (4 bits) */
	MONO_COUNTER_UNIT_SHIFT = 24,
	MONO_COUNTER_UNIT_MASK = 0xFu << MONO_COUNTER_UNIT_SHIFT,
	MONO_COUNTER_RAW        = 0 << 24,  /* Raw value */
	MONO_COUNTER_BYTES      = 1 << 24, /* Quantity of bytes. RSS, active heap, etc */
	MONO_COUNTER_TIME       = 2 << 24,  /* Time interval in 100ns units. Minor pause, JIT compilation*/
	MONO_COUNTER_COUNT      = 3 << 24, /*  Number of things (threads, queued jobs) or Number of events triggered (Major collections, Compiled methods).*/
	MONO_COUNTER_PERCENTAGE = 4 << 24, /* [0-1] Fraction Percentage of something. Load average. */

	/* Monotonicity, bits 28-31 (4 bits) */
	MONO_COUNTER_VARIANCE_SHIFT = 28,
	MONO_COUNTER_VARIANCE_MASK = 0xFu << MONO_COUNTER_VARIANCE_SHIFT,
	MONO_COUNTER_MONOTONIC      = 1 << 28, /* This counter value always increase/decreases over time. Reported by --stat. */
	MONO_COUNTER_CONSTANT       = 1 << 29, /* Fixed value. Used by configuration data. */
	MONO_COUNTER_VARIABLE       = 1 << 30, /* This counter value can be anything on each sampling. Only interesting when sampling. */
};

#define XA_LOG_COUNTERS (MONO_COUNTER_JIT | MONO_COUNTER_METADATA | MONO_COUNTER_GC | MONO_COUNTER_GENERICS)

#define MONO_DEBUG_FORMAT_MONO 1

typedef void MonoAssembly;
typedef void MonoAssemblyName;
typedef void MonoClass;
typedef void MonoClassField;
typedef void MonoVTable;
typedef void MonoDomain;
typedef void MonoException;
typedef void MonoImage;
typedef void MonoJitInfo;
typedef void MonoMethod;
typedef void MonoObject;
typedef void MonoProfiler;
typedef void MonoType;
typedef void (*MonoDomainFunc) (MonoDomain *domain, void* user_data);

enum {
	MONO_DL_LAZY  = 1,
	MONO_DL_LOCAL = 2,
	MONO_DL_MASK  = 3
};

typedef enum {
	MONO_IMAGE_OK,
	MONO_IMAGE_ERROR_ERRNO,
	MONO_IMAGE_MISSING_ASSEMBLYREF,
	MONO_IMAGE_IMAGE_INVALID
} MonoImageOpenStatus;

typedef enum {
	MONO_PROFILE_NONE = 0,
	MONO_PROFILE_APPDOMAIN_EVENTS = 1 << 0,
	MONO_PROFILE_ASSEMBLY_EVENTS  = 1 << 1,
	MONO_PROFILE_MODULE_EVENTS    = 1 << 2,
	MONO_PROFILE_CLASS_EVENTS     = 1 << 3,
	MONO_PROFILE_JIT_COMPILATION  = 1 << 4,
	MONO_PROFILE_INLINING         = 1 << 5,
	MONO_PROFILE_EXCEPTIONS       = 1 << 6,
	MONO_PROFILE_ALLOCATIONS      = 1 << 7,
	MONO_PROFILE_GC               = 1 << 8,
	MONO_PROFILE_THREADS          = 1 << 9,
	MONO_PROFILE_REMOTING         = 1 << 10,
	MONO_PROFILE_TRANSITIONS      = 1 << 11,
	MONO_PROFILE_ENTER_LEAVE      = 1 << 12,
	MONO_PROFILE_COVERAGE         = 1 << 13,
	MONO_PROFILE_INS_COVERAGE     = 1 << 14,
	MONO_PROFILE_STATISTICAL      = 1 << 15,
	MONO_PROFILE_METHOD_EVENTS    = 1 << 16,
	MONO_PROFILE_MONITOR_EVENTS   = 1 << 17,
	MONO_PROFILE_IOMAP_EVENTS     = 1 << 18, /* this should likely be removed, too */
	MONO_PROFILE_GC_MOVES         = 1 << 19
} MonoProfileFlags;

typedef enum {
	MONO_PROFILE_OK,
	MONO_PROFILE_FAILED
} MonoProfileResult;

typedef struct {
	const char *name;
	const unsigned char *data;
	const unsigned int size;
} MonoBundledAssembly;

typedef uint32_t mono_bool;
typedef uint8_t  mono_byte;

#ifndef MONO_ZERO_LEN_ARRAY
#ifdef __GNUC__
#define MONO_ZERO_LEN_ARRAY 0
#else
#define MONO_ZERO_LEN_ARRAY 1
#endif
#endif

enum {
	SGEN_BRIDGE_VERSION = 5
};

typedef enum {
	/* Instances of this class should be scanned when computing the transitive dependency among bridges. E.g. List<object>*/
	GC_BRIDGE_TRANSPARENT_CLASS,
	/* Instances of this class should not be scanned when computing the transitive dependency among bridges. E.g. String*/
	GC_BRIDGE_OPAQUE_CLASS,
	/* Instances of this class should be bridged and have their dependency computed. */
	GC_BRIDGE_TRANSPARENT_BRIDGE_CLASS,
	/* Instances of this class should be bridged but no dependencies should not be calculated. */
	GC_BRIDGE_OPAQUE_BRIDGE_CLASS,
} MonoGCBridgeObjectKind;

typedef struct {
	mono_bool is_alive;
	int num_objs;
	MonoObject *objs [MONO_ZERO_LEN_ARRAY];
} MonoGCBridgeSCC;

typedef struct {
	int src_scc_index;
	int dst_scc_index;
} MonoGCBridgeXRef;

typedef struct {
	int bridge_version;
	MonoGCBridgeObjectKind (*bridge_class_kind) (MonoClass *class);
	mono_bool (*is_bridge_object) (MonoObject *object);
	void (*cross_references) (int num_sccs, MonoGCBridgeSCC **sccs, int num_xrefs, MonoGCBridgeXRef *xrefs);
} MonoGCBridgeCallbacks;

typedef enum {
	/* Disables AOT mode */
	MONO_AOT_MODE_NONE,
	/* Enables normal AOT mode, equivalent to mono_jit_set_aot_only (false) */
	MONO_AOT_MODE_NORMAL,
	/* Enables hyrbid AOT mode, JIT can still be used for wrappers */
	MONO_AOT_MODE_HYBRID,
	/* Enables full AOT mode, JIT is disabled and not allowed,
	 * equivalent to mono_jit_set_aot_only (true) */
	MONO_AOT_MODE_FULL
} MonoAotMode;

typedef MonoAssembly*   (*MonoAssemblyPreLoadFunc) (MonoAssemblyName *aname, char **assemblies_path, void *user_data);
typedef void            (*MonoProfileJitResult) (MonoProfiler *prof, MonoMethod   *method,   MonoJitInfo* jinfo,   int result);

typedef void*           (*MonoDlFallbackLoad) (const char *name, int flags, char **err, void *user_data);
typedef void*           (*MonoDlFallbackSymbol) (void *handle, const char *name, char **err, void *user_data);
typedef void*           (*MonoDlFallbackClose) (void *handle, void *user_data);

typedef void            (*monodroid_mono_config_parse_memory_fptr) (const char *buffer);
typedef void            (*monodroid_mono_add_internal_call_fptr) (const char *name, const void *method);
typedef void*           (*monodroid_mono_assembly_get_image_fptr) (void *arg0);
typedef void*           (*monodroid_mono_assembly_load_from_full_fptr) (MonoImage *image, const char *fname, MonoImageOpenStatus *status, mono_bool refonly);
typedef void*           (*monodroid_mono_assembly_load_full_fptr) (MonoAssemblyName *aname, const char *basedir, MonoImageOpenStatus* status, mono_bool refonly);
typedef void*           (*monodroid_mono_assembly_loaded_fptr) (MonoAssemblyName *aname);
typedef void*           (*monodroid_mono_assembly_name_get_culture_fptr) (MonoAssemblyName *aname);
typedef void*           (*monodroid_mono_assembly_name_get_name_fptr) (MonoAssemblyName *aname);
typedef void*           (*monodroid_mono_assembly_name_new_fptr) (const char *name);
typedef void            (*monodroid_mono_assembly_name_free_fptr) (MonoAssemblyName *aname);
typedef void*           (*monodroid_mono_assembly_open_full_fptr) (const char *filename, MonoImageOpenStatus *status, mono_bool refonly);
typedef char*           (*monodroid_mono_check_corlib_version_fptr) ();
typedef void*           (*monodroid_mono_class_from_mono_type_fptr) (void *arg0);
typedef void*           (*monodroid_mono_class_from_name_fptr) (MonoImage *image, const char *name_space, const char *name);
typedef char*           (*monodroid_mono_class_get_name_fptr) (MonoClass *arg0);
typedef char*           (*monodroid_mono_class_get_namespace_fptr) (MonoClass *arg0);
typedef mono_bool       (*monodroid_mono_class_is_subclass_of_fptr) (MonoClass *klass, MonoClass *klassc, mono_bool use_interfaces);
typedef void*           (*monodroid_mono_class_get_field_from_name_fptr) (MonoClass *arg0, char *arg1);
typedef MonoClassField* (*monodroid_mono_class_get_fields_fptr) (MonoClass *arg0, void **arg1);
typedef void*           (*monodroid_mono_class_get_method_from_name_fptr) (MonoClass *arg0, char *arg1, int arg2);
typedef MonoVTable*     (*monodroid_mono_class_vtable_fptr) (MonoDomain *domain, MonoClass *class);
typedef void            (*monodroid_mono_config_for_assembly_fptr) (MonoImage *assembly);
typedef void            (*monodroid_mono_counters_dump_fptr) (int section_mask, FILE* outfile);
typedef void            (*monodroid_mono_counters_enable_fptr) (int arg0);
typedef void*           (*monodroid_mono_debug_init_fptr) (int format);
typedef void*           (*monodroid_mono_debug_open_image_from_memory_fptr) (MonoImage *image, const mono_byte *raw_contents, int size);
typedef void*           (*monodroid_mono_dl_fallback_register_fptr) (MonoDlFallbackLoad load_func, MonoDlFallbackSymbol symbol_func, MonoDlFallbackClose close_func, void *user_data);
typedef void*           (*monodroid_mono_domain_assembly_open_fptr) (MonoDomain *arg0, const char *arg1);
typedef MonoDomain*     (*monodroid_mono_domain_create_appdomain_fptr) (char *friendly_name, char *config_file);
typedef void            (*monodroid_mono_domain_foreach_fptr) (MonoDomainFunc func, void *user_data);
typedef MonoDomain*     (*monodroid_mono_domain_get_fptr) ();
typedef MonoDomain*     (*monodroid_mono_domain_get_by_id_fptr) (int ID);
typedef int             (*monodroid_mono_domain_get_id_fptr) (MonoDomain *domain);
typedef mono_bool       (*monodroid_mono_domain_set_fptr) (MonoDomain *domain, mono_bool force);
typedef void            (*monodroid_mono_domain_unload_fptr) (MonoDomain *domain);
typedef MonoType*       (*monodroid_mono_field_get_type_fptr) (MonoClassField *arg0);
typedef void            (*monodroid_mono_field_get_value_fptr) (MonoObject *arg0, MonoClassField *arg1, void *arg2);
typedef void            (*monodroid_mono_field_set_value_fptr) (MonoObject *arg0, MonoClassField *arg1, void *arg2);
typedef void            (*monodroid_mono_field_static_set_value_fptr) (MonoVTable *vtable, MonoClassField *field, void *value);
typedef void            (*monodroid_mono_gc_register_bridge_callbacks_fptr) (void *callback);
typedef void            (*monodroid_mono_gc_wait_for_bridge_processing_fptr) (void);
typedef void*           (*monodroid_mono_image_open_from_data_with_name_fptr) (char *data, uint32_t data_len, mono_bool need_copy, MonoImageOpenStatus *status, mono_bool refonly, const char *name);
typedef void*           (*monodroid_mono_install_assembly_preload_hook_fptr) (MonoAssemblyPreLoadFunc func, void *user_data);
typedef void*           (*monodroid_mono_jit_init_version_fptr) (char *arg0, char *arg1);
typedef void            (*monodroid_mono_jit_cleanup_fptr) (MonoDomain *domain);
typedef void*           (*monodroid_mono_jit_parse_options_fptr) (int argc, char **argv);
typedef mono_bool       (*monodroid_mono_jit_set_trace_options_fptr) (const char *options);
typedef MonoDomain*     (*monodroid_mono_jit_thread_attach) (MonoDomain *domain);
typedef void            (*monodroid_mono_jit_set_aot_mode) (MonoAotMode mode);
typedef char*           (*monodroid_mono_method_full_name_fptr) (MonoMethod *method, mono_bool signature);
typedef MonoClass*      (*monodroid_mono_object_get_class_fptr) (MonoObject *obj);
typedef void*           (*monodroid_mono_object_unbox_fptr) (MonoObject *obj);
typedef void            (*monodroid_mono_profiler_install_fptr) (void *profiler, void *callback);
typedef void            (*monodroid_mono_profiler_install_jit_end_fptr) (MonoProfileJitResult end);
typedef void            (*monodroid_mono_profiler_install_thread_fptr) (void *start_ftn, void *end_ftn);
typedef void            (*monodroid_mono_profiler_set_events_fptr) (MonoProfileFlags events);
typedef void            (*monodroid_mono_register_bundled_assemblies_fptr) (const MonoBundledAssembly **assemblies);
typedef void            (*monodroid_mono_register_config_for_assembly_fptr) (const char* assembly_name, const char* config_xml);
typedef void            (*monodroid_mono_register_symfile_for_assembly_fptr) (const char* assembly_name, const mono_byte *raw_contents, int size);
typedef void*           (*monodroid_mono_register_machine_config_fptr) (const char *config);
typedef void*           (*monodroid_mono_runtime_invoke_fptr) (MonoMethod *method, void *obj, void **params, MonoObject **exc);
typedef void            (*monodroid_mono_set_defaults_fptr)(int arg0, int arg1);
typedef void            (*monodroid_mono_set_crash_chaining_fptr)(mono_bool chain_crashes);
typedef void            (*monodroid_mono_set_signal_chaining_fptr)(mono_bool chain_signals);
typedef void*           (*monodroid_mono_thread_attach_fptr) (MonoDomain *domain);
typedef void            (*monodroid_mono_thread_create_fptr) (MonoDomain *domain, void* func, void* arg);
typedef void            (*monodroid_mono_gc_disable_fptr) (void);
typedef void*           (*monodroid_mono_install_assembly_refonly_preload_hook_fptr) (MonoAssemblyPreLoadFunc func, void *user_data);
typedef int             (*monodroid_mono_runtime_set_main_args_fptr) (int argc, char* argv[]);

/* NOTE: structure members MUST NOT CHANGE ORDER. */
struct DylibMono {
	void                                                   *dl_handle;
	int                                                     version;
	monodroid_mono_assembly_get_image_fptr                  mono_assembly_get_image;
	monodroid_mono_assembly_load_from_full_fptr             mono_assembly_load_from_full;
	monodroid_mono_assembly_load_full_fptr                  mono_assembly_load_full;
	monodroid_mono_assembly_name_get_culture_fptr           mono_assembly_name_get_culture;
	monodroid_mono_assembly_name_get_name_fptr              mono_assembly_name_get_name;
	monodroid_mono_assembly_name_new_fptr                   mono_assembly_name_new;
	monodroid_mono_assembly_name_free_fptr                  mono_assembly_name_free;
	monodroid_mono_assembly_open_full_fptr                  mono_assembly_open_full;
	monodroid_mono_class_from_mono_type_fptr                mono_class_from_mono_type;
	monodroid_mono_class_from_name_fptr                     mono_class_from_name;
	monodroid_mono_class_get_name_fptr                      mono_class_get_name;
	monodroid_mono_class_get_namespace_fptr                 mono_class_get_namespace;
	monodroid_mono_class_get_field_from_name_fptr           mono_class_get_field_from_name;
	monodroid_mono_class_get_fields_fptr                    mono_class_get_fields;
	monodroid_mono_class_get_method_from_name_fptr          mono_class_get_method_from_name;
	monodroid_mono_class_is_subclass_of_fptr                mono_class_is_subclass_of;
	monodroid_mono_class_vtable_fptr                        mono_class_vtable;
	monodroid_mono_config_parse_memory_fptr                 mono_config_parse_memory;
	monodroid_mono_counters_dump_fptr                       mono_counters_dump;
	monodroid_mono_counters_enable_fptr                     mono_counters_enable;
	monodroid_mono_debug_init_fptr                          mono_debug_init;
	monodroid_mono_debug_open_image_from_memory_fptr        mono_debug_open_image_from_memory;
	monodroid_mono_domain_assembly_open_fptr                mono_domain_assembly_open;
	monodroid_mono_dl_fallback_register_fptr                mono_dl_fallback_register;
	monodroid_mono_field_get_type_fptr                      mono_field_get_type;
	monodroid_mono_field_get_value_fptr                     mono_field_get_value;
	monodroid_mono_field_set_value_fptr                     mono_field_set_value;
	monodroid_mono_field_static_set_value_fptr              mono_field_static_set_value;
	monodroid_mono_gc_register_bridge_callbacks_fptr        mono_gc_register_bridge_callbacks;
	monodroid_mono_gc_wait_for_bridge_processing_fptr       mono_gc_wait_for_bridge_processing;
	monodroid_mono_image_open_from_data_with_name_fptr      mono_image_open_from_data_with_name;
	monodroid_mono_install_assembly_preload_hook_fptr       mono_install_assembly_preload_hook;
	monodroid_mono_jit_init_version_fptr                    mono_jit_init_version;
	monodroid_mono_jit_parse_options_fptr                   mono_jit_parse_options;
	monodroid_mono_jit_set_trace_options_fptr               mono_jit_set_trace_options;
	monodroid_mono_method_full_name_fptr                    mono_method_full_name;
	monodroid_mono_object_get_class_fptr                    mono_object_get_class;
	monodroid_mono_object_unbox_fptr                        mono_object_unbox;
	monodroid_mono_profiler_install_fptr                    mono_profiler_install;
	monodroid_mono_profiler_install_jit_end_fptr            mono_profiler_install_jit_end;
	monodroid_mono_profiler_install_thread_fptr             mono_profiler_install_thread;
	monodroid_mono_profiler_set_events_fptr                 mono_profiler_set_events;
	monodroid_mono_register_bundled_assemblies_fptr         mono_register_bundled_assemblies;
	monodroid_mono_register_config_for_assembly_fptr        mono_register_config_for_assembly;
	monodroid_mono_register_symfile_for_assembly_fptr       mono_register_symfile_for_assembly;
	monodroid_mono_register_machine_config_fptr             mono_register_machine_config;
	monodroid_mono_runtime_invoke_fptr                      mono_runtime_invoke;
	monodroid_mono_set_defaults_fptr                        mono_set_defaults;
	monodroid_mono_set_crash_chaining_fptr                  mono_set_crash_chaining;
	monodroid_mono_set_signal_chaining_fptr                 mono_set_signal_chaining;
	monodroid_mono_thread_attach_fptr                       mono_thread_attach;
	monodroid_mono_gc_disable_fptr                          mono_gc_disable;

	monodroid_mono_domain_foreach_fptr                      mono_domain_foreach;
	monodroid_mono_thread_create_fptr                       mono_thread_create;
	monodroid_mono_jit_thread_attach                        mono_jit_thread_attach;
	monodroid_mono_install_assembly_refonly_preload_hook_fptr       mono_install_assembly_refonly_preload_hook;
	monodroid_mono_jit_set_aot_mode                         mono_jit_set_aot_mode;
	monodroid_mono_runtime_set_main_args_fptr               mono_runtime_set_main_args;
	int*                                                    mono_use_llvm;

	monodroid_mono_jit_cleanup_fptr                         mono_jit_cleanup;
	monodroid_mono_domain_get_id_fptr                       mono_domain_get_id;
	monodroid_mono_domain_get_by_id_fptr                    mono_domain_get_by_id;
	monodroid_mono_domain_set_fptr                          mono_domain_set;
	monodroid_mono_domain_get_fptr                          mono_domain_get;
	monodroid_mono_domain_create_appdomain_fptr             mono_domain_create_appdomain;
	monodroid_mono_domain_get_fptr                          mono_get_root_domain;
	monodroid_mono_domain_unload_fptr                       mono_domain_unload;
	monodroid_mono_check_corlib_version_fptr                mono_check_corlib_version;

	monodroid_mono_add_internal_call_fptr                   mono_add_internal_call;
	monodroid_mono_config_for_assembly_fptr                 mono_config_for_assembly;

	monodroid_mono_assembly_loaded_fptr                     mono_assembly_loaded;
};

MONO_API  struct  DylibMono*  monodroid_dylib_mono_new (const char *libmono_path);
MONO_API  void                monodroid_dylib_mono_free (struct DylibMono *mono_imports);
          int                 monodroid_dylib_mono_init (struct DylibMono *mono_imports, const char *libmono_path);

#endif /* INC_MONODROID_DYLIB_MONO_H */
