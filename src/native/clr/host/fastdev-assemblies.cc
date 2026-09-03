#include <sys/types.h>
#include <dirent.h>
#include <fcntl.h>
#include <unistd.h>

#include <cerrno>
#include <cstdlib>
#include <cstring>
#include <limits>

#include <constants.hh>
#include <host/fastdev-assemblies.hh>
#include <runtime-base/android-system.hh>
#include <runtime-base/util.hh>

using namespace xamarin::android;

namespace {
	// A minimal growable NUL-terminated character buffer. The TPA list has no useful upper bound -
	// it holds one absolute path per assembly in the override directory - so it grows on demand
	// rather than using a fixed buffer.
	struct char_buffer final
	{
		char   *data = nullptr;
		size_t  length = 0;
		size_t  capacity = 0;
	};

	constexpr size_t CHAR_BUFFER_INITIAL_CAPACITY = 512uz;

	// Appends `text` to `buffer`, growing it if needed. Returns `false` if the buffer could not be
	// grown, in which case `buffer` is left unchanged and still owns its storage.
	auto buffer_append (char_buffer &buffer, std::string_view text) noexcept -> bool
	{
		size_t required_capacity = Helpers::add_with_overflow_check<size_t> (buffer.length, text.length ());
		required_capacity = Helpers::add_with_overflow_check<size_t> (required_capacity, 1uz);

		if (required_capacity > buffer.capacity) {
			size_t new_capacity = buffer.capacity == 0 ? CHAR_BUFFER_INITIAL_CAPACITY : buffer.capacity;
			while (new_capacity < required_capacity) {
				new_capacity = Helpers::add_with_overflow_check<size_t> (new_capacity, new_capacity);
			}

			char *grown = static_cast<char*> (std::realloc (buffer.data, new_capacity));
			if (grown == nullptr) {
				return false;
			}

			buffer.data = grown;
			buffer.capacity = new_capacity;
		}

		memcpy (buffer.data + buffer.length, text.data (), text.length ());
		buffer.length += text.length ();
		buffer.data [buffer.length] = '\0';

		return true;
	}
}

auto FastDevAssemblies::open_assembly (std::string_view const& name, int64_t &size) noexcept -> void*
{
	size = 0;

	// When the override directory was used to build a `TRUSTED_PLATFORM_ASSEMBLIES`
	// list (see `build_tpa_list`), the external probe should yield to TPA-based
	// loading so that CoreCLR opens the assembly from disk via `PEImage::OpenImage`
	// and `Assembly.Location` ends up populated. Otherwise sibling portable PDB
	// lookup (used by `StackTraceSymbols`) returns an empty path and stack frames
	// render without file/line info.
	//
	// The CoreLib bootstrap is a special case: CoreCLR loads
	// `System.Private.CoreLib.dll` via the external probe (not through the
	// regular TPA-aware binder), so we must keep returning the bytes for it
	// even when TPA is in use. CoreLib has no user code we'd symbolicate, so
	// the resulting bare-filename `Assembly.Location` does not matter.
	constexpr std::string_view corelib_name { "System.Private.CoreLib.dll" };
	if (tpa_in_use && name != corelib_name) {
		return nullptr;
	}

	const char *override_dir_path = AndroidSystem::get_primary_override_dir ();
	if (!Util::dir_exists (override_dir_path)) [[unlikely]] {
		log_debugf (LOG_ASSEMBLY, "Override directory '%s' does not exist", override_dir_path);
		return nullptr;
	}

	// NOTE: override_dir will be kept open, we have no way of knowing when it will be no longer
	//       needed
	if (override_dir_fd < 0) [[unlikely]] {
		std::lock_guard dir_lock { override_dir_lock };
		if (override_dir_fd < 0) [[likely]] {
			override_dir = opendir (override_dir_path);
			if (override_dir == nullptr) [[unlikely]] {
				log_warnf (LOG_ASSEMBLY, "Failed to open override dir '%s'. %s", override_dir_path, strerror (errno));
				return nullptr;
			}
			override_dir_fd = dirfd (override_dir);
		}
	}

	log_debugf (
		LOG_ASSEMBLY,
		"Attempting to load FastDev assembly '%.*s' from override directory '%s'",
		static_cast<int>(name.length ()), name.data (),
		override_dir_path
	);

	if (!Util::file_exists (override_dir_fd, name)) {
		log_warnf (LOG_ASSEMBLY, "FastDev assembly '%.*s' not found.", static_cast<int>(name.length ()), name.data ());
		return nullptr;
	}
	log_debugf (LOG_ASSEMBLY, "Found FastDev assembly '%.*s'", static_cast<int>(name.length ()), name.data ());

	auto file_size = Util::get_file_size_at (override_dir_fd, name);
	if (!file_size) [[unlikely]] {
		log_warnf (LOG_ASSEMBLY, "Unable to determine FastDev assembly '%.*s' file size", static_cast<int>(name.length ()), name.data ());
		return nullptr;
	}

	constexpr size_t MAX_SIZE = std::numeric_limits<std::remove_reference_t<decltype(size)>>::max ();
	if (file_size.value () > MAX_SIZE) [[unlikely]] {
		Helpers::abort_applicationf (
			LOG_ASSEMBLY,
			std::source_location::current (),
			"FastDev assembly '%.*s' size exceeds the maximum supported value of %zu",
			static_cast<int>(name.length ()), name.data (),
			MAX_SIZE
		);
	}

	size = static_cast<int64_t>(file_size.value ());
	int asm_fd = openat (override_dir_fd, name.data (), O_RDONLY);
	if (asm_fd < 0) {
		log_warnf (
			LOG_ASSEMBLY,
			"Failed to open FastDev assembly '%.*s' for reading. %s",
			static_cast<int>(name.length ()), name.data (),
			strerror (errno)
		);

		size = 0;
		return nullptr;
	}

	// TODO: consider who owns the pointer - we allocate the data, but we have no way of knowing when
	//       the allocated space is no longer (if ever) needed by CoreCLR. Probably would be best if
	//       CoreCLR notified us when it wants to free the data, as that eliminates any races as well
	//       as ambiguity.
	auto buffer = static_cast<uint8_t*> (std::malloc (file_size.value ()));
	if (buffer == nullptr) [[unlikely]] {
		log_warnf (
			LOG_ASSEMBLY,
			"Unable to allocate memory for FastDev assembly '%.*s'",
			static_cast<int>(name.length ()), name.data ()
		);

		close (asm_fd);
		size = 0;
		return nullptr;
	}

	ssize_t nread = 0;
	do {
		nread = read (asm_fd, reinterpret_cast<void*>(buffer), file_size.value ());
	} while (nread == -1 && errno == EINTR);
	close (asm_fd);

	if (nread != size) [[unlikely]] {
		std::free (buffer);

		log_warnf (
			LOG_ASSEMBLY,
			"Failed to read FastDev assembly '%.*s' data. %s",
			static_cast<int>(name.length ()), name.data (),
			strerror (errno)
		);

		size = 0;
		return nullptr;
	}
	log_debugf (LOG_ASSEMBLY, "Read %zd bytes of FastDev assembly '%.*s'", nread, static_cast<int>(name.length ()), name.data ());

	return reinterpret_cast<void*>(buffer);
}

auto FastDevAssemblies::build_tpa_list () noexcept -> char*
{
	tpa_in_use = false;

	const char *override_dir_path = AndroidSystem::get_primary_override_dir ();
	if (!Util::dir_exists (override_dir_path)) {
		return nullptr;
	}

	DIR *dir = opendir (override_dir_path);
	if (dir == nullptr) {
		log_warnf (LOG_ASSEMBLY, "FastDev: failed to open override dir '%s'. %s", override_dir_path, std::strerror (errno));
		return nullptr;
	}

	constexpr std::string_view dll_ext { ".dll" };
	constexpr std::string_view r2r_ext { ".r2r.dll" };
	constexpr std::string_view corelib_name { "System.Private.CoreLib.dll" };
	char_buffer tpa_list {};
	bool found_corelib = false;
	bool found_r2r = false;
	bool out_of_memory = false;
	size_t count = 0;
	dirent *e;
	while ((e = readdir (dir)) != nullptr) {
		std::string_view name { e->d_name };
		if (name.size () <= dll_ext.size () || !name.ends_with (dll_ext)) {
			continue;
		}
		if (name.ends_with (r2r_ext)) {
			// Release+EmbedAssembliesIntoApk=false deploys ReadyToRun
			// composites named `Foo.r2r.dll`. CoreCLR's binder probes for
			// these by filename and we don't have a clean way to satisfy
			// those probes via TPA, so we leave Release-style deployments
			// on the legacy probe-only path.
			found_r2r = true;
			break;
		}

		if (tpa_list.length > 0 && !buffer_append (tpa_list, ":"sv)) {
			out_of_memory = true;
			break;
		}
		if (!buffer_append (tpa_list, override_dir_path) ||
			!buffer_append (tpa_list, "/"sv) ||
			!buffer_append (tpa_list, name)) {
			out_of_memory = true;
			break;
		}
		if (name == corelib_name) {
			found_corelib = true;
		}
		count++;
	}
	closedir (dir);

	if (out_of_memory) {
		// FastDev is a Debug-only convenience, so fall back to the probe-only path rather than
		// aborting the application.
		log_warn (LOG_ASSEMBLY, "FastDev: unable to allocate memory for the TPA list");
		std::free (tpa_list.data);
		return nullptr;
	}

	log_debugf (
		LOG_ASSEMBLY,
		"FastDev: built TPA list with %zu assemblies from '%s' (corelib=%s, r2r=%s)",
		count,
		override_dir_path,
		found_corelib ? "true" : "false",
		found_r2r ? "true" : "false"
	);

	// We can only safely hand a TPA list to CoreCLR when it contains
	// `System.Private.CoreLib.dll`. Passing TPA without CoreLib changes the
	// CLR binder mode such that CoreLib is searched via TPA/probe instead of
	// the built-in bootstrap, which fails on incomplete FastDev deployments
	// (e.g. tests that only sync a handful of user assemblies). We also skip
	// TPA when ReadyToRun variants are present (Release+nonembed), since
	// CoreCLR's `.r2r.dll` probes aren't compatible with our TPA path.
	if (count > 0 && found_corelib && !found_r2r) {
		tpa_in_use = true;
		return tpa_list.data;
	}
	std::free (tpa_list.data);
	return nullptr;
}
