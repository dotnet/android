#ifndef ANDROID_SYSTEM_HH
#define ANDROID_SYSTEM_HH

#include <array>
#include <cstddef>
#include <cstdint>
#include <cstdlib>
#include <span>
#include <string_view>

#include <pthread.h>
#include <dirent.h>

#include <jni.h>
#include <sys/system_properties.h>
#include <mono/jit/jit.h>

#include "xamarin-app.hh"
#include "cpu-arch.hh"
#include "jni-wrappers.hh"
#include "strings.hh"

static inline constexpr size_t PROPERTY_VALUE_BUFFER_LEN = PROP_VALUE_MAX + 1;

extern  FILE  *gref_log;
extern  FILE  *lref_log;
extern  bool   gref_to_logcat;
extern  bool   lref_to_logcat;

namespace xamarin::android {
	class jstring_wrapper;
	class jstring_array_wrapper;
}

namespace xamarin::android::internal {
#if defined (DEBUG)
	struct BundledProperty;
#endif

	class AndroidSystem
	{
	protected:
		using ForEachApkHandler = void (*) (const char *apk, size_t index, size_t apk_count, void *user_data);

	private:
#if defined (__clang__)
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Wc99-designator"
#endif
		// Values correspond to the CPU_KIND_* macros
		static constexpr const char* android_abi_names[CPU_KIND_X86_64+1] = {
			[0]                 = "unknown",
			[CPU_KIND_ARM]      = "armeabi-v7a",
			[CPU_KIND_ARM64]    = "arm64-v8a",
			[CPU_KIND_MIPS]     = "mips",
			[CPU_KIND_X86]      = "x86",
			[CPU_KIND_X86_64]   = "x86_64",
		};
#if defined (__clang__)
#pragma clang diagnostic pop
#endif
		static constexpr size_t ANDROID_ABI_NAMES_SIZE = sizeof(android_abi_names) / sizeof (android_abi_names[0]);

#if defined (DEBUG)
		static constexpr std::string_view OVERRIDE_ENVIRONMENT_FILE_NAME { "environment" };
		static constexpr uint32_t OVERRIDE_ENVIRONMENT_FILE_HEADER_SIZE = 22;
		static BundledProperty *bundled_properties;
#endif

	public:
#ifdef ANDROID64
		static constexpr std::string_view SYSTEM_LIB_PATH { "/system/lib64" };
#else
		static constexpr std::string_view SYSTEM_LIB_PATH { "/system/lib" };
#endif

		inline static std::array<char*, 1> override_dirs{};

		// This optimizes things a little bit. The array is allocated at build time, so we pay no cost for its
		// allocation and at run time it allows us to skip dynamic memory allocation.
		inline static std::array<const char*, 1> single_app_lib_directory{};
		inline static std::span<const char*> app_lib_directories;

	public:
		static void setup_app_library_directories (jstring_array_wrapper& runtimeApks, jstring_array_wrapper& appDirs, bool have_split_apks) noexcept;

		static size_t monodroid_get_system_property_from_overrides (const char *name, char ** value) noexcept;
		static char* get_bundled_app (JNIEnv *env, jstring dir) noexcept;
		static int count_override_assemblies () noexcept;
		static long get_gref_gc_threshold () noexcept;
		static void* load_dso (const char *path, unsigned int dl_flags, bool skip_exists_check) noexcept;
		static void* load_dso_from_any_directories (const char *name, unsigned int dl_flags) noexcept;
		static bool get_full_dso_path_on_disk (const char *dso_name, dynamic_local_string<SENSIBLE_PATH_MAX>& path) noexcept;
		static void setup_environment () noexcept;
		static void setup_process_args (jstring_array_wrapper &runtimeApks) noexcept;
		static void create_update_dir (char *override_dir) noexcept;
		static int monodroid_get_system_property (const char *name, char **value) noexcept;
		static int monodroid_get_system_property (const char *name, dynamic_local_string<PROPERTY_VALUE_BUFFER_LEN> &value) noexcept;

		static int monodroid_get_system_property (std::string_view const& name, char **value) noexcept
		{
			return monodroid_get_system_property (name.data (), value);
		}

		static int monodroid_get_system_property (std::string_view const& name, dynamic_local_string<PROPERTY_VALUE_BUFFER_LEN>& value) noexcept
		{
			return monodroid_get_system_property (name.data (), value);
		}

		static void set_override_dir (uint32_t index, const char* dir) noexcept
		{
			if (index >= override_dirs.size ())
				return;

			override_dirs [index] = const_cast <char*> (dir);
		}

		static bool is_embedded_dso_mode_enabled () noexcept
		{
			return embedded_dso_mode_enabled;
		}

		static void detect_embedded_dso_mode (jstring_array_wrapper& appDirs) noexcept;

		static char *get_runtime_libdir () noexcept
		{
			return runtime_libdir;
		}

		static void set_runtime_libdir (char *dir) noexcept
		{
			runtime_libdir = dir;
		}

		static char *get_primary_override_dir () noexcept
		{
			return primary_override_dir;
		}

		static void set_primary_override_dir (jstring_wrapper& home) noexcept
		{
			primary_override_dir = determine_primary_override_dir (home);
		}

		static long get_max_gref_count () noexcept
		{
			return max_gref_count;
		}

		static void init_max_gref_count () noexcept
		{
			max_gref_count = get_max_gref_count_from_system ();
		}

		static bool is_assembly_preload_enabled () noexcept
		{
			return application_config.uses_assembly_preload;
		}

		static bool is_mono_llvm_enabled () noexcept
		{
			return application_config.uses_mono_llvm;
		}

		static bool is_mono_aot_enabled () noexcept
		{
			return application_config.uses_mono_aot;
		}

		static MonoAotMode get_mono_aot_mode () noexcept
		{
			return aotMode;
		}

		static bool is_interpreter_enabled () noexcept
		{
			return get_mono_aot_mode () == MonoAotMode::MONO_AOT_MODE_INTERP_ONLY;
		}

		// Hack, see comment for `aot_mode_last_is_interpreter` at the bottom of the class declaration
		static bool is_aot_mode_last_really_interpreter_mode () noexcept
		{
			return false;
		}

		static void set_running_in_emulator (bool yesno) noexcept
		{
			running_in_emulator = yesno;
		}

	protected:
		static void for_each_apk (jstring_array_wrapper &runtimeApks, ForEachApkHandler handler, void *user_data) noexcept;

	private:
#if defined (DEBUG)
		static void add_system_property (const char *name, const char *value) noexcept;
		static void setup_environment (const char *name, const char *value) noexcept;
		static void setup_environment_from_override_file (const char *path) noexcept;
		static BundledProperty* lookup_system_property (const char *name) noexcept;
#endif
		static const char* lookup_system_property (const char *name, size_t &value_len) noexcept;
		static long get_max_gref_count_from_system () noexcept;
		static void setup_process_args_apk (const char *apk, size_t index, size_t apk_count, void *user_data) noexcept;
		static int _monodroid__system_property_get (const char *name, char *sp_value, size_t sp_value_len) noexcept;
#if defined (DEBUG)
		static size_t  _monodroid_get_system_property_from_file (const char *path, char **value) noexcept;
#endif
		static bool get_full_dso_path (const char *base_dir, const char *dso_path, dynamic_local_string<SENSIBLE_PATH_MAX>& path) noexcept;
		static void* load_dso_from_specified_dirs (const char **directories, size_t num_entries, const char *dso_name, unsigned int dl_flags) noexcept;
		static void* load_dso_from_app_lib_dirs (const char *name, unsigned int dl_flags) noexcept;
		static void* load_dso_from_override_dirs (const char *name, unsigned int dl_flags) noexcept;
		static bool get_existing_dso_path_on_disk (const char *base_dir, const char *dso_name, dynamic_local_string<SENSIBLE_PATH_MAX>& path) noexcept;

	private:
		static void add_apk_libdir (const char *apk, size_t &index, const char *abi) noexcept;
		static void setup_apk_directories (unsigned short running_on_cpu, jstring_array_wrapper &runtimeApks, bool have_split_apks) noexcept;
		static char* determine_primary_override_dir (jstring_wrapper &home) noexcept;

		static void set_embedded_dso_mode_enabled (bool yesno) noexcept
		{
			embedded_dso_mode_enabled = yesno;
		}

	private:
		static inline bool  embedded_dso_mode_enabled = false;
		static inline char *runtime_libdir = nullptr;
		static inline char *primary_override_dir = nullptr;
		static inline long max_gref_count = 0;
		static inline MonoAotMode aotMode = MonoAotMode::MONO_AOT_MODE_NONE;
		static inline bool running_in_emulator = false;
	};
}
#endif // !ANDROID_SYSTEM_HH
