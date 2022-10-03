#ifndef __BASIC_ANDROID_SYSTEM_HH
#define __BASIC_ANDROID_SYSTEM_HH

#include <array>
#include <cstddef>
#include <cstdint>
#include <vector>

#include "cpu-arch.hh"
#include "jni-wrappers.hh"

namespace xamarin::android::internal
{
	class BasicAndroidSystem
	{
	private:
#if defined (__clang__)
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Wc99-designator"
#endif
		// Values correspond to the CPU_KIND_* macros
		static constexpr const char* android_abi_names[CPU_KIND_X86_64+1] = {
			[0]                 = "unknown",
			[CPU_KIND_ARM]      = "armeabi-v7a",
			[CPU_KIND_ARM64]    = "arm64-v8a",
			[CPU_KIND_MIPS]     = "mips",
			[CPU_KIND_X86]      = "x86",
			[CPU_KIND_X86_64]   = "x86_64",
		};
#if defined (__clang__)
#pragma clang diagnostic pop
#endif
		static constexpr size_t ANDROID_ABI_NAMES_SIZE = sizeof(android_abi_names) / sizeof (android_abi_names[0]);
		static const char* built_for_abi_name;
		static constexpr size_t MAX_OVERRIDES = 1;

	protected:
		using ForEachApkHandler = void (BasicAndroidSystem::*) (const char *apk, size_t index, size_t apk_count, void *user_data);
		using override_dirs_array = std::array<char*, MAX_OVERRIDES>;

	public:
#ifdef ANDROID64
		static constexpr char SYSTEM_LIB_PATH[] = "/system/lib64";
#elif ANDROID
		static constexpr char SYSTEM_LIB_PATH[] = "/system/lib";
#elif LINUX_FLATPAK
		static constexpr char SYSTEM_LIB_PATH[] = "/app/lib/mono";
#elif defined (__linux__) || defined (__linux)
		static constexpr char SYSTEM_LIB_PATH[] = "/usr/lib";
#elif APPLE_OS_X
		static constexpr char SYSTEM_LIB_PATH[] = "/Library/Frameworks/Xamarin.Android.framework/Versions/Current/lib/xamarin.android/xbuild/Xamarin/Android/lib/host-Darwin";
#elif WINDOWS
		static const char *SYSTEM_LIB_PATH;
#else
		static constexpr char SYSTEM_LIB_PATH[] = "";
#endif

		static const char* get_built_for_abi_name ();

	public:
		void setup_app_library_directories (jstring_array_wrapper& runtimeApks, jstring_array_wrapper& appDirs, bool have_split_apks);

		const char* get_override_dir (size_t index) const noexcept
		{
			if (index >= MAX_OVERRIDES)
				return nullptr;

			return _override_dirs [index];
		}

		void set_override_dir (uint32_t index, const char* dir) noexcept
		{
			if (index >= MAX_OVERRIDES)
				return;

			_override_dirs [index] = const_cast <char*> (dir);
		}

		bool is_embedded_dso_mode_enabled () const
		{
			return embedded_dso_mode_enabled;
		}

		void detect_embedded_dso_mode (jstring_array_wrapper& appDirs) noexcept;

		char *get_runtime_libdir () const
		{
			return runtime_libdir;
		}

		void set_runtime_libdir (char *dir)
		{
			runtime_libdir = dir;
		}

		char *get_primary_override_dir () const
		{
			return primary_override_dir;
		}

		void set_primary_override_dir (jstring_wrapper& home)
		{
			primary_override_dir = determine_primary_override_dir (home);
		}

		override_dirs_array& override_dirs () noexcept
		{
			return _override_dirs;
		}

		std::vector<char*>& app_lib_directories () noexcept
		{
			return _app_lib_directories;
		}

	protected:
		void  for_each_apk (jstring_array_wrapper &runtimeApks, ForEachApkHandler handler, void *user_data);

	private:
		void add_apk_libdir (const char *apk, size_t &index, const char *abi) noexcept;
		void setup_apk_directories (unsigned short running_on_cpu, jstring_array_wrapper &runtimeApks, bool have_split_apks) noexcept;
		char* determine_primary_override_dir (jstring_wrapper &home);

		void set_embedded_dso_mode_enabled (bool yesno) noexcept
		{
			embedded_dso_mode_enabled = yesno;
		}

	private:
		bool  embedded_dso_mode_enabled = false;
		char *runtime_libdir = nullptr;
		char *primary_override_dir = nullptr;
		std::vector<char*> _app_lib_directories;
		override_dirs_array _override_dirs;
	};
}
#endif // !__BASIC_ANDROID_SYSTEM_HH
