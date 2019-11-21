#include <cerrno>
#include <stdlib.h>
#include <stdarg.h>

#ifdef WINDOWS
#include <direct.h>
#include <shlwapi.h>
#endif

#include "basic-utilities.hh"
#include "logger.hh"
#include "cpp-util.hh"

using namespace xamarin::android;

char*
BasicUtilities::path_combine (const char *path1, const char *path2)
{
	// Don't let erroneous nullptr parameters situation propagate
	assert (path1 != nullptr || path2 != nullptr);

	if (path1 == nullptr)
		return strdup_new (path2);
	if (path2 == nullptr)
		return strdup_new (path1);

	size_t len = add_with_overflow_check<size_t> (__FILE__, __LINE__, strlen (path1), strlen (path2) + 2);
	char *ret = new char [len];
	*ret = '\0';

	strcat (ret, path1);
	strcat (ret, MONODROID_PATH_SEPARATOR);
	strcat (ret, path2);

	return ret;
}

void
BasicUtilities::create_public_directory (const char *dir)
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

int
BasicUtilities::create_directory (const char *pathname, mode_t mode)
{
	if (mode <= 0)
		mode = DEFAULT_DIRECTORY_MODE;

	if  (!pathname || *pathname == '\0') {
		errno = EINVAL;
		return -1;
	}
#ifdef WINDOWS
	int oldumask;
#else
	mode_t oldumask;
#endif
	oldumask = umask (022);
	simple_pointer_guard<char[]> path (strdup_new (pathname));
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

	if (ret == 0)
		ret = make_directory (pathname, mode);
	umask (oldumask);

	return ret;
}

void
BasicUtilities::set_world_accessable (const char *path)
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
BasicUtilities::set_user_executable (const char *path)
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

bool
BasicUtilities::file_exists (const char *file)
{
	monodroid_stat_t s;
	if (monodroid_stat (file, &s) == 0 && (s.st_mode & S_IFMT) == S_IFREG)
		return true;
	return false;
}

bool
BasicUtilities::directory_exists (const char *directory)
{
	monodroid_stat_t s;
	if (monodroid_stat (directory, &s) == 0 && (s.st_mode & S_IFMT) == S_IFDIR)
		return true;
	return false;
}

bool
BasicUtilities::file_copy (const char *to, const char *from)
{
	if (to == nullptr || *to == '\0') {
		log_error (LOG_DEFAULT, "BasicUtilities::file_copy: `to` parameter must not be null or empty");
		return false;
	}

	if (from == nullptr || *from == '\0') {
		log_error (LOG_DEFAULT, "BasicUtilities::file_copy: `from` parameter must not be null or empty");
		return false;
	}

	char buffer[BUFSIZ];
	size_t n;
	int saved_errno;

	FILE *f1 = monodroid_fopen (from, "r");
	if (f1 == nullptr)
		return false;

	FILE *f2 = monodroid_fopen (to, "w+");
	if (f2 == nullptr)
		return false;

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
BasicUtilities::is_path_rooted (const char *path)
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

FILE *
BasicUtilities::monodroid_fopen (const char *filename, const char *mode)
{
	FILE *ret;
#ifndef WINDOWS
	/* On Unix, both path and system calls are all assumed
	 * to be UTF-8 compliant.
	 */
	ret = fopen (filename, mode);
#else
	// Convert the path and mode to a UTF-16 and then use the wide variant of fopen
	wchar_t *wpath = utf8_to_utf16 (filename);
	wchar_t *wmode = utf8_to_utf16 (mode);

	ret = _wfopen (wpath, wmode);
	free (wpath);
	free (wmode);
#endif // ndef WINDOWS
	if (ret == nullptr) {
		log_error (LOG_DEFAULT, "fopen failed for file %s: %s", filename, strerror (errno));
		return nullptr;
	}

	return ret;
}

int
BasicUtilities::monodroid_stat (const char *path, monodroid_stat_t *s)
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
BasicUtilities::monodroid_opendir (const char *filename)
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
BasicUtilities::monodroid_closedir (monodroid_dir_t *dirp)
{
#ifndef WINDOWS
	return closedir (dirp);
#else
	return _wclosedir (dirp);
#endif
}

int
BasicUtilities::monodroid_dirent_hasextension (monodroid_dirent_t *e, const char *extension)
{
#ifndef WINDOWS
	return ends_with_slow (e->d_name, extension);
#else
	char *mb_dname = utf16_to_utf8 (e->d_name);
	int result = ends_with_slow (mb_dname, extension);
	free (mb_dname);
	return result;
#endif
}

void
BasicUtilities::monodroid_strfreev (char **str_array)
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
BasicUtilities::monodroid_strsplit (const char *str, const char *delimiter, size_t max_tokens)
{
	const char *c;
	char *token, **vector;
	size_t size = 1;

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
				size_t toklen = static_cast<size_t>((str - c));
				size_t alloc_size = add_with_overflow_check<size_t> (__FILE__, __LINE__, toklen, 1);
				token = new char [alloc_size];
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
BasicUtilities::monodroid_strdup_printf (const char *format, ...)
{
        va_list args;

        va_start (args, format);
        char *ret = monodroid_strdup_vprintf (format, args);
        va_end (args);

        return ret;
}

char*
BasicUtilities::monodroid_strdup_vprintf (const char *format, va_list vargs)
{
	char *ret = nullptr;
	int n = vasprintf (&ret, format, vargs);

	return n == -1 ? nullptr : ret;
}

void
BasicUtilities::add_to_vector (char ***vector, size_t size, char *token)
{
	if (*vector == nullptr) {
		*vector = (char **)static_cast<char**>(xmalloc (size * sizeof(*vector)));
	} else {
		size_t alloc_size = multiply_with_overflow_check<size_t> (__FILE__, __LINE__, sizeof(*vector), size + 1);
		*vector = static_cast<char**>(xrealloc (*vector, alloc_size));
	}

	(*vector)[size - 1] = token;
}
