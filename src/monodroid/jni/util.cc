#include <assert.h>
#include <stdlib.h>
#include <stdarg.h>
#include <stdio.h>
#include <errno.h>
#ifndef WINDOWS
#include <sys/socket.h>
#else
#include <winsock2.h>
#include <shlwapi.h>
#endif
#include <sys/stat.h>
#include <sys/types.h>
#include <string.h>

#ifdef WINDOWS
#include <direct.h>
#endif

extern "C" {
#include "java-interop-util.h"
}

#include "monodroid.h"
#include "util.h"
#include "globals.h"
#include "monodroid-glue.h"

using namespace xamarin::android;

#if defined (ANDROID) || defined (LINUX)
using timestruct = timespec;
#else
using timestruct = timeval;
#endif

static const char hex_chars [] = { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'a', 'b', 'c', 'd', 'e', 'f' };

void timing_point::mark ()
{
	int ret;
	uint64_t tail;
	timestruct tv_ctm;

#if defined (ANDROID) || defined (LINUX)
	ret = clock_gettime (CLOCK_MONOTONIC, &tv_ctm);
	tail = tv_ctm.tv_nsec;
#else
	ret = gettimeofday (&tv_ctm, static_cast<timestruct*> (nullptr));
	tail = tv_ctm.tv_usec * 1000LL;
#endif
	if (ret != 0) {
		sec = 0ULL;
		ns = 0ULL;
		return;
	}

	sec = tv_ctm.tv_sec;
	ns = tail;
}

timing_diff::timing_diff (const timing_period &period)
{
	uint64_t nsec;
	if (period.end.ns < period.start.ns) {
		sec = period.end.sec - period.start.sec - 1;
		if (sec < 0)
			sec = 0;
		nsec = 1000000000ULL + period.end.ns - period.start.ns;
	} else {
		sec = period.end.sec - period.start.sec;
		nsec = period.end.ns - period.start.ns;
	}

	ms = nsec / ms_in_nsec;
	if (ms >= 1000) {
		sec += ms / 1000;
		ms = ms % 1000;
	}

	ns = nsec % ms_in_nsec;
}

int
Util::ends_with (const char *str, const char *end)
{
	char *p;

	p = const_cast<char*> (strstr (str, end));

	return p != nullptr && p [strlen (end)] == 0;
}

char*
Util::path_combine (const char *path1, const char *path2)
{
	// Don't let erroneous nullptr parameters situation propagate
	assert (path1 != nullptr || path2 != nullptr);

	if (path1 == nullptr)
		return strdup_new (path2);
	if (path2 == nullptr)
		return strdup_new (path1);

	size_t len = strlen (path1) + strlen (path2) + 2;
	char *ret = new char [len];
	*ret = '\0';

	strcat (ret, path1);
	strcat (ret, MONODROID_PATH_SEPARATOR);
	strcat (ret, path2);

	return ret;
}

void
Util::add_to_vector (char ***vector, int size, char *token)
{
	*vector = *vector == nullptr ? 
		(char **)xmalloc(2 * sizeof(*vector)) :
		(char **)xrealloc(*vector, (size + 1) * sizeof(*vector));
		
	(*vector)[size - 1] = token;
}

void
Util::monodroid_strfreev (char **str_array)
{
	char **orig = str_array;
	if (str_array == nullptr)
		return;
	while (*str_array != nullptr){
		free (*str_array);
		str_array++;
	}
	free (orig);
}

char ** 
Util::monodroid_strsplit (const char *str, const char *delimiter, int max_tokens)
{
	const char *c;
	char *token, **vector;
	int size = 1;
	
	if (strncmp (str, delimiter, strlen (delimiter)) == 0) {
		vector = (char **)xmalloc (2 * sizeof(vector));
		vector[0] = strdup ("");
		size++;
		str += strlen (delimiter);
	} else {
		vector = nullptr;
	}

	while (*str && !(max_tokens > 0 && size >= max_tokens)) {
		c = str;
		if (strncmp (str, delimiter, strlen (delimiter)) == 0) {
			token = strdup ("");
			str += strlen (delimiter);
		} else {
			while (*str && strncmp (str, delimiter, strlen (delimiter)) != 0) {
				str++;
			}

			if (*str) {
				int toklen = (str - c);
				token = new char [toklen + 1];
				strncpy (token, c, toklen);
				token [toklen] = '\0';

				/* Need to leave a trailing empty
				 * token if the delimiter is the last
				 * part of the string
				 */
				if (strcmp (str, delimiter) != 0) {
					str += strlen (delimiter);
				}
			} else {
				token = strdup (c);
			}
		}
			
		add_to_vector (&vector, size, token);
		size++;
	}

	if (*str) {
		if (strcmp (str, delimiter) == 0)
			add_to_vector (&vector, size, strdup (""));
		else {
			/* Add the rest of the string as the last element */
			add_to_vector (&vector, size, strdup (str));
		}
		size++;
	}
	
	if (vector == nullptr) {
		vector = (char **) xmalloc (2 * sizeof (vector));
		vector [0] = nullptr;
	} else if (size > 0) {
		vector[size - 1] = nullptr;
	}
	
	return vector;
}

char *
Util::monodroid_strdup_printf (const char *format, ...)
{
        va_list args;

        va_start (args, format);
        char *ret = monodroid_strdup_vprintf (format, args);
        va_end (args);

        return ret;
}

char*
Util::monodroid_strdup_vprintf (const char *format, va_list vargs)
{
	char *ret = nullptr;
	int n = vasprintf (&ret, format, vargs);

	return n == -1 ? nullptr : ret;
}

int
Util::send_uninterrupted (int fd, void *buf, int len)
{
	int res;
#ifdef WINDOWS
	const char *buffer = static_cast<const char*> (buf);
#else
	void *buffer = buf;
#endif
	do {
		res = send (fd, buffer, len, 0);
	} while (res == -1 && errno == EINTR);

	return res == len;
}

int
Util::recv_uninterrupted (int fd, void *buf, int len)
{
	int res;
	int total = 0;
	int flags = 0;

	do { 
		res = recv (fd, (char *) buf + total, len - total, flags); 
		if (res > 0)
			total += res;
	} while ((res > 0 && total < len) || (res == -1 && errno == EINTR));

	return total;
}

#if WINDOWS
//
// This version should be removed once MXE we have on mac can build the glorious version in the
// #else below.
//
// Currently mxe fails with:
//
//  Cannot export _ZN7xamarin7android4Util19package_hash_to_hexIiIEEEvjT_DpT0_: symbol wrong type (4 vs 3)
//   Cannot export _ZN7xamarin7android4Util19package_hash_to_hexIiIiEEEvjT_DpT0_: symbol wrong type (4 vs 3)
//   Cannot export _ZN7xamarin7android4Util19package_hash_to_hexIiIiiEEEvjT_DpT0_: symbol wrong type (4 vs 3)
//   Cannot export _ZN7xamarin7android4Util19package_hash_to_hexIiIiiiEEEvjT_DpT0_: symbol wrong type (4 vs 3)
//   Cannot export _ZN7xamarin7android4Util19package_hash_to_hexIiIiiiiEEEvjT_DpT0_: symbol wrong type (4 vs 3)
//   Cannot export _ZN7xamarin7android4Util19package_hash_to_hexIiIiiiiiEEEvjT_DpT0_: symbol wrong type (4 vs 3)
//   Cannot export _ZN7xamarin7android4Util19package_hash_to_hexIiIiiiiiiEEEvjT_DpT0_: symbol wrong type (4 vs 3)
//   Cannot export _ZN7xamarin7android4Util19package_hash_to_hexIiIiiiiiiiEEEvjT_DpT0_: symbol wrong type (4 vs 3)
// collect2 : error : ld returned 1 exit status
//   [/Users/builder/jenkins/workspace/xamarin-android-pr-builder-debug/xamarin-android/src/monodroid/monodroid.csproj]
//
void Util::package_hash_to_hex (uint32_t hash)
{
	for (uint32_t idx = 0; idx < 8; idx++) {
		package_property_suffix [idx] = hex_chars [(hash & (0xF0000000 >> idx * 4)) >> ((7 - idx) * 4)];
	}
	package_property_suffix[sizeof (package_property_suffix) / sizeof (char) - 1] = 0x00;
}
#else
template<typename IdxType>
inline void
Util::package_hash_to_hex (IdxType /* idx */)
{
	package_property_suffix[sizeof (package_property_suffix) / sizeof (char) - 1] = 0x00;
}

template<typename IdxType, typename ...Indices>
inline void
Util::package_hash_to_hex (uint32_t hash, IdxType idx, Indices... indices)
{
	package_property_suffix [idx] = hex_chars [(hash & (0xF0000000 >> idx * 4)) >> ((7 - idx) * 4)];
	package_hash_to_hex <IdxType> (hash, indices...);
}
#endif

void
Util::monodroid_store_package_name (const char *name)
{
	if (!name || *name == '\0')
		return;

	/* Android properties can be at most 32 bytes long (!) and so we mustn't append the package name
	 * as-is since it will most likely generate conflicts (packages tend to be named
	 * com.mycompany.app), so we simply generate a hash code and use that instead. We treat the name
	 * as a stream of bytes assumming it's an ASCII string using a simplified version of the hash
	 * algorithm used by BCL's String.GetHashCode ()
	 */
	const char *ch = name;
	uint32_t hash = 0;
	while (*ch)
		hash = (hash << 5) - (hash + *ch++);

#if WINDOWS
	package_hash_to_hex (hash);
#else
	// In C++14 or newer we could use std::index_sequence, but in C++11 it's a bit too much ado
	// for this simple case, so a manual sequence it is.
	//
	// And yes, I know it could be done in a simple loop or in even simpler 8 lines of code, but
	// that would be boring, wouldn't it? :)
	package_hash_to_hex (hash, 0, 1, 2, 3, 4, 5, 6, 7);
#endif
	log_info (LOG_DEFAULT, "Generated hash 0x%s for package name %s", package_property_suffix, name);
}

int
Util::monodroid_get_namespaced_system_property (const char *name, char **value)
{
	char *local_value = nullptr;
	int result = -1;

	if (value)
		*value = nullptr;

	if (strlen (package_property_suffix) > 0) {
		log_info (LOG_DEFAULT, "Trying to get property %s.%s", name, package_property_suffix);
		char *propname;
#if WINDOWS
		propname = monodroid_strdup_printf ("%s.%s", name, package_property_suffix);
#else
		propname = string_concat (name, ".", package_property_suffix);
#endif
		result = androidSystem.monodroid_get_system_property (propname, &local_value);
#if WINDOWS
		free (propname);
#else
		delete[] propname;
#endif
	}

	if (result <= 0 || !local_value)
		result = androidSystem.monodroid_get_system_property (name, &local_value);

	if (result > 0) {
		if (strlen (local_value) == 0) {
			delete[] local_value;
			return 0;
		}

		log_info (LOG_DEFAULT, "Property '%s' has value '%s'.", name, local_value);

		if (value)
			*value = local_value;
		else
			delete[] local_value;
		return result;
	}

	return androidSystem.monodroid_get_system_property_from_overrides (name, value);
}

MonoAssembly *
Util::monodroid_load_assembly (MonoDomain *domain, const char *basename)
{
	MonoAssembly         *assm;
	MonoAssemblyName     *aname;
	MonoImageOpenStatus   status;

	aname = monoFunctions.assembly_name_new (basename);
	MonoDomain *current = monoFunctions.domain_get ();

	if (domain != current) {
		monoFunctions.domain_set (domain, FALSE);
		assm  = monoFunctions.assembly_load_full (aname, nullptr, &status, 0);
		monoFunctions.domain_set (current, FALSE);
	} else {
		assm  = monoFunctions.assembly_load_full (aname, nullptr, &status, 0);
	}

	monoFunctions.assembly_name_free (aname);

	if (!assm) {
		log_fatal (LOG_DEFAULT, "Unable to find assembly '%s'.", basename);
		exit (FATAL_EXIT_MISSING_ASSEMBLY);
	}
	return assm;
}

MonoObject *
Util::monodroid_runtime_invoke (MonoDomain *domain, MonoMethod *method, void *obj, void **params, MonoObject **exc)
{
	MonoDomain *current = monoFunctions.domain_get ();
	if (domain != current) {
		monoFunctions.domain_set (domain, FALSE);
		MonoObject *r = monoFunctions.runtime_invoke (method, obj, params, exc);
		monoFunctions.domain_set (current, FALSE);
		return r;
	} else {
		return monoFunctions.runtime_invoke (method, obj, params, exc);
	}
}

void
Util::monodroid_property_set (MonoDomain *domain, MonoProperty *property, void *obj, void **params, MonoObject **exc)
{
	MonoDomain *current = monoFunctions.domain_get ();
	if (domain != current) {
		monoFunctions.domain_set (domain, FALSE);
		monoFunctions.property_set_value (property, obj, params, exc);
		monoFunctions.domain_set (current, FALSE);
	} else {
		monoFunctions.property_set_value (property, obj, params, exc);
	}
}

MonoDomain*
Util::monodroid_create_appdomain (MonoDomain *parent_domain, const char *friendly_name, int shadow_copy, const char *shadow_directories)
{
	MonoClass *appdomain_setup_klass = monodroid_get_class_from_name (parent_domain, "mscorlib", "System", "AppDomainSetup");
	MonoClass *appdomain_klass = monodroid_get_class_from_name (parent_domain, "mscorlib", "System", "AppDomain");
	MonoMethod *create_domain = monoFunctions.class_get_method_from_name (appdomain_klass, "CreateDomain", 3);
	MonoProperty *shadow_copy_prop = monoFunctions.class_get_property_from_name (appdomain_setup_klass, "ShadowCopyFiles");
	MonoProperty *shadow_copy_dirs_prop = monoFunctions.class_get_property_from_name (appdomain_setup_klass, "ShadowCopyDirectories");

	MonoObject *setup = monoFunctions.object_new (parent_domain, appdomain_setup_klass);
	MonoString *mono_friendly_name = monoFunctions.string_new (parent_domain, friendly_name);
	MonoString *mono_shadow_copy = monoFunctions.string_new (parent_domain, shadow_copy ? "true" : "false");
	MonoString *mono_shadow_copy_dirs = shadow_directories == nullptr ? nullptr : monoFunctions.string_new (parent_domain, shadow_directories);

	monodroid_property_set (parent_domain, shadow_copy_prop, setup, reinterpret_cast<void**> (&mono_shadow_copy), nullptr);
	if (mono_shadow_copy_dirs != nullptr)
		monodroid_property_set (parent_domain, shadow_copy_dirs_prop, setup, reinterpret_cast<void**> (&mono_shadow_copy_dirs), nullptr);

	void *args[3] = { mono_friendly_name, nullptr, setup };
	MonoObject *appdomain = monodroid_runtime_invoke (parent_domain, create_domain, nullptr, args, nullptr);
	if (appdomain == nullptr)
		return nullptr;

	return monoFunctions.domain_from_appdomain (appdomain);
}

int
Util::create_directory (const char *pathname, int mode)
{
	if (mode <= 0)
		mode = DEFAULT_DIRECTORY_MODE;

	if  (!pathname || *pathname == '\0') {
		errno = EINVAL;
		return -1;
	}

	mode_t oldumask = umask (022);
	char *path = strdup (pathname);
	int rv, ret = 0;
	for (char *d = path; *d; ++d) {
		if (*d != '/')
			continue;
		*d = 0;
		if (*path) {
			rv = make_directory (path, mode);
			if  (rv == -1 && errno != EEXIST)  {
				ret = -1;
				break;
			}
		}
		*d = '/';
	}
	free (path);
	if (ret == 0)
		ret = make_directory (pathname, mode);
	umask (oldumask);

	return ret;
}

void
Util::set_world_accessable (const char *path)
{
#ifdef ANDROID
	int r;
	do
		r = chmod (path, 0664);
	while (r == -1 && errno == EINTR);

	if (r == -1)
		log_error (LOG_DEFAULT, "chmod(\"%s\", 0664) failed: %s", path, strerror (errno));
#endif
}

void
Util::set_user_executable (const char *path)
{
#ifdef ANDROID
	int r;
	do {
		r = chmod (path, S_IRUSR | S_IWUSR | S_IXUSR);
	} while (r == -1 && errno == EINTR);

	if (r == -1)
		log_error (LOG_DEFAULT, "chmod(\"%s\") failed: %s", path, strerror (errno));
#endif
}

MonoClass*
Util::monodroid_get_class_from_name (MonoDomain *domain, const char* assembly, const char *_namespace, const char *type)
{
	MonoAssembly *assm = nullptr;
	MonoImage *image = nullptr;
	MonoClass *result = nullptr;
	MonoAssemblyName *aname = monoFunctions.assembly_name_new (assembly);
	MonoDomain *current = monoFunctions.domain_get ();

	if (domain != current)
		monoFunctions.domain_set (domain, FALSE);

	assm = monoFunctions.assembly_loaded (aname);
	if (assm != nullptr) {
		image = monoFunctions.assembly_get_image (assm);
		result = monoFunctions.class_from_name (image, _namespace, type);
	}

	if (domain != current)
		monoFunctions.domain_set (current, FALSE);

	monoFunctions.assembly_name_free (aname);

	return result;
}

MonoClass*
Util::monodroid_get_class_from_image (MonoDomain *domain, MonoImage *image, const char *_namespace, const char *type)
{
	MonoClass *result = nullptr;
	MonoDomain *current = monoFunctions.domain_get ();

	if (domain != current)
		monoFunctions.domain_set (domain, FALSE);

	result = monoFunctions.class_from_name (image, _namespace, type);

	if (domain != current)
		monoFunctions.domain_set (current, FALSE);

	return result;
}

void
Util::create_public_directory (const char *dir)
{
#ifndef WINDOWS
	mode_t m = umask (0);
	mkdir (dir, 0777);
	umask (m);
#else
	wchar_t *buffer = utf8_to_utf16 (dir);
	_wmkdir (buffer);
	free (buffer);
#endif
}

FILE *
Util::monodroid_fopen (const char *filename, const char *mode)
{
#ifndef WINDOWS
	/* On Unix, both path and system calls are all assumed
	 * to be UTF-8 compliant.
	 */
	return fopen (filename, mode);
#else
	// Convert the path and mode to a UTF-16 and then use the wide variant of fopen
	wchar_t *wpath = utf8_to_utf16 (filename);
	wchar_t *wmode = utf8_to_utf16 (mode);

	FILE* file = _wfopen (wpath, wmode);
	free (wpath);
	free (wmode);

	return file;
#endif // ndef WINDOWS
}

int
Util::monodroid_stat (const char *path, monodroid_stat_t *s)
{
	int result;

#ifndef WINDOWS
	result = stat (path, s);
#else
	wchar_t *wpath = utf8_to_utf16 (path);
	result = _wstat (wpath, s);
	free (wpath);
#endif

	return result;
}

monodroid_dir_t*
Util::monodroid_opendir (const char *filename)
{
#ifndef WINDOWS
	return opendir (filename);
#else
	wchar_t *wfilename = utf8_to_utf16 (filename);
	monodroid_dir_t *result = _wopendir (wfilename);
	free (wfilename);
	return result;
#endif
}

int
Util::monodroid_closedir (monodroid_dir_t *dirp)
{
#ifndef WINDOWS
	return closedir (dirp);
#else
	return _wclosedir (dirp);
#endif

}

int
Util::monodroid_dirent_hasextension (monodroid_dirent_t *e, const char *extension)
{
#ifndef WINDOWS
	return ends_with (e->d_name, extension);
#else
	char *mb_dname = utf16_to_utf8 (e->d_name);
	int result = ends_with (mb_dname, extension);
	free (mb_dname);
	return result;
#endif
}

bool
Util::file_exists (const char *file)
{
	monodroid_stat_t s;
	if (monodroid_stat (file, &s) == 0 && (s.st_mode & S_IFMT) == S_IFREG)
		return true;
	return false;
}

bool
Util::directory_exists (const char *directory)
{
	monodroid_stat_t s;
	if (monodroid_stat (directory, &s) == 0 && (s.st_mode & S_IFMT) == S_IFDIR)
		return true;
	return false;
}

bool
Util::file_copy (const char *to, const char *from)
{
	char buffer[BUFSIZ];
	size_t n;
	int saved_errno;

	FILE *f1 = utils.monodroid_fopen (from, "r");
	FILE *f2 = utils.monodroid_fopen (to, "w+");

	while ((n = fread (buffer, sizeof(char), sizeof(buffer), f1)) > 0) {
		if (fwrite (buffer, sizeof(char), n, f2) != n) {
			saved_errno = errno;
			fclose (f1);
			fclose (f2);
			errno = saved_errno;

			return false;
		}
	}

	fclose (f1);
	fclose (f2);
	return true;
}

bool
Util::is_path_rooted (const char *path)
{
	if (path == nullptr)
		return false;
#ifdef WINDOWS
	LPCWSTR wpath = utf8_to_utf16 (path);
	bool ret = !PathIsRelativeW (wpath);
	free (const_cast<void*> (reinterpret_cast<const void*> (wpath)));
	return ret;
#else
	return path [0] == MONODROID_PATH_SEPARATOR_CHAR;
#endif
}

jclass
Util::get_class_from_runtime_field (JNIEnv *env, jclass runtime, const char *name, bool make_gref)
{
	static constexpr char java_lang_class_sig[] = "Ljava/lang/Class;";

	jfieldID fieldID = env->GetStaticFieldID (runtime, name, java_lang_class_sig);
	if (fieldID == nullptr)
		return nullptr;

	jobject field = env->GetStaticObjectField (runtime, fieldID);
	if (field == nullptr)
		return nullptr;

	return reinterpret_cast<jclass> (make_gref ? osBridge.lref_to_gref (env, field) : field);
}

extern "C" void
monodroid_strfreev (char **str_array)
{
	utils.monodroid_strfreev (str_array);
}

extern "C" char**
monodroid_strsplit (const char *str, const char *delimiter, int max_tokens)
{
	return utils.monodroid_strsplit (str, delimiter, max_tokens);
}

extern "C" char*
monodroid_strdup_printf (const char *format, ...)
{
	va_list args;

	va_start (args, format);
	char *ret = utils.monodroid_strdup_vprintf (format, args);
	va_end (args);

	return ret;
}

extern "C" void
monodroid_store_package_name (const char *name)
{
	utils.monodroid_store_package_name (name);
}

extern "C" int
monodroid_get_namespaced_system_property (const char *name, char **value)
{
	return utils.monodroid_get_namespaced_system_property (name, value);
}

extern "C" FILE*
monodroid_fopen (const char* filename, const char* mode)
{
	return utils.monodroid_fopen (filename, mode);
}

extern "C" int
send_uninterrupted (int fd, void *buf, int len)
{
	return utils.send_uninterrupted (fd, buf, len);
}

extern "C" int
recv_uninterrupted (int fd, void *buf, int len)
{
	return utils.recv_uninterrupted (fd, buf, len);
}

extern "C" void
set_world_accessable (const char *path)
{
	utils.set_world_accessable (path);
}

extern "C" void
create_public_directory (const char *dir)
{
	utils.create_public_directory (dir);
}

extern "C" char*
path_combine (const char *path1, const char *path2)
{
	return utils.path_combine (path1, path2);
}
