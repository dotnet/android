#pragma once

#include <cstddef>
#include <cstdint>
#include <span>

#include <jni.h>

namespace xamarin::android {
	class AppBootstrapProperties final
	{
	public:
		struct Entry
		{
			const char *name;
			const char *value;
		};

		static void initialize (JNIEnv *env) noexcept;
		static auto entries () noexcept -> std::span<const Entry> { return { property_entries, property_count }; }
		static auto lookup (const char *name, size_t &value_length) noexcept -> const char*;
		static auto remapping_data () noexcept -> std::span<const uint8_t> { return { remapping_bytes, remapping_length }; }
		static auto copy_java_string (JNIEnv *env, jstring value) noexcept -> char*;

	private:
		static inline Entry *property_entries = nullptr;
		static inline size_t property_count = 0;
		static inline uint8_t *remapping_bytes = nullptr;
		static inline size_t remapping_length = 0;
		static inline bool initialized = false;
	};
}
