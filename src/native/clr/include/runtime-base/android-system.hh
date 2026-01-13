#pragma once

#include <array>
#include <limits>
#include <string>
#include <string_view>
#include <unordered_map>

#include "../constants.hh"
#include "../shared/log_types.hh"
#include "../runtime-base/cpu-arch.hh"
#include <runtime-base/jni-wrappers.hh>
#include <runtime-base/strings.hh>
#include "util.hh"

struct BundledProperty;

namespace xamarin::android {
	class AndroidSystem
	{
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

		static auto get_primary_override_dir () noexcept -> std::string const&
		{
			return primary_override_dir;
		}

		static void set_primary_override_dir (jstring_wrapper& home) noexcept
		{
			primary_override_dir = determine_primary_override_dir (home);
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
				dynamic_local_property_string value;
				if (log_categories == 0 && monodroid_get_system_property (Constants::DEBUG_MONO_PROFILE_PROPERTY, value) == 0) [[likely]] {
					return;
				}
			}

			log_debug (
				LOG_DEFAULT,
#if defined(XA_HOST_NATIVEAOT)
				"Creating public update directory: `%s`",
				override_dir.data ()
#else
				"Creating public update directory: `{}`"sv,
				override_dir
#endif
			);
			Util::create_public_directory (override_dir);
		}

		static auto is_embedded_dso_mode_enabled () noexcept -> bool
		{
			return embedded_dso_mode_enabled;
		}

		static auto monodroid_get_system_property (std::string_view const& name, dynamic_local_property_string &value) noexcept -> int;
		static void detect_embedded_dso_mode (jstring_array_wrapper& appDirs) noexcept;
		static void setup_environment () noexcept;
		static void setup_app_library_directories (jstring_array_wrapper& runtimeApks, jstring_array_wrapper& appDirs, bool have_split_apks) noexcept;
		static auto load_dso_from_any_directories (std::string_view const& name, int dl_flags, bool is_jni) noexcept -> void*;

	private:
		static auto get_full_dso_path (std::string const& base_dir, std::string_view const& dso_path, dynamic_local_string<SENSIBLE_PATH_MAX>& path) noexcept -> bool;

		template<class TContainer> // TODO: replace with a concept
		static auto load_dso_from_specified_dirs (TContainer directories, std::string_view const& dso_name, int dl_flags, bool is_jni) noexcept -> void*;
		static auto load_dso_from_app_lib_dirs (std::string_view const& name, int dl_flags, bool is_jni) noexcept -> void*;
		static auto load_dso_from_override_dirs (std::string_view const& name, int dl_flags, bool is_jni) noexcept -> void*;
		static auto lookup_system_property (std::string_view const &name, size_t &value_len) noexcept -> const char*;
		static auto monodroid__system_property_get (std::string_view const&, char *sp_value, size_t sp_value_len) noexcept -> int;
		static auto get_max_gref_count_from_system () noexcept -> long;
		static void add_apk_libdir (std::string_view const& apk, size_t &index, std::string_view const& abi) noexcept;
        static void setup_apk_directories (unsigned short running_on_cpu, jstring_array_wrapper &runtimeApks, bool have_split_apks) noexcept;
#if defined(DEBUG)
		static void add_system_property (const char *name, const char *value) noexcept;
		static void setup_environment (const char *name, const char *value) noexcept;
		static void setup_environment_from_override_file (dynamic_local_string<Constants::SENSIBLE_PATH_MAX> const& path) noexcept;
#endif

		static void set_embedded_dso_mode_enabled (bool yesno) noexcept
		{
			embedded_dso_mode_enabled = yesno;
		}

		static auto determine_primary_override_dir (jstring_wrapper &home) noexcept -> std::string
		{
			dynamic_local_string<SENSIBLE_PATH_MAX> name { home.get_cstr () };
			name.append ("/")
				.append (Constants::OVERRIDE_DIRECTORY_NAME)
				.append ("/")
				.append (Constants::android_lib_abi);

			return {name.get (), name.length ()};
		}

	private:
		static inline long max_gref_count = 0;
		static inline bool running_in_emulator = false;
		static inline bool embedded_dso_mode_enabled = false;
		static inline std::string primary_override_dir;
		static inline std::string native_libraries_dir;

#if defined (DEBUG)
		static inline std::unordered_map<std::string, std::string> bundled_properties;
#endif
	};
}
