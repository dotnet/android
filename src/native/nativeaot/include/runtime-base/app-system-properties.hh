#pragma once

#include <jni.h>

#include <cstddef>
#include <span>

namespace xamarin::android {
	class AppSystemProperties
	{
	public:
		struct Entry {
			const char *name;
			const char *value;
		};

		static void initialize (JNIEnv *env) noexcept;
		static auto lookup (const char *name, size_t &value_length) noexcept -> const char*;

		static auto entries () noexcept -> std::span<const Entry>
		{
			return { property_entries, property_count };
		}

	private:
		static auto copy_java_string (JNIEnv *env, jstring value) noexcept -> char*;

		// Initialized once in JNI_OnLoad and retained for the lifetime of the process.
		static inline Entry *property_entries = nullptr;
		static inline size_t property_count = 0;
		static inline bool initialized = false;
	};
}
