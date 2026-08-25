#pragma once

#include <array>
#include <cstdio>
#include <limits>
#include <span>
#include <string>
#include <string_view>
#include <unordered_map>

#include "../constants.hh"
#include <shared/log_types.hh>
#include "../runtime-base/cpu-arch.hh"
#include <runtime-base/jni-wrappers.hh>
#include <runtime-base/strings.hh>
#include "util.hh"

struct BundledProperty;

namespace xamarin::android {
	class AndroidSystem
	{
#if !defined (XA_HOST_NATIVEAOT)
		// This optimizes things a little bit. The array is allocated at build time, so we pay no cost for its
		// allocation and at run time it allows us to skip dynamic memory allocation.
		inline static std::array<std::string, 1> single_app_lib_directory{};
		inline static std::span<std::string> app_lib_directories;

		// TODO: override dirs not implemented
		inline static std::array<std::string, 1> override_dirs{};

		static constexpr std::array<std::string_view, 7> android_abi_names {
			std::string_view { "unknown" },     // CPU_KIND_UNKNOWN
			std::string_view { "armeabi-v7a" }, // CPU_KIND_ARM
			std::string_view { "arm64-v8a" },   // CPU_KIND_ARM64
			std::string_view { "mips" },        // CPU_KIND_MIPS
			std::string_view { "x86" },         // CPU_KIND_X86
			std::string_view { "x86_64" },      // CPU_KIND_X86_64
			std::string_view { "riscv" },       // CPU_KIND_RISCV
		};
#endif

	public:
		static auto get_gref_gc_threshold () noexcept -> long
		{
			if (max_gref_count == std::numeric_limits<int>::max ()) {
				return max_gref_count;
			}
			return static_cast<int> ((max_gref_count * 90LL) / 100LL);
		}

		static auto get_max_gref_count () noexcept -> long
		{
			return max_gref_count;
		}

		static void init_max_gref_count () noexcept
		{
			max_gref_count = get_max_gref_count_from_system ();
		}

		static void set_running_in_emulator (bool yesno) noexcept
		{
			running_in_emulator = yesno;
		}

#if defined (XA_HOST_NATIVEAOT)
		static auto get_primary_override_dir () noexcept -> const char*
		{
			return primary_override_dir;
		}
#else
		static auto get_primary_override_dir () noexcept -> std::string const&
		{
			return primary_override_dir;
		}
#endif

		static void set_primary_override_dir (jstring_wrapper& home) noexcept
		{
#if defined (XA_HOST_NATIVEAOT)
			ssize_t result = format_primary_override_dir (home, primary_override_dir, sizeof (primary_override_dir));
			abort_unless (result >= 0, "Primary override directory path is too long");
#else
			primary_override_dir = determine_primary_override_dir (home);
#endif
		}

#if !defined (XA_HOST_NATIVEAOT)
		static auto get_app_code_cache_dir () noexcept -> std::string const&
		{
			return app_code_cache_dir;
		}

		static void set_app_code_cache_dir (jstring_wrapper& code_cache_dir) noexcept
		{
			app_code_cache_dir.assign (code_cache_dir.get_cstr ());
		}

		static auto get_native_libraries_dir () noexcept -> std::string const&
		{
			return native_libraries_dir;
		}

		static void create_update_dir (std::string const& override_dir) noexcept
		{
			if constexpr (Constants::is_release_build) {
				/*
				 * Don't create .__override__ on Release builds, because Google requires
				 * that pre-loaded apps not create world-writable directories.
				 *
				 * However, if any logging is enabled (which should _not_ happen with
				 * pre-loaded apps!), we need the .__override__ directory...
				 */
				char value[Constants::PROPERTY_VALUE_BUFFER_LEN];
				if (log_categories == 0 && monodroid_get_system_property (Constants::DEBUG_MONO_PROFILE_PROPERTY.data (), value, sizeof (value)) == nullptr) [[likely]] {
					return;
				}
			}

			log_debug (LOG_DEFAULT, "Creating public update directory: `{}`", override_dir);
			Util::create_public_directory (override_dir.c_str ());
		}
#endif

		static auto is_embedded_dso_mode_enabled () noexcept -> bool
		{
			return embedded_dso_mode_enabled;
		}

		// Returns the property's NUL-terminated value, or `nullptr` if it is not set. A property
		// that is set to an empty value is reported as not set, matching `__system_property_get`,
		// which cannot tell the two apart.
		//
		// `value` is a scratch buffer of at least `Constants::PROPERTY_VALUE_BUFFER_LEN` bytes, used
		// to receive Android system properties. Bundled properties are returned without copying, so
		// their length is not limited by `value_size`.
		//
		// Nothing is ever allocated and the caller must not free the result: it is either `value` or
		// a pointer to bundled property data. The latter is only guaranteed to stay valid until the
		// next `setup_environment()` call, because in Debug builds bundled properties are stored in
		// a mutable map. Copy the value if you need to retain it.
		static auto monodroid_get_system_property (const char *name, char *value, size_t value_size) noexcept -> const char*;
		static void detect_embedded_dso_mode (jstring_array_wrapper& appDirs) noexcept;
		static void setup_environment () noexcept;
		static void setup_app_library_directories (jstring_array_wrapper& runtimeApks, jstring_array_wrapper& appDirs, bool have_split_apks) noexcept;
		static auto load_dso_from_any_directories (std::string_view const& name, int dl_flags, bool is_jni) noexcept -> void*;

	private:
		static auto get_full_dso_path (std::string const& base_dir, std::string_view const& dso_path, char *buffer, size_t buffer_size) noexcept -> ssize_t;

		template<size_t Size>
		static auto get_full_dso_path (std::string const& base_dir, std::string_view const& dso_path, char (&stack_buffer)[Size], char *&heap_buffer) noexcept -> const char*
		{
			heap_buffer = nullptr;
			ssize_t result = get_full_dso_path (base_dir, dso_path, stack_buffer, Size);
			if (result >= 0) {
				return stack_buffer;
			}

			size_t required_capacity = static_cast<size_t>(-result);
			heap_buffer = static_cast<char*> (std::malloc (required_capacity));
			abort_unless (heap_buffer != nullptr, "Failed to allocate full DSO path");
			result = get_full_dso_path (base_dir, dso_path, heap_buffer, required_capacity);
			abort_unless (result >= 0, "Failed to format full DSO path using the required capacity");
			return heap_buffer;
		}

		template<class TContainer> // TODO: replace with a concept
		static auto load_dso_from_specified_dirs (TContainer directories, std::string_view const& dso_name, int dl_flags, bool is_jni) noexcept -> void*;
		static auto load_dso_from_app_lib_dirs (std::string_view const& name, int dl_flags, bool is_jni) noexcept -> void*;
		static auto load_dso_from_override_dirs (std::string_view const& name, int dl_flags, bool is_jni) noexcept -> void*;
		static auto lookup_system_property (const char *name, size_t &value_len) noexcept -> const char*;
		static auto monodroid__system_property_get (const char *name, char *sp_value) noexcept -> int;
		static auto get_max_gref_count_from_system () noexcept -> long;
		static void add_apk_libdir (std::string_view const& apk, size_t &index, std::string_view const& abi) noexcept;
        static void setup_apk_directories (unsigned short running_on_cpu, jstring_array_wrapper &runtimeApks, bool have_split_apks) noexcept;
#if defined(DEBUG)
		static void add_system_property (const char *name, const char *value) noexcept;
		static void setup_environment (const char *name, const char *value) noexcept;
		static void setup_environment_from_override_file (const char *path) noexcept;
#endif

		static void set_embedded_dso_mode_enabled (bool yesno) noexcept
		{
			embedded_dso_mode_enabled = yesno;
		}

		static auto format_primary_override_dir (jstring_wrapper &home, char *buffer, size_t buffer_size) noexcept -> ssize_t
		{
			abort_unless (buffer != nullptr, "Primary override directory buffer must not be null");

			// `jstring_wrapper::get_cstr()` returns `nullptr` for a null `jstring`, and passing that
			// to `%s` is undefined behaviour. An app without a files directory cannot work anyway.
			const char *home_path = home.get_cstr ();
			abort_unless (home_path != nullptr, "Application home directory must not be null");

			int length = snprintf (
				buffer,
				buffer_size,
				"%s/%.*s/%.*s",
				home_path,
				static_cast<int>(Constants::OVERRIDE_DIRECTORY_NAME.length ()),
				Constants::OVERRIDE_DIRECTORY_NAME.data (),
				static_cast<int>(Constants::android_lib_abi.length ()),
				Constants::android_lib_abi.data ()
			);
			abort_unless (length >= 0, "Failed to format primary override directory path");
			size_t required_capacity = Helpers::add_with_overflow_check<size_t> (static_cast<size_t>(length), 1uz);
			abort_unless (required_capacity <= static_cast<size_t>(std::numeric_limits<ssize_t>::max ()), "Primary override directory path is too long");
			if (buffer_size < required_capacity) {
				return -static_cast<ssize_t>(required_capacity);
			}
			return static_cast<ssize_t>(length);
		}

#if !defined (XA_HOST_NATIVEAOT)
		static auto determine_primary_override_dir (jstring_wrapper &home) noexcept -> std::string
		{
			char stack_buffer [Constants::SENSIBLE_PATH_MAX];
			size_t length;
			char *name = Util::format_with_retry (
				stack_buffer,
				sizeof (stack_buffer),
				[&home](char *buffer, size_t buffer_size) noexcept {
					return format_primary_override_dir (home, buffer, buffer_size);
				},
				&length
			);

			std::string path { name, length };
			if (name != stack_buffer) {
				std::free (name);
			}
			return path;
		}
#endif

	private:
		static inline long max_gref_count = 0;
		static inline bool running_in_emulator = false;
		static inline bool embedded_dso_mode_enabled = false;
#if defined (XA_HOST_NATIVEAOT)
		static inline char primary_override_dir[Constants::SENSIBLE_PATH_MAX] {};
#else
		static inline std::string primary_override_dir;
		static inline std::string native_libraries_dir;
		static inline std::string app_code_cache_dir;

#if defined (DEBUG)
		static inline std::unordered_map<std::string, std::string> bundled_properties;
#endif
#endif
	};
}
