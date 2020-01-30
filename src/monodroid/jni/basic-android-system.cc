#include <cerrno>

#include "basic-android-system.hh"
#include "globals.hh"

using namespace xamarin::android;
using namespace xamarin::android::internal;

char* BasicAndroidSystem::override_dirs [MAX_OVERRIDES];
const char **BasicAndroidSystem::app_lib_directories;
size_t BasicAndroidSystem::app_lib_directories_size = 0;

void
BasicAndroidSystem::setup_app_library_directories (jstring_array_wrapper& runtimeApks, jstring_array_wrapper& appDirs, int androidApiLevel)
{
	if (androidApiLevel < 23 || !is_embedded_dso_mode_enabled ()) {
		log_info (LOG_DEFAULT, "Setting up for DSO lookup in app data directories");
		BasicAndroidSystem::app_lib_directories_size = 1;
		BasicAndroidSystem::app_lib_directories = new const char*[app_lib_directories_size]();
		BasicAndroidSystem::app_lib_directories [0] = utils.strdup_new (appDirs[2].get_cstr ());
	} else {
		log_info (LOG_DEFAULT, "Setting up for DSO lookup directly in the APK");
		BasicAndroidSystem::app_lib_directories_size = runtimeApks.get_length ();
		BasicAndroidSystem::app_lib_directories = new const char*[app_lib_directories_size]();

		unsigned short built_for_cpu = 0, running_on_cpu = 0;
		unsigned char is64bit = 0;
		_monodroid_detect_cpu_and_architecture (&built_for_cpu, &running_on_cpu, &is64bit);
		setup_apk_directories (running_on_cpu, runtimeApks);
	}
}

void
BasicAndroidSystem::for_each_apk (jstring_array_wrapper &runtimeApks, ForEachApkHandler handler, void *user_data)
{
	size_t apksLength = runtimeApks.get_length ();
	for (size_t i = 0; i < apksLength; ++i) {
		jstring_wrapper &e = runtimeApks [i];

		(this->*handler) (e.get_cstr (), i, apksLength, user_data);
	}
}

void
BasicAndroidSystem::add_apk_libdir (const char *apk, size_t index, [[maybe_unused]] size_t apk_count, void *user_data)
{
	assert (user_data != nullptr);
	assert (index < app_lib_directories_size);
	app_lib_directories [index] = utils.string_concat (apk, "!/lib/", static_cast<const char*>(user_data));
}

void
BasicAndroidSystem::setup_apk_directories (unsigned short running_on_cpu, jstring_array_wrapper &runtimeApks)
{
	// Man, the cast is ugly...
	for_each_apk (runtimeApks, &BasicAndroidSystem::add_apk_libdir, const_cast <void*> (static_cast<const void*> (android_abi_names [running_on_cpu])));
}

char*
BasicAndroidSystem::determine_primary_override_dir (jstring_wrapper &home)
{
	return utils.path_combine (home.get_cstr (), ".__override__");
}
