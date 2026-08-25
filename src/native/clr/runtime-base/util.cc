#include <cerrno>
#include <cstring>

#include <sys/stat.h>

#include <shared/log_types.hh>
#include <runtime-base/strings.hh>
#include <runtime-base/util.hh>

using namespace xamarin::android;

int
Util::create_directory (const char *pathname, mode_t mode)
{
	if  (pathname == nullptr || *pathname == '\0') {
		errno = EINVAL;
		return -1;
	}

	if (mode <= 0) {
	 	mode = Constants::DEFAULT_DIRECTORY_MODE;
	}

	size_t path_length = strlen (pathname);
	size_t allocation_size = Helpers::add_with_overflow_check<size_t> (path_length, 1uz);
	char stack_buffer [Constants::SENSIBLE_PATH_MAX];
	char *heap_buffer = nullptr;
	char *path = stack_buffer;
	if (allocation_size > sizeof (stack_buffer)) {
		heap_buffer = static_cast<char*> (std::malloc (allocation_size));
		abort_unless (heap_buffer != nullptr, "Failed to allocate directory path");
		path = heap_buffer;
	}
	memcpy (path, pathname, path_length + 1);

	mode_t oldumask = umask (022);
	int ret = 0;

	for (char *d = path; *d != '\0'; d++) {
		if (*d != '/') {
			continue;
		}

		*d = '\0';
		int rv = *path == '\0' ? 0 : ::mkdir (path, mode);
		*d = '/';

		if (rv == -1 && errno != EEXIST) {
			ret = -1;
			break;
		}
	}

	if (ret == 0) {
		ret = ::mkdir (path, mode);
	}
	int saved_errno = errno;
	umask (oldumask);
	std::free (heap_buffer);
	errno = saved_errno;

	return ret;
}

void
Util::create_public_directory (const char *dir)
{
	mode_t m = umask (0);
	int ret = create_directory (dir, 0777);
	if (ret < 0) {
		if (errno == EEXIST) {
			// Try to change the mode, just in case
			chmod (dir, 0777);
		} else {
			log_warnf (LOG_DEFAULT, "Failed to create directory '%s'. %s", dir, std::strerror (errno));
		}
	}
	umask (m);
}

auto
Util::monodroid_fopen (const char *filename, const char *mode) noexcept -> FILE*
{
	/* On Unix, both path and system calls are all assumed
	 * to be UTF-8 compliant.
	 */
	FILE *ret = fopen (filename, mode);
	if (ret == nullptr) {
		log_errorf (LOG_DEFAULT, "fopen failed for file %s: %s", filename, strerror (errno));
		return nullptr;
	}

	return ret;
}

void Util::set_world_accessable (const char *path)
{
	int r;
	do {
		r = chmod (path, 0664);
	} while (r == -1 && errno == EINTR);

	if (r == -1) {
		log_errorf (LOG_DEFAULT, "chmod(\"%s\", 0664) failed: %s", path, strerror (errno));
	}
}

auto Util::set_world_accessible (int fd) noexcept -> bool
{
	int r;
	do {
		r = fchmod (fd, 0664);
	} while (r == -1 && errno == EINTR);

	if (r == -1) {
		log_errorf (LOG_DEFAULT, "fchmod() failed: %s", strerror (errno));
		return false;
	}

	return true;
}
