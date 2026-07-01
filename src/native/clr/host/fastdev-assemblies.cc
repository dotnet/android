#include <sys/types.h>
#include <dirent.h>
#include <fcntl.h>
#include <unistd.h>

#include <cerrno>
#include <cstring>
#include <limits>
#include <string>

#include <constants.hh>
#include <host/fastdev-assemblies.hh>
#include <runtime-base/android-system.hh>
#include <runtime-base/util.hh>

using namespace xamarin::android;

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

	std::string const& override_dir_path = AndroidSystem::get_primary_override_dir ();
	if (!Util::dir_exists (override_dir_path)) [[unlikely]] {
		log_debug (LOG_ASSEMBLY, "Override directory '{}' does not exist"sv, override_dir_path);
		return nullptr;
	}

	// NOTE: override_dir will be kept open, we have no way of knowing when it will be no longer
	//       needed
	if (override_dir_fd < 0) [[unlikely]] {
		std::lock_guard dir_lock { override_dir_lock };
		if (override_dir_fd < 0) [[likely]] {
			override_dir = opendir (override_dir_path.c_str ());
			if (override_dir == nullptr) [[unlikely]] {
				log_warn (LOG_ASSEMBLY, "Failed to open override dir '{}'. {}"sv, override_dir_path, strerror (errno));
				return nullptr;
			}
			override_dir_fd = dirfd (override_dir);
		}
	}

	log_debug (
		LOG_ASSEMBLY,
		"Attempting to load FastDev assembly '{}' from override directory '{}'"sv,
		name,
		override_dir_path
	);

	if (!Util::file_exists (override_dir_fd, name)) {
		log_warn (LOG_ASSEMBLY, "FastDev assembly '{}' not found."sv, name);
		return nullptr;
	}
	log_debug (LOG_ASSEMBLY, "Found FastDev assembly '{}'"sv, name);

	auto file_size = Util::get_file_size_at (override_dir_fd, name);
	if (!file_size) [[unlikely]] {
		log_warn (LOG_ASSEMBLY, "Unable to determine FastDev assembly '{}' file size"sv, name);
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
			"Failed to open FastDev assembly '{}' for reading. {}"sv,
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
			"Failed to read FastDev assembly '{}' data. {}"sv,
			name,
			strerror (errno)
		);

		size = 0;
		return nullptr;
	}
	log_debug (LOG_ASSEMBLY, "Read {} bytes of FastDev assembly '{}'"sv, nread, name);

	return reinterpret_cast<void*>(buffer);
}

auto FastDevAssemblies::build_tpa_list (std::string &tpa_list) noexcept -> bool
{
	tpa_list.clear ();

	std::string const& override_dir_path = AndroidSystem::get_primary_override_dir ();
	if (!Util::dir_exists (override_dir_path)) {
		return false;
	}

	DIR *dir = opendir (override_dir_path.c_str ());
	if (dir == nullptr) {
		log_warn (LOG_ASSEMBLY, "FastDev: failed to open override dir '{}'. {}"sv, override_dir_path, std::strerror (errno));
		return false;
	}

	constexpr std::string_view dll_ext { ".dll" };
	constexpr std::string_view r2r_ext { ".r2r.dll" };
	constexpr std::string_view corelib_name { "System.Private.CoreLib.dll" };
	bool found_corelib = false;
	bool found_r2r = false;
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

		if (!tpa_list.empty ()) {
			tpa_list.append (":");
		}
		tpa_list.append (override_dir_path);
		tpa_list.append ("/");
		tpa_list.append (name);
		if (name == corelib_name) {
			found_corelib = true;
		}
		count++;
	}
	closedir (dir);

	log_debug (
		LOG_ASSEMBLY,
		"FastDev: built TPA list with {} assemblies from '{}' (corelib={}, r2r={})"sv,
		count,
		override_dir_path,
		found_corelib,
		found_r2r
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
		return true;
	}
	tpa_list.clear ();
	return false;
}
