#include <limits.h>
#include <string.h>
#include <errno.h>
#include <assert.h>
#include <ctype.h>
#include <dlfcn.h>

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

#include "unzip.h"
#include "globals.h"
#include "android-system.h"
#include "monodroid.h"
#include "monodroid-glue-internal.h"
#include "jni-wrappers.h"

namespace xamarin { namespace android { namespace internal {
	struct BundledProperty {
		char *name;
		char *value;
		int   value_len;
		struct BundledProperty *next;
	};
}}}

using namespace xamarin::android;
using namespace xamarin::android::internal;

BundledProperty *AndroidSystem::bundled_properties = nullptr;
char* AndroidSystem::override_dirs [MAX_OVERRIDES];
const char **AndroidSystem::app_lib_directories;
size_t AndroidSystem::app_lib_directories_size = 0;
#if WINDOWS
static const char *SYSTEM_LIB_PATH;
#else
constexpr char AndroidSystem::SYSTEM_LIB_PATH[];
#endif
constexpr char AndroidSystem::MONO_SGEN_SO[];
constexpr char AndroidSystem::MONO_SGEN_ARCH_SO[];

#if defined (WINDOWS)
std::mutex AndroidSystem::readdir_mutex;
char *AndroidSystem::libmonoandroid_directory_path = nullptr;
#endif

// Values correspond to the CPU_KIND_* macros
const char* AndroidSystem::android_abi_names[CPU_KIND_X86_64+1] = {
	"unknown",
	[CPU_KIND_ARM]      = "armeabi-v7a",
	[CPU_KIND_ARM64]    = "arm64-v8a",
	[CPU_KIND_MIPS]     = "mips",
	[CPU_KIND_X86]      = "x86",
	[CPU_KIND_X86_64]   = "x86_64",
};
#define ANDROID_ABI_NAMES_SIZE (sizeof(android_abi_names) / sizeof (android_abi_names[0]))

#if !defined (ANDROID)
static constexpr uint32_t PROP_NAME_MAX = 32;
static constexpr uint32_t PROP_VALUE_MAX = 92;
#endif

BundledProperty*
AndroidSystem::lookup_system_property (const char *name)
{
	BundledProperty *p = bundled_properties;
	for ( ; p ; p = p->next)
		if (strcmp (p->name, name) == 0)
			return p;
	return nullptr;
}

void
AndroidSystem::add_system_property (const char *name, const char *value)
{
	BundledProperty* p = lookup_system_property (name);
	if (p) {
		char *n = value != nullptr ? strdup (value) : nullptr;
		if (n == nullptr)
			return;
		free (p->value);
		p->value      = n;
		p->value_len  = strlen (p->value);
		return;
	}

	int name_len  = strlen (name);
	p = reinterpret_cast<BundledProperty*> (malloc (sizeof ( BundledProperty) + name_len + 1));
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

	strncpy (sp_value, env_value, sp_value_len);
	sp_value[sp_value_len] = '\0';

	return strlen (sp_value);
}
#else
int
AndroidSystem::_monodroid__system_property_get (const char *name, char *sp_value, size_t sp_value_len)
{
	if (name == nullptr || sp_value == nullptr)
		return -1;

	char *buf = nullptr;
	if (sp_value_len < PROP_VALUE_MAX + 1) {
		log_warn (LOG_DEFAULT, "Buffer to store system property may be too small, will copy only %u bytes", sp_value_len);
		buf = new char [PROP_VALUE_MAX + 1];
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

	BundledProperty *p;
	if (len <= 0 && (p = lookup_system_property (name)) != nullptr) {
		pvalue  = p->value;
		len     = p->value_len;
	}

	if (len >= 0 && value) {
		*value = new char [len+1];
		if (*value == nullptr)
			return -len;
		if (len > 0)
			memcpy (*value, pvalue, len);
		(*value)[len] = '\0';
	}
	return len;
}

int
AndroidSystem::monodroid_read_file_into_memory (const char *path, char **value)
{
	int r = 0;
	if (value) {
		*value = nullptr;
	}
	FILE *fp = utils.monodroid_fopen (path, "r");
	if (fp != nullptr) {
		struct stat fileStat;
		if (fstat (fileno (fp), &fileStat) == 0) {
			r = fileStat.st_size+1;
			if (value && (*value = new char[r])) {
				fread (*value, 1, fileStat.st_size, fp);
			}
		}
		fclose (fp);
	}
	return r;
}

int
AndroidSystem::_monodroid_get_system_property_from_file (const char *path, char **value)
{
	if (value)
		*value = nullptr;

	FILE* fp = utils.monodroid_fopen (path, "r");
	if (fp == nullptr)
		return 0;

	struct stat fileStat;
	if (fstat (fileno (fp), &fileStat) < 0) {
		fclose (fp);
		return 0;
	}

	if (!value) {
		fclose (fp);
		return fileStat.st_size+1;
	}

	*value = new char[fileStat.st_size+1];
	if (!(*value)) {
		fclose (fp);
		return fileStat.st_size+1;
	}

	ssize_t len = fread (*value, 1, fileStat.st_size, fp);
	fclose (fp);
	for (size_t i = 0; i < fileStat.st_size+1; ++i) {
		if ((*value) [i] != '\n' && (*value) [i] != '\r')
			continue;
		(*value) [i] = 0;
		break;
	}
	return len;
}

int
AndroidSystem::monodroid_get_system_property_from_overrides (const char *name, char ** value)
{
	int result = -1;

	for (size_t oi = 0; oi < MAX_OVERRIDES; ++oi) {
		if (override_dirs [oi]) {
			char *overide_file = utils.path_combine (override_dirs [oi], name);
			log_info (LOG_DEFAULT, "Trying to get property from %s", overide_file);
			result = _monodroid_get_system_property_from_file (overide_file, value);
			free (overide_file);
			if (result <= 0 || value == nullptr || (*value) == nullptr || strlen (*value) == 0) {
				continue;
			}
			log_info (LOG_DEFAULT, "Property '%s' from  %s has value '%s'.", name, override_dirs [oi], *value);
			return result;
		}
	}
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
	if (log_categories == 0 && utils.monodroid_get_namespaced_system_property (Debug::DEBUG_MONO_PROFILE_PROPERTY, nullptr) == 0) {
		return;
	}
#endif

	override_dirs [0] = override_dir;
	utils.create_public_directory (override_dir);
	log_warn (LOG_DEFAULT, "Creating public update directory: `%s`", override_dir);
}

#define TRY_LIBMONOSGEN(dir) \
	if (dir) { \
		libmonoso = utils.path_combine (dir, MONO_SGEN_SO); \
		log_warn (LOG_DEFAULT, "Trying to load sgen from: %s", libmonoso);	\
		if (utils.file_exists (libmonoso)) \
			return libmonoso; \
		free (libmonoso); \
	}

#ifndef RELEASE
void
AndroidSystem::copy_native_libraries_to_internal_location ()
{
	for (size_t i = 0; i < MAX_OVERRIDES; ++i) {
		monodroid_dir_t *dir;
		monodroid_dirent_t b, *e;

		char *dir_path = utils.path_combine (override_dirs [i], "lib");
		log_warn (LOG_DEFAULT, "checking directory: `%s`", dir_path);

		if (dir_path == nullptr || !utils.directory_exists (dir_path)) {
			log_warn (LOG_DEFAULT, "directory does not exist: `%s`", dir_path);
			free (dir_path);
			continue;
		}

		if ((dir = utils.monodroid_opendir (dir_path)) == nullptr) {
			log_warn (LOG_DEFAULT, "could not open directory: `%s`", dir_path);
			free (dir_path);
			continue;
		}

		while (readdir_r (dir, &b, &e) == 0 && e) {
			log_warn (LOG_DEFAULT, "checking file: `%s`", e->d_name);
			if (utils.monodroid_dirent_hasextension (e, ".so")) {
#if WINDOWS
				char *file_name = utils.utf16_to_utf8 (e->d_name);
#else   /* def WINDOWS */
				char *file_name = e->d_name;
#endif  /* ndef WINDOWS */
				copy_file_to_internal_location (primary_override_dir, dir_path, file_name);
#if WINDOWS
				free (file_name);
#endif  /* def WINDOWS */
			}
		}
		utils.monodroid_closedir (dir);
		free (dir_path);
	}
}
#endif

inline bool AndroidSystem::try_load_libmonosgen (const char *dir, char*& libmonoso)
{
	if (dir == nullptr || *dir == '\0')
		return false;

	libmonoso = utils.path_combine (dir, MONO_SGEN_SO);
	log_warn (LOG_DEFAULT, "Trying to load sgen from: %s", libmonoso);
	if (utils.file_exists (libmonoso))
		return true;
	free (libmonoso);
	libmonoso = nullptr;

	return false;
}

char*
AndroidSystem::get_libmonosgen_path ()
{
	char *libmonoso;
	bool embedded_dso_mode_enabled = is_embedded_dso_mode_enabled ();

#ifndef RELEASE
	// Android 5 includes some restrictions on loading dynamic libraries via dlopen() from
	// external storage locations so we need to file copy the shared object to an internal
	// storage location before loading it.
	copy_native_libraries_to_internal_location ();

	if (!embedded_dso_mode_enabled) {
		for (size_t i = 0; i < MAX_OVERRIDES; ++i) {
			if (try_load_libmonosgen (override_dirs [i], libmonoso)) {
				return libmonoso;
			}
		}
	}
#endif
	if (!embedded_dso_mode_enabled) {
		for (size_t i = 0; i < app_lib_directories_size; i++) {
			if (try_load_libmonosgen (app_lib_directories [i], libmonoso)) {
				return libmonoso;
			}
		}
	}

	if (runtime_libdir != nullptr) {
		libmonoso = utils.path_combine (runtime_libdir, MONO_SGEN_ARCH_SO);
	} else
		libmonoso = nullptr;

	if (libmonoso != nullptr && utils.file_exists (libmonoso)) {
		char* links_dir = utils.path_combine (primary_override_dir, "links");
		char* link = utils.path_combine (links_dir, MONO_SGEN_SO);
		if (!utils.directory_exists (links_dir)) {
			if (!utils.directory_exists (primary_override_dir))
				utils.create_public_directory (primary_override_dir);
			utils.create_public_directory (links_dir);
		}
		free (links_dir);
		if (!utils.file_exists (link)) {
			int result = symlink (libmonoso, link);
			if (result != 0 && errno == EEXIST) {
				log_warn (LOG_DEFAULT, "symlink exists, recreating: %s -> %s", link, libmonoso);
				unlink (link);
				result = symlink (libmonoso, link);
			}
			if (result != 0)
				log_warn (LOG_DEFAULT, "symlink failed with errno=%i %s", errno, strerror (errno));
		}
		free (libmonoso);
		libmonoso = link;
	}

	log_warn (LOG_DEFAULT, "Trying to load sgen from: %s", libmonoso);
	if (libmonoso != nullptr && utils.file_exists (libmonoso))
		return libmonoso;
	free (libmonoso);

#ifdef WINDOWS
	if (try_load_libmonosgen (get_libmonoandroid_directory_path (), libmonoso))
		return libmonoso;
#endif

	if (try_load_libmonosgen (SYSTEM_LIB_PATH, libmonoso))
		return libmonoso;
	log_fatal (LOG_DEFAULT, "Cannot find '%s'. Looked in the following locations:", MONO_SGEN_SO);

#ifndef RELEASE
	if (!embedded_dso_mode_enabled) {
		for (size_t i = 0; i < MAX_OVERRIDES; ++i) {
			if (override_dirs [i] == nullptr)
				continue;
			log_fatal (LOG_DEFAULT, "  %s", override_dirs [i]);
		}
	}
#endif
	for (size_t i = 0; i < app_lib_directories_size; i++) {
		log_fatal (LOG_DEFAULT, "  %s", app_lib_directories [i]);
	}

	log_fatal (LOG_DEFAULT, "Do you have a shared runtime build of your app with AndroidManifest.xml android:minSdkVersion < 10 while running on a 64-bit Android 5.0 target? This combination is not supported.");
	log_fatal (LOG_DEFAULT, "Please either set android:minSdkVersion >= 10 or use a build without the shared runtime (like default Release configuration).");
	exit (FATAL_EXIT_CANNOT_FIND_LIBMONOSGEN);

	return libmonoso;
}

char*
AndroidSystem::get_full_dso_path (const char *base_dir, const char *dso_path, bool *needs_free)
{
	assert (needs_free != nullptr);

	*needs_free = false;
	if (!dso_path)
		return nullptr;

	if (base_dir == nullptr || utils.is_path_rooted (dso_path))
		return (char*)dso_path; // Absolute path or no base path, can't do much with it

	char *full_path = utils.path_combine (base_dir, dso_path);
	*needs_free = true;
	return full_path;
}

void*
AndroidSystem::load_dso (const char *path, int dl_flags, bool skip_exists_check)
{
	if (path == nullptr)
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
AndroidSystem::load_dso_from_specified_dirs (const char **directories, int num_entries, const char *dso_name, int dl_flags)
{
	assert (directories != nullptr);
	if (dso_name == nullptr)
		return nullptr;

	bool needs_free = false;
	char *full_path = nullptr;
	for (size_t i = 0; i < num_entries; i++) {
		full_path = get_full_dso_path (directories [i], dso_name, &needs_free);
		void *handle = load_dso (full_path, dl_flags, false);
		if (needs_free)
			free (full_path);
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
AndroidSystem::get_existing_dso_path_on_disk (const char *base_dir, const char *dso_name, bool *needs_free)
{
	assert (needs_free != nullptr);

	*needs_free = false;
	char *dso_path = get_full_dso_path (base_dir, dso_name, needs_free);
	if (utils.file_exists (dso_path))
		return dso_path;

	*needs_free = false;
	free (dso_path);
	return nullptr;
}

void
AndroidSystem::dso_alloc_cleanup (char **dso_path, bool *needs_free)
{
	assert (needs_free != nullptr);
	if (dso_path != nullptr) {
		if (*needs_free)
			free (*dso_path);
		*dso_path = nullptr;
	}
	*needs_free = false;
}

char*
AndroidSystem::get_full_dso_path_on_disk (const char *dso_name, bool *needs_free)
{
	assert (needs_free != nullptr);

	*needs_free = false;
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
		dso_alloc_cleanup (&dso_path, needs_free);
	}
#endif
	for (size_t i = 0; i < app_lib_directories_size; i++) {
		dso_path = get_existing_dso_path_on_disk (app_lib_directories [i], dso_name, needs_free);
		if (dso_path != nullptr)
			return dso_path;
		dso_alloc_cleanup (&dso_path, needs_free);
	}

	return nullptr;
}

int
AndroidSystem::count_override_assemblies (void)
{
	int c = 0;

	for (size_t i = 0; i < MAX_OVERRIDES; ++i) {
		monodroid_dir_t *dir;
		monodroid_dirent_t b, *e;

		const char *dir_path = override_dirs [i];

		if (dir_path == nullptr || !utils.directory_exists (dir_path))
			continue;

		if ((dir = utils.monodroid_opendir (dir_path)) == nullptr)
			continue;

		while (readdir_r (dir, &b, &e) == 0 && e) {
			if (utils.monodroid_dirent_hasextension (e, ".dll"))
				++c;
		}
		utils.monodroid_closedir (dir);
	}

	return c;
}

int
AndroidSystem::get_max_gref_count_from_system (void)
{
	constexpr char HARDWARE_TYPE[] = "ro.hardware";
	constexpr char HARDWARE_EMULATOR[] = "goldfish";

	int max;
	char value [PROP_VALUE_MAX+1];
	char *override;

	int len = _monodroid__system_property_get (HARDWARE_TYPE, value, sizeof (value));
	if (len > 0 && strcmp (value, HARDWARE_EMULATOR) == 0) {
		max = 2000;
	} else {
		max = 51200;
	}

	if (utils.monodroid_get_namespaced_system_property (Debug::DEBUG_MONO_MAX_GREFC, &override) > 0) {
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
		free (override);
	}
	return max;
}

#ifdef ANDROID
void
AndroidSystem::copy_file_to_internal_location (char *to_dir, char *from_dir, char *file)
{
	char *from_file = utils.path_combine (from_dir, file);
	char *to_file   = nullptr;

	do {
		if (!from_file || !utils.file_exists (from_file))
			break;

		log_warn (LOG_DEFAULT, "Copying file `%s` from external location `%s` to internal location `%s`",
				file, from_dir, to_dir);

		to_file = utils.path_combine (to_dir, file);
		if (!to_file)
			break;

		int r = unlink (to_file);
		if (r < 0 && errno != ENOENT) {
			log_warn (LOG_DEFAULT, "Unable to delete file `%s`: %s", to_file, strerror (errno));
			break;
		}

		if (!utils.file_copy (to_file, from_file)) {
			log_warn (LOG_DEFAULT, "Copy failed from `%s` to `%s`: %s", from_file, to_file, strerror (errno));
			break;
		}

		utils.set_user_executable (to_file);
	} while (0);

	free (from_file);
	free (to_file);
}
#else  /* !defined (ANDROID) */
void
AndroidSystem::copy_file_to_internal_location (char *to_dir, char *from_dir, char* file)
{
}
#endif /* defined (ANDROID) */

int
AndroidSystem::get_gref_gc_threshold ()
{
	if (max_gref_count == INT_MAX)
		return max_gref_count;
	return static_cast<int> ((max_gref_count * 90LL) / 100LL);
}

void
AndroidSystem::setup_environment (jstring_wrapper& name, jstring_wrapper& value)
{
	const char *k = name.get_cstr ();

	if (k == nullptr || *k == '\0')
		return;

	const char *v = value.get_cstr ();
	if (v == nullptr || *v == '\0')
		v = "";

	if (isupper (k [0]) || k [0] == '_') {
		if (k [0] == '_') {
			if (strcmp (k, "__XA_DSO_IN_APK") == 0) {
				knownEnvVars.DSOInApk = true;
				return;
			}
		}

		setenv (k, v, 1);
		return;
	}

	if (k [0] == 'm') {
		if (strcmp (k, "mono.aot") == 0) {
			if (*v == '\0') {
				knownEnvVars.MonoAOT = MonoAotMode::MONO_AOT_MODE_NONE;
				return;
			}

			switch (v [0]) {
				case 'n':
					knownEnvVars.MonoAOT = MonoAotMode::MONO_AOT_MODE_NORMAL;
					break;

				case 'h':
					knownEnvVars.MonoAOT = MonoAotMode::MONO_AOT_MODE_HYBRID;
					break;

				case 'f':
					knownEnvVars.MonoAOT = MonoAotMode::MONO_AOT_MODE_FULL;
					break;

				default:
					knownEnvVars.MonoAOT = MonoAotMode::MONO_AOT_MODE_UNKNOWN;
					break;
			}

			if (knownEnvVars.MonoAOT != MonoAotMode::MONO_AOT_MODE_UNKNOWN)
				log_info (LOG_DEFAULT, "Mono AOT mode: %s", v);
			else
				log_warn (LOG_DEFAULT, "Unknown Mono AOT mode: %s", v);

			return;
		}

		if (strcmp (k, "mono.llvm") == 0) {
			knownEnvVars.MonoLLVM = true;
			return;
		}

		if (strcmp (k, "mono.enable_assembly_preload") == 0) {
			if (*v == '\0')
				knownEnvVars.EnableAssemblyPreload = KnownEnvironmentVariables::AssemblyPreloadDefault;
			else if (v[0] == '1')
				knownEnvVars.EnableAssemblyPreload = true;
			else
				knownEnvVars.EnableAssemblyPreload = false;
			return;
		}
	}

	add_system_property (k, v);
}

void
AndroidSystem::setup_environment (JNIEnv *env, jobjectArray environmentVariables)
{
	jsize envvarsLength = env->GetArrayLength (environmentVariables);
	if (envvarsLength == 0)
		return;

	jstring_wrapper name (env), value (env);
	for (jsize i = 0; (i + 1) < envvarsLength; i += 2) {
		name = reinterpret_cast<jstring> (env->GetObjectArrayElement (environmentVariables, i));
		value = reinterpret_cast<jstring> (env->GetObjectArrayElement (environmentVariables, i + 1));
		setup_environment (name, value);
	}
}

void
AndroidSystem::for_each_apk (JNIEnv *env, jstring_array_wrapper &runtimeApks, void (AndroidSystem::*handler) (const char *apk, int index, int apk_count, void *user_data), void *user_data)
{
	size_t apksLength = runtimeApks.get_length ();
	for (size_t i = 0; i < apksLength; ++i) {
		jstring_wrapper &e = runtimeApks [i];

		(this->*handler) (e.get_cstr (), i, apksLength, user_data);
	}
}

void
AndroidSystem::setup_process_args_apk (const char *apk, int index, int apk_count, void *user_data)
{
	if (apk == nullptr || index != apk_count - 1)
		return;

	char *args[1] = { (char*) apk };
	monoFunctions.runtime_set_main_args (1, args);
}

void
AndroidSystem::setup_process_args (JNIEnv *env, jstring_array_wrapper &runtimeApks)
{
	for_each_apk (env, runtimeApks, &AndroidSystem::setup_process_args_apk, nullptr);
}

void
AndroidSystem::add_apk_libdir (const char *apk, int index, int apk_count, void *user_data)
{
	assert (user_data != nullptr);
	assert (index >= 0 && index < app_lib_directories_size);
	app_lib_directories [index] = monodroid_strdup_printf ("%s!/lib/%s", apk, (const char*)user_data);
}

void
AndroidSystem::setup_apk_directories (JNIEnv *env, unsigned short running_on_cpu, jstring_array_wrapper &runtimeApks)
{
	// Man, the cast is ugly...
	for_each_apk (env, runtimeApks, &AndroidSystem::add_apk_libdir, const_cast <void*> (static_cast<const void*> (android_abi_names [running_on_cpu])));
}

int AndroidSystem::readdir (monodroid_dir_t *dir, monodroid_dirent_t *b, monodroid_dirent_t **e)
{
	return readdir_r (dir, b, e);
}

#if defined (WINDOWS)
int
AndroidSystem::readdir_r (_WDIR *dirp, struct _wdirent *entry, struct _wdirent **result)
{
	int error_code = 0;

	std::lock_guard<std::mutex> lock (readdir_mutex);
	errno = 0;
	entry = _wreaddir (dirp);
	*result = entry;

	if (entry == nullptr && errno != 0)
		error_code = -1;

	return error_code;
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
#endif
