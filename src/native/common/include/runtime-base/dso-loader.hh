#pragma once

#include <jni.h>
#include <dlfcn.h>
#include <android/dlext.h>

#include <string_view>

#include <runtime-base/android-system.hh>
#include <runtime-base/util.hh>
#include <shared/helpers.hh>

namespace xamarin::android {
	class DsoLoader
	{
	public:
		[[gnu::flatten]]
		static void init (JNIEnv *env, jclass systemClass)
		{
			jni_env = env;
			systemKlass = systemClass;
			System_loadLibrary = env->GetMethodID (systemClass, "loadLibrary", "(Ljava/lang/String;)V");
			if (System_loadLibrary == nullptr) [[unlikely]] {
				Helpers::abort_application ("Failed to look up the Java System.loadLibrary method.");
			}
		}

		// Overload used to load libraries from the file system.
		template<bool SkipExistsCheck = false>
		[[gnu::always_inline, gnu::flatten]]
		static auto load (std::string_view const& path, int flags, bool is_jni) -> void*
		{
			if (is_jni) {
				return load_jni (path, true /* name_is_path */);
			}

			log_info (LOG_ASSEMBLY, "Trying to load shared library '{}'", path);
			if constexpr (!SkipExistsCheck) {
				if (!AndroidSystem::is_embedded_dso_mode_enabled () && !Util::file_exists (path)) {
					log_info (LOG_ASSEMBLY, "Shared library '{}' not found", path);
					return nullptr;
				}
			}

			return log_and_return (dlopen (path.data (), flags), path);
		}

		// Overload used to load libraries from the APK.
		[[gnu::always_inline, gnu::flatten]]
		static auto load (int fd, off64_t offset, std::string_view const& name, int flags, bool is_jni) -> void*
		{
			if (is_jni) {
				return load_jni (name, true /* name_is_path */);
			}

			android_dlextinfo dli;
			dli.flags = ANDROID_DLEXT_USE_LIBRARY_FD | ANDROID_DLEXT_USE_LIBRARY_FD_OFFSET;
			dli.library_fd = fd;
			dli.library_fd_offset = offset;

			return log_and_return (android_dlopen_ext (name.data (), flags, &dli), name);
		}

	private:
		[[gnu::always_inline]]
		static auto log_and_return (void *handle, std::string_view const& full_name) -> void*
		{
			if (handle != nullptr) [[likely]] {
				return handle;
			}

			const char *load_error = dlerror ();
			if (load_error == nullptr) {
				load_error = "Unknown error";
			}
			log_error (
				LOG_ASSEMBLY,
				"Could not load library '{}'. {}"sv,
				full_name,
				load_error
			);

			return nullptr;
		}

		static auto load_jni (std::string_view const& name, bool name_is_path) -> void*
		{
			if (jni_env == nullptr || systemKlass == nullptr) [[unlikely]] {
				Helpers::abort_application ("DSO loader class not initialized properly."sv);
			}

			// System.loadLibrary call is going to be slow anyway, so we can spend some more time generating an
			// undecorated library name here instead of at build time. This saves us a little bit of space in
			// `libxamarin-app.so` and makes the build code less complicated.
			auto get_undecorated_name = [](std::string_view const& full_name, bool is_path) -> std::string_view {
				std::string_view name;

				if (!is_path) {
					name = full_name;
				} else {
					name = full_name; // TODO: cut the path
				}

				size_t name_start = 0;
				size_t name_end = full_name.length ();

				if (name.starts_with ("lib"sv) && name.length () > 3) {
					name_start = 3;
				}

				if (name.ends_with (".so"sv) && name.length () > 3) {
					name_end -= 3;
				}

				if (name_start >= name_end) [[unlikely]] {
					return name;
				}

				return std::move (name.substr (name_start, name.length () - name_end));
			};

			const std::string_view undecorated_lib_name = get_undecorated_name (name, name_is_path);

			jstring lib_name = jni_env->NewStringUTF (undecorated_lib_name.data ());
			jni_env->CallStaticVoidMethod (systemKlass, System_loadLibrary, lib_name);

			// This is unfortunate, but since `System.loadLibrary` doesn't return the class handle, we must get it this
			// way :(
			return log_and_return (dlopen (name.data (), RTLD_NOLOAD), name);
		}

	private:
		static inline jmethodID System_loadLibrary = nullptr;
		static inline jclass systemKlass = nullptr;
		static inline JNIEnv *jni_env = nullptr;
	};
}
