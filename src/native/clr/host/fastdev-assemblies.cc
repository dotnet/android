#include <sys/types.h>
#include <dirent.h>
#include <fcntl.h>
#include <unistd.h>

#include <cerrno>
#include <cstring>
#include <limits>

#include <constants.hh>
#include <host/fastdev-assemblies.hh>
#include <runtime-base/android-system.hh>
#include <runtime-base/util.hh>

using namespace xamarin::android;

auto FastDevAssemblies::open_assembly (std::string_view const& name, int64_t &size) noexcept -> void*
{
	size = 0;

	std::string const& override_dir_path = AndroidSystem::get_primary_override_dir ();
	if (!Util::dir_exists (override_dir_path)) [[unlikely]] {
		log_debug (LOG_ASSEMBLY, "Override directory '{}' does not exist", override_dir_path);
		return nullptr;
	}

	if (override_dir_fd == -1) [[unlikely]] {
		std::lock_guard dir_lock { override_dir_lock };
		if (override_dir_fd == -1) [[likely]] {
			override_dir = opendir (override_dir_path.c_str ());
			if (override_dir == nullptr) [[unlikely]] {
				log_warn (LOG_ASSEMBLY, "Failed to open override dir '{}'. {}", override_dir_path, strerror (errno));
				return nullptr;
			}
			override_dir_fd = dirfd (override_dir);
		}
	}

	log_debug (
		LOG_ASSEMBLY,
		"Attempting to load FastDev assembly '{}' from override directory '{}'",
		name,
		override_dir_path
	);

	if (!Util::file_exists (override_dir_fd, name)) {
		log_warn (
			LOG_ASSEMBLY,
			"FastDev assembly '{}' not found.",
			name
		);
		return nullptr;
	}

	log_debug (
		LOG_ASSEMBLY,
		"Found FastDev assembly '{}'",
		name
	);

	auto file_size = Util::get_file_size_at (override_dir_fd, name);
	if (!file_size) [[unlikely]] {
		log_warn (
			LOG_ASSEMBLY,
			"Unable to determine FastDev assembly '{}' file size",
			name
		);
		return nullptr;
	}

	constexpr size_t MAX_SIZE = std::numeric_limits<std::remove_reference_t<decltype(size)>>::max ();
	if (file_size.value () > MAX_SIZE) [[unlikely]] {
		Helpers::abort_application (
			LOG_ASSEMBLY,
			std::format (
				"FastDev assembly '{}' size exceeds the maximum supported value of {}",
				name,
				MAX_SIZE
			)
		);
	}

	size = static_cast<int64_t>(file_size.value ());
	int asm_fd = openat (override_dir_fd, name.data (), O_RDONLY);
	if (asm_fd < 0) {
		log_warn (
			LOG_ASSEMBLY,
			"Failed to open FastDev assembly '{}' for reading. {}",
			name,
			strerror (errno)
		);

		size = 0;
		return nullptr;
	}

	// TODO: consider who owns the pointer - we allocate the data, but we have no way of knowing when
	//       the allocated space is no longer (if ever) needed by CoreCLR. Probably would be best if
	//       CoreCLR notified us when it wants to free the data, as that eliminates any races as well
	//       as ambiguity.
	auto buffer = new uint8_t[file_size.value ()];
	ssize_t nread = 0;
	do {
		nread = read (asm_fd, reinterpret_cast<void*>(buffer), file_size.value ());
	} while (nread == -1 && errno == EINTR);
	close (asm_fd);

	if (nread != size) [[unlikely]] {
		delete[] buffer;

		log_warn (
			LOG_ASSEMBLY,
			"Failed to read FastDev assembly '{}' data. {}",
			name,
			strerror (errno)
		);

		size = 0;
		return nullptr;
	}

	log_debug (
		LOG_ASSEMBLY,
		"Read {} bytes of FastDev assembly '{}'",
		nread,
		name
	);

	return reinterpret_cast<void*>(buffer);
}
