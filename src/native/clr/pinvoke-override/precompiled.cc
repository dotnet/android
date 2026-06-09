#include <format>

#define PINVOKE_OVERRIDE_INLINE [[gnu::always_inline]]

#include <host/host.hh>
#include <host/pinvoke-override-impl.hh>
#include <runtime-base/internal-pinvokes.hh>

using namespace xamarin::android;

#include "pinvoke-tables.include"

[[gnu::flatten]]
auto PinvokeOverride::monodroid_pinvoke_override (const char *library_name, const char *entrypoint_name) noexcept -> void*
{
	if (library_name == nullptr || entrypoint_name == nullptr) {
		return nullptr;
	}

	hash_t library_name_hash = xxhash::hash (library_name, strlen (library_name));
	hash_t entrypoint_hash = xxhash::hash (entrypoint_name, strlen (entrypoint_name));

	if (library_name_hash == java_interop_library_hash || library_name_hash == xa_internal_api_library_hash || library_name_hash == android_liblog_library_hash) {
		PinvokeEntry *entry = find_pinvoke_address (entrypoint_hash, internal_pinvokes.data (), internal_pinvokes_count);

		if (entry == nullptr) [[unlikely]] {
			log_fatal (LOG_ASSEMBLY, "Internal p/invoke symbol '%s @ %s' (hash: %zx) not found in compile-time map.",
									 optional_string (library_name), optional_string (entrypoint_name), static_cast<size_t>(entrypoint_hash));
			log_fatal (LOG_ASSEMBLY, "compile-time map contents:"sv);
			for (size_t i = 0uz; i < internal_pinvokes_count; i++) {
				PinvokeEntry const& e = internal_pinvokes[i];
				log_fatal (LOG_ASSEMBLY, "\t'%s'=%p (hash: %zx)", optional_string (e.name), e.func, static_cast<size_t>(e.hash));
			}
			Helpers::abort_application (
				LOG_ASSEMBLY,
				std::format (
					"Failure handling a p/invoke request for '{}'@'{}'"sv,
					optional_string (entrypoint_name),
					optional_string (library_name)
				)
			);
		}

		return entry->func;
	}

	// The .NET BCL native libraries (`libSystem.Native`, `libSystem.Globalization.Native`,
	// `libSystem.Security.Cryptography.Native.Android` and `libSystem.IO.Compression.Native`) are
	// shipped as standalone shared libraries in the default (separate-`.so`) runtime layout, so
	// CoreCLR is able to resolve their p/invoke entry points itself via its default resolution.
	//
	// We used to serve them here from a hand-maintained static table (`dotnet_pinvokes`). That table
	// drifted from the runtime's real exports whenever a p/invoke was added or renamed, aborting the
	// application on the first missing entry (see https://github.com/dotnet/android/issues/11530).
	// Measurements on a physical device showed the table provides no measurable startup benefit, so
	// we return `nullptr` for these libraries and let CoreCLR's own resolver handle them, removing
	// the whole class of drift bugs.
	//
	// NOTE: this only affects the precompiled override used by the default separate-`.so` layout.
	// The unified-DSO layout uses the generated `find_pinvoke` table in `dynamic.cc`, where these
	// symbols are hidden inside a single DSO and therefore must still be resolved by the override.
	if (library_name_hash == system_native_library_hash ||
			library_name_hash == system_security_cryptography_native_android_library_hash ||
			library_name_hash == system_io_compression_native_library_hash ||
			library_name_hash == system_globalization_native_library_hash) {
		return nullptr;
	}

	// Any other library (e.g. `e_sqlite3`, app-specific or third-party native libraries) is resolved
	// through dotnet/android's own loader. Unlike the BCL libraries above, this is NOT equivalent to
	// returning `nullptr`: `handle_other_pinvoke_request` goes through `MonodroidDl::monodroid_dlopen`,
	// which knows how to load DSOs embedded in the APK (`extractNativeLibs=false`), normalizes
	// `[DllImport ("log")]`/`[DllImport ("liblog")]`-style names, and consults the runtime's lib
	// directories. CoreCLR's default resolver does not replicate that behaviour, so this path is kept.
	// It also carries no static table, so it is not subject to the drift problem that motivated the
	// BCL change above.
	return handle_other_pinvoke_request (library_name, library_name_hash, entrypoint_name, entrypoint_hash);
}

const void* Host::clr_pinvoke_override (const char *library_name, const char *entry_point_name) noexcept
{
	log_debug (LOG_ASSEMBLY, "[precompiled] clr_pinvoke_override (\"%s\", \"%s\")", optional_string (library_name), optional_string (entry_point_name));
	void *ret = PinvokeOverride::monodroid_pinvoke_override (library_name, entry_point_name);
	log_debug (LOG_DEFAULT, "[precompiled] p/invoke %sfound", ret == nullptr ? "not" : "");
	return ret;
}
