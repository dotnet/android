#include <cerrno>
#include <cstdlib>
#include <cstring>
#include <fcntl.h>

#include <mono/metadata/object.h>

#include "android-system.hh"
#include "cpp-util.hh"
#include "java-interop-dlfcn.h"
#include "java-interop.h"
#include "jni-wrappers.hh"
#include "shared-constants.hh"
#include "strings.hh"
#include "util.hh"
#include "xamarin-app.hh"

using namespace microsoft::java_interop;
using namespace xamarin::android::internal;
using namespace xamarin::android;

// These two must stay here until JavaInterop is converted to C++
FILE  *gref_log;
FILE  *lref_log;
bool    gref_to_logcat;
bool    lref_to_logcat;

#if defined (DEBUG)
namespace xamarin::android::internal {
	struct BundledProperty {
		char     *name;
		char     *value;
		size_t    value_len;
		struct BundledProperty *next;
	};
}

BundledProperty *AndroidSystem::bundled_properties = nullptr;

BundledProperty*
AndroidSystem::lookup_system_property (const char *name) noexcept
{
	for (BundledProperty *p = bundled_properties; p != nullptr; p = p->next) {
		if (strcmp (p->name, name) == 0) {
			return p;
		}
	}
	return nullptr;
}
#endif // DEBUG

const char*
AndroidSystem::lookup_system_property (const char *name, size_t &value_len) noexcept
{
	value_len = 0;
#if defined (DEBUG)
	BundledProperty *p = lookup_system_property (name);
	if (p != nullptr) {
		value_len = p->value_len;
		return p->name;
	}
#endif // DEBUG || !ANDROID

	if (application_config.system_property_count == 0)
		return nullptr;

	if (application_config.system_property_count % 2 != 0) {
		log_warn (LOG_DEFAULT, "Corrupted environment variable array: does not contain an even number of entries (%u)", application_config.system_property_count);
		return nullptr;
	}

	const char *prop_name;
	const char *prop_value;
	for (size_t i = 0uz; i < application_config.system_property_count; i += 2uz) {
		prop_name = app_system_properties[i];
		if (prop_name == nullptr || *prop_name == '\0')
			continue;

		if (strcmp (prop_name, name) == 0) {
			prop_value = app_system_properties [i + 1uz];
			if (prop_value == nullptr || *prop_value == '\0') {
				value_len = 0uz;
				return "";
			}

			value_len = strlen (prop_value);
			return prop_value;
		}
	}

	return nullptr;
}

#if defined (DEBUG)
void
AndroidSystem::add_system_property (const char *name, const char *value) noexcept
{
	BundledProperty* p = lookup_system_property (name);
	if (p != nullptr) {
		char *n = value != nullptr ? strdup (value) : nullptr;
		if (n == nullptr)
			return;
		free (p->value);
		p->value      = n;
		p->value_len  = strlen (p->value);
		return;
	}

	size_t name_len  = strlen (name);
	size_t alloc_size = Helpers::add_with_overflow_check<size_t> (sizeof (BundledProperty), name_len + 1uz);
	p = reinterpret_cast<BundledProperty*> (malloc (alloc_size));
	if (p == nullptr)
		return;

	p->name = ((char*) p) + sizeof (struct BundledProperty);
	memcpy (p->name, name, name_len);
	p->name [name_len] = '\0';

	if (value == nullptr) {
		p->value = nullptr;
		p->value_len = 0;
	} else {
		p->value = strdup (value);
		p->value_len = strlen (value);
	}

	p->next             = bundled_properties;
	bundled_properties  = p;
}
#endif // DEBUG

int
AndroidSystem::_monodroid__system_property_get (const char *name, char *sp_value, size_t sp_value_len) noexcept
{
	if (name == nullptr || sp_value == nullptr)
		return -1;

	char *buf = nullptr;
	if (sp_value_len < PROPERTY_VALUE_BUFFER_LEN) {
		size_t alloc_size = Helpers::add_with_overflow_check<size_t> (PROPERTY_VALUE_BUFFER_LEN, 1uz);
		log_warn (LOG_DEFAULT, "Buffer to store system property may be too small, will copy only %u bytes", sp_value_len);
		buf = new char [alloc_size];
	}

	int len = __system_property_get (name, buf ? buf : sp_value);
	if (buf != nullptr) {
		strncpy (sp_value, buf, sp_value_len);
		sp_value [sp_value_len] = '\0';
		delete[] buf;
	}

	return len;
}

int
AndroidSystem::monodroid_get_system_property (const char *name, dynamic_local_string<PROPERTY_VALUE_BUFFER_LEN>& value) noexcept
{
	int len = _monodroid__system_property_get (name, value.get (), value.size ());
	if (len > 0) {
		// Clumsy, but if we want direct writes to be fast, this is the price we pay
		value.set_length_after_direct_write (static_cast<size_t>(len));
		return len;
	}

	size_t plen;
	const char *v = lookup_system_property (name, plen);
	if (v == nullptr)
		return len;

	value.assign (v, plen);
	return Helpers::add_with_overflow_check<int> (plen, 0);
}

int
AndroidSystem::monodroid_get_system_property (const char *name, char **value) noexcept
{
	if (value)
		*value = nullptr;

	char  sp_value [PROPERTY_VALUE_BUFFER_LEN] = { 0, };
	char *pvalue = sp_value;
	int len = _monodroid__system_property_get (name, sp_value, sizeof (sp_value));

	if (len <= 0) {
		size_t plen;
		const char *v = lookup_system_property (name, plen);
		if (v != nullptr) {
			pvalue  = const_cast<char*> (v);
			len     = static_cast<int> (plen);
		}
	}

	if (len >= 0 && value) {
		size_t alloc_size = Helpers::add_with_overflow_check<size_t> (static_cast<size_t>(len), 1uz);
		*value = new char [alloc_size];
		if (*value == nullptr)
			return -len;
		if (len > 0)
			memcpy (*value, pvalue, static_cast<size_t>(len));
		(*value)[len] = '\0';
	}
	return len;
}

#if defined (DEBUG)
size_t
AndroidSystem::_monodroid_get_system_property_from_file (const char *path, char **value) noexcept
{
	if (value != nullptr)
		*value = nullptr;

	FILE* fp = Util::monodroid_fopen (path, "r");
	if (fp == nullptr)
		return 0;

	struct stat fileStat;
	if (fstat (fileno (fp), &fileStat) < 0) {
		fclose (fp);
		return 0;
	}

	size_t file_size = static_cast<size_t>(fileStat.st_size);
	if (value == nullptr) {
		fclose (fp);
		return file_size + 1;
	}

	size_t alloc_size = Helpers::add_with_overflow_check<size_t> (file_size, 1uz);
	*value = new char[alloc_size];

	size_t len = fread (*value, 1, file_size, fp);
	fclose (fp);
	for (size_t i = 0uz; i < file_size + 1uz; ++i) {
		if ((*value) [i] != '\n' && (*value) [i] != '\r')
			continue;
		(*value) [i] = 0;
		break;
	}
	return len;
}
#endif // def DEBUG

size_t
AndroidSystem::monodroid_get_system_property_from_overrides ([[maybe_unused]] const char *name, [[maybe_unused]] char ** value) noexcept
{
#if defined (DEBUG)
	for (const char *od : override_dirs) {
		if (od == nullptr) {
			continue;
		}

		std::unique_ptr<char[]> override_file {Util::path_combine (od, name)};
		log_info (LOG_DEFAULT, "Trying to get property from %s", override_file.get ());
		size_t result = _monodroid_get_system_property_from_file (override_file.get (), value);
		if (result == 0 || value == nullptr || (*value) == nullptr || **value == '\0') {
			continue;
		}
		log_info (LOG_DEFAULT, "Property '%s' from  %s has value '%s'.", name, od, *value);
		return result;
	}
#endif // def DEBUG
	return 0;
}

// TODO: review this. Do we really have to create the dir in release?
void
AndroidSystem::create_update_dir (char *override_dir) noexcept
{
#if defined (RELEASE)
	/*
	 * Don't create .__override__ on Release builds, because Google requires
	 * that pre-loaded apps not create world-writable directories.
	 *
	 * However, if any logging is enabled (which should _not_ happen with
	 * pre-loaded apps!), we need the .__override__ directory...
	 */
	if (log_categories == 0 && monodroid_get_system_property (SharedConstants::DEBUG_MONO_PROFILE_PROPERTY, nullptr) == 0) {
		return;
	}
#endif // def RELEASE

	override_dirs [0] = override_dir;
	Util::create_public_directory (override_dir);
	log_warn (LOG_DEFAULT, "Creating public update directory: `%s`", override_dir);
}

bool
AndroidSystem::get_full_dso_path (const char *base_dir, const char *dso_path, dynamic_local_string<SENSIBLE_PATH_MAX>& path) noexcept
{
	if (dso_path == nullptr)
		return false;

	if (base_dir == nullptr || Util::is_path_rooted (dso_path))
		return const_cast<char*>(dso_path); // Absolute path or no base path, can't do much with it

	path.assign_c (base_dir)
		.append ("/")
		.append_c (dso_path);

	return true;
}

void*
AndroidSystem::load_dso (const char *path, unsigned int dl_flags, bool skip_exists_check) noexcept
{
	if (path == nullptr || *path == '\0')
		return nullptr;

	log_info (LOG_ASSEMBLY, "Trying to load shared library '%s'", path);
	if (!skip_exists_check && !is_embedded_dso_mode_enabled () && !Util::file_exists (path)) {
		log_info (LOG_ASSEMBLY, "Shared library '%s' not found", path);
		return nullptr;
	}

	char *error = nullptr;
	void *handle = java_interop_lib_load (path, dl_flags, &error);
	if (handle == nullptr && Util::should_log (LOG_ASSEMBLY))
		log_info_nocheck (LOG_ASSEMBLY, "Failed to load shared library '%s'. %s", path, error);
	java_interop_free (error);
	return handle;
}

void*
AndroidSystem::load_dso_from_specified_dirs (const char **directories, size_t num_entries, const char *dso_name, unsigned int dl_flags) noexcept
{
	abort_if_invalid_pointer_argument (directories, "directories");
	if (dso_name == nullptr)
		return nullptr;

	dynamic_local_string<SENSIBLE_PATH_MAX> full_path;
	for (size_t i = 0uz; i < num_entries; i++) {
		if (!get_full_dso_path (directories [i], dso_name, full_path)) {
			continue;
		}
		void *handle = load_dso (full_path.get (), dl_flags, false);
		if (handle != nullptr)
			return handle;
	}

	return nullptr;
}

void*
AndroidSystem::load_dso_from_app_lib_dirs (const char *name, unsigned int dl_flags) noexcept
{
	return load_dso_from_specified_dirs (app_lib_directories.data (), app_lib_directories.size (), name, dl_flags);
}

void*
AndroidSystem::load_dso_from_override_dirs ([[maybe_unused]] const char *name, [[maybe_unused]] unsigned int dl_flags) noexcept
{
#ifdef RELEASE
	return nullptr;
#else // def RELEASE
	return load_dso_from_specified_dirs (const_cast<const char**> (AndroidSystem::override_dirs.data ()), AndroidSystem::override_dirs.size (), name, dl_flags);
#endif // ndef RELEASE
}

void*
AndroidSystem::load_dso_from_any_directories (const char *name, unsigned int dl_flags) noexcept
{
	void *handle = load_dso_from_override_dirs (name, dl_flags);
	if (handle == nullptr)
		handle = load_dso_from_app_lib_dirs (name, dl_flags);
	return handle;
}

bool
AndroidSystem::get_existing_dso_path_on_disk (const char *base_dir, const char *dso_name, dynamic_local_string<SENSIBLE_PATH_MAX>& path) noexcept
{
	if (get_full_dso_path (base_dir, dso_name, path) && Util::file_exists (path.get ()))
		return true;

	return false;
}

bool
AndroidSystem::get_full_dso_path_on_disk (const char *dso_name, dynamic_local_string<SENSIBLE_PATH_MAX>& path) noexcept
{
	if (is_embedded_dso_mode_enabled ())
		return false;

#ifndef RELEASE
	for (const char *od : override_dirs) {
		if (od == nullptr)
			continue;
		if (get_existing_dso_path_on_disk (od, dso_name, path))
			return true;
	}
#endif // ndef RELEASE
	for (const char *app_lib_dir : app_lib_directories) {
		if (get_existing_dso_path_on_disk (app_lib_dir, dso_name, path)) {
			return true;
		}
	}

	return false;
}

int
AndroidSystem::count_override_assemblies (void) noexcept
{
	int c = 0;

	for (const char *dir_path : override_dirs) {
		DIR *dir;
		dirent *e;

		if (dir_path == nullptr || !Util::directory_exists (dir_path))
			continue;

		if ((dir = ::opendir (dir_path)) == nullptr)
			continue;

		while ((e = ::readdir (dir)) != nullptr && e) {
			if (Util::monodroid_dirent_hasextension (e, ".dll"))
				++c;
		}
		::closedir (dir);
	}

	return c;
}

long
AndroidSystem::get_max_gref_count_from_system (void) noexcept
{
	long max;

	if (running_in_emulator) {
		max = 2000;
	} else {
		max = 51200;
	}

	dynamic_local_string<PROPERTY_VALUE_BUFFER_LEN> override;
	if (monodroid_get_system_property (SharedConstants::DEBUG_MONO_MAX_GREFC, override) > 0) {
		char *e;
		max       = strtol (override.get (), &e, 10);
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
		if (max < 0)
			max = std::numeric_limits<int>::max ();
		if (*e) {
			log_warn (LOG_GC, "Unsupported '%s' value '%s'.", SharedConstants::DEBUG_MONO_MAX_GREFC.data (), override.get ());
		}
		log_warn (LOG_GC, "Overriding max JNI Global Reference count to %i", max);
	}
	return max;
}

long
AndroidSystem::get_gref_gc_threshold () noexcept
{
	if (max_gref_count == std::numeric_limits<int>::max ())
		return max_gref_count;
	return static_cast<int> ((max_gref_count * 90LL) / 100LL);
}

#if defined (DEBUG)
void
AndroidSystem::setup_environment (const char *name, const char *value) noexcept
{
	if (name == nullptr || *name == '\0')
		return;

	const char *v = value;
	if (v == nullptr)
		v = "";

	if (isupper (name [0]) || name [0] == '_') {
		if (setenv (name, v, 1) < 0)
			log_warn (LOG_DEFAULT, "(Debug) Failed to set environment variable: %s", strerror (errno));
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
		log_warn (LOG_DEFAULT, "Failed to stat the environment override file %s: %s", path, strerror (errno));
		return;
	}

	int fd = open (path, O_RDONLY);
	if (fd < 0) {
		log_warn (LOG_DEFAULT, "Failed to open the environment override file %s: %s", path, strerror (errno));
		return;
	}

	auto     file_size = static_cast<size_t>(sbuf.st_size);
	size_t   nread = 0uz;
	ssize_t  r;
	auto     buf = std::make_unique<char[]> (file_size);

	do {
		auto read_count = static_cast<read_count_type>(file_size - nread);
		r = read (fd, buf.get () + nread, read_count);
		if (r > 0)
			nread += static_cast<size_t>(r);
	} while (r < 0 && errno == EINTR);

	if (nread == 0) {
		log_warn (LOG_DEFAULT, "Failed to read the environment override file %s: %s", path, strerror (errno));
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
	if (nread < OVERRIDE_ENVIRONMENT_FILE_HEADER_SIZE) {
		log_warn (LOG_DEFAULT, "Invalid format of the environment override file %s: malformatted header", path);
		return;
	}

	char *endptr;
	unsigned long name_width = strtoul (buf.get (), &endptr, 16);
	if ((name_width == std::numeric_limits<unsigned long>::max () && errno == ERANGE) || (buf[0] != '\0' && *endptr != '\0')) {
		log_warn (LOG_DEFAULT, "Malformed header of the environment override file %s: name width has invalid format", path);
		return;
	}

	unsigned long value_width = strtoul (buf.get () + 11, &endptr, 16);
	if ((value_width == std::numeric_limits<unsigned long>::max () && errno == ERANGE) || (buf[0] != '\0' && *endptr != '\0')) {
		log_warn (LOG_DEFAULT, "Malformed header of the environment override file %s: value width has invalid format", path);
		return;
	}

	uint64_t data_width = name_width + value_width;
	if (data_width > file_size - OVERRIDE_ENVIRONMENT_FILE_HEADER_SIZE || (file_size - OVERRIDE_ENVIRONMENT_FILE_HEADER_SIZE) % data_width != 0) {
		log_warn (LOG_DEFAULT, "Malformed environment override file %s: invalid data size", path);
		return;
	}

	uint64_t data_size = static_cast<uint64_t>(file_size);
	char *name = buf.get () + OVERRIDE_ENVIRONMENT_FILE_HEADER_SIZE;
	while (data_size > 0 && data_size >= data_width) {
		if (*name == '\0') {
			log_warn (LOG_DEFAULT, "Malformed environment override file %s: name at offset %lu is empty", path, name - buf.get ());
			return;
		}

		log_debug (LOG_DEFAULT, "Setting environment variable from the override file %s: '%s' = '%s'", path, name, name + name_width);
		setup_environment (name, name + name_width);
		name += data_width;
		data_size -= data_width;
	}
}
#endif // def DEBUG

void
AndroidSystem::setup_environment () noexcept
{
	if (is_mono_aot_enabled () && *mono_aot_mode_name != '\0') {
		switch (mono_aot_mode_name [0]) {
			case 'n':
				aotMode = MonoAotMode::MONO_AOT_MODE_NORMAL;
				break;

			case 'h':
				aotMode = MonoAotMode::MONO_AOT_MODE_HYBRID;
				break;

			case 'f':
				aotMode = MonoAotMode::MONO_AOT_MODE_FULL;
				break;

			case 'i':
				aotMode = MonoAotMode::MONO_AOT_MODE_INTERP_ONLY;
				break;

			default:
				aotMode = MonoAotMode::MONO_AOT_MODE_LAST;
				break;
		}

		if (aotMode != MonoAotMode::MONO_AOT_MODE_LAST) {
			log_debug (LOG_DEFAULT, "Mono AOT mode: %s", mono_aot_mode_name);
		} else {
			if (!is_interpreter_enabled ()) {
				log_warn (LOG_DEFAULT, "Unknown Mono AOT mode: %s", mono_aot_mode_name);
			} else {
				log_warn (LOG_DEFAULT, "Mono AOT mode: interpreter");
			}
		}
	}

	if (application_config.environment_variable_count % 2 != 0) {
		log_warn (LOG_DEFAULT, "Corrupted environment variable array: does not contain an even number of entries (%u)", application_config.environment_variable_count);
		return;
	}

	const char *var_name;
	const char *var_value;
	for (size_t i = 0uz; i < application_config.environment_variable_count; i += 2) {
		var_name = app_environment_variables [i];
		if (var_name == nullptr || *var_name == '\0')
			continue;

		var_value = app_environment_variables [i + 1uz];
		if (var_value == nullptr)
			var_value = "";

#if defined (DEBUG)
		log_info (LOG_DEFAULT, "Setting environment variable '%s' to '%s'", var_name, var_value);
#endif // def DEBUG
		if (setenv (var_name, var_value, 1) < 0)
			log_warn (LOG_DEFAULT, "Failed to set environment variable: %s", strerror (errno));
	}
#if defined (DEBUG)
	log_debug (LOG_DEFAULT, "Loading environment from  override directories.");
	for (const char *od : override_dirs) {
		if (od == nullptr) {
			continue;
		}
		std::unique_ptr<char[]> env_override_file {Util::path_combine (od, OVERRIDE_ENVIRONMENT_FILE_NAME.data ())};
		log_debug (LOG_DEFAULT, "%s", env_override_file.get ());
		if (Util::file_exists (env_override_file.get ())) {
			log_debug (LOG_DEFAULT, "Loading %s", env_override_file.get ());
			setup_environment_from_override_file (env_override_file.get ());
		}
	}
#endif // def DEBUG
}

void
AndroidSystem::setup_process_args_apk (const char *apk, size_t index, size_t apk_count, [[maybe_unused]] void *user_data) noexcept
{
	if (apk == nullptr || index != apk_count - 1)
		return;

	char *args[1] = { (char*) apk };
	mono_runtime_set_main_args (1, args);
}

void
AndroidSystem::setup_process_args (jstring_array_wrapper &runtimeApks) noexcept
{
	for_each_apk (runtimeApks, static_cast<AndroidSystem::ForEachApkHandler> (&AndroidSystem::setup_process_args_apk), nullptr);
}

void
AndroidSystem::detect_embedded_dso_mode (jstring_array_wrapper& appDirs) noexcept
{
	// appDirs[SharedConstants::APP_DIRS_DATA_DIR_INDEX] points to the native library directory
	std::unique_ptr<char> libmonodroid_path {Util::path_combine (appDirs[SharedConstants::APP_DIRS_DATA_DIR_INDEX].get_cstr (), "libmonodroid.so")};
	log_debug (LOG_ASSEMBLY, "Checking if libmonodroid was unpacked to %s", libmonodroid_path.get ());
	if (!Util::file_exists (libmonodroid_path.get ())) {
		log_debug (LOG_ASSEMBLY, "%s not found, assuming application/android:extractNativeLibs == false", libmonodroid_path.get ());
		set_embedded_dso_mode_enabled (true);
	} else {
		log_debug (LOG_ASSEMBLY, "Native libs extracted to %s, assuming application/android:extractNativeLibs == true", appDirs[SharedConstants::APP_DIRS_DATA_DIR_INDEX].get_cstr ());
		set_embedded_dso_mode_enabled (false);
	}
}

void
AndroidSystem::setup_app_library_directories (jstring_array_wrapper& runtimeApks, jstring_array_wrapper& appDirs, bool have_split_apks) noexcept
{
	if (!is_embedded_dso_mode_enabled ()) {
		log_debug (LOG_DEFAULT, "Setting up for DSO lookup in app data directories");

		AndroidSystem::app_lib_directories = std::span<const char*> (single_app_lib_directory);
		AndroidSystem::app_lib_directories [0] = Util::strdup_new (appDirs[SharedConstants::APP_DIRS_DATA_DIR_INDEX].get_cstr ());
		log_debug (LOG_ASSEMBLY, "Added filesystem DSO lookup location: %s", appDirs[SharedConstants::APP_DIRS_DATA_DIR_INDEX].get_cstr ());
	} else {
		log_debug (LOG_DEFAULT, "Setting up for DSO lookup directly in the APK");

		if (have_split_apks) {
			// If split apks are used, then we will have just a single app library directory. Don't allocate any memory
			// dynamically in this case
			AndroidSystem::app_lib_directories = std::span<const char*> (single_app_lib_directory);
		} else {
			size_t app_lib_directories_size = have_split_apks ? 1uz : runtimeApks.get_length ();
			AndroidSystem::app_lib_directories = std::span<const char*> (new const char*[app_lib_directories_size], app_lib_directories_size);
		}

		unsigned short built_for_cpu = 0, running_on_cpu = 0;
		unsigned char is64bit = 0;
		_monodroid_detect_cpu_and_architecture (&built_for_cpu, &running_on_cpu, &is64bit);
		setup_apk_directories (running_on_cpu, runtimeApks, have_split_apks);
	}
}

void
AndroidSystem::for_each_apk (jstring_array_wrapper &runtimeApks, ForEachApkHandler handler, void *user_data) noexcept
{
	size_t apksLength = runtimeApks.get_length ();
	for (size_t i = 0uz; i < apksLength; ++i) {
		jstring_wrapper &e = runtimeApks [i];

		(handler) (e.get_cstr (), i, apksLength, user_data);
	}
}

force_inline void
AndroidSystem::add_apk_libdir (const char *apk, size_t &index, const char *abi) noexcept
{
	abort_unless (index < app_lib_directories.size (), "Index out of range");
	app_lib_directories [index] = Util::string_concat (apk, "!/lib/", abi);
	log_debug (LOG_ASSEMBLY, "Added APK DSO lookup location: %s", app_lib_directories[index]);
	index++;
}

force_inline void
AndroidSystem::setup_apk_directories (unsigned short running_on_cpu, jstring_array_wrapper &runtimeApks, bool have_split_apks) noexcept
{
	const char *abi = android_abi_names [running_on_cpu];
	size_t number_of_added_directories = 0uz;

	for (size_t i = 0uz; i < runtimeApks.get_length (); ++i) {
		jstring_wrapper &e = runtimeApks [i];
		const char *apk = e.get_cstr ();

		if (have_split_apks) {
			if (Util::ends_with (apk, SharedConstants::split_config_abi_apk_name)) {
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
AndroidSystem::determine_primary_override_dir (jstring_wrapper &home) noexcept
{
	dynamic_local_string<SENSIBLE_PATH_MAX> name { home.get_cstr () };
	name.append ("/")
	    .append (SharedConstants::OVERRIDE_DIRECTORY_NAME)
	    .append ("/")
	    .append (SharedConstants::android_lib_abi);

	return Util::strdup_new (name.get ());
}
