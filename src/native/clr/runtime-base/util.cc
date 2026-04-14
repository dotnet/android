#include <cerrno>
#include <cstring>

#include <sys/stat.h>

#include <shared/log_types.hh>
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

	mode_t oldumask = umask (022);
	dynamic_local_string<Constants::SENSIBLE_PATH_MAX> path { pathname };
	int rv, ret = 0;

	for (char *d = path.get (); d != nullptr && *d != '\0'; d++) {
		if (*d != '/') {
			continue;
		}

		*d = '\0';
		if (*path.get () != '\0') {
			rv = ::mkdir (path.get (), mode);
			if (rv == -1 && errno != EEXIST) {
				ret = -1;
				break;
			}
		}
		*d = '/';
	}

	if (ret == 0) {
		ret = ::mkdir (pathname, mode);
	}
	umask (oldumask);

	return ret;
}

void
Util::create_public_directory (std::string_view const& dir)
{
	mode_t m = umask (0);
	int ret = create_directory (dir.data (), 0777);
	if (ret < 0) {
		if (errno == EEXIST) {
			// Try to change the mode, just in case
			chmod (dir.data (), 0777);
		} else {
			log_warn (
				LOG_DEFAULT,
#if defined(XA_HOST_NATIVEAOT)
				"Failed to create directory '%s'. %s",
				dir.data (),
#else
				"Failed to create directory '{}'. {}"sv,
				dir,
#endif
				std::strerror (errno)
			);
		}
	}
	umask (m);
}

auto
Util::monodroid_fopen (std::string_view const& filename, std::string_view const& mode) noexcept -> FILE*
{
	/* On Unix, both path and system calls are all assumed
	 * to be UTF-8 compliant.
	 */
	FILE *ret = fopen (filename.data (), mode.data ());
	if (ret == nullptr) {
		log_error (
			LOG_DEFAULT,
#if defined(XA_HOST_NATIVEAOT)
			"fopen failed for file %s: %s",
#else
			"fopen failed for file {}: {}"sv,
#endif
			filename,
			strerror (errno)
		);
		return nullptr;
	}

	return ret;
}

void Util::set_world_accessable (std::string_view const& path)
{
	int r;
	do {
		r = chmod (path.data (), 0664);
	} while (r == -1 && errno == EINTR);

	if (r == -1) {
		log_error (
			LOG_DEFAULT,
#if defined(XA_HOST_NATIVEAOT)
			"chmod(\"%s\", 0664) failed: %s",
#else
			"chmod(\"{}\", 0664) failed: {}"sv,
#endif
			path,
			strerror (errno)
		);
	}
}

auto Util::set_world_accessible (int fd) noexcept -> bool
{
	int r;
	do {
		r = fchmod (fd, 0664);
	} while (r == -1 && errno == EINTR);

	if (r == -1) {
		log_error (
			LOG_DEFAULT,
#if defined(XA_HOST_NATIVEAOT)
			"fchmod() failed: %s",
#else
			"fchmod() failed: {}"sv,
#endif
			strerror (errno)
		);
		return false;
	}

	return true;
}
