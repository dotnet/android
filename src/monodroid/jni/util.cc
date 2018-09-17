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

int
Util::ends_with (const char *str, const char *end)
{
	char *p;

	p = const_cast<char*> (strstr (str, end));

	return p != NULL && p [strlen (end)] == 0;
}

char*
Util::path_combine(const char *path1, const char *path2)
{
	// Don't let erroneous NULL parameters situation propagate
	assert (path1 != NULL || path2 != NULL);

	if (path1 == NULL)
		return strdup (path2);
	if (path2 == NULL)
		return strdup (path1);
	return monodroid_strdup_printf ("%s" MONODROID_PATH_SEPARATOR "%s", path1, path2);
}

void
Util::add_to_vector (char ***vector, int size, char *token)
{
	*vector = *vector == NULL ? 
		(char **)xmalloc(2 * sizeof(*vector)) :
		(char **)xrealloc(*vector, (size + 1) * sizeof(*vector));
		
	(*vector)[size - 1] = token;
}

void
Util::monodroid_strfreev (char **str_array)
{
	char **orig = str_array;
	if (str_array == NULL)
		return;
	while (*str_array != NULL){
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
		vector = NULL;
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
	
	if (vector == NULL) {
		vector = (char **) xmalloc (2 * sizeof (vector));
		vector [0] = NULL;
	} else if (size > 0) {
		vector[size - 1] = NULL;
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

void
Util::monodroid_store_package_name (const char *name)
{
	const char *ch;
	int hash;

	memset (package_property_suffix, 0, sizeof (package_property_suffix));
	if (!name || strlen (name) == 0)
		return;

	/* Android properties can be at most 32 bytes long (!) and so we mustn't append the package name
	 * as-is since it will most likely generate conflicts (packages tend to be named
	 * com.mycompany.app), so we simply generate a hash code and use that instead. We treat the name
	 * as a stream of bytes assumming it's an ASCII string using a simplified version of the hash
	 * algorithm used by BCL's String.GetHashCode ()
	 */
	ch = name;
	hash = 0;
	while (*ch)
		hash = (hash << 5) - (hash + *ch++);
	snprintf (package_property_suffix, sizeof (package_property_suffix), "%08x", hash);
	log_info (LOG_DEFAULT, "Generated hash 0x%s for package name %s", package_property_suffix, name);
}

int
Util::monodroid_get_namespaced_system_property (const char *name, char **value)
{
	char *local_value = NULL;
	int result = -1;

	if (value)
		*value = NULL;

	if (strlen (package_property_suffix) > 0) {
		log_info (LOG_DEFAULT, "Trying to get property %s.%s", name, package_property_suffix);
		char *propname = monodroid_strdup_printf ("%s.%s", name, package_property_suffix);
		if (propname) {
			result = monodroid_get_system_property (propname, &local_value);
			free (propname);
		}
	}

	if (result <= 0 || !local_value)
		result = monodroid_get_system_property (name, &local_value);

	if (result > 0) {
		if (strlen (local_value) == 0) {
			free (local_value);
			return 0;
		}

		log_info (LOG_DEFAULT, "Property '%s' has value '%s'.", name, local_value);

		if (value)
			*value = local_value;
		else
			free (local_value);
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
		assm  = monoFunctions.assembly_load_full (aname, NULL, &status, 0);
		monoFunctions.domain_set (current, FALSE);
	} else {
		assm  = monoFunctions.assembly_load_full (aname, NULL, &status, 0);
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
	MonoString *mono_shadow_copy_dirs = shadow_directories == NULL ? NULL : monoFunctions.string_new (parent_domain, shadow_directories);

	monodroid_property_set (parent_domain, shadow_copy_prop, setup, reinterpret_cast<void**> (&mono_shadow_copy), NULL);
	if (mono_shadow_copy_dirs != NULL)
		monodroid_property_set (parent_domain, shadow_copy_dirs_prop, setup, reinterpret_cast<void**> (&mono_shadow_copy_dirs), NULL);

	void *args[3] = { mono_friendly_name, NULL, setup };
	MonoObject *appdomain = monodroid_runtime_invoke (parent_domain, create_domain, NULL, args, NULL);
	if (appdomain == NULL)
		return NULL;

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
	MonoAssembly *assm = NULL;
	MonoImage *image = NULL;
	MonoClass *result = NULL;
	MonoAssemblyName *aname = monoFunctions.assembly_name_new (assembly);
	MonoDomain *current = monoFunctions.domain_get ();

	if (domain != current)
		monoFunctions.domain_set (domain, FALSE);

	assm = monoFunctions.assembly_loaded (aname);
	if (assm != NULL) {
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
	MonoClass *result = NULL;
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
