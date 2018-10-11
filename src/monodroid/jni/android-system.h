// Dear Emacs, this is a -*- C++ -*- header
#ifndef __ANDROID_SYSTEM_H
#define __ANDROID_SYSTEM_H

#include <cstdint>
#include <cstddef>
#include <pthread.h>
#include <jni.h>

#include "dylib-mono.h"
#include "util.h"
#include "cpu-arch.h"

namespace xamarin { namespace android { namespace internal
{
	struct BundledProperty {
		char *name;
		char *value;
		int   value_len;
		struct BundledProperty *next;
	};

	class AndroidSystem
	{
	private:
		static BundledProperty *bundled_properties;
		static const char* android_abi_names[CPU_KIND_X86_64+1];
#if defined (WINDOWS)
		static pthread_mutex_t readdir_mutex;
		static char *libmonoandroid_directory_path;
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
		static constexpr char SYSTEM_LIB_PATH[] = "/Library/Frameworks/Xamarin.Android.framework/Libraries/";
#elif WINDOWS
		static const char *SYSTEM_LIB_PATH;
#else
		static constexpr char SYSTEM_LIB_PATH[] = "";
#endif

#if ANDROID || LINUX
		static constexpr char MONO_SGEN_SO[]      = "libmonosgen-2.0.so";
		static constexpr char MONO_SGEN_ARCH_SO[] = "libmonosgen-%s-2.0.so";
#elif APPLE_OS_X
		static constexpr char MONO_SGEN_SO[]      = "libmonosgen-2.0.dylib";
		static constexpr char MONO_SGEN_ARCH_SO[] = "libmonosgen-%s-2.0.dylib";
#elif WINDOWS
		static constexpr char MONO_SGEN_SO[]      = "libmonosgen-2.0.dll";
		static constexpr char MONO_SGEN_ARCH_SO[] = "libmonosgen-%s-2.0.dll";
#else
		static constexpr char MONO_SGEN_SO[]      = "monosgen-2.0";
		static constexpr char MONO_SGEN_ARCH_SO[] = "monosgen-%s-2.0";
#endif

	public:
#ifdef RELEASE
		static constexpr uint32_t MAX_OVERRIDES = 1;
#else
		static constexpr uint32_t MAX_OVERRIDES = 3;
#endif
		static char* override_dirs [MAX_OVERRIDES];
		static const char **app_lib_directories;
		static size_t app_lib_directories_size;

	public:
		void  add_system_property (const char *name, const char *value);
		void  setup_environment (JNIEnv *env, jobjectArray runtimeApks);
		void  setup_process_args (JNIEnv *env, jobjectArray runtimeApks);
		int   monodroid_get_system_property (const char *name, char **value);
		int   monodroid_get_system_property_from_overrides (const char *name, char ** value);
		void  create_update_dir (char *override_dir);
		char* get_libmonosgen_path ();
		char* get_bundled_app (JNIEnv *env, jstring dir);
		int   count_override_assemblies ();
		int   get_gref_gc_threshold ();
		void  setup_apk_directories (JNIEnv *env, unsigned short running_on_cpu, jobjectArray runtimeApks);
		void* load_dso (const char *path, int dl_flags, mono_bool skip_exists_check);
		void* load_dso_from_any_directories (const char *name, int dl_flags);
		char* get_full_dso_path_on_disk (const char *dso_name, mono_bool *needs_free);

		const char* get_override_dir (uint32_t index) const
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

		int get_max_gref_count () const
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

	private:
		int  get_max_gref_count_from_system ();
		void setup_environment_from_line (const char *line);
		void setup_environment_from_file (const char *apk, int index, int apk_count, void *user_data);
		BundledProperty* lookup_system_property (const char *name);
		void setup_process_args_apk (const char *apk, int index, int apk_count, void *user_data);
		int  _monodroid__system_property_get (const char *name, char *sp_value, size_t sp_value_len);
		int  _monodroid_get_system_property_from_file (const char *path, char **value);
		void  copy_native_libraries_to_internal_location ();
		void  copy_file_to_internal_location (char *to_dir, char *from_dir, char *file);
		void  add_apk_libdir (const char *apk, int index, int apk_count, void *user_data);
		void  for_each_apk (JNIEnv *env, jobjectArray runtimeApks, void (AndroidSystem::*handler) (const char *apk, int index, int apk_count, void *user_data), void *user_data);
		char* get_full_dso_path (const char *base_dir, const char *dso_path, mono_bool *needs_free);
		void* load_dso_from_specified_dirs (const char **directories, int num_entries, const char *dso_name, int dl_flags);
		void* load_dso_from_app_lib_dirs (const char *name, int dl_flags);
		void* load_dso_from_override_dirs (const char *name, int dl_flags);
		char* get_existing_dso_path_on_disk (const char *base_dir, const char *dso_name, mono_bool *needs_free);
		void  dso_alloc_cleanup (char **dso_path, mono_bool *needs_free);
#if defined (WINDOWS)
		int readdir_r (_WDIR *dirp, struct _wdirent *entry, struct _wdirent **result);
		char* get_libmonoandroid_directory_path ();
		int symlink (const char *target, const char *linkpath);
#endif // WINDOWS

#if !defined (ANDROID)
		void monodroid_strreplace (char *buffer, char old_char, char new_char);
#endif // !ANDROID
	private:
		int max_gref_count = 0;
	};
}}}
#endif // !__ANDROID_SYSTEM_H
