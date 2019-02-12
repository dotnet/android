// Dear Emacs, this is a -*- C++ -*- header
#ifndef __ANDROID_SYSTEM_H
#define __ANDROID_SYSTEM_H

#include <stdint.h>
#include <stddef.h>
#include <pthread.h>
#include <jni.h>

#include "dylib-mono.h"
#include "util.h"
#include "cpu-arch.h"
#include "cppcompat.h"
#include "xamarin-app.h"

namespace xamarin { namespace android {
	class jstring_wrapper;
	class jstring_array_wrapper;
}}

namespace xamarin { namespace android { namespace internal
{
#if defined (DEBUG) || !defined (ANDROID)
	struct BundledProperty;
#endif

	class AndroidSystem
	{
	private:
#if defined (DEBUG) || !defined (ANDROID)
		static constexpr char OVERRIDE_ENVIRONMENT_FILE_NAME[] = "environment";
		static constexpr uint32_t OVERRIDE_ENVIRONMENT_FILE_HEADER_SIZE = 22;
		static BundledProperty *bundled_properties;
#endif
		static const char* android_abi_names[CPU_KIND_X86_64+1];
#if defined (WINDOWS)
		static std::mutex readdir_mutex;
		static char *libmonoandroid_directory_path;
#endif

// _WIN32 is defined with _WIN64 so _WIN64 must be checked first.
#if __SIZEOF_POINTER__ == 8 || defined (_WIN64)
#define __BITNESS__ "64bit"
#elif __SIZEOF_POINTER__ == 4 || defined (_WIN32)
#define __BITNESS__ "32bit"
#else
#error Unknown pointer size for this platform
#endif

	public:
#ifdef ANDROID64
		static constexpr char SYSTEM_LIB_PATH[] = "/system/lib64";
#elif ANDROID
		static constexpr char SYSTEM_LIB_PATH[] = "/system/lib";
#elif LINUX_FLATPAK
		static constexpr char SYSTEM_LIB_PATH[] = "/app/lib/mono";
#elif LINUX
		static constexpr char SYSTEM_LIB_PATH[] = "/usr/lib";
#elif APPLE_OS_X
		static constexpr char SYSTEM_LIB_PATH[] = "/Library/Frameworks/Xamarin.Android.framework/Versions/Current/lib/xamarin.android/xbuild/Xamarin/Android/lib/host-Darwin";
#elif WINDOWS
		static const char *SYSTEM_LIB_PATH;
#else
		static constexpr char SYSTEM_LIB_PATH[] = "";
#endif

#if ANDROID || LINUX
		static constexpr char MONO_SGEN_SO[]      = "libmonosgen-2.0.so";
		static constexpr char MONO_SGEN_ARCH_SO[] = "libmonosgen-" __BITNESS__ "-2.0.so";
#elif APPLE_OS_X
		static constexpr char MONO_SGEN_SO[]      = "libmonosgen-2.0.dylib";
		static constexpr char MONO_SGEN_ARCH_SO[] = "libmonosgen-" __BITNESS__ "-2.0.dylib";
#elif WINDOWS
		static constexpr char MONO_SGEN_SO[]      = "libmonosgen-2.0.dll";
		static constexpr char MONO_SGEN_ARCH_SO[] = "libmonosgen-" __BITNESS__ "-2.0.dll";
#else
		static constexpr char MONO_SGEN_SO[]      = "monosgen-2.0";
		static constexpr char MONO_SGEN_ARCH_SO[] = "monosgen-" __BITNESS__ "-2.0";
#endif

	public:
#ifdef RELEASE
		static constexpr size_t MAX_OVERRIDES = 1;
#else
		static constexpr size_t MAX_OVERRIDES = 3;
#endif
		static char* override_dirs [MAX_OVERRIDES];
		static const char **app_lib_directories;
		static size_t app_lib_directories_size;

	public:
		void  setup_environment ();
		void  setup_process_args (JNIEnv *env, jstring_array_wrapper &runtimeApks);
		int   monodroid_get_system_property (const char *name, char **value);
		size_t monodroid_get_system_property_from_overrides (const char *name, char ** value);
		size_t monodroid_read_file_into_memory (const char *path, char **value);
		void  create_update_dir (char *override_dir);
		char* get_libmonosgen_path ();
		char* get_bundled_app (JNIEnv *env, jstring dir);
		int   count_override_assemblies ();
		long  get_gref_gc_threshold ();
		void  setup_apk_directories (JNIEnv *env, unsigned short running_on_cpu, jstring_array_wrapper &runtimeApks);
		void* load_dso (const char *path, int dl_flags, bool skip_exists_check);
		void* load_dso_from_any_directories (const char *name, int dl_flags);
		char* get_full_dso_path_on_disk (const char *dso_name, bool *needs_free);
		monodroid_dirent_t* readdir (monodroid_dir_t *dir);

		const char* get_override_dir (size_t index) const
		{
			if (index >= MAX_OVERRIDES)
				return nullptr;

			return override_dirs [index];
		}

		void set_override_dir (uint32_t index, const char* dir)
		{
			if (index >= MAX_OVERRIDES)
				return;

			override_dirs [index] = const_cast <char*> (dir);
		}

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

		bool is_embedded_dso_mode_enabled () const
		{
			return embedded_dso_mode_enabled;
		}

		void set_embedded_dso_mode_enabled (bool yesno)
		{
			embedded_dso_mode_enabled = yesno;
		}

		MonoAotMode get_mono_aot_mode () const
		{
			return aotMode;
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
		void  copy_native_libraries_to_internal_location ();
		void  copy_file_to_internal_location (char *to_dir, char *from_dir, char *file);
		void  add_apk_libdir (const char *apk, size_t index, size_t apk_count, void *user_data);
		void  for_each_apk (JNIEnv *env, jstring_array_wrapper &runtimeApks, void (AndroidSystem::*handler) (const char *apk, size_t index, size_t apk_count, void *user_data), void *user_data);
		char* get_full_dso_path (const char *base_dir, const char *dso_path, bool *needs_free);
		void* load_dso_from_specified_dirs (const char **directories, size_t num_entries, const char *dso_name, int dl_flags);
		void* load_dso_from_app_lib_dirs (const char *name, int dl_flags);
		void* load_dso_from_override_dirs (const char *name, int dl_flags);
		char* get_existing_dso_path_on_disk (const char *base_dir, const char *dso_name, bool *needs_free);
		bool try_load_libmonosgen (const char *dir, char*& libmonoso);
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
		bool embedded_dso_mode_enabled = false;
	};
}}}
#endif // !__ANDROID_SYSTEM_H
