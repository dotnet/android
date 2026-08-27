#pragma once

#include <array>
#include <cstdio>
#include <limits>
#include <span>
#include <string>
#include <string_view>

#include "../constants.hh"
#include <shared/log_types.hh>
#include "../runtime-base/cpu-arch.hh"
#include <runtime-base/jni-wrappers.hh>
#include "util.hh"

namespace xamarin::android {
#if defined (DEBUG)
	// A system property bundled with the application, read from the environment override files.
	// `name` is allocated together with the structure and never changes, `value` is allocated
	// separately so that it can be replaced when the same property is set more than once.
	struct BundledProperty
	{
		BundledProperty *next;
		char            *name;
		char            *value;
		size_t           value_len;
	};
#endif

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

			log_debugf (LOG_DEFAULT, "Creating public update directory: `%s`", override_dir.c_str ());
			Util::create_public_directory (override_dir.c_str ());
		}
#endif

		static auto is_embedded_dso_mode_enabled () noexcept -> bool
		{
			return embedded_dso_mode_enabled;
		}

		// Returns the property's NUL-terminated value, or `nullptr` if it is not set.
		//
		// `value` is a scratch buffer of at least `Constants::PROPERTY_VALUE_BUFFER_LEN` bytes, used
		// to receive Android system properties. Bundled properties are returned without copying, so
		// their length is not limited by `value_size`.
		//
		// Nothing is ever allocated: the result is either `value` or a pointer to application data
		// which lives as long as the process. The caller must not free it.
		static auto monodroid_get_system_property (const char *name, char *value, size_t value_size) noexcept -> const char*;
		static void detect_embedded_dso_mode (jstring_array_wrapper& appDirs) noexcept;
		static void setup_environment () noexcept;
		static void setup_app_library_directories (jstring_array_wrapper& runtimeApks, jstring_array_wrapper& appDirs, bool have_split_apks) noexcept;
		static auto load_dso_from_any_directories (std::string_view const& name, int dl_flags, bool is_jni) noexcept -> void*;

	private:
		static auto format_full_dso_path (std::string const& base_dir, std::string_view const& dso_path, char *buffer, size_t buffer_size) noexcept -> ssize_t;

		static auto get_full_dso_path (std::string const& base_dir, std::string_view const& dso_path, char *stack_buffer, size_t stack_buffer_size) noexcept -> char*
		{
			ssize_t result = format_full_dso_path (base_dir, dso_path, stack_buffer, stack_buffer_size);
			if (result >= 0) {
				return stack_buffer;
			}

			size_t required_capacity = static_cast<size_t>(-result);
			char *heap_buffer = static_cast<char*> (std::malloc (required_capacity));
			abort_unless (heap_buffer != nullptr, "Failed to allocate full DSO path");
			result = format_full_dso_path (base_dir, dso_path, heap_buffer, required_capacity);
			abort_unless (result >= 0, "Failed to format full DSO path using the required capacity");
			return heap_buffer;
		}

		template<class TContainer> // TODO: replace with a concept
		static auto load_dso_from_specified_dirs (TContainer directories, std::string_view const& dso_name, int dl_flags, bool is_jni) noexcept -> void*;
		static auto load_dso_from_app_lib_dirs (std::string_view const& name, int dl_flags, bool is_jni) noexcept -> void*;
		static auto load_dso_from_override_dirs (std::string_view const& name, int dl_flags, bool is_jni) noexcept -> void*;
		static auto lookup_system_property (const char *name, size_t &value_len) noexcept -> const char*;
#if defined (DEBUG)
		static auto find_bundled_property (const char *name) noexcept -> BundledProperty*;
#endif
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
			int length = snprintf (
				buffer,
				buffer_size,
				"%s/%.*s/%.*s",
				home.get_cstr (),
				static_cast<int>(Constants::OVERRIDE_DIRECTORY_NAME.length ()),
				Constants::OVERRIDE_DIRECTORY_NAME.data (),
				static_cast<int>(Constants::android_lib_abi.length ()),
				Constants::android_lib_abi.data ()
			);
			abort_unless (length >= 0, "Failed to format primary override directory path");
			size_t required_capacity = Helpers::add_with_overflow_check<size_t> (static_cast<size_t>(length), 1uz);
			abort_unless (required_capacity <= static_cast<size_t>(std::numeric_limits<ssize_t>::max ()), "Primary override directory path is too long");
			if (buffer == nullptr || buffer_size < required_capacity) {
				return -static_cast<ssize_t>(required_capacity);
			}
			return static_cast<ssize_t>(length);
		}

#if !defined (XA_HOST_NATIVEAOT)
		static auto determine_primary_override_dir (jstring_wrapper &home) noexcept -> std::string
		{
			char stack_buffer [Constants::SENSIBLE_PATH_MAX];
			char *name = stack_buffer;
			ssize_t result = format_primary_override_dir (home, name, sizeof (stack_buffer));
			if (result < 0) {
				size_t required_capacity = static_cast<size_t>(-result);
				name = static_cast<char*> (std::malloc (required_capacity));
				abort_unless (name != nullptr, "Failed to allocate primary override directory path");
				result = format_primary_override_dir (home, name, required_capacity);
			}
			abort_unless (result >= 0, "Failed to format primary override directory path using the required capacity");

			std::string path { name, static_cast<size_t>(result) };
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
		static inline BundledProperty *bundled_properties = nullptr;
#endif
#endif
	};
}
