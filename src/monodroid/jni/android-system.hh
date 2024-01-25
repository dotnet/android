// Dear Emacs, this is a -*- C++ -*- header
#ifndef __ANDROID_SYSTEM_H
#define __ANDROID_SYSTEM_H

#include <string_view>

#include <stdint.h>
#include <stdlib.h>
#include <pthread.h>
#include <jni.h>

#ifdef ANDROID
#include <sys/system_properties.h>
#endif

#include "util.hh"
#include "cppcompat.hh"
#include "xamarin-app.hh"
#include "shared-constants.hh"
#include "basic-android-system.hh"
#include "strings.hh"

#include <mono/jit/jit.h>

#if !defined (ANDROID)
constexpr uint32_t PROP_NAME_MAX = 32;
constexpr uint32_t PROP_VALUE_MAX = 92;
#endif

constexpr size_t PROPERTY_VALUE_BUFFER_LEN = PROP_VALUE_MAX + 1;

namespace xamarin::android {
	class jstring_wrapper;
	class jstring_array_wrapper;
}

namespace xamarin::android::internal
{
#if defined (DEBUG) || !defined (ANDROID)
	struct BundledProperty;
#endif

	class AndroidSystem : public BasicAndroidSystem
	{
	private:
#if defined (DEBUG) || !defined (ANDROID)
		static constexpr std::string_view OVERRIDE_ENVIRONMENT_FILE_NAME { "environment" };
		static constexpr uint32_t OVERRIDE_ENVIRONMENT_FILE_HEADER_SIZE = 22;
		static BundledProperty *bundled_properties;
#endif
#if defined (WINDOWS)
		static std::mutex readdir_mutex;
		static char *libmonoandroid_directory_path;
#endif

	public:
		void  setup_environment ();
		void  setup_process_args (jstring_array_wrapper &runtimeApks);
		void  create_update_dir (char *override_dir);
		int   monodroid_get_system_property (const char *name, char **value);
		int   monodroid_get_system_property (const char *name, dynamic_local_string<PROPERTY_VALUE_BUFFER_LEN>& value);

		int   monodroid_get_system_property (std::string_view const& name, char **value) noexcept
		{
			return monodroid_get_system_property (name.data (), value);
		}

		int   monodroid_get_system_property (std::string_view const& name, dynamic_local_string<PROPERTY_VALUE_BUFFER_LEN>& value) noexcept
		{
			return monodroid_get_system_property (name.data (), value);
		}

		size_t monodroid_get_system_property_from_overrides (const char *name, char ** value);
		char* get_bundled_app (JNIEnv *env, jstring dir);
		int   count_override_assemblies ();
		long  get_gref_gc_threshold ();
		void* load_dso (const char *path, unsigned int dl_flags, bool skip_exists_check);
		void* load_dso_from_any_directories (const char *name, unsigned int dl_flags);
		bool get_full_dso_path_on_disk (const char *dso_name, dynamic_local_string<SENSIBLE_PATH_MAX>& path);
		monodroid_dirent_t* readdir (monodroid_dir_t *dir);

		long get_max_gref_count () const
		{
			return max_gref_count;
		}

		void init_max_gref_count ()
		{
			max_gref_count = get_max_gref_count_from_system ();
		}

#if defined (WINDOWS)
		int setenv (const char *name, const char *value, int overwrite);
#endif
		bool is_assembly_preload_enabled () const
		{
			return application_config.uses_assembly_preload;
		}

		bool is_mono_llvm_enabled () const
		{
			return application_config.uses_mono_llvm;
		}

		bool is_mono_aot_enabled () const
		{
			return application_config.uses_mono_aot;
		}

		MonoAotMode get_mono_aot_mode () const
		{
			return aotMode;
		}

		bool is_interpreter_enabled () const
		{
#if !defined (NET)
			// HACK! See below
			return get_mono_aot_mode () == MonoAotMode::MONO_AOT_MODE_LAST && is_aot_mode_last_really_interpreter_mode ();
#else   // defined (NET)
			return get_mono_aot_mode () == MonoAotMode::MONO_AOT_MODE_INTERP_ONLY;
#endif  // !defined (NET)
		}

		// Hack, see comment for `aot_mode_last_is_interpreter` at the bottom of the class declaration
		bool is_aot_mode_last_really_interpreter_mode () const
		{
#if !defined(NET)
			return aot_mode_last_is_interpreter;
#else   // defined (NET)
			return false;
#endif  // !defined (NET)
		}

		void set_running_in_emulator (bool yesno)
		{
			running_in_emulator = yesno;
		}

	private:
#if defined (DEBUG) || !defined (ANDROID)
		void add_system_property (const char *name, const char *value);
		void setup_environment (const char *name, const char *value);
		void setup_environment_from_override_file (const char *path);
		BundledProperty* lookup_system_property (const char *name);
#endif
		const char* lookup_system_property (const char *name, size_t &value_len);
		long  get_max_gref_count_from_system ();
		void setup_process_args_apk (const char *apk, size_t index, size_t apk_count, void *user_data);
		int  _monodroid__system_property_get (const char *name, char *sp_value, size_t sp_value_len);
#if defined (DEBUG) || !defined (ANDROID)
		size_t  _monodroid_get_system_property_from_file (const char *path, char **value);
#endif
		bool get_full_dso_path (const char *base_dir, const char *dso_path, dynamic_local_string<SENSIBLE_PATH_MAX>& path);
		void* load_dso_from_specified_dirs (const char **directories, size_t num_entries, const char *dso_name, unsigned int dl_flags);
		void* load_dso_from_app_lib_dirs (const char *name, unsigned int dl_flags);
		void* load_dso_from_override_dirs (const char *name, unsigned int dl_flags);
		bool get_existing_dso_path_on_disk (const char *base_dir, const char *dso_name, dynamic_local_string<SENSIBLE_PATH_MAX>& path);

#if defined (WINDOWS)
		struct _wdirent* readdir_windows (_WDIR *dirp);
		char* get_libmonoandroid_directory_path ();
		int symlink (const char *target, const char *linkpath);

#endif // WINDOWS

#if !defined (ANDROID)
		void monodroid_strreplace (char *buffer, char old_char, char new_char);
#endif // !ANDROID
	private:
		long max_gref_count = 0;
		MonoAotMode aotMode = MonoAotMode::MONO_AOT_MODE_NONE;
		bool running_in_emulator = false;

#if !defined (NET)
		// This is a hack because of the way Mono currently switches the full interpreter (no JIT) mode. In Mono
		// **internal** headers there's an AOT mode macro, `MONO_EE_MODE_INTERP`, whose value is exactly the same as
		// MonoAotMode::MONO_AOT_MODE_LAST.  However, we use `MonoAotMode::MONO_AOT_MODE_LAST` as a sentinel to indicate
		// that we want to use the default Mono AOT/JIT mode and so we can't "overload" it to mean something else for
		// the sake of using Mono's internal functionality.  Until Mono makes `MONO_EE_MODE_INTERP` part of the public
		// `MonoAotMode` enum and its value is not in conflict with the sentinel, we will use this hack.
		//
		// See also: https://github.com/mono/mono/issues/18893
		//
		bool aot_mode_last_is_interpreter = false;
#endif  // !defined (NET)
	};
}
#endif // !__ANDROID_SYSTEM_H
