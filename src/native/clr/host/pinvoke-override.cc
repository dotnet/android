#include <host/host.hh>
#include <host/pinvoke-override.hh>
#include <runtime-base/internal-pinvokes.hh>

using namespace xamarin::android;

#if defined (DEBUG)
void xamarin_app_init ([[maybe_unused]] JNIEnv *env, [[maybe_unused]] get_function_pointer_fn fn) noexcept {}
#endif

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
			log_fatal (LOG_ASSEMBLY, "Internal p/invoke symbol '{} @ {}' (hash: {:x}) not found in compile-time map.",
									 optional_string (library_name), optional_string (entrypoint_name), entrypoint_hash);
			log_fatal (LOG_ASSEMBLY, "compile-time map contents:"sv);
			for (size_t i = 0uz; i < internal_pinvokes_count; i++) {
				PinvokeEntry const& e = internal_pinvokes[i];
				log_fatal (LOG_ASSEMBLY, "\t'{}'={:p} (hash: {:x})", optional_string (e.name), e.func, e.hash);
			}
			Helpers::abort_application (
				LOG_ASSEMBLY,
				std::format (
					"Failure handling a p/invoke request for '{}'@'{}'",
					optional_string (entrypoint_name),
					optional_string (library_name)
				)
			);
		}

		return entry->func;
	}

	// The order of statements below should be kept in the descending probability of occurrence order (as much as
	// possible, of course). `libSystem.Native` is requested during early startup for each MAUI app, so its
	// probability is higher, just as it's more likely that `libSystem.Security.Cryptography.Android` will be used
	// in an app rather than `libSystem.IO.Compression.Native`
	void **dotnet_dso_handle; // Set to a non-null value only for dotnet shared libraries
	if (library_name_hash == system_native_library_hash) {
		dotnet_dso_handle = &system_native_library_handle;
	} else if (library_name_hash == system_security_cryptography_native_android_library_hash) {
		dotnet_dso_handle = &system_security_cryptography_native_android_library_handle;
	} else if (library_name_hash == system_io_compression_native_library_hash) {
		dotnet_dso_handle = &system_io_compression_native_library_handle;
	} else if (library_name_hash == system_globalization_native_library_hash) {
		dotnet_dso_handle = &system_globalization_native_library_handle;
	} else {
		dotnet_dso_handle = nullptr;
	}

	if (dotnet_dso_handle != nullptr) {
		PinvokeEntry *entry = find_pinvoke_address (entrypoint_hash, dotnet_pinvokes.data (), dotnet_pinvokes_count);
		if (entry != nullptr) {
			if (entry->func != nullptr) {
				return entry->func;
			}

			load_library_entry (library_name, entrypoint_name, *entry, dotnet_dso_handle);
			if (entry->func == nullptr) {
				log_fatal (LOG_ASSEMBLY, "Failed to load symbol '{}' from shared library '{}'",
										 optional_string (entrypoint_name), optional_string (library_name));
				return nullptr; // let Mono deal with the fallout
			}

			return entry->func;
		}

		// It's possible we don't have an entry for some `dotnet` p/invoke, fall back to the slow path below
		log_debug (
			LOG_ASSEMBLY,
			"Symbol '{}' in library '{}' not found in the generated tables, falling back to slow path",
			optional_string (entrypoint_name),
			optional_string (library_name)
		);

		// This is temporary, to catch p/invokes we might be missing that are used in the default templates
		Helpers::abort_application (
			LOG_ASSEMBLY,
			std::format (
				"Missing pinvoke {}@{}",
				optional_string (entrypoint_name),
				optional_string (library_name)
			)
		);
	}

	return handle_other_pinvoke_request (library_name, library_name_hash, entrypoint_name, entrypoint_hash);
}

const void* Host::clr_pinvoke_override (const char *library_name, const char *entry_point_name) noexcept
{
	log_debug (LOG_ASSEMBLY, "clr_pinvoke_override (\"{}\", \"{}\")", library_name, entry_point_name);
	void *ret = PinvokeOverride::monodroid_pinvoke_override (library_name, entry_point_name);
	log_debug (LOG_DEFAULT, "p/invoke {}found", ret == nullptr ? "not"sv : ""sv);
	return ret;
}
