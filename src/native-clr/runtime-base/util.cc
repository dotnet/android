#include <cerrno>
#include <cstring>

#include <sys/stat.h>

#include <shared/log_types.hh>
#include <runtime-base/util.hh>

using namespace xamarin::android;

void Util::create_public_directory (std::string_view const& dir)
{
	mode_t m = umask (0);
	int ret = mkdir (dir.data (), 0777);
	if (ret < 0) {
		log_warn (LOG_DEFAULT, "Failed to create directory '{}'. {}", dir, std::strerror (errno));
	}
	umask (m);
}

auto Util::monodroid_fopen (std::string_view const& filename, std::string_view const& mode) noexcept -> FILE*
{
	/* On Unix, both path and system calls are all assumed
	 * to be UTF-8 compliant.
	 */
	FILE *ret = fopen (filename.data (), mode.data ());
	if (ret == nullptr) {
		log_error (LOG_DEFAULT, "fopen failed for file {}: {}", filename, strerror (errno));
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
		log_error (LOG_DEFAULT, "chmod(\"{}\", 0664) failed: {}", path, strerror (errno));
	}
}
