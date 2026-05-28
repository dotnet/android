#include <sys/types.h>
#include <dirent.h>
#include <fcntl.h>
#include <unistd.h>

#include <cerrno>
#include <cstring>
#include <format>
#include <limits>

#include <constants.hh>
#include <host/fastdev-assemblies.hh>
#include <runtime-base/android-system.hh>
#include <runtime-base/util.hh>

using namespace xamarin::android;

auto FastDevAssemblies::open_assembly (std::string_view const& name, int64_t &size) noexcept -> void*
{
	size = 0;

	const char *override_dir_path = AndroidSystem::get_primary_override_dir ();
	if (!Util::dir_exists (override_dir_path)) [[unlikely]] {
		log_debug (LOG_ASSEMBLY, "Override directory '%s' does not exist", optional_string (override_dir_path));
		return nullptr;
	}

	// NOTE: override_dir will be kept open, we have no way of knowing when it will be no longer
	//       needed
	if (override_dir_fd < 0) [[unlikely]] {
		std::lock_guard dir_lock { override_dir_lock };
		if (override_dir_fd < 0) [[likely]] {
			override_dir = opendir (override_dir_path);
			if (override_dir == nullptr) [[unlikely]] {
				log_warn (LOG_ASSEMBLY, "Failed to open override dir '%s'. %s", optional_string (override_dir_path), strerror (errno));
				return nullptr;
			}
			override_dir_fd = dirfd (override_dir);
		}
	}

	log_debug (
		LOG_ASSEMBLY,
		"Attempting to load FastDev assembly '%.*s' from override directory '%s'",
		static_cast<int>(name.length ()),
		name.data (),
		optional_string (override_dir_path)
	);

	if (!Util::file_exists (override_dir_fd, name)) {
		log_warn (LOG_ASSEMBLY, "FastDev assembly '%.*s' not found.", static_cast<int>(name.length ()), name.data ());
		return nullptr;
	}
	log_debug (LOG_ASSEMBLY, "Found FastDev assembly '%.*s'", static_cast<int>(name.length ()), name.data ());

	auto file_size = Util::get_file_size_at (override_dir_fd, name);
	if (!file_size) [[unlikely]] {
		log_warn (LOG_ASSEMBLY, "Unable to determine FastDev assembly '%.*s' file size", static_cast<int>(name.length ()), name.data ());
		return nullptr;
	}

	constexpr size_t MAX_SIZE = std::numeric_limits<std::remove_reference_t<decltype(size)>>::max ();
	if (file_size.value () > MAX_SIZE) [[unlikely]] {
		Helpers::abort_application (
			LOG_ASSEMBLY,
			std::format (
				"FastDev assembly '{}' size exceeds the maximum supported value of {}"sv,
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
			"Failed to open FastDev assembly '%.*s' for reading. %s",
			static_cast<int>(name.length ()),
			name.data (),
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
			"Failed to read FastDev assembly '%.*s' data. %s",
			static_cast<int>(name.length ()),
			name.data (),
			strerror (errno)
		);

		size = 0;
		return nullptr;
	}
	log_debug (LOG_ASSEMBLY, "Read %zd bytes of FastDev assembly '%.*s'", nread, static_cast<int>(name.length ()), name.data ());

	return reinterpret_cast<void*>(buffer);
}
