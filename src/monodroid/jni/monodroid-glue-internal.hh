// Dear Emacs, this is a -*- C++ -*- header
#ifndef __MONODROID_GLUE_INTERNAL_H
#define __MONODROID_GLUE_INTERNAL_H

#include <string>
#include <jni.h>
#include "android-system.hh"
#include "osbridge.hh"
#include "timing.hh"
#include "cpp-util.hh"
#include "xxhash.hh"

#include <mono/utils/mono-counters.h>
#include <mono/metadata/profiler.h>

#if defined (NET)
// NDEBUG causes robin_map.h not to include <iostream> which, in turn, prevents indirect inclusion of <mutex>. <mutex>
// conflicts with our std::mutex definition in cppcompat.hh
#if !defined (NDEBUG)
#define NDEBUG
#define NDEBUG_UNDEFINE
#endif

// hush some compiler warnings
#if defined (__clang__)
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Wunused-parameter"
#endif // __clang__

#include <tsl/robin_map.h>

#if defined (__clang__)
#pragma clang diagnostic pop
#endif // __clang__

#if defined (NDEBUG_UNDEFINE)
#undef NDEBUG
#undef NDEBUG_UNDEFINE
#endif

//#include <mono/utils/mono-publib.h>
#include <mono/jit/mono-private-unstable.h>
#include <mono/metadata/mono-private-unstable.h>
#endif

#if defined (NET)
// See https://github.com/dotnet/runtime/pull/67024
// See https://github.com/xamarin/xamarin-android/issues/6935
extern mono_bool mono_opt_aot_lazy_assembly_load;
#endif // def NET

namespace xamarin::android::internal
{
	struct PinvokeEntry
	{
		hash_t      hash;
		const char *name;
		void       *func;
	};

	struct string_hash
	{
		force_inline xamarin::android::hash_t operator() (std::string const& s) const noexcept
		{
			return xamarin::android::xxhash::hash (s.c_str (), s.length ());
		}
	};

	class MonodroidRuntime
	{
#if defined (NET)
		using pinvoke_api_map = tsl::robin_map<
			std::string,
			void*,
			string_hash,
			std::equal_to<std::string>,
			std::allocator<std::pair<std::string, void*>>,
			true
		>;

		using pinvoke_api_map_ptr = pinvoke_api_map*;
		using pinvoke_library_map = tsl::robin_map<
			std::string,
			pinvoke_api_map_ptr,
			string_hash,
			std::equal_to<std::string>,
			std::allocator<std::pair<std::string, pinvoke_api_map_ptr>>,
			true
		>;

		using load_assemblies_context_type = MonoAssemblyLoadContextGCHandle;
		static constexpr pinvoke_library_map::size_type LIBRARY_MAP_INITIAL_BUCKET_COUNT = 1;
#else // def NET
		using load_assemblies_context_type = MonoDomain*;
#endif // ndef NET

#if defined (DEBUG) && !defined (WINDOWS)
		struct RuntimeOptions {
			bool debug = false;
			int loglevel = 0;
			int64_t timeout_time = 0;
			char *host = nullptr;
			uint16_t sdb_port = 0;
			uint16_t out_port = 0;
			bool server = false;
		};
#endif

		// Keep the enum values in sync with those in src/Mono.Android/AndroidRuntime/BoundExceptionType.cs
		enum class BoundExceptionType : uint8_t
		{
			System = 0x00,
			Java   = 0x01,
		};

		struct JnienvInitializeArgs {
			JavaVM         *javaVm;
			JNIEnv         *env;
			jobject         grefLoader;
			jmethodID       Loader_loadClass;
			jclass          grefClass;
			jmethodID       Class_forName;
			unsigned int    logCategories;
			int             version;
			int             androidSdkVersion;
			int             localRefsAreIndirect;
			int             grefGcThreshold;
			jobject         grefIGCUserPeer;
			int             isRunningOnDesktop;
			uint8_t         brokenExceptionTransitions;
			int             packageNamingPolicy;
			uint8_t         boundExceptionType;
			int             jniAddNativeMethodRegistrationAttributePresent;
			bool            jniRemappingInUse;
			bool            marshalMethodsEnabled;
		};

#if defined (NET)
		using jnienv_initialize_fn = void (*) (JnienvInitializeArgs*);
		using jnienv_register_jni_natives_fn = void (*)(const jchar *typeName_ptr, int32_t typeName_len, jclass jniClass, const jchar *methods_ptr, int32_t methods_len);
#endif

	private:
		static constexpr char base_apk_name[] = "/base.apk";
		static constexpr size_t SMALL_STRING_PARSE_BUFFER_LEN = 50;
		static constexpr bool is_running_on_desktop =
#if ANDROID
		false;
#else
		true;
#endif

		static constexpr char mono_component_debugger_name[]            = "libmono-component-debugger.so";
		static constexpr hash_t mono_component_debugger_hash            = xxhash::hash (mono_component_debugger_name);

		static constexpr char mono_component_hot_reload_name[]          = "libmono-component-hot_reload.so";
		static constexpr hash_t mono_component_hot_reload_hash          = xxhash::hash (mono_component_hot_reload_name);

		static constexpr char mono_component_diagnostics_tracing_name[] = "libmono-component-diagnostics_tracing.so";
		static constexpr hash_t mono_component_diagnostics_tracing_hash = xxhash::hash (mono_component_diagnostics_tracing_name);

#if !defined (NET)
#define MAKE_API_DSO_NAME(_ext_) "libxa-internal-api." # _ext_
#if defined (WINDOWS)
		static constexpr char API_DSO_NAME[] = MAKE_API_DSO_NAME (dll);
#elif defined (APPLE_OS_X)
		static constexpr char API_DSO_NAME[] = MAKE_API_DSO_NAME (dylib);
#else   // !defined(WINDOWS) && !defined(APPLE_OS_X)
		static constexpr char API_DSO_NAME[] = MAKE_API_DSO_NAME (so);
#endif  // defined(WINDOWS)
#endif // ndef NET
	public:
		static constexpr int XA_LOG_COUNTERS = MONO_COUNTER_JIT | MONO_COUNTER_METADATA | MONO_COUNTER_GC | MONO_COUNTER_GENERICS | MONO_COUNTER_INTERP;

	public:
		void Java_mono_android_Runtime_register (JNIEnv *env, jstring managedType, jclass nativeClass, jstring methods);
		void Java_mono_android_Runtime_initInternal (JNIEnv *env, jclass klass, jstring lang, jobjectArray runtimeApksJava,
		                                             jstring runtimeNativeLibDir, jobjectArray appDirs, jint localDateTimeOffset,
		                                             jobject loader, jobjectArray assembliesJava, jint apiLevel, jboolean isEmulator,
		                                             jboolean haveSplitApks);
#if !defined (ANDROID)
		jint Java_mono_android_Runtime_createNewContextWithData (JNIEnv *env, jclass klass, jobjectArray runtimeApksJava, jobjectArray assembliesJava,
		                                                         jobjectArray assembliesBytes, jobjectArray assembliesPaths, jobject loader, jboolean force_preload_assemblies);
		void Java_mono_android_Runtime_switchToContext (JNIEnv *env, jint contextID);
		void Java_mono_android_Runtime_destroyContexts (JNIEnv *env, jintArray array);
		void shutdown_android_runtime (MonoDomain *domain);
#endif
		jint Java_JNI_OnLoad (JavaVM *vm, void *reserved);

		static bool is_startup_in_progress () noexcept
		{
			return startup_in_progress;
		}

		int get_android_api_level () const
		{
			return android_api_level;
		}

		jclass get_java_class_System () const
		{
			return java_System;
		}

		jmethodID get_java_class_method_System_identityHashCode () const
		{
			return java_System_identityHashCode;
		}

		jclass get_java_class_TimeZone () const
		{
			return java_TimeZone;
		}

		void set_monodroid_gdb_wait (bool yes_no)
		{
			monodroid_gdb_wait = yes_no;
		}

#if defined (NET)
		void propagate_uncaught_exception (JNIEnv *env, jobject javaThread, jthrowable javaException);
#else // def NET
		void propagate_uncaught_exception (MonoDomain *domain, JNIEnv *env, jobject javaThread, jthrowable javaException);

		FILE *get_counters () const
		{
			return counters;
		}

		// The reason we don't use the C++ overload feature here is that there appears to be an issue in clang++ that
		// comes with the Android NDK. The issue is that for calls like:
		//
		//   char *s = "something";
		//   dump_counters ("My string: %s", s);
		//
		// the compiler will resolve the overload taking `va_list` instead of the one with the ellipsis, thus causing
		// `vfprintf` to segfault while trying to perform a `strlen` on `args`.
		//
		// The issue appears to stem from the fact that `va_list` in the NDK is a typedef to `__builtin_va_list` which
		// in turn is internally defined by clang to be `char*`
		//
		// Desktop builds (using both clang++ and g++) do NOT appear to have this issue, so it might be a problem in the
		// NDK. More investigation would be required, but for now lets work around the issue by not overloading the
		// function
		void dump_counters (const char *format, ...);
		void dump_counters_v (const char *format, va_list args);
#endif // ndef NET

		char*	get_java_class_name_for_TypeManager (jclass klass);

	private:
#if defined (ANDROID)
		static void mono_log_handler (const char *log_domain, const char *log_level, const char *message, mono_bool fatal, void *user_data);
		static void mono_log_standard_streams_handler (const char *str, mono_bool is_stdout);

		// A reference to unique_ptr is not the best practice ever, but it's faster this way
		void setup_mono_tracing (std::unique_ptr<char[]> const& mono_log_mask, bool have_log_assembly, bool have_log_gc);
		void install_logging_handlers ();
#endif // def ANDROID

		unsigned int convert_dl_flags (int flags);
#if defined (WINDOWS) || defined (APPLE_OS_X)
		static const char* get_my_location (bool remove_file_name = true);
#endif  // defined(WINDOWS) || defined(APPLE_OS_X)
#if defined (NET)
		static void  cleanup_runtime_config (MonovmRuntimeConfigArguments *args, void *user_data);
		static void* load_library_symbol (const char *library_name, const char *symbol_name, void **dso_handle = nullptr) noexcept;
		static void* load_library_entry (std::string const& library_name, std::string const& entrypoint_name, pinvoke_api_map_ptr api_map) noexcept;
		static void  load_library_entry (const char *library_name, const char *entrypoint_name, PinvokeEntry &entry, void **dso_handle) noexcept;
		static void* fetch_or_create_pinvoke_map_entry (std::string const& library_name, std::string const& entrypoint_name, hash_t entrypoint_name_hash, pinvoke_api_map_ptr api_map, bool need_lock) noexcept;
		static PinvokeEntry* find_pinvoke_address (hash_t hash, const PinvokeEntry *entries, size_t entry_count) noexcept;
		static void* handle_other_pinvoke_request (const char *library_name, hash_t library_name_hash, const char *entrypoint_name, hash_t entrypoint_name_hash) noexcept;
		static void* monodroid_pinvoke_override (const char *library_name, const char *entrypoint_name);
#endif // def NET
		static void* monodroid_dlopen_ignore_component_or_load (hash_t hash, const char *name, int flags, char **err) noexcept;
		static void* monodroid_dlopen (const char *name, int flags, char **err) noexcept;
		static void* monodroid_dlopen (const char *name, int flags, char **err, void *user_data) noexcept;
		static void* monodroid_dlsym (void *handle, const char *name, char **err, void *user_data);
		static void* monodroid_dlopen_log_and_return (void *handle, char **err, const char *full_name, bool free_memory, bool need_api_init = false);
		static DSOCacheEntry* find_dso_cache_entry (hash_t hash) noexcept;
#if !defined (NET)
		static void  init_internal_api_dso (void *handle);
#endif // ndef NET
		int LocalRefsAreIndirect (JNIEnv *env, jclass runtimeClass, int version);
		void create_xdg_directory (jstring_wrapper& home, size_t home_len, const char *relativePath, size_t relative_path_len, const char *environmentVariableName);
		void create_xdg_directories_and_environment (jstring_wrapper &homeDir);
		void disable_external_signal_handlers ();
		void lookup_bridge_info (MonoClass *klass, const OSBridge::MonoJavaGCBridgeType *type, OSBridge::MonoJavaGCBridgeInfo *info);
#if defined (NET)
		void lookup_bridge_info (MonoImage *image, const OSBridge::MonoJavaGCBridgeType *type, OSBridge::MonoJavaGCBridgeInfo *info);
#else // def NET
		void lookup_bridge_info (MonoDomain *domain, MonoImage *image, const OSBridge::MonoJavaGCBridgeType *type, OSBridge::MonoJavaGCBridgeInfo *info);
#endif // ndef NET
		void load_assembly (MonoDomain *domain, jstring_wrapper &assembly);
#if defined (NET)
		void load_assembly (MonoAssemblyLoadContextGCHandle alc_handle, jstring_wrapper &assembly);
#endif // ndef NET
		void load_assemblies (load_assemblies_context_type ctx, bool preload, jstring_array_wrapper &assemblies);

		void set_debug_options ();
		void parse_gdb_options ();
		void mono_runtime_init (dynamic_local_string<PROPERTY_VALUE_BUFFER_LEN>& runtime_args);
#if defined (NET)
		void init_android_runtime (JNIEnv *env, jclass runtimeClass, jobject loader);
#else //def NET
		void init_android_runtime (MonoDomain *domain, JNIEnv *env, jclass runtimeClass, jobject loader);
#endif // ndef NET
		void set_environment_variable_for_directory (const char *name, jstring_wrapper &value, bool createDirectory, mode_t mode);

		void set_environment_variable_for_directory (const char *name, jstring_wrapper &value)
		{
			set_environment_variable_for_directory (name, value, true, DEFAULT_DIRECTORY_MODE);
		}

		void set_environment_variable (const char *name, jstring_wrapper &value)
		{
			set_environment_variable_for_directory (name, value, false, 0);
		}

#if defined (NET)
		static void monodroid_unhandled_exception (MonoObject *java_exception);

		MonoClass* get_android_runtime_class ();
#else // def NET
		MonoClass* get_android_runtime_class (MonoDomain *domain);
#endif
		MonoDomain*	create_domain (JNIEnv *env, jstring_array_wrapper &runtimeApks, bool is_root_domain, bool have_split_apks);
		MonoDomain* create_and_initialize_domain (JNIEnv* env, jclass runtimeClass, jstring_array_wrapper &runtimeApks,
		                                          jstring_array_wrapper &assemblies, jobjectArray assembliesBytes, jstring_array_wrapper &assembliesPaths,
		                                          jobject loader, bool is_root_domain, bool force_preload_assemblies,
		                                          bool have_split_apks);

		void gather_bundled_assemblies (jstring_array_wrapper &runtimeApks, size_t *out_user_assemblies_count, bool have_split_apks);
		static bool should_register_file (const char *filename);
		void set_trace_options ();
		void set_profile_options ();

		void log_jit_event (MonoMethod *method, const char *event_name);
		static void jit_begin (MonoProfiler *prof, MonoMethod *method);
		static void jit_failed (MonoProfiler *prof, MonoMethod *method);
		static void jit_done (MonoProfiler *prof, MonoMethod *method, MonoJitInfo* jinfo);
		static void thread_start (MonoProfiler *prof, uintptr_t tid);
		static void thread_end (MonoProfiler *prof, uintptr_t tid);
#if !defined (RELEASE) || !defined (ANDROID)
		static MonoReflectionType* typemap_java_to_managed (MonoString *java_type_name) noexcept;
		static const char* typemap_managed_to_java (MonoReflectionType *type, const uint8_t *mvid) noexcept;
#endif // !def RELEASE || !def ANDROID

#if defined (NET)
		static void monodroid_debugger_unhandled_exception (MonoException *ex);

#if defined (RELEASE) && defined (ANDROID)
		static const char* get_method_name (uint32_t mono_image_index, uint32_t method_token) noexcept;
		static const char* get_class_name (uint32_t class_index) noexcept;

		template<bool NeedsLocking>
		static void get_function_pointer (uint32_t mono_image_index, uint32_t class_index, uint32_t method_token, void*& target_ptr) noexcept;
		static void get_function_pointer_at_startup (uint32_t mono_image_index, uint32_t class_token, uint32_t method_token, void*& target_ptr) noexcept;
		static void get_function_pointer_at_runtime (uint32_t mono_image_index, uint32_t class_token, uint32_t method_token, void*& target_ptr) noexcept;
#endif // def RELEASE && def ANDROID
#endif // def NET

#if defined (DEBUG)
		void set_debug_env_vars (void);

#if !defined (WINDOWS)
		bool parse_runtime_args (dynamic_local_string<PROPERTY_VALUE_BUFFER_LEN> &runtime_args, RuntimeOptions *options);
		int monodroid_debug_connect (int sock, struct sockaddr_in addr);
		int monodroid_debug_accept (int sock, struct sockaddr_in addr);
#endif // !WINDOWS
#endif // DEBUG

#if !defined (RELEASE)
		static MonoAssembly* open_from_update_dir (MonoAssemblyName *aname, char **assemblies_path, void *user_data);
#endif
	private:
		MonoMethod         *registerType          = nullptr;
		int                 android_api_level     = 0;
		volatile bool       monodroid_gdb_wait    = true;
		jclass              java_System;
		jmethodID           java_System_identityHashCode;
		jmethodID           Class_getName;
		jclass              java_TimeZone;
		timing_period       jit_time;
		FILE               *jit_log;
		MonoProfilerHandle  profiler_handle;
#if !defined (NET)
		FILE               *counters;
#endif // ndef NET

		/*
		 * If set, monodroid will spin in a loop until the debugger breaks the wait by
		 * clearing monodroid_gdb_wait.
		 */
		bool                wait_for_gdb;

		/* The context (mapping to a Mono AppDomain) that is currently selected as the
		 * active context from the point of view of Java. We cannot rely on the value
		 * of `mono_domain_get` for this as it's stored per-thread and we want to be
		 * able to switch our different contexts from different threads.
		 */
		int                 current_context_id = -1;
		static bool         startup_in_progress;

#if defined (NET)
#if defined (ANDROID)
		jnienv_register_jni_natives_fn jnienv_register_jni_natives = nullptr;
#endif
		MonoAssemblyLoadContextGCHandle default_alc = nullptr;

		static std::mutex             pinvoke_map_write_lock;
		static pinvoke_library_map    other_pinvoke_map;
		static MonoCoreRuntimeProperties monovm_core_properties;
		MonovmRuntimeConfigArguments  runtime_config_args;

		static void *system_native_library_handle;
		static void *system_security_cryptography_native_android_library_handle;
		static void *system_io_compression_native_library_handle;
#else // def NET
		static std::mutex   api_init_lock;
		static void        *api_dso_handle;
#endif // !def NET
		static std::mutex   dso_handle_write_lock;
	};
}
#endif
