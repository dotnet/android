#pragma once

#include <cstddef>
#include <span>

#include <jni.h>

#include <xamarin-app.hh>

namespace xamarin::android {
	class JavaAppConfig final
	{
	public:
		struct Library
		{
			const char *name;
			bool is_jni_library;
			bool preload;
			void *handle;
		};

		static void initialize (JNIEnv *env) noexcept;

		static auto application () noexcept -> const ApplicationConfig& { return config; }
		static auto libraries () noexcept -> std::span<Library> { return { native_libraries, native_library_count }; }
		static auto runtime_property_names () noexcept -> const char** { return property_names; }
		static auto runtime_property_values () noexcept -> char** { return property_values; }

	private:
		static inline bool enabled = false;
		static inline ApplicationConfig config {};
		static inline Library *native_libraries = nullptr;
		static inline size_t native_library_count = 0;
		static inline const char **property_names = nullptr;
		static inline char **property_values = nullptr;
	};
}
