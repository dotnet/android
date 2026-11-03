#include <sys/types.h>
#include <dirent.h>
#include <fcntl.h>
#include <unistd.h>

#include <cerrno>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <limits>

#include <constants.hh>
#include <host/fastdev-assemblies.hh>
#include <runtime-base/android-system.hh>
#include <runtime-base/util.hh>

using namespace xamarin::android;

namespace {
	struct assembly_data final
	{
		assembly_data *next;
		void         *data;
		int64_t       size;
		size_t        name_length;
		// The exact probe name follows this structure in the same allocation.
	};

	// CoreCLR borrows external probe data; it neither copies nor releases it. Keep the first
	// successful snapshot of each name until process exit, including across collectible loads.
	// Protected by FastDevAssemblies::override_dir_lock.
	assembly_data *loaded_assemblies = nullptr;

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
		log_debugf (
			LOG_ASSEMBLY,
			"Deferring assembly '%.*s' to CoreCLR's FastDev disk loader; this is expected",
			static_cast<int>(name.length ()), name.data ()
		);
		return nullptr;
	}

	lock_guard dir_lock { override_dir_lock };
	for (assembly_data *entry = loaded_assemblies; entry != nullptr; entry = entry->next) {
		if (entry->name_length == name.length () && memcmp (entry + 1, name.data (), name.length ()) == 0) {
			size = entry->size;
			return entry->data;
		}
	}

	const char *override_dir_path = AndroidSystem::get_primary_override_dir ();
	if (!Util::dir_exists (override_dir_path)) [[unlikely]] {
		log_debugf (LOG_ASSEMBLY, "Override directory '%s' does not exist", override_dir_path);
		return nullptr;
	}

	// NOTE: override_dir will be kept open, we have no way of knowing when it will be no longer
	//       needed
	if (override_dir_fd < 0) [[unlikely]] {
		override_dir = opendir (override_dir_path);
		if (override_dir == nullptr) [[unlikely]] {
			log_warnf (LOG_ASSEMBLY, "Failed to open override dir '%s'. %s", override_dir_path, strerror (errno));
			return nullptr;
		}
		override_dir_fd = dirfd (override_dir);
	}

	log_debugf (
		LOG_ASSEMBLY,
		"Attempting to load FastDev assembly '%.*s' from override directory '%s'",
		static_cast<int>(name.length ()), name.data (),
		override_dir_path
	);

	if (!Util::file_exists (override_dir_fd, name)) {
		log_debugf (LOG_ASSEMBLY, "FastDev assembly '%.*s' not found.", static_cast<int>(name.length ()), name.data ());
		return nullptr;
	}
	log_debugf (LOG_ASSEMBLY, "Found FastDev assembly '%.*s'", static_cast<int>(name.length ()), name.data ());

	auto file_size = Util::get_file_size_at (override_dir_fd, name);
	if (!file_size) [[unlikely]] {
		log_warnf (LOG_ASSEMBLY, "Unable to determine FastDev assembly '%.*s' file size", static_cast<int>(name.length ()), name.data ());
		return nullptr;
	}

	if (file_size.value () == 0 || static_cast<uintmax_t>(file_size.value ()) > static_cast<uintmax_t>(std::numeric_limits<int64_t>::max ())) [[unlikely]] {
		log_warnf (LOG_ASSEMBLY, "Invalid FastDev assembly '%.*s' file size: %zu", static_cast<int>(name.length ()), name.data (), file_size.value ());
		return nullptr;
	}

	size_t assembly_size = file_size.value ();
	int asm_fd = openat (override_dir_fd, name.data (), O_RDONLY);
	if (asm_fd < 0) {
		log_warnf (
			LOG_ASSEMBLY,
			"Failed to open FastDev assembly '%.*s' for reading. %s",
			static_cast<int>(name.length ()), name.data (),
			strerror (errno)
		);

		return nullptr;
	}

	auto buffer = static_cast<uint8_t*> (std::malloc (assembly_size));
	if (buffer == nullptr) [[unlikely]] {
		log_warnf (
			LOG_ASSEMBLY,
			"Unable to allocate memory for FastDev assembly '%.*s'",
			static_cast<int>(name.length ()), name.data ()
		);

		if (close (asm_fd) < 0) {
			log_warnf (LOG_ASSEMBLY, "Failed to close FastDev assembly '%.*s'. %s", static_cast<int>(name.length ()), name.data (), strerror (errno));
		}
		return nullptr;
	}

	size_t nread = 0;
	int read_errno = 0;
	while (nread < assembly_size) {
		size_t read_count = assembly_size - nread;
		if (read_count > static_cast<size_t>(std::numeric_limits<ssize_t>::max ())) {
			read_count = static_cast<size_t>(std::numeric_limits<ssize_t>::max ());
		}

		ssize_t r = read (asm_fd, buffer + nread, read_count);
		if (r > 0) {
			nread += static_cast<size_t>(r);
			continue;
		}

		if (r < 0 && errno == EINTR) {
			continue;
		}
		read_errno = r < 0 ? errno : 0;
		break;
	}
	if (close (asm_fd) < 0) {
		log_warnf (LOG_ASSEMBLY, "Failed to close FastDev assembly '%.*s'. %s", static_cast<int>(name.length ()), name.data (), strerror (errno));
	}

	if (nread != assembly_size) [[unlikely]] {
		std::free (buffer);

		if (read_errno != 0) {
			log_warnf (
				LOG_ASSEMBLY,
				"Failed to read FastDev assembly '%.*s' data. %s",
				static_cast<int>(name.length ()), name.data (),
				strerror (read_errno)
			);
		} else {
			log_warnf (
				LOG_ASSEMBLY,
				"Failed to read FastDev assembly '%.*s' data: unexpected end of file after %zu of %zu bytes",
				static_cast<int>(name.length ()), name.data (),
				nread, assembly_size
			);
		}

		return nullptr;
	}
	log_debugf (LOG_ASSEMBLY, "Read %zu bytes of FastDev assembly '%.*s'", nread, static_cast<int>(name.length ()), name.data ());

	if (size == 0) [[unlikely]] {
		log_warnf (LOG_ASSEMBLY, "FastDev assembly '%.*s' is empty", static_cast<int> (name.length ()), name.data ());
		std::free (buffer);
		return nullptr;
	}

	size_t entry_size = Helpers::add_with_overflow_check<size_t> (sizeof (assembly_data), name.length ());
	auto entry = static_cast<assembly_data*> (std::malloc (entry_size));
	if (entry == nullptr) [[unlikely]] {
		log_warnf (
			LOG_ASSEMBLY,
			"Unable to retain FastDev assembly '%.*s'",
			static_cast<int>(name.length ()), name.data ()
		);
		std::free (buffer);
		return nullptr;
	}

	memcpy (entry + 1, name.data (), name.length ());
	entry->next = loaded_assemblies;
	entry->data = buffer;
	entry->size = static_cast<int64_t>(assembly_size);
	entry->name_length = name.length ();
	loaded_assemblies = entry;

	size = entry->size;
	return buffer;
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

	char *tpa_list = nullptr;
	size_t tpa_length = 0;
	FILE *stream = open_memstream (&tpa_list, &tpa_length);
	if (stream == nullptr) {
		log_warnf (LOG_ASSEMBLY, "FastDev: unable to allocate memory for the TPA list. %s", std::strerror (errno));
		closedir (dir);
		return nullptr;
	}

	constexpr std::string_view dll_ext { ".dll" };
	constexpr std::string_view r2r_ext { ".r2r.dll" };
	constexpr std::string_view corelib_name { "System.Private.CoreLib.dll" };
	bool found_corelib = false;
	bool found_r2r = false;
	bool write_failed = false;
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

		if (std::fprintf (stream, "%s%s/%s", count > 0 ? ":" : "", override_dir_path, e->d_name) < 0) {
			log_warnf (LOG_ASSEMBLY, "FastDev: unable to write the TPA list. %s", std::strerror (errno));
			write_failed = true;
			break;
		}
		if (name == corelib_name) {
			found_corelib = true;
		}
		count++;
	}
	if (std::fclose (stream) != 0) {
		log_warnf (LOG_ASSEMBLY, "FastDev: unable to close the TPA list stream. %s", std::strerror (errno));
		write_failed = true;
	}
	closedir (dir);

	if (write_failed) {
		// FastDev is a Debug-only convenience, so fall back to the probe-only path rather than
		// aborting the application.
		std::free (tpa_list);
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
		log_infof (
			LOG_ASSEMBLY,
			"FastDev: Found %zu assemblies in '%s'. Loading assemblies from disk is normal during Fast Deployment.",
			count,
			override_dir_path
		);
		return tpa_list;
	}
	std::free (tpa_list);
	return nullptr;
}

void FastDevAssemblies::discard_tpa_list (char *tpa_list) noexcept
{
	std::free (tpa_list);
	tpa_in_use = false;
}
