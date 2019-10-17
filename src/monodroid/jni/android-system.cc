#include <limits.h>
#include <string.h>
#include <stdlib.h>
#include <errno.h>
#include <assert.h>
#include <ctype.h>
#include <dlfcn.h>
#include <fcntl.h>

#ifdef ANDROID
#include <sys/system_properties.h>
#endif

#if defined (WINDOWS)
#include <windef.h>
#include <winbase.h>
#include <shlobj.h>
#include <objbase.h>
#include <knownfolders.h>
#include <shlwapi.h>
#endif

#include "globals.hh"
#include "android-system.hh"
#include "monodroid.h"
#include "monodroid-glue-internal.hh"
#include "jni-wrappers.hh"
#include "xamarin-app.h"
#include "cpp-util.hh"

#if defined (DEBUG) || !defined (ANDROID)
namespace xamarin::android::internal {
	struct BundledProperty {
		char     *name;
		char     *value;
		size_t    value_len;
		struct BundledProperty *next;
	};
}
#endif // DEBUG || !ANDROID

using namespace xamarin::android;
using namespace xamarin::android::internal;

#if defined (DEBUG) || !defined (ANDROID)
BundledProperty *AndroidSystem::bundled_properties = nullptr;
#endif // DEBUG || !ANDROID

#if defined (WINDOWS)
std::mutex AndroidSystem::readdir_mutex;
char *AndroidSystem::libmonoandroid_directory_path = nullptr;
#endif

#if !defined (ANDROID)
static constexpr uint32_t PROP_NAME_MAX = 32;
static constexpr uint32_t PROP_VALUE_MAX = 92;
#endif

#if defined (DEBUG) || !defined (ANDROID)
BundledProperty*
AndroidSystem::lookup_system_property (const char *name)
{
	for (BundledProperty *p = bundled_properties; p != nullptr; p = p->next) {
		if (strcmp (p->name, name) == 0) {
			return p;
		}
	}
	return nullptr;
}
#endif // DEBUG || !ANDROID

const char*
AndroidSystem::lookup_system_property (const char *name, size_t &value_len)
{
	value_len = 0;
#if defined (DEBUG) || !defined (ANDROID)
	BundledProperty *p = lookup_system_property (name);
	if (p != nullptr) {
		value_len = p->value_len;
		return p->name;
	}
#endif // DEBUG || !ANDROID

	if (application_config.system_property_count == 0)
		return nullptr;

	if (application_config.system_property_count % 2 != 0) {
		log_warn (LOG_DEFAULT, "Corrupted environment variable array: does not contain an even number of entries (%u)", application_config.environment_variable_count);
		return nullptr;
	}

	const char *prop_name;
	const char *prop_value;
	for (size_t i = 0; i < application_config.system_property_count; i += 2) {
		prop_name = app_system_properties[i];
		if (prop_name == nullptr || *prop_name == '\0')
			continue;

		if (strcmp (prop_name, name) == 0) {
			prop_value = app_system_properties [i + 1];
			if (prop_value == nullptr || *prop_value == '\0') {
				value_len = 0;
				return "";
			}

			value_len = strlen (prop_value);
			return prop_value;
		}
	}

	return nullptr;
}

#if defined (DEBUG) || !defined (ANDROID)
void
AndroidSystem::add_system_property (const char *name, const char *value)
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
	size_t alloc_size = ADD_WITH_OVERFLOW_CHECK (size_t, sizeof (BundledProperty), name_len + 1);
	p = reinterpret_cast<BundledProperty*> (malloc (alloc_size));
	if (p == nullptr)
		return;

	p->name = ((char*) p) + sizeof (struct BundledProperty);
	strncpy (p->name, name, name_len);
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
#endif // DEBUG || !ANDROID

#ifndef ANDROID
void
AndroidSystem::monodroid_strreplace (char *buffer, char old_char, char new_char)
{
	if (buffer == nullptr)
		return;
	while (*buffer != '\0') {
		if (*buffer == old_char)
			*buffer = new_char;
		buffer++;
	}
}

int
AndroidSystem::_monodroid__system_property_get (const char *name, char *sp_value, size_t sp_value_len)
{
	if (!name || !sp_value)
		return -1;

	char *env_name = utils.monodroid_strdup_printf ("__XA_%s", name);
	monodroid_strreplace (env_name, '.', '_');
	char *env_value = getenv (env_name);
	free (env_name);

	size_t env_value_len = env_value ? strlen (env_value) : 0;
	if (env_value_len == 0) {
		sp_value[0] = '\0';
		return 0;
	}

	if (env_value_len >= sp_value_len)
		log_warn (LOG_DEFAULT, "System property buffer size too small by %u bytes", env_value_len == sp_value_len ? 1 : env_value_len - sp_value_len);

	//
	// sp_value_len includes the terminating nul, avoid a mingw g++ warning about string truncation
	// by making the amount of data copied one less than the indicated length. The warning reported
	// is:
	//
	//  In function ‘int xamarin::android::internal::AndroidSystem::_monodroid__system_property_get(const char*, char*, size_t)’,
	//    inlined from ‘int xamarin::android::internal::AndroidSystem::monodroid_get_system_property(const char*, char**)’ at ../../../jni/android-system.cc:243:44:
	//    ../../../jni/android-system.cc(206,10): warning G20816D19: ‘char* strncpy(char*, const char*, size_t)’ specified bound 93 equals destination size [-Wstringop-truncation] [/home/grendel/vc/xamarin/xamarin-android-worktrees/code-quality-improvements/src/monodroid/monodroid.csproj]
	//    strncpy (sp_value, env_value, sp_value_len);
	//
	strncpy (sp_value, env_value, sp_value_len - 1);
	sp_value[sp_value_len] = '\0';

	return static_cast<int>(strlen (sp_value));
}
#else
int
AndroidSystem::_monodroid__system_property_get (const char *name, char *sp_value, size_t sp_value_len)
{
	if (name == nullptr || sp_value == nullptr)
		return -1;

	char *buf = nullptr;
	if (sp_value_len < PROP_VALUE_MAX + 1) {
		size_t alloc_size = ADD_WITH_OVERFLOW_CHECK (size_t, PROP_VALUE_MAX, 1);
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
#endif

int
AndroidSystem::monodroid_get_system_property (const char *name, char **value)
{
	if (value)
		*value = nullptr;

	char  sp_value [PROP_VALUE_MAX+1] = { 0, };
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
		size_t alloc_size = ADD_WITH_OVERFLOW_CHECK (size_t, static_cast<size_t>(len), 1);
		*value = new char [alloc_size];
		if (*value == nullptr)
			return -len;
		if (len > 0)
			memcpy (*value, pvalue, static_cast<size_t>(len));
		(*value)[len] = '\0';
	}
	return len;
}

size_t
AndroidSystem::monodroid_read_file_into_memory (const char *path, char *&value)
{
	size_t r = 0;
	value = nullptr;
	FILE *fp = utils.monodroid_fopen (path, "r");
	if (fp != nullptr) {
		struct stat fileStat;
		if (fstat (fileno (fp), &fileStat) == 0) {
			r = ADD_WITH_OVERFLOW_CHECK (size_t, static_cast<size_t>(fileStat.st_size), 1);
			value = new char[r];
			size_t nread = fread (value, 1, static_cast<size_t>(fileStat.st_size), fp);
			if (nread == 0 || nread != static_cast<size_t>(fileStat.st_size)) {
				log_warn(LOG_DEFAULT, "While reading file %s: expected to read %u bytes, actually read %u bytes", path, r, nread);
			}
		}
		fclose (fp);
	}
	return r;
}

#if defined (DEBUG) || !defined (ANDROID)
size_t
AndroidSystem::_monodroid_get_system_property_from_file (const char *path, char **value)
{
	if (value != nullptr)
		*value = nullptr;

	FILE* fp = utils.monodroid_fopen (path, "r");
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

	size_t alloc_size = ADD_WITH_OVERFLOW_CHECK (size_t, file_size, 1);
	*value = new char[alloc_size];

	size_t len = fread (*value, 1, file_size, fp);
	fclose (fp);
	for (size_t i = 0; i < file_size + 1; ++i) {
		if ((*value) [i] != '\n' && (*value) [i] != '\r')
			continue;
		(*value) [i] = 0;
		break;
	}
	return len;
}
#endif

size_t
AndroidSystem::monodroid_get_system_property_from_overrides (const char *name, char ** value)
{
#if defined (DEBUG) || !defined (ANDROID)
	for (size_t oi = 0; oi < MAX_OVERRIDES; ++oi) {
		if (override_dirs [oi]) {
			simple_pointer_guard<char[]> override_file (utils.path_combine (override_dirs [oi], name));
			log_info (LOG_DEFAULT, "Trying to get property from %s", override_file.get ());
			size_t result = _monodroid_get_system_property_from_file (override_file, value);
			if (result == 0 || value == nullptr || (*value) == nullptr || **value == '\0') {
				continue;
			}
			log_info (LOG_DEFAULT, "Property '%s' from  %s has value '%s'.", name, override_dirs [oi], *value);
			return result;
		}
	}
#endif
	return 0;
}

void
AndroidSystem::create_update_dir (char *override_dir)
{
#if defined(RELEASE)
	/*
	 * Don't create .__override__ on Release builds, because Google requires
	 * that pre-loaded apps not create world-writable directories.
	 *
	 * However, if any logging is enabled (which should _not_ happen with
	 * pre-loaded apps!), we need the .__override__ directory...
	 */
	if (log_categories == 0 && monodroid_get_system_property (Debug::DEBUG_MONO_PROFILE_PROPERTY, nullptr) == 0) {
		return;
	}
#endif

	override_dirs [0] = override_dir;
	utils.create_public_directory (override_dir);
	log_warn (LOG_DEFAULT, "Creating public update directory: `%s`", override_dir);
}

char*
AndroidSystem::get_full_dso_path (const char *base_dir, const char *dso_path, bool &needs_free)
{
	needs_free = false;
	if (dso_path == nullptr)
		return nullptr;

	if (base_dir == nullptr || utils.is_path_rooted (dso_path))
		return const_cast<char*>(dso_path); // Absolute path or no base path, can't do much with it

	needs_free = true;
	return utils.path_combine (base_dir, dso_path);
}

void*
AndroidSystem::load_dso (const char *path, int dl_flags, bool skip_exists_check)
{
	if (path == nullptr || *path == '\0')
		return nullptr;

	log_info (LOG_ASSEMBLY, "Trying to load shared library '%s'", path);
	if (!skip_exists_check && !is_embedded_dso_mode_enabled () && !utils.file_exists (path)) {
		log_info (LOG_ASSEMBLY, "Shared library '%s' not found", path);
		return nullptr;
	}

	void *handle = dlopen (path, dl_flags);
	if (handle == nullptr && utils.should_log (LOG_ASSEMBLY))
		log_info_nocheck (LOG_ASSEMBLY, "Failed to load shared library '%s'. %s", path, dlerror ());
	return handle;
}

void*
AndroidSystem::load_dso_from_specified_dirs (const char **directories, size_t num_entries, const char *dso_name, int dl_flags)
{
	assert (directories != nullptr);
	if (dso_name == nullptr)
		return nullptr;

	bool needs_free = false;
	char *full_path = nullptr;
	for (size_t i = 0; i < num_entries; i++) {
		full_path = get_full_dso_path (directories [i], dso_name, needs_free);
		void *handle = load_dso (full_path, dl_flags, false);
		if (needs_free)
			delete[] full_path;
		if (handle != nullptr)
			return handle;
	}

	return nullptr;
}

void*
AndroidSystem::load_dso_from_app_lib_dirs (const char *name, int dl_flags)
{
	return load_dso_from_specified_dirs (static_cast<const char**> (app_lib_directories), app_lib_directories_size, name, dl_flags);
}

void*
AndroidSystem::load_dso_from_override_dirs (const char *name, int dl_flags)
{
#ifdef RELEASE
	return nullptr;
#else
	return load_dso_from_specified_dirs (const_cast<const char**> (AndroidSystem::override_dirs), AndroidSystem::MAX_OVERRIDES, name, dl_flags);
#endif
}

void*
AndroidSystem::load_dso_from_any_directories (const char *name, int dl_flags)
{
	void *handle = load_dso_from_override_dirs (name, dl_flags);
	if (handle == nullptr)
		handle = load_dso_from_app_lib_dirs (name, dl_flags);
	return handle;
}

char*
AndroidSystem::get_existing_dso_path_on_disk (const char *base_dir, const char *dso_name, bool &needs_free)
{
	needs_free = false;
	char *dso_path = get_full_dso_path (base_dir, dso_name, needs_free);
	if (utils.file_exists (dso_path))
		return dso_path;

	needs_free = false;
	delete[] dso_path;
	return nullptr;
}

char*
AndroidSystem::get_full_dso_path_on_disk (const char *dso_name, bool &needs_free)
{
	needs_free = false;
	if (is_embedded_dso_mode_enabled ())
		return nullptr;

	char *dso_path = nullptr;
#ifndef RELEASE
	for (size_t i = 0; i < AndroidSystem::MAX_OVERRIDES; i++) {
		if (AndroidSystem::override_dirs [i] == nullptr)
			continue;
		dso_path = get_existing_dso_path_on_disk (AndroidSystem::override_dirs [i], dso_name, needs_free);
		if (dso_path != nullptr)
			return dso_path;
	}
#endif
	for (size_t i = 0; i < app_lib_directories_size; i++) {
		dso_path = get_existing_dso_path_on_disk (app_lib_directories [i], dso_name, needs_free);
		if (dso_path != nullptr)
			return dso_path;
	}

	return nullptr;
}

int
AndroidSystem::count_override_assemblies (void)
{
	int c = 0;

	for (size_t i = 0; i < MAX_OVERRIDES; ++i) {
		monodroid_dir_t *dir;
		monodroid_dirent_t *e;

		const char *dir_path = override_dirs [i];

		if (dir_path == nullptr || !utils.directory_exists (dir_path))
			continue;

		if ((dir = utils.monodroid_opendir (dir_path)) == nullptr)
			continue;

		while ((e = readdir (dir)) != nullptr && e) {
			if (utils.monodroid_dirent_hasextension (e, ".dll"))
				++c;
		}
		utils.monodroid_closedir (dir);
	}

	return c;
}

long
AndroidSystem::get_max_gref_count_from_system (void)
{
	long max;

	if (running_in_emulator) {
		max = 2000;
	} else {
		max = 51200;
	}

	char *override;
	if (androidSystem.monodroid_get_system_property (Debug::DEBUG_MONO_MAX_GREFC, &override) > 0) {
		char *e;
		max       = strtol (override, &e, 10);
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
			max = INT_MAX;
		if (*e) {
			log_warn (LOG_GC, "Unsupported '%s' value '%s'.", Debug::DEBUG_MONO_MAX_GREFC, override);
		}
		log_warn (LOG_GC, "Overriding max JNI Global Reference count to %i", max);
		delete[] override;
	}
	return max;
}

long
AndroidSystem::get_gref_gc_threshold ()
{
	if (max_gref_count == INT_MAX)
		return max_gref_count;
	return static_cast<int> ((max_gref_count * 90LL) / 100LL);
}

#if defined (DEBUG) || !defined (ANDROID)
void
AndroidSystem::setup_environment (const char *name, const char *value)
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
AndroidSystem::setup_environment_from_override_file (const char *path)
{
#if WINDOWS
	using read_count_type = unsigned int;
#else
	using read_count_type = size_t;
#endif
	monodroid_stat_t sbuf;
	if (utils.monodroid_stat (path, &sbuf) < 0) {
		log_warn (LOG_DEFAULT, "Failed to stat the environment override file %s: %s", path, strerror (errno));
		return;
	}

	int fd = open (path, O_RDONLY);
	if (fd < 0) {
		log_warn (LOG_DEFAULT, "Failed to open the environment override file %s: %s", path, strerror (errno));
		return;
	}

	auto     file_size = static_cast<size_t>(sbuf.st_size);
	size_t   nread = 0;
	ssize_t  r;
	simple_pointer_guard<char[]> buf (new char [file_size]);

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
	unsigned long name_width = strtoul (buf, &endptr, 16);
	if ((name_width == ULONG_MAX && errno == ERANGE) || (*buf != '\0' && *endptr != '\0')) {
		log_warn (LOG_DEFAULT, "Malformed header of the environment override file %s: name width has invalid format", path);
		return;
	}

	unsigned long value_width = strtoul (buf.get () + 11, &endptr, 16);
	if ((value_width == ULONG_MAX && errno == ERANGE) || (*buf != '\0' && *endptr != '\0')) {
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
			log_warn (LOG_DEFAULT, "Malformed environment override file %s: name at offset %lu is empty", path, name - buf);
			return;
		}

		log_debug (LOG_DEFAULT, "Setting environment variable from the override file %s: '%s' = '%s'", path, name, name + name_width);
		setup_environment (name, name + name_width);
		name += data_width;
		data_size -= data_width;
	}
}
#endif // DEBUG || !ANDROID

void
AndroidSystem::setup_environment ()
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

			default:
				aotMode = MonoAotMode::MONO_AOT_MODE_LAST;
				break;
		}

		if (aotMode == MonoAotMode::MONO_AOT_MODE_LAST)
			log_info (LOG_DEFAULT, "Mono AOT mode: %s", mono_aot_mode_name);
		else
			log_warn (LOG_DEFAULT, "Unknown Mono AOT mode: %s", mono_aot_mode_name);
	}

	if (application_config.environment_variable_count == 0)
		return;

	if (application_config.environment_variable_count % 2 != 0) {
		log_warn (LOG_DEFAULT, "Corrupted environment variable array: does not contain an even number of entries (%u)", application_config.environment_variable_count);
		return;
	}

	const char *var_name;
	const char *var_value;
	for (size_t i = 0; i < application_config.environment_variable_count; i += 2) {
		var_name = app_environment_variables [i];
		if (var_name == nullptr || *var_name == '\0')
			continue;

		var_value = app_environment_variables [i + 1];
		if (var_value == nullptr)
			var_value = "";

#if defined (DEBUG)
		log_info (LOG_DEFAULT, "Setting environment variable '%s' to '%s'", var_name, var_value);
#endif
		if (setenv (var_name, var_value, 1) < 0)
			log_warn (LOG_DEFAULT, "Failed to set environment variable: %s", strerror (errno));
	}
#if defined (DEBUG) || !defined (ANDROID)
	// TODO: for debug read from file in the override directory named `environment`
	for (size_t oi = 0; oi < MAX_OVERRIDES; oi++) {
		simple_pointer_guard<char[]> env_override_file (utils.path_combine (override_dirs [oi], OVERRIDE_ENVIRONMENT_FILE_NAME));
		if (utils.file_exists (env_override_file)) {
			setup_environment_from_override_file (env_override_file);
		}
	}
#endif
}

void
AndroidSystem::setup_process_args_apk (const char *apk, size_t index, size_t apk_count, void *user_data)
{
	if (apk == nullptr || index != apk_count - 1)
		return;

	char *args[1] = { (char*) apk };
	mono_runtime_set_main_args (1, args);
}

void
AndroidSystem::setup_process_args (JNIEnv *env, jstring_array_wrapper &runtimeApks)
{
	for_each_apk (env, runtimeApks, static_cast<BasicAndroidSystem::ForEachApkHandler> (&AndroidSystem::setup_process_args_apk), nullptr);
}

monodroid_dirent_t*
AndroidSystem::readdir (monodroid_dir_t *dir)
{
#if defined (WINDOWS)
	return readdir_windows (dir);
#else
	return ::readdir (dir);
#endif
}

#if defined (WINDOWS)
struct _wdirent*
AndroidSystem::readdir_windows (_WDIR *dirp)
{
	std::lock_guard<std::mutex> lock (readdir_mutex);
	errno = 0;
	struct _wdirent *entry = _wreaddir (dirp);

	if (entry == nullptr && errno != 0)
		return nullptr;

	return entry;
}

// Returns the directory in which this library was loaded from
char*
AndroidSystem::get_libmonoandroid_directory_path ()
{
	wchar_t module_path[MAX_PATH];
	HMODULE module = nullptr;

	if (libmonoandroid_directory_path != nullptr)
		return libmonoandroid_directory_path;

	DWORD flags = GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT;
	const wchar_t *dir_path = reinterpret_cast<wchar_t*>(&libmonoandroid_directory_path);
	BOOL retval = GetModuleHandleExW (flags, dir_path, &module);
	if (!retval)
		return nullptr;

	GetModuleFileNameW (module, module_path, sizeof (module_path) / sizeof (module_path[0]));
	PathRemoveFileSpecW (module_path);
	libmonoandroid_directory_path = utils.utf16_to_utf8 (module_path);
	return libmonoandroid_directory_path;
}

int
AndroidSystem::setenv (const char *name, const char *value, int overwrite)
{
	wchar_t *wname  = utils.utf8_to_utf16 (name);
	wchar_t *wvalue = utils.utf8_to_utf16 (value);

	BOOL result = SetEnvironmentVariableW (wname, wvalue);
	free (wname);
	free (wvalue);

	return result ? 0 : -1;
}

int
AndroidSystem::symlink (const char *target, const char *linkpath)
{
	return utils.file_copy (target, linkpath);
}
#else
#endif
