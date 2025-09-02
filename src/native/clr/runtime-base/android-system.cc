#include <limits>
#include <string_view>

#include <java-interop-dlfcn.h>
#include <java-interop.h>

#include <constants.hh>
#include <xamarin-app.hh>
#include <host/host-environment-clr.hh>
#include <runtime-base/android-system.hh>
#include <runtime-base/cpu-arch.hh>
#include <runtime-base/dso-loader.hh>
#include <runtime-base/strings.hh>
#include <runtime-base/util.hh>

using namespace microsoft::java_interop;
using namespace xamarin::android;

using std::operator""sv;

#if defined(DEBUG)
[[gnu::always_inline]]
void
AndroidSystem::add_system_property (const char *name, const char *value) noexcept
{
	if (name == nullptr || *name == '\0') {
		log_warn (LOG_DEFAULT, "Attempt to add a bundled system property without a valid name");
		return;
	}

	if (value == nullptr) {
		value = "";
	}

	bundled_properties[name] = value;
}

void
AndroidSystem::setup_environment (const char *name, const char *value) noexcept
{
	if (name == nullptr || *name == '\0') {
		return;
	}

	const char *v = value;
	if (v == nullptr) {
		v = "";
	}

	if (isupper (name [0]) || name [0] == '_') {
		if (setenv (name, v, 1) < 0) {
			log_warn (LOG_DEFAULT, "(Debug) Failed to set environment variable: {}", strerror (errno));
		}
		return;
	}

	add_system_property (name, v);
}

void
AndroidSystem::setup_environment_from_override_file (dynamic_local_string<Constants::SENSIBLE_PATH_MAX> const& path) noexcept
{
	using read_count_type = size_t;

	struct stat sbuf;
	if (::stat (path.get (), &sbuf) < 0) {
		log_warn (LOG_DEFAULT, "Failed to stat the environment override file {}: {}", path.get (), strerror (errno));
		return;
	}

	int fd = open (path.get (), O_RDONLY);
	if (fd < 0) {
		log_warn (LOG_DEFAULT, "Failed to open the environment override file {}: {}", path.get (), strerror (errno));
		return;
	}

	auto     file_size = static_cast<size_t>(sbuf.st_size);
	size_t   nread = 0uz;
	ssize_t  r;
	auto     buf = std::make_unique<char[]> (file_size);

	do {
		auto read_count = static_cast<read_count_type>(file_size - nread);
		r = read (fd, buf.get () + nread, read_count);
		if (r > 0) {
			nread += static_cast<size_t>(r);
		}
	} while (r < 0 && errno == EINTR);

	if (nread == 0) {
		log_warn (LOG_DEFAULT, "Failed to read the environment override file {}: {}", path.get (), strerror (errno));
		return;
	}

	// The file format is as follows (no newlines are used, this is just for illustration
	// purposes, comments aren't part of the file either):
	//
	// # 10 ASCII characters formattted as a C++ hexadecimal number terminated with NUL: name
	// # width (including the terminating NUL)
	// 0x00000000\0
	//
	// # 10 ASCII characters formattted as a C++ hexadecimal number terminated with NUL: value
	// # width (including the terminating NUL)
	// 0x00000000\0
	//
	// # Variable name, terminated with NUL and padded to [name width] with NUL characters
	// name\0
	//
	// # Variable value, terminated with NUL and padded to [value width] with NUL characters
	// value\0
	if (nread < Constants::OVERRIDE_ENVIRONMENT_FILE_HEADER_SIZE) {
		log_warn (LOG_DEFAULT, "Invalid format of the environment override file {}: malformatted header", path.get ());
		return;
	}

	char *endptr;
	unsigned long name_width = strtoul (buf.get (), &endptr, 16);
	if ((name_width == std::numeric_limits<unsigned long>::max () && errno == ERANGE) || (buf[0] != '\0' && *endptr != '\0')) {
		log_warn (LOG_DEFAULT, "Malformed header of the environment override file {}: name width has invalid format", path.get ());
		return;
	}

	unsigned long value_width = strtoul (buf.get () + 11, &endptr, 16);
	if ((value_width == std::numeric_limits<unsigned long>::max () && errno == ERANGE) || (buf[0] != '\0' && *endptr != '\0')) {
		log_warn (LOG_DEFAULT, "Malformed header of the environment override file {}: value width has invalid format", path.get ());
		return;
	}

	uint64_t data_width = name_width + value_width;
	if (data_width > file_size - Constants::OVERRIDE_ENVIRONMENT_FILE_HEADER_SIZE || (file_size - Constants::OVERRIDE_ENVIRONMENT_FILE_HEADER_SIZE) % data_width != 0) {
		log_warn (LOG_DEFAULT, "Malformed environment override file {}: invalid data size", path.get ());
		return;
	}

	uint64_t data_size = static_cast<uint64_t>(file_size);
	char *name = buf.get () + Constants::OVERRIDE_ENVIRONMENT_FILE_HEADER_SIZE;
	while (data_size > 0 && data_size >= data_width) {
		if (*name == '\0') {
			log_warn (LOG_DEFAULT, "Malformed environment override file {}: name at offset {} is empty", path.get (), name - buf.get ());
			return;
		}

		log_debug (LOG_DEFAULT, "Setting environment variable from the override file {}: '{}' = '{}'", path.get (), name, name + name_width);
		setup_environment (name, name + name_width);
		name += data_width;
		data_size -= data_width;
	}
}
#endif

[[gnu::always_inline]]
void
AndroidSystem::add_apk_libdir (std::string_view const& apk, size_t &index, std::string_view const& abi) noexcept
{
	abort_unless (index < app_lib_directories.size (), "Index out of range");
	static constexpr std::string_view lib_prefix { "!/lib/" };
	std::string dir;

	dir.reserve (apk.size () + lib_prefix.size () + abi.size ());
	dir.assign (apk);
	dir.append (lib_prefix);
	dir.append (abi);
	app_lib_directories [index] = dir;
	log_debug (LOG_ASSEMBLY, "Added APK DSO lookup location: {}", dir);
	index++;
}

[[gnu::always_inline]]
void
AndroidSystem::setup_apk_directories (unsigned short running_on_cpu, jstring_array_wrapper &runtimeApks, bool have_split_apks) noexcept
{
	std::string_view const& abi = android_abi_names [running_on_cpu];
	size_t number_of_added_directories = 0uz;

	for (size_t i = 0uz; i < runtimeApks.get_length (); ++i) {
		jstring_wrapper &e = runtimeApks [i];
		std::string_view apk = e.get_string_view ();

		if (have_split_apks) {
			if (apk.ends_with (Constants::split_config_abi_apk_name.data ())) {
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

void
AndroidSystem::setup_app_library_directories (jstring_array_wrapper& runtimeApks, jstring_array_wrapper& appDirs, bool have_split_apks) noexcept
{
	if (!is_embedded_dso_mode_enabled ()) {
		log_debug (LOG_DEFAULT, "Setting up for DSO lookup in app data directories"sv);

		app_lib_directories = std::span<std::string> (single_app_lib_directory);
		app_lib_directories [0] = std::string (appDirs[Constants::APP_DIRS_DATA_DIR_INDEX].get_cstr ());
		log_debug (LOG_ASSEMBLY, "Added filesystem DSO lookup location: {}", app_lib_directories [0]);
		return;
	}

	log_debug (LOG_DEFAULT, "Setting up for DSO lookup directly in the APK"sv);
	if (have_split_apks) {
		// If split apks are used, then we will have just a single app library directory. Don't allocate any memory
		// dynamically in this case
		AndroidSystem::app_lib_directories = std::span<std::string> (single_app_lib_directory);
	} else {
		size_t app_lib_directories_size = runtimeApks.get_length ();
		AndroidSystem::app_lib_directories = std::span<std::string> (new std::string[app_lib_directories_size], app_lib_directories_size);
	}

	uint16_t built_for_cpu = 0, running_on_cpu = 0;
	bool is64bit = false;
	monodroid_detect_cpu_and_architecture (built_for_cpu, running_on_cpu, is64bit);
	setup_apk_directories (running_on_cpu, runtimeApks, have_split_apks);
}

void
AndroidSystem::setup_environment () noexcept
{
	if (application_config.environment_variable_count > 0) {
		log_debug (LOG_DEFAULT, "Setting environment variables ({})", application_config.environment_variable_count);
		HostEnvironment::set_values<HostEnvironment::set_variable> (
            application_config.environment_variable_count,
            app_environment_variables,
            app_environment_variable_contents
        );
	}

	if (application_config.system_property_count > 0) {
		log_debug (LOG_DEFAULT, "Setting system properties ({})", application_config.system_property_count);
		HostEnvironment::set_values<HostEnvironment::set_system_property> (
            application_config.system_property_count,
            app_system_properties,
            app_system_property_contents
        );
	}

#if defined(DEBUG)
	log_debug (LOG_DEFAULT, "Loading environment from the override directory."sv);

	dynamic_local_string<Constants::SENSIBLE_PATH_MAX> env_override_file;
	Util::path_combine (env_override_file, std::string_view {primary_override_dir}, Constants::OVERRIDE_ENVIRONMENT_FILE_NAME);
	log_debug (LOG_DEFAULT, "{}", env_override_file.get ());
	if (Util::file_exists (env_override_file)) {
		log_debug (LOG_DEFAULT, "Loading {}"sv, env_override_file.get ());
		setup_environment_from_override_file (env_override_file);
	}
#endif // def DEBUG
}

void
AndroidSystem::detect_embedded_dso_mode (jstring_array_wrapper& appDirs) noexcept
{
	// appDirs[Constants::APP_DIRS_DATA_DIR_INDEX] points to the native library directory
	dynamic_local_string<Constants::SENSIBLE_PATH_MAX> libmonodroid_path;
	Util::path_combine (libmonodroid_path, appDirs[Constants::APP_DIRS_DATA_DIR_INDEX].get_string_view (), "libmonodroid.so"sv);

	log_debug (LOG_ASSEMBLY, "Checking if libmonodroid was unpacked to {}", libmonodroid_path.get ());
	if (!Util::file_exists (libmonodroid_path)) {
		log_debug (LOG_ASSEMBLY, "{} not found, assuming application/android:extractNativeLibs == false", libmonodroid_path.get ());
		set_embedded_dso_mode_enabled (true);
	} else {
		log_debug (LOG_ASSEMBLY, "Native libs extracted to {}, assuming application/android:extractNativeLibs == true", appDirs[Constants::APP_DIRS_DATA_DIR_INDEX].get_cstr ());
		set_embedded_dso_mode_enabled (false);
		native_libraries_dir.assign (appDirs[Constants::APP_DIRS_DATA_DIR_INDEX].get_cstr ());
	}
}

auto
AndroidSystem::lookup_system_property (std::string_view const& name, size_t &value_len) noexcept -> const char*
{
	value_len = 0;
#if defined (DEBUG)
	if (!bundled_properties.empty ()) {
		auto prop_iter = bundled_properties.find (name.data ());
		if (prop_iter != bundled_properties.end ()) {
			value_len = prop_iter->second.length ();
			return prop_iter->first.c_str ();
		}
	}
#endif // DEBUG

	if (application_config.system_property_count == 0) {
		return nullptr;
	}

	return HostEnvironment::lookup_system_property (
		name,
		value_len,
		application_config.system_property_count,
		app_system_properties,
		app_system_property_contents
	);
}

auto
AndroidSystem::get_max_gref_count_from_system () noexcept -> long
{
	long max;

	if (running_in_emulator) {
		max = 2000;
	} else {
		max = 51200;
	}

	dynamic_local_property_string override;
	if (monodroid_get_system_property (Constants::DEBUG_MONO_MAX_GREFC, override) > 0) {
		char *e;
		max = strtol (override.get (), &e, 10);
		switch (*e) {
			case 'k':
				e++;
				max *= 1000;
				break;
			case 'm':
				e++;
				max *= 1000000;
				break;
		}

		if (max < 0) {
			max = std::numeric_limits<int>::max ();
		}

		if (*e) {
			log_warn (LOG_GC, "Unsupported '{}' value '{}'.", Constants::DEBUG_MONO_MAX_GREFC.data (), override.get ());
		}

		log_warn (LOG_GC, "Overriding max JNI Global Reference count to {}", max);
	}

	return max;
}

auto AndroidSystem::get_full_dso_path (std::string const& base_dir, std::string_view const& dso_path, dynamic_local_string<SENSIBLE_PATH_MAX>& path) noexcept -> bool
{
	if (dso_path.empty ()) {
		return false;
	}

	dynamic_local_path_string lib_path { dso_path };
	bool have_so_extension = lib_path.ends_with (Constants::dso_suffix);
	if (base_dir.empty () || Util::is_path_rooted (dso_path)) {
		// Absolute path or no base path, can't do much with it
		path.assign (dso_path);
		if (!have_so_extension) {
			path.append (Constants::dso_suffix);
		}

		return true;
	}

	path.assign (base_dir).append (Constants::DIR_SEP);

	if (!Util::path_has_directory_components (dso_path) && !lib_path.starts_with (Constants::DSO_PREFIX)) {
		path.append (Constants::DSO_PREFIX);
	}
	path.append (dso_path);
	if (!have_so_extension) {
		path.append (Constants::dso_suffix);
	}

	return true;
}

template<class TContainer> [[gnu::always_inline]]
auto AndroidSystem::load_dso_from_specified_dirs (TContainer directories, std::string_view const& dso_name, int dl_flags, bool is_jni) noexcept -> void*
{
	if (dso_name.empty ()) {
		return nullptr;
	}

	dynamic_local_string<SENSIBLE_PATH_MAX> full_path;
	for (std::string const& dir : directories) {
		if (!get_full_dso_path (dir, dso_name, full_path)) {
			continue;
		}

		void *handle = DsoLoader::load (full_path.get (), dl_flags, is_jni);
		if (handle != nullptr) {
			return handle;
		}
	}

	return nullptr;
}

auto AndroidSystem::load_dso_from_app_lib_dirs (std::string_view const& name, int dl_flags, bool is_jni) noexcept -> void*
{
	return load_dso_from_specified_dirs (app_lib_directories, name, dl_flags, is_jni);
}

auto AndroidSystem::load_dso_from_override_dirs (std::string_view const& name, int dl_flags, bool is_jni) noexcept -> void*
{
	if constexpr (Constants::is_release_build) {
		return nullptr;
	} else {
		return load_dso_from_specified_dirs (AndroidSystem::override_dirs, name, dl_flags, is_jni);
	}
}

[[gnu::flatten]]
auto AndroidSystem::load_dso_from_any_directories (std::string_view const& name, int dl_flags, bool is_jni) noexcept -> void*
{
	void *handle = load_dso_from_override_dirs (name, dl_flags, is_jni);
	if (handle == nullptr) {
		handle = load_dso_from_app_lib_dirs (name, dl_flags, is_jni);
	}
	return handle;
}
