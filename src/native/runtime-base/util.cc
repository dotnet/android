#include <cerrno>
#include <cstdarg>
#include <cstdio>
#include <cstdlib>
#include <cstring>

#include <sys/socket.h>
#include <sys/stat.h>
#include <sys/types.h>

#include <mono/metadata/appdomain.h>
#include <mono/metadata/assembly.h>
#include <mono/metadata/class.h>

#include "util.hh"

using namespace xamarin::android;

void Util::initialize () noexcept
{
	page_size = getpagesize ();
}

int
Util::send_uninterrupted (int fd, void *buf, size_t len)
{
	ssize_t res;

	do {
		res = send (fd, buf, len, 0);
	} while (res == -1 && errno == EINTR);

	return static_cast<size_t>(res) == len;
}

ssize_t
Util::recv_uninterrupted (int fd, void *buf, size_t len)
{
	using nbytes_type = size_t;

	ssize_t res;
	size_t total = 0;
	int flags = 0;
	nbytes_type nbytes;

	do {
		nbytes = static_cast<nbytes_type>(len - total);
		res = recv (fd, (char *) buf + total, nbytes, flags);

		if (res > 0)
			total += static_cast<size_t>(res);
	} while ((res > 0 && total < len) || (res == -1 && errno == EINTR));

	return static_cast<ssize_t>(total);
}

MonoAssembly*
Util::monodroid_load_assembly (MonoAssemblyLoadContextGCHandle alc_handle, const char *basename)
{
	MonoImageOpenStatus  status;
	MonoAssemblyName    *aname = mono_assembly_name_new (basename);
	MonoAssembly        *assm = mono_assembly_load_full_alc (alc_handle, aname, nullptr, &status);

	mono_assembly_name_free (aname);

	if (assm == nullptr || status != MonoImageOpenStatus::MONO_IMAGE_OK) {
		log_fatal (LOG_DEFAULT, "Unable to find assembly '%s'.", basename);
		Helpers::abort_application ();
	}
	return assm;
}

MonoAssembly *
Util::monodroid_load_assembly (MonoDomain *domain, const char *basename)
{
	MonoAssembly         *assm;
	MonoAssemblyName     *aname;
	MonoImageOpenStatus   status;

	aname = mono_assembly_name_new (basename);
	MonoDomain *current = get_current_domain ();

	if (domain != current) {
		mono_domain_set (domain, FALSE);
		assm  = mono_assembly_load_full (aname, nullptr, &status, 0);
		mono_domain_set (current, FALSE);
	} else {
		assm  = mono_assembly_load_full (aname, nullptr, &status, 0);
	}

	mono_assembly_name_free (aname);

	if (!assm) {
		log_fatal (LOG_DEFAULT, "Unable to find assembly '%s'.", basename);
		Helpers::abort_application ();
	}
	return assm;
}

MonoClass*
Util::monodroid_get_class_from_name ([[maybe_unused]] MonoDomain *domain, const char* assembly, const char *_namespace, const char *type)
{
	MonoClass *result;
	MonoAssemblyName *aname = mono_assembly_name_new (assembly);
	MonoAssembly *assm = mono_assembly_loaded (aname);
	if (assm != nullptr) {
		MonoImage *image = mono_assembly_get_image (assm);
		result = mono_class_from_name (image, _namespace, type);
	} else
		result = nullptr;

	mono_assembly_name_free (aname);
	return result;
}

char*
Util::path_combine (const char *path1, const char *path2)
{
	// Don't let erroneous nullptr parameters situation propagate
	abort_unless (path1 != nullptr || path2 != nullptr, "At least one path must be a valid pointer");

	if (path1 == nullptr)
		return strdup_new (path2);
	if (path2 == nullptr)
		return strdup_new (path1);

	size_t len = Helpers::add_with_overflow_check<size_t> (strlen (path1), strlen (path2) + 2);
	char *ret = new char [len];
	*ret = '\0';

	strncat (ret, path1, len - 1);
	strncat (ret, "/", len - 1);
	strncat (ret, path2, len - 1);

	return ret;
}

void
Util::create_public_directory (const char *dir)
{
	mode_t m = umask (0);
	int ret = mkdir (dir, 0777);
	if (ret < 0) {
		log_warn (LOG_DEFAULT, "Failed to create directory '%s'. %s", dir, std::strerror (errno));
	}
	umask (m);
}

int
Util::create_directory (const char *pathname, mode_t mode)
{
	if (mode <= 0)
		mode = DEFAULT_DIRECTORY_MODE;

	if  (!pathname || *pathname == '\0') {
		errno = EINVAL;
		return -1;
	}
	mode_t oldumask = umask (022);
	std::unique_ptr<char> path {strdup_new (pathname)};
	int rv, ret = 0;
	for (char *d = path.get (); d != nullptr && *d; ++d) {
		if (*d != '/')
			continue;
		*d = 0;
		if (*path) {
			rv = make_directory (path.get (), mode);
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
Util::set_world_accessable ([[maybe_unused]] const char *path)
{
	int r;
	do {
		r = chmod (path, 0664);
	} while (r == -1 && errno == EINTR);

	if (r == -1) {
		log_error (LOG_DEFAULT, "chmod(\"%s\", 0664) failed: %s", path, strerror (errno));
	}
}

void
Util::set_user_executable ([[maybe_unused]] const char *path)
{
	int r;
	do {
		r = chmod (path, S_IRUSR | S_IWUSR | S_IXUSR);
	} while (r == -1 && errno == EINTR);

	if (r == -1) {
		log_error (LOG_DEFAULT, "chmod(\"%s\") failed: %s", path, strerror (errno));
	}
}

bool
Util::file_exists (const char *file)
{
	struct stat s;
	if (::stat (file, &s) == 0 && (s.st_mode & S_IFMT) == S_IFREG)
		return true;
	return false;
}

bool
Util::directory_exists (const char *directory)
{
	if (directory == nullptr) {
		return false;
	}

	struct stat s;
	if (::stat (directory, &s) == 0 && (s.st_mode & S_IFMT) == S_IFDIR)
		return true;
	return false;
}

bool
Util::file_copy (const char *to, const char *from)
{
	if (to == nullptr || *to == '\0') {
		log_error (LOG_DEFAULT, "Util::file_copy: `to` parameter must not be null or empty");
		return false;
	}

	if (from == nullptr || *from == '\0') {
		log_error (LOG_DEFAULT, "Util::file_copy: `from` parameter must not be null or empty");
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
Util::is_path_rooted (const char *path) noexcept
{
	if (path == nullptr) {
		return false;
	}

	return path [0] == '/';
}

FILE *
Util::monodroid_fopen (const char *filename, const char *mode)
{
	FILE *ret;

	/* On Unix, both path and system calls are all assumed
	 * to be UTF-8 compliant.
	 */
	ret = fopen (filename, mode);
	if (ret == nullptr) {
		log_error (LOG_DEFAULT, "fopen failed for file %s: %s", filename, strerror (errno));
		return nullptr;
	}

	return ret;
}

int
Util::monodroid_dirent_hasextension (dirent *e, const char *extension)
{
	return ends_with_slow (e->d_name, extension);
}

void
Util::monodroid_strfreev (char **str_array)
{
	char **orig = str_array;
	if (str_array == nullptr) {
		return;
	}

	while (*str_array != nullptr){
		free (*str_array);
		str_array++;
	}
	free (orig);
}

char **
Util::monodroid_strsplit (const char *str, const char *delimiter, size_t max_tokens)
{
	if (str == nullptr || *str == '\0') {
		return static_cast<char**>(xcalloc (sizeof(char*), 1));
	}

	const char *p_str = str;
	size_t tokens_in_str = 0;
	size_t delimiter_len = strlen (delimiter);

	while (*p_str != '\0') {
		size_t bytes = strspn (p_str, delimiter);
		if (bytes == 0) {
			bytes = 1;
		} else {
			tokens_in_str += bytes / delimiter_len;
		}

		p_str += bytes;
	}

	size_t vector_size = (max_tokens > 0 && tokens_in_str >= max_tokens) ? max_tokens + 1 : tokens_in_str + 2; // Includes the terminating 'nullptr` entry

	char **vector = static_cast<char**>(xmalloc (Helpers::multiply_with_overflow_check<size_t> (sizeof(char*), vector_size)));
	size_t vector_idx = 0;

	while (*str != '\0' && !(max_tokens > 0 && vector_idx + 1 >= max_tokens)) {
		const char *c = str;

		if (strncmp (str, delimiter, delimiter_len) == 0) {
			vector[vector_idx++] = strdup ("");
			str += delimiter_len;
			continue;
		}

		while (*str != '\0' && strncmp (str, delimiter, delimiter_len) != 0) {
			str++;
		}

		if (*str == '\0') {
			vector[vector_idx++] = strdup (c);
			continue;
		}

		size_t toklen = static_cast<size_t>((str - c));
		size_t alloc_size = Helpers::add_with_overflow_check<size_t> (toklen, 1);
		char *token = static_cast<char*>(xmalloc (alloc_size));
		strncpy (token, c, toklen);
		token [toklen] = '\0';
		vector[vector_idx++] = token;

		/* Need to leave a trailing empty
		 * token if the delimiter is the last
		 * part of the string
		 */
		if (strcmp (str, delimiter) != 0) {
			str += delimiter_len;
		}
	}

	if (*str != '\0') {
		if (strncmp (str, delimiter, delimiter_len) == 0) {
			vector[vector_idx++] = strdup ("");
		} else {
			vector[vector_idx++] = strdup (str);
		}
	}

	vector[vector_idx] = nullptr;
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
