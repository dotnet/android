#include "basic-android-system.hh"
#include "cpp-util.hh"
#include "globals.hh"

using namespace xamarin::android;
using namespace xamarin::android::internal;

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

		BasicAndroidSystem::app_lib_directories = std::span<const char*> (single_app_lib_directory);
		BasicAndroidSystem::app_lib_directories [0] = utils.strdup_new (appDirs[SharedConstants::APP_DIRS_DATA_DIR_INDEX].get_cstr ());
		log_debug (LOG_ASSEMBLY, "Added filesystem DSO lookup location: %s", appDirs[SharedConstants::APP_DIRS_DATA_DIR_INDEX].get_cstr ());
	} else {
		log_debug (LOG_DEFAULT, "Setting up for DSO lookup directly in the APK");

		if (have_split_apks) {
			// If split apks are used, then we will have just a single app library directory. Don't allocate any memory
			// dynamically in this case
			BasicAndroidSystem::app_lib_directories = std::span<const char*> (single_app_lib_directory);
		} else {
			size_t app_lib_directories_size = have_split_apks ? 1 : runtimeApks.get_length ();
			BasicAndroidSystem::app_lib_directories = std::span<const char*> (new const char*[app_lib_directories_size], app_lib_directories_size);
		}

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
	abort_unless (index < app_lib_directories.size (), "Index out of range");
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

	if (app_lib_directories.size () == number_of_added_directories) [[likely]] {
		return;
	}

	abort_unless (number_of_added_directories > 0, "At least a single application lib directory must be added");
	app_lib_directories = app_lib_directories.subspan (0, number_of_added_directories);
}

char*
BasicAndroidSystem::determine_primary_override_dir (jstring_wrapper &home)
{
	return utils.path_combine (home.get_cstr (), SharedConstants::OVERRIDE_DIRECTORY_NAME);
}
