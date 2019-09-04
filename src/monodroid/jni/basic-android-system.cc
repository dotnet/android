#include <cerrno>

#include "basic-android-system.hh"
#include "globals.hh"

using namespace xamarin::android;
using namespace xamarin::android::internal;

char* BasicAndroidSystem::override_dirs [MAX_OVERRIDES];
const char **BasicAndroidSystem::app_lib_directories;
size_t BasicAndroidSystem::app_lib_directories_size = 0;
#if WINDOWS
static const char *SYSTEM_LIB_PATH;
#endif

// Values correspond to the CPU_KIND_* macros
const char* BasicAndroidSystem::android_abi_names[CPU_KIND_X86_64+1] = {
	"unknown",
	[CPU_KIND_ARM]      = "armeabi-v7a",
	[CPU_KIND_ARM64]    = "arm64-v8a",
	[CPU_KIND_MIPS]     = "mips",
	[CPU_KIND_X86]      = "x86",
	[CPU_KIND_X86_64]   = "x86_64",
};
#define ANDROID_ABI_NAMES_SIZE (sizeof(android_abi_names) / sizeof (android_abi_names[0]))

void
BasicAndroidSystem::setup_app_library_directories (JNIEnv *env, jstring_array_wrapper& runtimeApks, jstring_array_wrapper& appDirs, int androidApiLevel)
{
	if (androidApiLevel < 23 || !is_embedded_dso_mode_enabled ()) {
		log_info (LOG_DEFAULT, "Setting up for DSO lookup in app data directories");
		BasicAndroidSystem::app_lib_directories_size = 1;
		BasicAndroidSystem::app_lib_directories = (const char**) xcalloc (BasicAndroidSystem::app_lib_directories_size, sizeof(char*));
		BasicAndroidSystem::app_lib_directories [0] = strdup (appDirs[2].get_cstr ());
	} else {
		log_info (LOG_DEFAULT, "Setting up for DSO lookup directly in the APK");
		BasicAndroidSystem::app_lib_directories_size = runtimeApks.get_length ();
		BasicAndroidSystem::app_lib_directories = (const char**) xcalloc (BasicAndroidSystem::app_lib_directories_size, sizeof(char*));

		unsigned short built_for_cpu = 0, running_on_cpu = 0;
		unsigned char is64bit = 0;
		_monodroid_detect_cpu_and_architecture (&built_for_cpu, &running_on_cpu, &is64bit);
		setup_apk_directories (env, running_on_cpu, runtimeApks);
	}
}

void
BasicAndroidSystem::for_each_apk (JNIEnv *env, jstring_array_wrapper &runtimeApks, ForEachApkHandler handler, void *user_data)
{
	size_t apksLength = runtimeApks.get_length ();
	for (size_t i = 0; i < apksLength; ++i) {
		jstring_wrapper &e = runtimeApks [i];

		(this->*handler) (e.get_cstr (), i, apksLength, user_data);
	}
}

void
BasicAndroidSystem::add_apk_libdir (const char *apk, size_t index, size_t apk_count, void *user_data)
{
	assert (user_data != nullptr);
	assert (index >= 0 && index < app_lib_directories_size);
	app_lib_directories [index] = utils.monodroid_strdup_printf ("%s!/lib/%s", apk, (const char*)user_data);
}

void
BasicAndroidSystem::setup_apk_directories (JNIEnv *env, unsigned short running_on_cpu, jstring_array_wrapper &runtimeApks)
{
	// Man, the cast is ugly...
	for_each_apk (env, runtimeApks, &BasicAndroidSystem::add_apk_libdir, const_cast <void*> (static_cast<const void*> (android_abi_names [running_on_cpu])));
}

char*
BasicAndroidSystem::determine_primary_override_dir (JNIEnv *env, jstring_wrapper &home)
{
	return utils.path_combine (home.get_cstr (), ".__override__");
}
