#include <cerrno>

#include "basic-android-system.hh"
#include "cpp-util.hh"
#include "globals.hh"

using namespace xamarin::android;
using namespace xamarin::android::internal;

char* BasicAndroidSystem::override_dirs [MAX_OVERRIDES];
const char **BasicAndroidSystem::app_lib_directories;
size_t BasicAndroidSystem::app_lib_directories_size = 0;
const char* BasicAndroidSystem::built_for_abi_name = nullptr;

void
BasicAndroidSystem::detect_embedded_dso_mode (jstring_array_wrapper& appDirs) noexcept
{
	// appDirs[SharedConstants::APP_DIRS_DATA_DIR_INDEX] points to the native library directory
	std::unique_ptr<char> libmonodroid_path {utils.path_combine (appDirs[SharedConstants::APP_DIRS_DATA_DIR_INDEX].get_cstr (), "libmonodroid.so")};
	log_debug (LOG_ASSEMBLY, "Checking if libmonodroid was unpacked to %s", libmonodroid_path.get ());
	if (!utils.file_exists (libmonodroid_path.get ())) {
		log_debug (LOG_ASSEMBLY, "%s not found, assuming application/android:extractNativeLibs == false", libmonodroid_path.get ());
		set_embedded_dso_mode_enabled (true);
	} else {
		log_debug (LOG_ASSEMBLY, "Native libs extracted to %s, assuming application/android:extractNativeLibs == true", appDirs[SharedConstants::APP_DIRS_DATA_DIR_INDEX].get_cstr ());
		set_embedded_dso_mode_enabled (false);
	}
}

void
BasicAndroidSystem::setup_app_library_directories (jstring_array_wrapper& runtimeApks, jstring_array_wrapper& appDirs, bool have_split_apks)
{
	if (!is_embedded_dso_mode_enabled ()) {
		log_debug (LOG_DEFAULT, "Setting up for DSO lookup in app data directories");
		BasicAndroidSystem::app_lib_directories_size = 1;
		BasicAndroidSystem::app_lib_directories = new const char*[app_lib_directories_size]();
		BasicAndroidSystem::app_lib_directories [0] = utils.strdup_new (appDirs[SharedConstants::APP_DIRS_DATA_DIR_INDEX].get_cstr ());
		log_debug (LOG_ASSEMBLY, "Added filesystem DSO lookup location: %s", appDirs[SharedConstants::APP_DIRS_DATA_DIR_INDEX].get_cstr ());
	} else {
		log_debug (LOG_DEFAULT, "Setting up for DSO lookup directly in the APK");
		BasicAndroidSystem::app_lib_directories_size = runtimeApks.get_length ();
		BasicAndroidSystem::app_lib_directories = new const char*[app_lib_directories_size]();

		unsigned short built_for_cpu = 0, running_on_cpu = 0;
		unsigned char is64bit = 0;
		_monodroid_detect_cpu_and_architecture (&built_for_cpu, &running_on_cpu, &is64bit);
		setup_apk_directories (running_on_cpu, runtimeApks, have_split_apks);
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

force_inline void
BasicAndroidSystem::add_apk_libdir (const char *apk, size_t &index, const char *abi) noexcept
{
	abort_unless (index < app_lib_directories_size, "Index out of range");
	app_lib_directories [index] = utils.string_concat (apk, "!/lib/", abi);
	log_debug (LOG_ASSEMBLY, "Added APK DSO lookup location: %s", app_lib_directories[index]);
	index++;
}

force_inline void
BasicAndroidSystem::setup_apk_directories (unsigned short running_on_cpu, jstring_array_wrapper &runtimeApks, bool have_split_apks) noexcept
{
	const char *abi = android_abi_names [running_on_cpu];
	size_t number_of_added_directories = 0;

	for (size_t i = 0; i < runtimeApks.get_length (); ++i) {
		jstring_wrapper &e = runtimeApks [i];
		const char *apk = e.get_cstr ();

		if (have_split_apks) {
			if (utils.ends_with (apk, SharedConstants::split_config_abi_apk_name)) {
				add_apk_libdir (apk, number_of_added_directories, abi);
				break;
			}
		} else {
			add_apk_libdir (apk, number_of_added_directories, abi);
		}
	}

	app_lib_directories_size = number_of_added_directories;
}

char*
BasicAndroidSystem::determine_primary_override_dir (jstring_wrapper &home)
{
	dynamic_local_string<SENSIBLE_PATH_MAX> dir{};
	dir.assign_c (home.get_cstr ());
	dir.append (MONODROID_PATH_SEPARATOR);
	dir.append (".__override__");
	dir.append (MONODROID_PATH_SEPARATOR);
	dir.append (SharedConstants::android_lib_abi);

	return utils.strdup_new (dir.get ());
}

const char*
BasicAndroidSystem::get_built_for_abi_name ()
{
	if (built_for_abi_name == nullptr) {
		unsigned short built_for_cpu = 0, running_on_cpu = 0;
		unsigned char is64bit = 0;
		_monodroid_detect_cpu_and_architecture (&built_for_cpu, &running_on_cpu, &is64bit);
		built_for_abi_name = android_abi_names [built_for_cpu];
	}
	return built_for_abi_name;
}
