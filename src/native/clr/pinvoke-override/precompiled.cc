#define PINVOKE_OVERRIDE_INLINE [[gnu::always_inline]]

#include <format>
#include <string_view>

#include <android/log.h>

#include <host/host.hh>
#include <host/pinvoke-override-impl.hh>
#include <runtime-base/internal-pinvokes.hh>

using namespace xamarin::android;

namespace {
	[[noreturn]]
	void abort_missing_internal_symbol (std::string_view const& library_name, std::string_view const& entrypoint_name)
	{
		Helpers::abort_application (
			LOG_ASSEMBLY,
			std::format (
				"Internal p/invoke symbol '{}'@'{}' not found"sv,
				entrypoint_name,
				library_name
			)
		);
	}

	auto load_xa_internal_api_symbol (std::string_view const& entrypoint_name) -> void*
	{
		if (entrypoint_name == "_monodroid_detect_cpu_and_architecture"sv) {
			return reinterpret_cast<void*> (&_monodroid_detect_cpu_and_architecture);
		}
		if (entrypoint_name == "monodroid_free"sv) {
			return reinterpret_cast<void*> (&monodroid_free);
		}
		if (entrypoint_name == "_monodroid_gc_wait_for_bridge_processing"sv) {
			return reinterpret_cast<void*> (&_monodroid_gc_wait_for_bridge_processing);
		}
		if (entrypoint_name == "_monodroid_gref_dec"sv) {
			return reinterpret_cast<void*> (&_monodroid_gref_dec);
		}
		if (entrypoint_name == "_monodroid_gref_get"sv) {
			return reinterpret_cast<void*> (&_monodroid_gref_get);
		}
		if (entrypoint_name == "_monodroid_gref_inc"sv) {
			return reinterpret_cast<void*> (&_monodroid_gref_inc);
		}
		if (entrypoint_name == "_monodroid_gref_log"sv) {
			return reinterpret_cast<void*> (&_monodroid_gref_log);
		}
		if (entrypoint_name == "_monodroid_gref_log_delete"sv) {
			return reinterpret_cast<void*> (&_monodroid_gref_log_delete);
		}
		if (entrypoint_name == "_monodroid_gref_log_new"sv) {
			return reinterpret_cast<void*> (&_monodroid_gref_log_new);
		}
		if (entrypoint_name == "monodroid_log"sv) {
			return reinterpret_cast<void*> (&monodroid_log);
		}
		if (entrypoint_name == "_monodroid_lookup_replacement_type"sv) {
			return reinterpret_cast<void*> (&_monodroid_lookup_replacement_type);
		}
		if (entrypoint_name == "_monodroid_lookup_replacement_method_info"sv) {
			return reinterpret_cast<void*> (&_monodroid_lookup_replacement_method_info);
		}
		if (entrypoint_name == "_monodroid_lref_log_delete"sv) {
			return reinterpret_cast<void*> (&_monodroid_lref_log_delete);
		}
		if (entrypoint_name == "_monodroid_lref_log_new"sv) {
			return reinterpret_cast<void*> (&_monodroid_lref_log_new);
		}
		if (entrypoint_name == "_monodroid_max_gref_get"sv) {
			return reinterpret_cast<void*> (&_monodroid_max_gref_get);
		}
		if (entrypoint_name == "monodroid_timing_start"sv) {
			return reinterpret_cast<void*> (&monodroid_timing_start);
		}
		if (entrypoint_name == "monodroid_timing_stop"sv) {
			return reinterpret_cast<void*> (&monodroid_timing_stop);
		}
		if (entrypoint_name == "monodroid_TypeManager_get_java_class_name"sv) {
			return reinterpret_cast<void*> (&monodroid_TypeManager_get_java_class_name);
		}
		if (entrypoint_name == "clr_typemap_managed_to_java"sv) {
			return reinterpret_cast<void*> (&clr_typemap_managed_to_java);
		}
		if (entrypoint_name == "clr_typemap_java_to_managed"sv) {
			return reinterpret_cast<void*> (&clr_typemap_java_to_managed);
		}
		if (entrypoint_name == "clr_initialize_gc_bridge"sv) {
			return reinterpret_cast<void*> (&clr_initialize_gc_bridge);
		}
		if (entrypoint_name == "_monodroid_weak_gref_dec"sv) {
			return reinterpret_cast<void*> (&_monodroid_weak_gref_dec);
		}
		if (entrypoint_name == "_monodroid_weak_gref_delete"sv) {
			return reinterpret_cast<void*> (&_monodroid_weak_gref_delete);
		}
		if (entrypoint_name == "_monodroid_weak_gref_get"sv) {
			return reinterpret_cast<void*> (&_monodroid_weak_gref_get);
		}
		if (entrypoint_name == "_monodroid_weak_gref_inc"sv) {
			return reinterpret_cast<void*> (&_monodroid_weak_gref_inc);
		}
		if (entrypoint_name == "_monodroid_weak_gref_new"sv) {
			return reinterpret_cast<void*> (&_monodroid_weak_gref_new);
		}
		if (entrypoint_name == "xamarin_app_init"sv) {
			return reinterpret_cast<void*> (&xamarin_app_init);
		}

		abort_missing_internal_symbol ("xa-internal-api"sv, entrypoint_name);
	}

	auto load_liblog_symbol (std::string_view const& entrypoint_name) -> void*
	{
		if (entrypoint_name == "__android_log_print"sv) {
			return reinterpret_cast<void*> (&__android_log_print);
		}

		abort_missing_internal_symbol ("liblog"sv, entrypoint_name);
	}
}

[[gnu::flatten]]
auto PinvokeOverride::monodroid_pinvoke_override (const char *library_name, const char *entrypoint_name) noexcept -> void*
{
	if (library_name == nullptr || entrypoint_name == nullptr) {
		return nullptr;
	}

	std::string_view library_name_view {library_name};
	std::string_view entrypoint_name_view {entrypoint_name};

	if (library_name_view == "xa-internal-api"sv) {
		return load_xa_internal_api_symbol (entrypoint_name_view);
	} else if (library_name_view == "liblog"sv) {
		return load_liblog_symbol (entrypoint_name_view);
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
	if (library_name_view == "libSystem.Native"sv ||
			library_name_view == "libSystem.Security.Cryptography.Native.Android"sv ||
			library_name_view == "libSystem.IO.Compression.Native"sv ||
			library_name_view == "libSystem.Globalization.Native"sv) {
		return nullptr;
	}

	// Any other library (e.g. `e_sqlite3`, app-specific or third-party native libraries) is resolved
	// through dotnet/android's own loader. Unlike the BCL libraries above, this is NOT equivalent to
	// returning `nullptr`: `load_library_symbol` goes through `MonodroidDl::monodroid_dlopen`, which
	// knows how to load DSOs embedded in the APK (`extractNativeLibs=false`), normalizes
	// `[DllImport ("log")]`/`[DllImport ("liblog")]`-style names, and consults the runtime's lib
	// directories. CoreCLR's default resolver does not replicate that behaviour, so this path is kept.
	return load_library_symbol (library_name_view, entrypoint_name_view);
}

const void* Host::clr_pinvoke_override (const char *library_name, const char *entry_point_name) noexcept
{
	log_debug (LOG_ASSEMBLY, "[precompiled] clr_pinvoke_override (\"{}\", \"{}\")"sv, library_name, entry_point_name);
	void *ret = PinvokeOverride::monodroid_pinvoke_override (library_name, entry_point_name);
	log_debug (LOG_DEFAULT, "[precompiled] p/invoke {}found"sv, ret == nullptr ? "not"sv : ""sv);
	return ret;
}
