// This is a -*- c++ -*- header
#ifndef INC_MONODROID_DYLIB_MONO_H
#define INC_MONODROID_DYLIB_MONO_H

#include <stdint.h>
#include <stdio.h>

#include "monodroid.h"

#ifdef __cplusplus
namespace xamarin { namespace android
{
#endif // __cplusplus

enum MonoCounters {
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

#ifdef __cplusplus
inline MonoCounters operator | (MonoCounters left, MonoCounters right)
{
    return static_cast<MonoCounters > (static_cast<int> (left) | static_cast<int> (right));
}

inline MonoCounters operator & (MonoCounters left, MonoCounters right)
{
    return static_cast<MonoCounters> (static_cast<int> (left) & static_cast<int> (right));
}

inline MonoCounters& operator |= (MonoCounters& left, MonoCounters right)
{
    return left = left | right;
}
#endif // __cplusplus

#define XA_LOG_COUNTERS (MONO_COUNTER_JIT | MONO_COUNTER_METADATA | MONO_COUNTER_GC | MONO_COUNTER_GENERICS)

#define MONO_DEBUG_FORMAT_MONO 1

// Mock declarations of all the Mono types used by Mono API declared below.
// DO NOT use `typedef void`! This is a dangerous practice which may lead to hard to discover bugs
// during the runtime. Take for instance this scenario:
//
//   void my_code (MonoDomain *domain) { }
//   // ...
//   MonoDomain *domain = get_domain ();
//   my_code (&domain);
//
// The above code will compile without any warning or error from the compilers, since they allow
// silent cast from `void**` to `void*` and MonoDomain IS void! This kind of typedef is very
// confusing since the developer reading code will not know that MonoDomain is void unless they
// think to check it here.
//
// At the same time, we don't care what those types *really* are - a struct is simply a safe way to
// create a unique type and avoid overload resolution, parameter type casting etc errors.
//
#ifndef MonoAssembly
	typedef struct _MonoAssembly {} MonoAssembly;
#endif

#ifndef MonoAssemblyName
	typedef struct _MonoAssemblyName {} MonoAssemblyName;
#endif

#ifndef MonoClass
	typedef struct _MonoClass {} MonoClass;
#endif

#ifndef MonoClassField
	typedef struct _MonoClassField {} MonoClassField;
#endif

#ifndef MonoVTable
	typedef struct _MonoVTable {} MonoVTable;
#endif

#ifndef MonoDomain
	typedef struct _MonoDomain {} MonoDomain;
#endif

#ifndef MonoException
	typedef struct _MonoException {} MonoException;
#endif

#ifndef MonoImage
	typedef struct _MonoImage {} MonoImage;
#endif

#ifndef MonoJitInfo
	typedef struct _MonoJitInfo {} MonoJitInfo;
#endif

#ifndef MonoMethod
	typedef struct _MonoMethod {} MonoMethod;
#endif

#ifndef MonoObject
	typedef struct _MonoObject {} MonoObject;
#endif

#ifndef MonoProfiler
	typedef struct _MonoProfiler {} MonoProfiler;
#endif

#ifndef MonoProfilerHandle
	typedef struct _MonoProfilerDesc {} * MonoProfilerHandle;
#endif

#ifndef MonoProperty
	typedef struct _MonoProperty {} MonoProperty;
#endif

#ifndef MonoString
	typedef struct _MonoString {} MonoString;
#endif

#ifndef MonoThread
	typedef struct _MonoThread {} MonoThread;
#endif

#ifndef MonoType
	typedef struct _MonoType {} MonoType;
#endif

#ifndef MonoDlFallbackHandler
	typedef struct _MonoDlFallbackHandler {} MonoDlFallbackHandler;
#endif

typedef void (*MonoDomainFunc) (MonoDomain *domain, void* user_data);
typedef void (*MonoJitBeginEventFunc) (MonoProfiler *prof, MonoMethod *method);
typedef void (*MonoJitDoneEventFunc) (MonoProfiler *prof, MonoMethod *method, MonoJitInfo* jinfo);
typedef void (*MonoThreadStartedEventFunc) (MonoProfiler *prof, uintptr_t tid);
typedef void (*MonoThreadStoppedEventFunc) (MonoProfiler *prof, uintptr_t tid);

#ifdef __cplusplus
enum class MonoDlKind {
#else
enum MonoDlKind {
#endif // __cplusplus
	MONO_DL_LAZY  = 1,
	MONO_DL_LOCAL = 2,
	MONO_DL_MASK  = 3
};

#ifdef __cplusplus
enum class MonoImageOpenStatus {
#else
enum MonoImageOpenStatusC {
#endif // __cplusplus
	MONO_IMAGE_OK,
	MONO_IMAGE_ERROR_ERRNO,
	MONO_IMAGE_MISSING_ASSEMBLYREF,
	MONO_IMAGE_IMAGE_INVALID
};

#ifndef __cplusplus
typedef int MonoImageOpenStatus;
#endif

#ifdef __cplusplus
enum class MonoProfileFlags {
#else
enum MonoProfileFlags {
#endif // __cplusplus
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
};
#ifndef __cplusplus
typedef int MonoProfileFlags;
#endif

#ifdef __cplusplus
inline MonoProfileFlags operator | (MonoProfileFlags left, MonoProfileFlags right)
{
    return static_cast<MonoProfileFlags > (static_cast<int> (left) | static_cast<int> (right));
}

inline MonoProfileFlags operator & (MonoProfileFlags left, MonoProfileFlags right)
{
    return static_cast<MonoProfileFlags> (static_cast<int> (left) & static_cast<int> (right));
}

inline MonoProfileFlags& operator |= (MonoProfileFlags& left, MonoProfileFlags right)
{
    return left = left | right;
}

enum class MonoProfileResult {
#else
enum MonoProfileResult {
#endif // __cplusplus
	MONO_PROFILE_OK,
	MONO_PROFILE_FAILED
};

struct MonoBundledAssembly {
	const char *name;
	const unsigned char *data;
	const unsigned int size;
};

typedef struct MonoBundledAssembly MonoBundledAssembly;

typedef uint32_t mono_bool;
typedef uint8_t  mono_byte;

#ifndef MONO_ZERO_LEN_ARRAY
#ifdef __GNUC__
#define MONO_ZERO_LEN_ARRAY 0
#else
#define MONO_ZERO_LEN_ARRAY 1
#endif
#endif

#ifndef SGEN_BRIDGE_VERSION
#error  SGEN_BRIDGE_VERSION must be defined! (Use the `$(MonoSgenBridgeVersion)` MSBuild property)
#endif  /* ndef SGEN_BRIDGE_VERSION */

#if (SGEN_BRIDGE_VERSION < 4) || (SGEN_BRIDGE_VERSION >= 6)
#error  Only SGEN_BRIDGE_VERSION/$(MonoSgenBridgeVersion) values of 4 or 5 are supported.
#endif  /* SGEN_BRIDGE_VERSION check */

#ifdef __cplusplus
enum class MonoGCBridgeObjectKind {
#else
enum MonoGCBridgeObjectKind {
#endif // __cplusplus
	/* Instances of this class should be scanned when computing the transitive dependency among bridges. E.g. List<object>*/
	GC_BRIDGE_TRANSPARENT_CLASS,
	/* Instances of this class should not be scanned when computing the transitive dependency among bridges. E.g. String*/
	GC_BRIDGE_OPAQUE_CLASS,
	/* Instances of this class should be bridged and have their dependency computed. */
	GC_BRIDGE_TRANSPARENT_BRIDGE_CLASS,
	/* Instances of this class should be bridged but no dependencies should not be calculated. */
	GC_BRIDGE_OPAQUE_BRIDGE_CLASS,
};
#ifndef __cplusplus
typedef int MonoGCBridgeObjectKind;
#endif

struct MonoGCBridgeSCC {
	mono_bool is_alive;
	int num_objs;
	MonoObject *objs [MONO_ZERO_LEN_ARRAY];
};
#ifndef __cplusplus
typedef struct MonoGCBridgeSCC MonoGCBridgeSCC;
#endif

struct MonoGCBridgeXRef {
	int src_scc_index;
	int dst_scc_index;
};
#ifndef __cplusplus
typedef struct MonoGCBridgeXRef MonoGCBridgeXRef;
#endif

struct MonoGCBridgeCallbacks {
	int bridge_version;

	// UGLY!!!!!!
#ifdef __cplusplus
	MonoGCBridgeObjectKind
#else
	int
#endif // __cplusplus
	(*bridge_class_kind) (MonoClass *klass);

	mono_bool (*is_bridge_object) (MonoObject *object);
	void (*cross_references) (int num_sccs,
#ifdef __cplusplus
	                          MonoGCBridgeSCC **sccs,
#else
	                          int **sccs,
#endif // __cplusplus
	                          int num_xrefs,
#ifdef __cplusplus
	                          MonoGCBridgeXRef *xrefs
#else
	                          int *xrefs
#endif // __cplusplus
		);
};
#ifndef __cplusplus
typedef struct MonoGCBridgeCallbacks MonoGCBridgeCallbacks;
#endif

#ifdef __cplusplus
enum class MonoAotMode {
#else
enum MonoAotMode {
#endif // __cplusplus
	/* Disables AOT mode */
	MONO_AOT_MODE_NONE,
	/* Enables normal AOT mode, equivalent to mono_jit_set_aot_only (false) */
	MONO_AOT_MODE_NORMAL,
	/* Enables hyrbid AOT mode, JIT can still be used for wrappers */
	MONO_AOT_MODE_HYBRID,
	/* Enables full AOT mode, JIT is disabled and not allowed,
	 * equivalent to mono_jit_set_aot_only (true) */
	MONO_AOT_MODE_FULL,

	MONO_AOT_MODE_UNKNOWN = 0xBADBAD
};
#ifndef __cplusplus
typedef int MonoAotMode;
#endif

#ifdef __cplusplus
/* NOTE: structure members MUST NOT CHANGE ORDER. */
class DylibMono
{
#endif /* !__cplusplus */
	// Make sure the typedefs below match exactly their actual Mono counterpart!

	typedef MonoAssembly*          (*MonoAssemblyPreLoadFunc) (MonoAssemblyName *aname, char **assemblies_path, void *user_data);
	typedef void                   (*MonoProfileJitResult) (MonoProfiler *prof, MonoMethod   *method,   MonoJitInfo* jinfo,   int result);

	typedef void*                  (*MonoDlFallbackLoad) (const char *name, int flags, char **err, void *user_data);
	typedef void*                  (*MonoDlFallbackSymbol) (void *handle, const char *name, char **err, void *user_data);
	typedef void*                  (*MonoDlFallbackClose) (void *handle, void *user_data);

	typedef void                   (*monodroid_mono_config_parse_memory_fptr) (const char *buffer);
	typedef void                   (*monodroid_mono_add_internal_call_fptr) (const char *name, const void *method);
	typedef MonoImage*             (*monodroid_mono_assembly_get_image_fptr) (void *arg0);
	typedef MonoAssembly*          (*monodroid_mono_assembly_load_from_full_fptr) (MonoImage *image, const char *fname, MonoImageOpenStatus *status, mono_bool refonly);
	typedef MonoAssembly*          (*monodroid_mono_assembly_load_full_fptr) (MonoAssemblyName *aname, const char *basedir, MonoImageOpenStatus* status, mono_bool refonly);
	typedef MonoAssembly*          (*monodroid_mono_assembly_loaded_fptr) (MonoAssemblyName *aname);
	typedef const char*            (*monodroid_mono_assembly_name_get_culture_fptr) (MonoAssemblyName *aname);
	typedef const char*            (*monodroid_mono_assembly_name_get_name_fptr) (MonoAssemblyName *aname);
	typedef MonoAssemblyName*      (*monodroid_mono_assembly_name_new_fptr) (const char *name);
	typedef void                   (*monodroid_mono_assembly_name_free_fptr) (MonoAssemblyName *aname);
	typedef MonoAssembly*          (*monodroid_mono_assembly_open_full_fptr) (const char *filename, MonoImageOpenStatus *status, mono_bool refonly);
	typedef char*                  (*monodroid_mono_check_corlib_version_fptr) ();
	typedef MonoClass*             (*monodroid_mono_class_from_mono_type_fptr) (void *arg0);
	typedef MonoClass*             (*monodroid_mono_class_from_name_fptr) (MonoImage *image, const char *name_space, const char *name);
	typedef const char*            (*monodroid_mono_class_get_name_fptr) (MonoClass *arg0);
	typedef const char*            (*monodroid_mono_class_get_namespace_fptr) (MonoClass *arg0);
	typedef mono_bool              (*monodroid_mono_class_is_subclass_of_fptr) (MonoClass *klass, MonoClass *klassc, mono_bool use_interfaces);
	typedef MonoClassField*        (*monodroid_mono_class_get_field_from_name_fptr) (MonoClass *arg0, char *arg1);
	typedef MonoClassField*        (*monodroid_mono_class_get_fields_fptr) (MonoClass *arg0, void **arg1);
	typedef MonoMethod*            (*monodroid_mono_class_get_method_from_name_fptr) (MonoClass *arg0, const char *arg1, int arg2);
	typedef MonoProperty*          (*monodroid_mono_class_get_property_from_name_fptr) (MonoClass *klass, const char *name);
	typedef MonoVTable*            (*monodroid_mono_class_vtable_fptr) (MonoDomain *domain, MonoClass *klass);
	typedef void                   (*monodroid_mono_config_for_assembly_fptr) (MonoImage *assembly);
	typedef void                   (*monodroid_mono_counters_dump_fptr) (int section_mask, FILE* outfile);
	typedef void                   (*monodroid_mono_counters_enable_fptr) (int arg0);
	typedef void                   (*monodroid_mono_debug_init_fptr) (int format);
	typedef void                   (*monodroid_mono_debug_open_image_from_memory_fptr) (MonoImage *image, const mono_byte *raw_contents, int size);
	typedef MonoDlFallbackHandler* (*monodroid_mono_dl_fallback_register_fptr) (MonoDlFallbackLoad load_func, MonoDlFallbackSymbol symbol_func, MonoDlFallbackClose close_func, void *user_data);
	typedef MonoAssembly*          (*monodroid_mono_domain_assembly_open_fptr) (MonoDomain *arg0, const char *arg1);
	typedef MonoDomain*            (*monodroid_mono_domain_create_appdomain_fptr) (char *friendly_name, char *config_file);
	typedef void                   (*monodroid_mono_domain_foreach_fptr) (MonoDomainFunc func, void *user_data);
	typedef MonoDomain*            (*monodroid_mono_domain_from_appdomain_fptr) (MonoObject *appdomain);
	typedef MonoDomain*            (*monodroid_mono_domain_get_fptr) ();
	typedef MonoDomain*            (*monodroid_mono_domain_get_by_id_fptr) (int ID);
	typedef int                    (*monodroid_mono_domain_get_id_fptr) (MonoDomain *domain);
	typedef mono_bool              (*monodroid_mono_domain_set_fptr) (MonoDomain *domain, mono_bool force);
	typedef void                   (*monodroid_mono_domain_unload_fptr) (MonoDomain *domain);
	typedef MonoType*              (*monodroid_mono_field_get_type_fptr) (MonoClassField *arg0);
	typedef void                   (*monodroid_mono_field_get_value_fptr) (MonoObject *arg0, MonoClassField *arg1, void *arg2);
	typedef void                   (*monodroid_mono_field_set_value_fptr) (MonoObject *arg0, MonoClassField *arg1, void *arg2);
	typedef void                   (*monodroid_mono_field_static_set_value_fptr) (MonoVTable *vtable, MonoClassField *field, void *value);
	typedef void                   (*monodroid_mono_gc_register_bridge_callbacks_fptr) (void *callback);
	typedef void                   (*monodroid_mono_gc_wait_for_bridge_processing_fptr) (void);
	typedef MonoImage*             (*monodroid_mono_image_open_from_data_with_name_fptr) (char *data, uint32_t data_len, mono_bool need_copy, MonoImageOpenStatus *status, mono_bool refonly, const char *name);
	typedef void                   (*monodroid_mono_install_assembly_preload_hook_fptr) (MonoAssemblyPreLoadFunc func, void *user_data);
	typedef MonoDomain*            (*monodroid_mono_jit_init_version_fptr) (char *arg0, char *arg1);
	typedef void                   (*monodroid_mono_jit_cleanup_fptr) (MonoDomain *domain);
	typedef void                   (*monodroid_mono_jit_parse_options_fptr) (int argc, char **argv);
	typedef mono_bool              (*monodroid_mono_jit_set_trace_options_fptr) (const char *options);
	typedef MonoDomain*            (*monodroid_mono_jit_thread_attach) (MonoDomain *domain);
	typedef void                   (*monodroid_mono_jit_set_aot_mode_fptr) (MonoAotMode mode);
	typedef char*                  (*monodroid_mono_method_full_name_fptr) (MonoMethod *method, mono_bool signature);
	typedef MonoClass*             (*monodroid_mono_object_get_class_fptr) (MonoObject *obj);
	typedef MonoObject*            (*monodroid_mono_object_new_fptr) (MonoDomain *domain, MonoClass *klass);
	typedef void*                  (*monodroid_mono_object_unbox_fptr) (MonoObject *obj);
	typedef void                   (*monodroid_mono_profiler_install_fptr) (void *profiler, void *callback);
	typedef void                   (*monodroid_mono_profiler_install_jit_end_fptr) (MonoProfileJitResult end);
	typedef void                   (*monodroid_mono_profiler_install_thread_fptr) (void *start_ftn, void *end_ftn);
	typedef void                   (*monodroid_mono_profiler_set_events_fptr) (MonoProfileFlags events);
	typedef void                   (*monodroid_mono_property_set_value_fptr) (MonoProperty *prop, void *obj, void **params, MonoObject **exc);
	typedef void                   (*monodroid_mono_register_bundled_assemblies_fptr) (const MonoBundledAssembly **assemblies);
	typedef void                   (*monodroid_mono_register_config_for_assembly_fptr) (const char* assembly_name, const char* config_xml);
	typedef void                   (*monodroid_mono_register_symfile_for_assembly_fptr) (const char* assembly_name, const mono_byte *raw_contents, int size);
	typedef void                   (*monodroid_mono_register_machine_config_fptr) (const char *config);
	typedef MonoObject*            (*monodroid_mono_runtime_invoke_fptr) (MonoMethod *method, void *obj, void **params, MonoObject **exc);
	typedef void                   (*monodroid_mono_set_defaults_fptr)(int arg0, int arg1);
	typedef void                   (*monodroid_mono_set_crash_chaining_fptr)(mono_bool chain_crashes);
	typedef void                   (*monodroid_mono_set_signal_chaining_fptr)(mono_bool chain_signals);
	typedef MonoString*            (*monodroid_mono_string_new_fptr)(MonoDomain *domain, const char *text);
	typedef MonoThread*            (*monodroid_mono_thread_attach_fptr) (MonoDomain *domain);
	typedef void                   (*monodroid_mono_thread_create_fptr) (MonoDomain *domain, void* func, void* arg);
	typedef MonoThread*            (*monodroid_mono_thread_current_fptr) (void);
	typedef void                   (*monodroid_mono_gc_disable_fptr) (void);
	typedef void                   (*monodroid_mono_install_assembly_refonly_preload_hook_fptr) (MonoAssemblyPreLoadFunc func, void *user_data);
	typedef int                    (*monodroid_mono_runtime_set_main_args_fptr) (int argc, char* argv[]);
	typedef void                   (*mono_aot_register_module_fptr) (void* aot_info);
	typedef MonoProfilerHandle     (*monodroid_mono_profiler_create_fptr) (MonoProfiler* profiler);
	typedef void                   (*monodroid_mono_profiler_set_jit_begin_callback_fptr) (MonoProfilerHandle handle, MonoJitBeginEventFunc begin_ftn);
	typedef void                   (*monodroid_mono_profiler_set_jit_done_callback_fptr) (MonoProfilerHandle handle, MonoJitDoneEventFunc done_ftn);
	typedef void                   (*monodroid_mono_profiler_set_thread_started_callback_fptr) (MonoProfilerHandle handle, MonoThreadStartedEventFunc start_ftn);
	typedef void                   (*monodroid_mono_profiler_set_thread_stopped_callback_fptr) (MonoProfilerHandle handle, MonoThreadStoppedEventFunc stopped_ftn);

#ifdef __cplusplus
private:
#else
struct DylibMono {
#endif /* !__cplusplus */
	void                                                           *dl_handle;
	int                                                             version;
	monodroid_mono_assembly_get_image_fptr                          mono_assembly_get_image;
	monodroid_mono_assembly_load_from_full_fptr                     mono_assembly_load_from_full;
	monodroid_mono_assembly_load_full_fptr                          mono_assembly_load_full;
	monodroid_mono_assembly_name_get_culture_fptr                   mono_assembly_name_get_culture;
	monodroid_mono_assembly_name_get_name_fptr                      mono_assembly_name_get_name;
	monodroid_mono_assembly_name_new_fptr                           mono_assembly_name_new;
	monodroid_mono_assembly_name_free_fptr                          mono_assembly_name_free;
	monodroid_mono_assembly_open_full_fptr                          mono_assembly_open_full;
	monodroid_mono_class_from_mono_type_fptr                        mono_class_from_mono_type;
	monodroid_mono_class_from_name_fptr                             mono_class_from_name;
	monodroid_mono_class_get_name_fptr                              mono_class_get_name;
	monodroid_mono_class_get_namespace_fptr                         mono_class_get_namespace;
	monodroid_mono_class_get_field_from_name_fptr                   mono_class_get_field_from_name;
	monodroid_mono_class_get_fields_fptr                            mono_class_get_fields;
	monodroid_mono_class_get_method_from_name_fptr                  mono_class_get_method_from_name;
	monodroid_mono_class_is_subclass_of_fptr                        mono_class_is_subclass_of;
	monodroid_mono_class_vtable_fptr                                mono_class_vtable;
	monodroid_mono_config_parse_memory_fptr                         mono_config_parse_memory;
	monodroid_mono_counters_dump_fptr                               mono_counters_dump;
	monodroid_mono_counters_enable_fptr                             mono_counters_enable;
	monodroid_mono_debug_init_fptr                                  mono_debug_init;
	monodroid_mono_debug_open_image_from_memory_fptr                mono_debug_open_image_from_memory;
	monodroid_mono_domain_assembly_open_fptr                        mono_domain_assembly_open;
	monodroid_mono_dl_fallback_register_fptr                        mono_dl_fallback_register;
	monodroid_mono_field_get_type_fptr                              mono_field_get_type;
	monodroid_mono_field_get_value_fptr                             mono_field_get_value;
	monodroid_mono_field_set_value_fptr                             mono_field_set_value;
	monodroid_mono_field_static_set_value_fptr                      mono_field_static_set_value;
	monodroid_mono_gc_register_bridge_callbacks_fptr                mono_gc_register_bridge_callbacks;
	monodroid_mono_gc_wait_for_bridge_processing_fptr               mono_gc_wait_for_bridge_processing;
	monodroid_mono_image_open_from_data_with_name_fptr              mono_image_open_from_data_with_name;
	monodroid_mono_install_assembly_preload_hook_fptr               mono_install_assembly_preload_hook;
	monodroid_mono_jit_init_version_fptr                            mono_jit_init_version;
	monodroid_mono_jit_parse_options_fptr                           mono_jit_parse_options;
	monodroid_mono_jit_set_trace_options_fptr                       mono_jit_set_trace_options;
	monodroid_mono_method_full_name_fptr                            mono_method_full_name;
	monodroid_mono_object_get_class_fptr                            mono_object_get_class;
	monodroid_mono_object_unbox_fptr                                mono_object_unbox;
	monodroid_mono_profiler_install_fptr                            mono_profiler_install;
	monodroid_mono_profiler_install_jit_end_fptr                    mono_profiler_install_jit_end;
	monodroid_mono_profiler_install_thread_fptr                     mono_profiler_install_thread;
	monodroid_mono_profiler_set_events_fptr                         mono_profiler_set_events;
	monodroid_mono_register_bundled_assemblies_fptr                 mono_register_bundled_assemblies;
	monodroid_mono_register_config_for_assembly_fptr                mono_register_config_for_assembly;
	monodroid_mono_register_symfile_for_assembly_fptr               mono_register_symfile_for_assembly;
	monodroid_mono_register_machine_config_fptr                     mono_register_machine_config;
	monodroid_mono_runtime_invoke_fptr                              mono_runtime_invoke;
	monodroid_mono_set_defaults_fptr                                mono_set_defaults;
	monodroid_mono_set_crash_chaining_fptr                          mono_set_crash_chaining;
	monodroid_mono_set_signal_chaining_fptr                         mono_set_signal_chaining;
	monodroid_mono_thread_attach_fptr                               mono_thread_attach;
	monodroid_mono_gc_disable_fptr                                  mono_gc_disable;

	monodroid_mono_domain_foreach_fptr                              mono_domain_foreach;
	monodroid_mono_thread_create_fptr                               mono_thread_create;
	monodroid_mono_jit_thread_attach                                mono_jit_thread_attach;
	monodroid_mono_install_assembly_refonly_preload_hook_fptr       mono_install_assembly_refonly_preload_hook;
	monodroid_mono_jit_set_aot_mode_fptr                            mono_jit_set_aot_mode;
	monodroid_mono_runtime_set_main_args_fptr                       mono_runtime_set_main_args;
	int*                                                            mono_use_llvm;

	monodroid_mono_jit_cleanup_fptr                                 mono_jit_cleanup;
	monodroid_mono_domain_get_id_fptr                               mono_domain_get_id;
	monodroid_mono_domain_get_by_id_fptr                            mono_domain_get_by_id;
	monodroid_mono_domain_set_fptr                                  mono_domain_set;
	monodroid_mono_domain_get_fptr                                  mono_domain_get;
	monodroid_mono_domain_create_appdomain_fptr                     mono_domain_create_appdomain;
	monodroid_mono_domain_get_fptr                                  mono_get_root_domain;
	monodroid_mono_domain_unload_fptr                               mono_domain_unload;
	monodroid_mono_check_corlib_version_fptr                        mono_check_corlib_version;

	monodroid_mono_add_internal_call_fptr                           mono_add_internal_call;
	monodroid_mono_config_for_assembly_fptr                         mono_config_for_assembly;

	monodroid_mono_assembly_loaded_fptr                             mono_assembly_loaded;

	monodroid_mono_object_new_fptr                                  mono_object_new;
	monodroid_mono_string_new_fptr                                  mono_string_new;

	monodroid_mono_property_set_value_fptr                          mono_property_set_value;
	monodroid_mono_class_get_property_from_name_fptr                mono_class_get_property_from_name;
	monodroid_mono_domain_from_appdomain_fptr                       mono_domain_from_appdomain;
	monodroid_mono_thread_current_fptr                              mono_thread_current;
	mono_aot_register_module_fptr                                   mono_aot_register_module;
	monodroid_mono_profiler_create_fptr                             mono_profiler_create;
	monodroid_mono_profiler_set_jit_done_callback_fptr              mono_profiler_set_jit_done_callback;
	monodroid_mono_profiler_set_thread_started_callback_fptr        mono_profiler_set_thread_started_callback;
	monodroid_mono_profiler_set_thread_stopped_callback_fptr        mono_profiler_set_thread_stopped_callback;
	monodroid_mono_profiler_set_jit_begin_callback_fptr             mono_profiler_set_jit_begin_callback;

#ifdef __cplusplus
	bool initialized;

public:
	explicit DylibMono () = default;

	bool init (void *libmono_path);
	void close ();

	void set_use_llvm (bool use_llvm)
	{
		if (mono_use_llvm != nullptr)
			*mono_use_llvm = static_cast<int> (use_llvm);
	}

	monodroid_mono_register_bundled_assemblies_fptr get_register_bundled_assemblies_ptr () const
	{
		return mono_register_bundled_assemblies;
	}

	monodroid_mono_register_config_for_assembly_fptr get_register_config_for_assembly_ptr () const
	{
		return mono_register_config_for_assembly;
	}

	monodroid_mono_jit_set_aot_mode_fptr get_jit_set_aot_mode_ptr () const
	{
		return mono_jit_set_aot_mode;
	}

	monodroid_mono_config_parse_memory_fptr get_config_parse_memory_ptr () const
	{
		return mono_config_parse_memory;
	}

	monodroid_mono_register_machine_config_fptr get_register_machine_config_ptr () const
	{
		return mono_register_machine_config;
	}

	mono_aot_register_module_fptr get_aot_register_module_ptr () const
	{
		return mono_aot_register_module;
	}

	void config_parse_memory (const char *buffer);
	void add_internal_call (const char *name, const void *method);
	MonoImage* assembly_get_image (void *arg0);
	MonoAssembly* assembly_load_from_full (MonoImage *image, const char *fname, MonoImageOpenStatus *status, bool refonly);
	MonoAssembly* assembly_load_full (MonoAssemblyName *aname, const char *basedir, MonoImageOpenStatus* status, bool refonly);
	MonoAssembly* assembly_loaded (MonoAssemblyName *aname);
	const char* assembly_name_get_culture (MonoAssemblyName *aname);
	const char* assembly_name_get_name (MonoAssemblyName *aname);
	MonoAssemblyName* assembly_name_new (const char *name);
	void assembly_name_free (MonoAssemblyName *aname);
	MonoAssembly* assembly_open_full (const char *filename, MonoImageOpenStatus *status, bool refonly);
	char* check_corlib_version ();
	MonoClass* class_from_mono_type (void *arg0);
	MonoClass* class_from_name (MonoImage *image, const char *name_space, const char *name);
	const char* class_get_name (MonoClass *arg0);
	const char* class_get_namespace (MonoClass *arg0);
	bool class_is_subclass_of (MonoClass *klass, MonoClass *klassc, bool use_interfaces);
	MonoClassField* class_get_field_from_name (MonoClass *arg0, char *arg1);
	MonoClassField* class_get_fields (MonoClass *arg0, void **arg1);
	MonoMethod* class_get_method_from_name (MonoClass *arg0, const char *arg1, int arg2);
	MonoProperty* class_get_property_from_name (MonoClass *klass, const char *name);
	MonoVTable* class_vtable (MonoDomain *domain, MonoClass *klass);
	void config_for_assembly (MonoImage *assembly);
	void counters_dump (int section_mask, FILE* outfile);
	void counters_enable (int arg0);
	void debug_init (int format);
	void debug_open_image_from_memory (MonoImage *image, const mono_byte *raw_contents, int size);
	MonoDlFallbackHandler* dl_fallback_register (MonoDlFallbackLoad load_func, MonoDlFallbackSymbol symbol_func, MonoDlFallbackClose close_func, void *user_data);
	MonoAssembly* domain_assembly_open (MonoDomain *arg0, const char *arg1);
	MonoDomain* domain_create_appdomain (char *friendly_name, char *config_file);
	void domain_foreach (MonoDomainFunc func, void *user_data);
	MonoDomain* domain_from_appdomain (MonoObject *appdomain);
	MonoDomain* domain_get ();
	MonoDomain* domain_get_by_id (int ID);
	int domain_get_id (MonoDomain *domain);
	bool domain_set (MonoDomain *domain, bool force);
	void domain_unload (MonoDomain *domain);
	MonoType* field_get_type (MonoClassField *arg0);
	void field_get_value (MonoObject *arg0, MonoClassField *arg1, void *arg2);
	void field_set_value (MonoObject *arg0, MonoClassField *arg1, void *arg2);
	void field_static_set_value (MonoVTable *vtable, MonoClassField *field, void *value);
	void gc_register_bridge_callbacks (void *callback);
	void gc_wait_for_bridge_processing (void);
	MonoImage* image_open_from_data_with_name (char *data, uint32_t data_len, bool need_copy, MonoImageOpenStatus *status, bool refonly, const char *name);
	void install_assembly_preload_hook (MonoAssemblyPreLoadFunc func, void *user_data);
	MonoDomain* jit_init_version (char *arg0, char *arg1);
	void jit_cleanup (MonoDomain *domain);
	void jit_parse_options (int argc, char **argv);
	bool jit_set_trace_options (const char *options);
	MonoDomain* jit_thread_attach (MonoDomain *domain);
	void jit_set_aot_mode (MonoAotMode mode);
	char* method_full_name (MonoMethod *method, bool signature);
	MonoClass* object_get_class (MonoObject *obj);
	MonoObject* object_new (MonoDomain *domain, MonoClass *klass);
	void* object_unbox (MonoObject *obj);
	MonoProfilerHandle profiler_create ();
	void profiler_install_thread (MonoProfilerHandle handle, MonoThreadStartedEventFunc start_ftn, MonoThreadStoppedEventFunc end_ftn);
	void profiler_set_jit_begin_callback (MonoProfilerHandle handle, MonoJitBeginEventFunc begin_ftn);
	void profiler_set_jit_done_callback (MonoProfilerHandle handle, MonoJitDoneEventFunc done_ftn);
	void property_set_value (MonoProperty *prop, void *obj, void **params, MonoObject **exc);
	void register_bundled_assemblies (const MonoBundledAssembly **assemblies);
	void register_config_for_assembly (const char* assembly_name, const char* config_xml);
	void register_symfile_for_assembly (const char* assembly_name, const mono_byte *raw_contents, int size);
	void register_machine_config (const char *config);
	MonoObject* runtime_invoke (MonoMethod *method, void *obj, void **params, MonoObject **exc);
	void set_defaults(int arg0, int arg1);
	void set_crash_chaining(bool chain_crashes);
	void set_signal_chaining(bool chain_signals);
	MonoString* string_new(MonoDomain *domain, const char *text);
	MonoThread* thread_attach (MonoDomain *domain);
	void thread_create (MonoDomain *domain, void* func, void* arg);
	MonoThread *thread_current (void);
	void gc_disable (void);
	void install_assembly_refonly_preload_hook (MonoAssemblyPreLoadFunc func, void *user_data);
	int runtime_set_main_args (int argc, char* argv[]);
	MonoDomain* get_root_domain ();
	void aot_register_module (void *aot_info);

#endif /* __cplusplus */
};
#ifndef __cplusplus
typedef struct DylibMono DylibMono;
#endif

#ifdef __cplusplus
extern "C" {
#endif /* __cplusplus */
	MONO_API  DylibMono*  monodroid_dylib_mono_new (const char *libmono_path);
	MONO_API  void        monodroid_dylib_mono_free (DylibMono *mono_imports);
	          int         monodroid_dylib_mono_init (DylibMono *mono_imports, const char *libmono_path);
	          DylibMono*  monodroid_get_dylib (void);
	          int         monodroid_dylib_mono_init_with_handle (DylibMono *mono_imports, void *libmono_handle);
#ifdef __cplusplus
};

} }
#endif /* __cplusplus */

#endif /* INC_MONODROID_DYLIB_MONO_H */
