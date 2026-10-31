#pragma once

#include <string_view>

#include <dlfcn.h>
#include <android/dlext.h>

#include <runtime-base/mutex.hh>

#include "../xamarin-app.hh"

#include "android-system.hh"
#include "coreclr-bootstrap.hh"
#include <runtime-base/crc32.hh>
#include <runtime-base/dso-loader.hh>
#include <runtime-base/search.hh>
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

		// Mirror of AddNameMutations () in
		// src/Xamarin.Android.Build.Tasks/Utilities/ApplicationConfigNativeAssemblyGeneratorCLR.cs.
		//
		// The DSO cache stores one entry per name mutation of a library. Each entry is keyed by the
		// CRC32 of the *mutation*, but only the library's real (unmutated) name is stored (referenced
		// through `name_index`); the mutation strings themselves are discarded. To disambiguate a
		// CRC32 collision we re-derive the mutations of a matched entry's real name and check whether
		// the requested name is one of them. This MUST be kept in sync with the managed generator.
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

		// Entries are sorted by `hash`, so all entries sharing `hash` are contiguous. CRC32 is a
		// 32-bit hash, so collisions are possible (though very unlikely); walk the whole run of
		// entries with a matching hash and confirm the requested name really is a mutation of the
		// candidate library's name before accepting it.
		[[gnu::always_inline, gnu::flatten]]
		static auto find_dso_cache_entry (std::string_view const& name, hash_t hash) noexcept -> DSOCacheEntry*
		{
			log_debugf (LOG_ASSEMBLY, "Looking for hash %x in DSO cache", hash);

			auto less_than = [](DSOCacheEntry const& entry, hash_t key) -> bool { return entry.hash < key; };
			size_t idx = Search::lower_bound<DSOCacheEntry, hash_t, less_than> (hash, CoreClrBootstrap::dso_cache, CoreClrBootstrap::config.number_of_dso_cache_entries);

			while (idx < CoreClrBootstrap::config.number_of_dso_cache_entries && CoreClrBootstrap::dso_cache[idx].hash == hash) {
				DSOCacheEntry &entry = CoreClrBootstrap::dso_cache[idx];
				if (name_is_mutation_of (name, get_dso_name (&entry))) {
					return &entry;
				}
				idx++;
			}

			return nullptr;
		}

	public:
		[[gnu::always_inline]]
		static auto get_dso_name (const DSOCacheEntry *const dso) -> std::string_view
		{
			if (dso == nullptr) {
				return "<unknown>"sv;
			}

			return CoreClrBootstrap::string (dso->name_index);
		}

		static auto monodroid_dlopen (DSOCacheEntry *dso, std::string_view const& name, int flags) noexcept -> void*
		{
			auto dso_name_for_log = get_dso_name (dso);
			log_debugf (LOG_ASSEMBLY, "monodroid_dlopen: hash match %sfound, DSO name is '%.*s'", dso == nullptr ? "not " : "", static_cast<int>(dso_name_for_log.length ()), dso_name_for_log.data ());

			if (dso == nullptr) {
				// Packaged libraries have build-time JNI metadata in the DSO cache.  A cache miss is therefore
				// assumed to be a platform or other non-JNI library, avoiding an unnecessary JNI System.loadLibrary
				// call.
				constexpr bool IsJniLibrary = false;
				constexpr bool SkipExistsCheck = true;
				return DsoLoader::load<SkipExistsCheck> (name, flags, IsJniLibrary);
			}

			void *cached_handle = __atomic_load_n (&dso->handle, __ATOMIC_ACQUIRE);
			if (cached_handle != nullptr) {
				log_debugf (LOG_ASSEMBLY, "monodroid_dlopen: library %.*s already loaded, returning handle %p", static_cast<int>(name.length ()), name.data (), cached_handle);
				return cached_handle;
			}

			if (dso->ignore) {
				auto ignored_dso_name = get_dso_name (dso);
				log_infof (LOG_ASSEMBLY, "Request to load '%.*s' ignored, it is known not to exist", static_cast<int>(ignored_dso_name.length ()), ignored_dso_name.data ());
				return nullptr;
			}

			// Perform the actual load outside dso_handle_write_lock. Synchronous JNI_OnLoad callbacks
			// can re-enter managed code and resolve another P/Invoke on the same thread, which would
			// deadlock on a non-recursive cache mutex. ART and Bionic coordinate concurrent and
			// recursive loads internally; only handle publication needs the cache lock.
			std::string_view dso_name = get_dso_name (dso);
			void *handle = AndroidSystem::load_dso_from_any_directories (dso_name, flags, dso->is_jni_library);
			if (handle == nullptr) {
				handle = AndroidSystem::load_dso_from_any_directories (name, flags, dso->is_jni_library);
			}
			if (handle == nullptr) {
				return nullptr;
			}

			StartupAwareLock lock (dso_handle_write_lock);
			cached_handle = __atomic_load_n (&dso->handle, __ATOMIC_RELAXED);
			if (cached_handle != nullptr) {
				return cached_handle;
			}

			__atomic_store_n (&dso->handle, handle, __ATOMIC_RELEASE);
			return handle;
		}

		static auto monodroid_dlopen (std::string_view const& name, int flags) noexcept -> void*
		{
			if (name.empty ()) [[unlikely]] {
				log_warnf (LOG_ASSEMBLY, "monodroid_dlopen got a null name. This is not supported in NET+");
				return nullptr;
			}

			hash_t name_hash = crc32_hash (name);
			log_debugf (LOG_ASSEMBLY, "monodroid_dlopen: hash for name '%.*s' is %x", static_cast<int>(name.length ()), name.data (), name_hash);

			DSOCacheEntry *dso = find_dso_cache_entry (name, name_hash);
			return monodroid_dlopen (dso, name, flags);
		}
	};
}
