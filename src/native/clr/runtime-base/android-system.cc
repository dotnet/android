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
#include <runtime-base/util.hh>

using namespace microsoft::java_interop;
using namespace xamarin::android;

using std::operator""sv;

#if defined(DEBUG)
auto
AndroidSystem::find_bundled_property (const char *name) noexcept -> BundledProperty*
{
	for (BundledProperty *p = bundled_properties; p != nullptr; p = p->next) {
		if (strcmp (name, p->name) == 0) {
			return p;
		}
	}

	return nullptr;
}

[[gnu::always_inline]]
void
AndroidSystem::add_system_property (const char *name, const char *value) noexcept
{
	if (name == nullptr || *name == '\0') {
		log_warnf (LOG_DEFAULT, "Attempt to add a bundled system property without a valid name");
		return;
	}

	if (value == nullptr) {
		value = "";
	}

	size_t value_len = strlen (value);
	char *new_value = strdup (value);
	if (new_value == nullptr) [[unlikely]] {
		Helpers::abort_application (LOG_DEFAULT, "Unable to allocate memory for a bundled system property value");
	}

	// Setting the same property again replaces its value, the name is kept.
	BundledProperty *p = find_bundled_property (name);
	if (p != nullptr) {
		std::free (p->value);
		p->value = new_value;
		p->value_len = value_len;
		return;
	}

	// The name is stored right after the structure, so that a single allocation covers both.
	// Bundled properties are never removed, they live for as long as the process does.
	size_t name_len = strlen (name);
	size_t alloc_size = Helpers::add_with_overflow_check<size_t> (sizeof (BundledProperty), name_len + 1uz);
	p = static_cast<BundledProperty*> (std::malloc (alloc_size));
	if (p == nullptr) [[unlikely]] {
		Helpers::abort_application (LOG_DEFAULT, "Unable to allocate memory for a bundled system property");
	}

	p->name = reinterpret_cast<char*>(p) + sizeof (BundledProperty);
	memcpy (p->name, name, name_len);
	p->name [name_len] = '\0';
	p->value = new_value;
	p->value_len = value_len;
	p->next = bundled_properties;
	bundled_properties = p;
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
			log_warnf (LOG_DEFAULT, "(Debug) Failed to set environment variable: %s", strerror (errno));
		}
		return;
	}

	add_system_property (name, v);
}

void
AndroidSystem::setup_environment_from_override_file (const char *path) noexcept
{
	using read_count_type = size_t;

	struct stat sbuf;
	if (::stat (path, &sbuf) < 0) {
		log_warnf (LOG_DEFAULT, "Failed to stat the environment override file %s: %s", path, strerror (errno));
		return;
	}

	int fd = open (path, O_RDONLY);
	if (fd < 0) {
		log_warnf (LOG_DEFAULT, "Failed to open the environment override file %s: %s", path, strerror (errno));
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
		log_warnf (LOG_DEFAULT, "Failed to read the environment override file %s: %s", path, strerror (errno));
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
		log_warnf (LOG_DEFAULT, "Invalid format of the environment override file %s: malformatted header", path);
		return;
	}

	char *endptr;
	unsigned long name_width = strtoul (buf.get (), &endptr, 16);
	if ((name_width == std::numeric_limits<unsigned long>::max () && errno == ERANGE) || (buf[0] != '\0' && *endptr != '\0')) {
		log_warnf (LOG_DEFAULT, "Malformed header of the environment override file %s: name width has invalid format", path);
		return;
	}

	unsigned long value_width = strtoul (buf.get () + 11, &endptr, 16);
	if ((value_width == std::numeric_limits<unsigned long>::max () && errno == ERANGE) || (buf[0] != '\0' && *endptr != '\0')) {
		log_warnf (LOG_DEFAULT, "Malformed header of the environment override file %s: value width has invalid format", path);
		return;
	}

	uint64_t data_width = name_width + value_width;
	if (data_width > file_size - Constants::OVERRIDE_ENVIRONMENT_FILE_HEADER_SIZE || (file_size - Constants::OVERRIDE_ENVIRONMENT_FILE_HEADER_SIZE) % data_width != 0) {
		log_warnf (LOG_DEFAULT, "Malformed environment override file %s: invalid data size", path);
		return;
	}

	uint64_t data_size = static_cast<uint64_t>(file_size);
	char *name = buf.get () + Constants::OVERRIDE_ENVIRONMENT_FILE_HEADER_SIZE;
	while (data_size > 0 && data_size >= data_width) {
		if (*name == '\0') {
			log_warnf (LOG_DEFAULT, "Malformed environment override file %s: name at offset %td is empty", path, name - buf.get ());
			return;
		}

		log_debugf (LOG_DEFAULT, "Setting environment variable from the override file %s: '%s' = '%s'", path, name, name + name_width);
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
	log_debugf (LOG_ASSEMBLY, "Added APK DSO lookup location: %s", dir.c_str ());
	index++;
}

[[gnu::always_inline]]
void
AndroidSystem::setup_apk_directories (unsigned short running_on_cpu, jstring_array_wrapper &runtimeApks, bool have_split_apks) noexcept
{
	std::string_view const& abi = android_abi_names [running_on_cpu];
	size_t number_of_added_directories = 0uz;

	std::string_view base_apk{};
	for (size_t i = 0uz; i < runtimeApks.get_length (); ++i) {
		jstring_wrapper &e = runtimeApks [i];
		std::string_view apk = e.get_string_view ();

		if (have_split_apks) {
			if (apk.ends_with (Constants::split_config_abi_apk_name.data ())) {
				add_apk_libdir (apk, number_of_added_directories, abi);
				break;
			} else if (base_apk.empty () && apk.ends_with (Constants::base_apk_name)) {
				base_apk = apk;
			}
		} else {
			add_apk_libdir (apk, number_of_added_directories, abi);
		}
	}

	// This apparently can happen now... It seems that sometimes (when and why? No idea) when AAB format is used, bundletool
	// won't put the native libraries in a separate split config file, but it will instead put **all** of the ABIs
	// in base.apk
	if (have_split_apks && number_of_added_directories == 0 && !base_apk.empty ()) {
		add_apk_libdir (base_apk, number_of_added_directories, abi);
	}

	log_debugf (LOG_DEFAULT, "Number of added dirs: %zu", number_of_added_directories);
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
		log_debugf (LOG_DEFAULT, "Setting up for DSO lookup in app data directories");

		app_lib_directories = std::span<std::string> (single_app_lib_directory);
		app_lib_directories [0] = std::string (appDirs[Constants::APP_DIRS_DATA_DIR_INDEX].get_cstr ());
		log_debugf (LOG_ASSEMBLY, "Added filesystem DSO lookup location: %s", app_lib_directories [0].c_str ());
		return;
	}

	log_debugf (LOG_DEFAULT, "Setting up for DSO lookup directly in the APK");
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
		log_debugf (LOG_DEFAULT, "Setting environment variables (%u)", application_config.environment_variable_count);
		HostEnvironment::set_values<HostEnvironment::set_variable> (
            application_config.environment_variable_count,
            app_environment_variables,
            app_environment_variable_contents
        );
	}

	if (application_config.system_property_count > 0) {
		log_debugf (LOG_DEFAULT, "Setting system properties (%u)", application_config.system_property_count);
		HostEnvironment::set_values<HostEnvironment::set_system_property> (
            application_config.system_property_count,
            app_system_properties,
            app_system_property_contents
        );
	}

#if defined(DEBUG)
	log_debugf (LOG_DEFAULT, "Loading environment from the override directory.");

	char stack_buffer [Util::LocalPathBufferSize];
	char *env_override_file = Util::join_paths (stack_buffer, sizeof (stack_buffer), primary_override_dir, Constants::OVERRIDE_ENVIRONMENT_FILE_NAME);

	if (Util::file_exists (env_override_file)) {
		log_debugf (LOG_DEFAULT, "Loading %s", env_override_file);
		setup_environment_from_override_file (env_override_file);
	}
	if (env_override_file != stack_buffer) {
		std::free (env_override_file);
	}
#endif // def DEBUG
}

void
AndroidSystem::detect_embedded_dso_mode (jstring_array_wrapper& appDirs) noexcept
{
	// appDirs[Constants::APP_DIRS_DATA_DIR_INDEX] points to the native library directory
	std::string_view app_data_dir = appDirs[Constants::APP_DIRS_DATA_DIR_INDEX].get_string_view ();
	char stack_buffer [Util::LocalPathBufferSize];
	char *libmonodroid_path = Util::join_paths (stack_buffer, sizeof (stack_buffer), app_data_dir, "libmonodroid.so"sv);

	log_debugf (LOG_ASSEMBLY, "Checking if libmonodroid was unpacked to %s", libmonodroid_path);
	if (!Util::file_exists (libmonodroid_path)) {
		log_debugf (LOG_ASSEMBLY, "%s not found, assuming application/android:extractNativeLibs == false", libmonodroid_path);
		set_embedded_dso_mode_enabled (true);
	} else {
		log_debugf (LOG_ASSEMBLY, "Native libs extracted to %s, assuming application/android:extractNativeLibs == true", appDirs[Constants::APP_DIRS_DATA_DIR_INDEX].get_cstr ());
		set_embedded_dso_mode_enabled (false);
		native_libraries_dir.assign (appDirs[Constants::APP_DIRS_DATA_DIR_INDEX].get_cstr ());
	}
	if (libmonodroid_path != stack_buffer) {
		std::free (libmonodroid_path);
	}
}

auto
AndroidSystem::lookup_system_property (const char *name, size_t &value_len) noexcept -> const char*
{
	value_len = 0;
#if defined (DEBUG)
	BundledProperty *p = find_bundled_property (name);
	if (p != nullptr) {
		value_len = p->value_len;
		return p->value;
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

auto AndroidSystem::format_full_dso_path (std::string const& base_dir, std::string_view const& dso_path, char *buffer, size_t buffer_size) noexcept -> ssize_t
{
	bool is_rooted = Util::is_path_rooted (dso_path);
	bool add_lib_prefix = !base_dir.empty () && !is_rooted && !Util::path_has_directory_components (dso_path);
	size_t dso_name_length = Util::get_dso_name_length (dso_path, add_lib_prefix);
	size_t path_length = dso_name_length;
	if (!base_dir.empty () && !is_rooted) {
		path_length = Helpers::add_with_overflow_check<size_t> (base_dir.length (), dso_name_length);
		path_length = Helpers::add_with_overflow_check<size_t> (path_length, 1uz);
	}

	size_t required_capacity = Helpers::add_with_overflow_check<size_t> (path_length, 1uz);
	abort_unless (required_capacity <= static_cast<size_t>(std::numeric_limits<ssize_t>::max ()), "Full DSO path is too long");
	if (buffer == nullptr || buffer_size < required_capacity) {
		return -static_cast<ssize_t>(required_capacity);
	}

	char *destination = buffer;
	if (!base_dir.empty () && !is_rooted) {
		memcpy (destination, base_dir.data (), base_dir.length ());
		destination += base_dir.length ();
		*destination++ = Constants::DIR_SEP [0];
	}

	ssize_t result = Util::format_dso_name (dso_path, add_lib_prefix, destination, buffer_size - static_cast<size_t>(destination - buffer));
	abort_unless (result >= 0, "Failed to format DSO name using the required capacity");
	return static_cast<ssize_t>(path_length);
}

template<class TContainer> [[gnu::always_inline]]
auto AndroidSystem::load_dso_from_specified_dirs (TContainer directories, std::string_view const& dso_name, int dl_flags, bool is_jni) noexcept -> void*
{
	if (dso_name.empty ()) {
		return nullptr;
	}

	for (std::string const& dir : directories) {
		char stack_buffer [Util::LocalPathBufferSize];
		char *full_path = get_full_dso_path (dir, dso_name, stack_buffer, sizeof (stack_buffer));

		std::string_view full_path_view { full_path };
		void *handle = DsoLoader::load (full_path_view, dl_flags, is_jni);
		if (full_path != stack_buffer) {
			std::free (full_path);
		}
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
