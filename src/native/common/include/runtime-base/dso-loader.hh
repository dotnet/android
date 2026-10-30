#pragma once

#include <jni.h>
#include <dlfcn.h>

#include <android/dlext.h>

#include <string_view>

#include <runtime-base/android-system.hh>
#include <runtime-base/runtime-environment.hh>
#include <runtime-base/system-loadlibrary-wrapper.hh>
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
		static void init (JNIEnv *env, jclass helperClass) noexcept
		{
			SystemLoadLibraryWrapper::init (env, helperClass);
		}

		// Overload used to load libraries from the file system.
		template<bool SkipExistsCheck = false>
		[[gnu::always_inline, gnu::flatten]]
		static auto load (std::string_view const& path, int flags, bool is_jni) -> void*
		{
			if (is_jni) {
				return load_jni (path, true /* name_is_path */);
			}

			log_infof (LOG_ASSEMBLY, "[filesystem] Trying to load shared library '%.*s'", static_cast<int>(path.length ()), path.data ());
			if constexpr (!SkipExistsCheck) {
				if (!AndroidSystem::is_embedded_dso_mode_enabled () && !Util::file_exists (path)) {
					log_infof (LOG_ASSEMBLY, "Shared library '%.*s' not found", static_cast<int>(path.length ()), path.data ());
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

			log_infof (LOG_ASSEMBLY, "[apk] Trying to load shared library '%.*s', offset in the apk == %lld", static_cast<int>(name.length ()), name.data (), static_cast<long long>(offset));

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
				log_debugf (LOG_ASSEMBLY, "Shared library %.*s loaded (handle %p)", static_cast<int>(full_name.length ()), full_name.data (), handle);
				return handle;
			}

			const char *load_error = dlerror ();
			if (load_error == nullptr) {
				load_error = "Unknown error";
			}
			log_errorf (
				LOG_ASSEMBLY,
				"Could not load library '%.*s'. %s",
				static_cast<int>(full_name.length ()),
				full_name.data (),
				load_error
			);

			return nullptr;
		}

		static auto load_jni (std::string_view const& name, bool name_is_path) -> void*
		{
			log_debugf (LOG_ASSEMBLY, "Trying to load shared JNI library %.*s with System.loadLibrary", static_cast<int>(name.length ()), name.data ());

			auto get_file_name = [](std::string_view const& full_name, bool is_path) -> std::string_view {
				if (!is_path) {
					return full_name;
				}

				std::string_view name = full_name;
				size_t last_slash = name.find_last_of ('/');
				if (last_slash != std::string_view::npos) [[likely]] {
					last_slash++;
					if (last_slash <= name.length ()) {
						name.remove_prefix (last_slash);
					}
				}

				return name;
			};

			// System.loadLibrary call is going to be slow anyway, so we can spend some more time generating an
			// undecorated library name here instead of at build time. This saves us a little bit of space in
			// the bootstrap blob and makes the build code less complicated.
			auto get_undecorated_name = [&get_file_name](std::string_view const& full_name, bool is_path) -> std::string_view {
				std::string_view name;

				if (!is_path) {
					name = full_name;
				} else {
					name = get_file_name (full_name, is_path);
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

			// The Java helper supplies the application class loader even on an attached native thread.
			// ART runs JNI_OnLoad synchronously with that loader and handles recursive loads itself.
			if (!SystemLoadLibraryWrapper::load (RuntimeEnvironment::get_jnienv (), get_undecorated_name (name, name_is_path))) {
				// Do not return a handle to a library whose JNI initialization failed.
				return nullptr;
			}

			// This is unfortunate, but since `System.loadLibrary` doesn't return the class handle, we must get it this
			// way :(
			// We must use full name of the library, because dlopen won't accept an undecorated one without kicking up
			// a fuss.
			auto resolved_file_name = get_file_name (name, name_is_path);
			log_debugf (LOG_ASSEMBLY, "Attempting to get library %.*s handle after System.loadLibrary. Will try to load using '%.*s'", static_cast<int>(name.length ()), name.data (), static_cast<int>(resolved_file_name.length ()), resolved_file_name.data ());
			return log_and_return (dlopen (get_file_name (name, name_is_path).data (), RTLD_NOLOAD), name);
		}
	};
}
