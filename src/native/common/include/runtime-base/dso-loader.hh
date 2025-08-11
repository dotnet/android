#pragma once

#include <jni.h>
#include <dlfcn.h>
#include <android/dlext.h>

#include <string_view>

#include <runtime-base/android-system.hh>
#include <runtime-base/util.hh>
#include <shared/helpers.hh>

#if defined(XA_HOST_MONOVM)
using AndroidSystem = xamarin::android::internal::AndroidSystem;
#endif

namespace xamarin::android {
	class DsoLoader
	{
	public:
		[[gnu::flatten]]
		static void init (JNIEnv *env, jclass systemClass)
		{
			systemKlass = systemClass;
			System_loadLibrary = env->GetStaticMethodID (systemClass, "loadLibrary", "(Ljava/lang/String;)V");
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

			log_info (LOG_ASSEMBLY, "[filesystem] Trying to load shared library '{}'", path);
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

			log_info (LOG_ASSEMBLY, "[apk] Trying to load shared library '{}', offset in the apk == {}", name, offset);

			android_dlextinfo dli;
			dli.flags = ANDROID_DLEXT_USE_LIBRARY_FD | ANDROID_DLEXT_USE_LIBRARY_FD_OFFSET;
			dli.library_fd = fd;
			dli.library_fd_offset = offset;

			return log_and_return (android_dlopen_ext (name.data (), flags, &dli), name);
		}

	private:
		static auto get_jnienv () noexcept -> JNIEnv*;

		[[gnu::always_inline]]
		static auto log_and_return (void *handle, std::string_view const& full_name) -> void*
		{
			if (handle != nullptr) [[likely]] {
				log_debug (LOG_ASSEMBLY, "Shared library {} loaded (handle {:p})", full_name, handle);
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
			log_debug (LOG_ASSEMBLY, "Trying to load loading shared JNI library {} with System.loadLibrary", name);

			if (systemKlass == nullptr) [[unlikely]] {
				Helpers::abort_application ("DSO loader class not initialized properly."sv);
			}

			JNIEnv *jni_env = get_jnienv ();
			// System.loadLibrary call is going to be slow anyway, so we can spend some more time generating an
			// undecorated library name here instead of at build time. This saves us a little bit of space in
			// `libxamarin-app.so` and makes the build code less complicated.
			auto get_undecorated_name = [](std::string_view const& full_name, bool is_path) -> std::string_view {
				std::string_view name;

				if (!is_path) {
					name = full_name;
				} else {
					name = full_name;
					size_t last_slash = name.find_last_of ('/');
					if (last_slash != std::string_view::npos) [[likely]] {
						last_slash++;
						if (last_slash <= name.length ()) {
							name.remove_prefix (last_slash);
						}
					}
				}

				constexpr std::string_view lib_prefix { "lib" };
				if (name.starts_with (lib_prefix) && name.length () > 3) {
					if (lib_prefix.length () <= name.length ()) {
						name.remove_prefix (lib_prefix.length ());
					}
				}

				constexpr std::string_view lib_ext { ".so" };
				if (name.ends_with (lib_ext) && name.length () > 3) {
					if (lib_ext.length () <= name.length ()) {
						name.remove_suffix (lib_ext.length ());
					}
				}

				return name;
			};

			// std::string is needed because we must pass a NUL-terminated string to Java, otherwise
			// strange things happen (and std::string_view is not necessarily such a string)
			const std::string undecorated_lib_name { get_undecorated_name (name, name_is_path) };
			log_debug (LOG_ASSEMBLY, "Undecorated library name: {}", undecorated_lib_name);

			jstring lib_name = jni_env->NewStringUTF (undecorated_lib_name.c_str ());
			if (lib_name == nullptr) [[unlikely]] {
				// It's an OOM, there's nothing better we can do
				Helpers::abort_application ("Java string allocation failed while loading a shared library.");
			}
			jni_env->CallStaticVoidMethod (systemKlass, System_loadLibrary, lib_name);
			if (jni_env->ExceptionCheck ()) {
				log_debug (LOG_ASSEMBLY, "System.loadLibrary threw a Java exception. Will attempt to log it.");
				jni_env->ExceptionDescribe ();
				jni_env->ExceptionClear ();
				log_debug (LOG_ASSEMBLY, "Java exception cleared");
			}
			// This is unfortunate, but since `System.loadLibrary` doesn't return the class handle, we must get it this
			// way :(
			// We must use full name of the library, because dlopen won't accept an undecorated one without kicking up
			// a fuss.
			log_debug (LOG_ASSEMBLY, "Attempting to get library {} handle after System.loadLibrary", name);
			return log_and_return (dlopen (name.data (), RTLD_NOLOAD), name);
		}

	private:
		static inline jmethodID System_loadLibrary = nullptr;
		static inline jclass systemKlass = nullptr;
	};
}
