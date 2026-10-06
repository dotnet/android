#pragma once

#include <cstddef>
#include <cstdint>
#include <span>

#include <jni.h>

namespace xamarin::android {
	class JavaAppConfig final
	{
	public:
		struct Entry
		{
			const char *name;
			const char *value;
		};

		struct Library
		{
			const char *name;
			bool is_jni_library;
			bool preload;
			void *handle;
		};

		static void initialize (JNIEnv *env) noexcept;
		static auto package_name () noexcept -> const char* { return android_package_name; }
		static auto package_naming_policy () noexcept -> uint32_t { return naming_policy; }
		static auto have_assembly_store () noexcept -> bool { return assembly_store_enabled; }
		static auto ignore_split_configs () noexcept -> bool { return split_configs_ignored; }
		static auto environment () noexcept -> std::span<const Entry> { return { environment_entries, environment_count }; }
		static auto system_properties () noexcept -> std::span<const Entry> { return { system_property_entries, system_property_count }; }
		static auto libraries () noexcept -> std::span<Library> { return { native_libraries, native_library_count }; }
		static auto runtime_property_names () noexcept -> const char** { return property_names; }
		static auto runtime_property_values () noexcept -> char** { return property_values; }
		static auto runtime_property_count () noexcept -> int { return property_count; }
		static auto lookup_system_property (const char *name, size_t &value_length) noexcept -> const char*;

	private:
		static inline bool initialized = false;
		static inline char *bootstrap_data = nullptr;
		static inline char *android_package_name = nullptr;
		static inline uint32_t naming_policy = 0;
		static inline bool assembly_store_enabled = false;
		static inline bool split_configs_ignored = false;
		static inline Entry *environment_entries = nullptr;
		static inline size_t environment_count = 0;
		static inline Entry *system_property_entries = nullptr;
		static inline size_t system_property_count = 0;
		static inline Library *native_libraries = nullptr;
		static inline size_t native_library_count = 0;
		static inline const char **property_names = nullptr;
		static inline char **property_values = nullptr;
		static inline int property_count = 0;
	};
}
