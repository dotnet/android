#pragma once

#include <string_view>

#include <dlfcn.h>
#include <java-interop-dlfcn.h>
#include <runtime-base/mutex.hh>

#include "android-system.hh"
#include <runtime-base/dso-loader.hh>
#include <runtime-base/java-app-config.hh>
#include "startup-aware-lock.hh"

namespace xamarin::android
{
	class MonodroidDl
	{
		static inline pthread_mutex_t dso_handle_write_lock = PTHREAD_MUTEX_INITIALIZER;

		[[gnu::always_inline]]
		static constexpr auto ascii_to_lower (char c) noexcept -> char
		{
			return (c >= 'A' && c <= 'Z') ? static_cast<char>(c + ('a' - 'A')) : c;
		}

		[[gnu::always_inline]]
		static auto ends_with_ci (std::string_view value, std::string_view suffix) noexcept -> bool
		{
			if (value.length () < suffix.length ()) {
				return false;
			}

			size_t offset = value.length () - suffix.length ();
			for (size_t i = 0; i < suffix.length (); i++) {
				if (ascii_to_lower (value[offset + i]) != ascii_to_lower (suffix[i])) {
					return false;
				}
			}

			return true;
		}

		[[gnu::always_inline]]
		static auto starts_with_ci (std::string_view value, std::string_view prefix) noexcept -> bool
		{
			if (value.length () < prefix.length ()) {
				return false;
			}

			for (size_t i = 0; i < prefix.length (); i++) {
				if (ascii_to_lower (value[i]) != ascii_to_lower (prefix[i])) {
					return false;
				}
			}

			return true;
		}

		// Equivalent of Path.GetFileNameWithoutExtension for a bare file name: strip the last '.' and
		// everything following it.
		[[gnu::always_inline]]
		static auto strip_last_extension (std::string_view name) noexcept -> std::string_view
		{
			size_t dot = name.find_last_of ('.');
			return dot == std::string_view::npos ? name : name.substr (0, dot);
		}

		// Accept the same library-name aliases as the former generated DSO cache.
		static auto name_is_mutation_of (std::string_view requested, std::string_view real_name) noexcept -> bool
		{
			// Mutation: the (real) name itself.
			if (requested == real_name) {
				return true;
			}

			if (ends_with_ci (real_name, ".dll.so"sv)) {
				// Path.GetFileNameWithoutExtension applied twice strips ".so" and then ".dll".
				std::string_view no_ext = strip_last_extension (strip_last_extension (real_name));

				// Mutation: the name without the ".dll.so" suffix.
				if (requested == no_ext) {
					return true;
				}

				// Mutation: that same stem with a plain ".so" suffix.
				if (requested.ends_with (".so"sv) && requested.substr (0, requested.length () - ".so"sv.length ()) == no_ext) {
					return true;
				}
			} else if (requested == strip_last_extension (real_name)) {
				// Mutation: the name without its final extension.
				return true;
			}

			// The generator also emits the mutations of the name with a leading "lib" removed.
			if (starts_with_ci (real_name, "lib"sv)) {
				return name_is_mutation_of (requested, real_name.substr ("lib"sv.length ()));
			}

			return false;
		}

		static auto load_configured_dso (std::string_view const& name, int flags) noexcept -> void*
		{
			for (JavaAppConfig::Library &library : JavaAppConfig::libraries ()) {
				if (!name_is_mutation_of (name, library.name)) {
					continue;
				}

				void *handle = __atomic_load_n (&library.handle, __ATOMIC_ACQUIRE);
				if (handle != nullptr) {
					return handle;
				}

				StartupAwareLock lock (dso_handle_write_lock);
				handle = __atomic_load_n (&library.handle, __ATOMIC_RELAXED);
				if (handle == nullptr) {
					handle = AndroidSystem::load_dso_from_any_directories (library.name, flags, library.is_jni_library);
					if (handle == nullptr) {
						handle = AndroidSystem::load_dso_from_any_directories (name, flags, library.is_jni_library);
					}
					__atomic_store_n (&library.handle, handle, __ATOMIC_RELEASE);
				}
				return handle;
			}

			// Unknown libraries may be platform libraries; do not assume they need JNI_OnLoad.
			constexpr bool SkipExistsCheck = true;
			return DsoLoader::load<SkipExistsCheck> (name, flags, false);
		}

	public:
		static auto monodroid_dlopen (std::string_view const& name, int flags) noexcept -> void*
		{
			if (name.empty ()) [[unlikely]] {
				log_warnf (LOG_ASSEMBLY, "monodroid_dlopen got a null name. This is not supported in NET+");
				return nullptr;
			}

			return load_configured_dso (name, flags);
		}

		[[gnu::flatten]]
		static auto monodroid_dlsym (void *handle, std::string_view const& name) -> void*
		{
			char *e = nullptr;
			void *s = microsoft::java_interop::java_interop_lib_symbol (handle, name.data (), &e);

			if (s == nullptr) {
				log_errorf (
					LOG_ASSEMBLY,
					"Could not find symbol '%.*s': %s",
					static_cast<int>(name.length ()),
					name.data (),
					optional_string (e)
				);
			}

			if (e != nullptr) {
				java_interop_free (e);
			}

			return s;
		}
	};
}
