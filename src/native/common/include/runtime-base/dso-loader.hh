#pragma once

#include <jni.h>
#include <dlfcn.h>
#include <unistd.h>

#include <android/dlext.h>
#include <android/looper.h>

#include <string_view>

#include <runtime-base/android-system.hh>
#include <runtime-base/mainthread-dso-loader.hh>
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
		static void init (JNIEnv *env, jclass systemClass, ALooper *main_looper, pid_t _main_thread_id) noexcept
		{
			SystemLoadLibraryWrapper::init (env, systemClass);
			MainThreadDsoLoader::init (main_looper);
			main_thread_id = _main_thread_id;
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
		static auto load_jni_on_main_thread (std::string_view const& full_name, std::string const& undecorated_name) noexcept -> void*;

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
			// `libxamarin-app.so` and makes the build code less complicated.
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

			// So, we have a rather nasty problem here. If we're on a thread other than the main one (or, to be more
			// precise - one not created by Java), we will NOT have the special class loader Android uses in JVM and
			// which knows about the special application-specific .so paths (like the one inside the APK itself). For
			// that reason, `System.loadLibrary` will not be able to find the requested .so and we can't pass it a full
			// path to it, since it accepts only the undecorated library name.
			// We have to call `System.loadLibrary` on the main thread, so that the special class loader is available to
			// it. At the same time, we have to do it synchronously, because we must be able to get the library handle
			// **here**. We could call to a Java function here, but then synchronization might be an issue. So, instead,
			// we use a wrapper around System.loadLibrary that uses the ALooper native Android interface. It's a bit
			// clunky (as it requires using a fake pipe(2) to force the looper to call us on the main thread) but it
			// should work across all the Android versions.

			// TODO: implement the above
			if (gettid () == main_thread_id) {
				if (!SystemLoadLibraryWrapper::load (get_jnienv (), get_undecorated_name (name, name_is_path))) {
					// We could abort, but let's let the managed land react to this library missing. We cannot continue
					// with `dlopen` below, because without `JNI_OnLoad` etc invoked, we might have nasty crashes in the
					// library code if e.g. it assumes that `JNI_OnLoad` initialized all the Java class, method etc
					// pointers.
					return nullptr;
				}
			} else {
				Helpers::abort_application ("Loading DSO on the main thread not implemented yet"sv);
			}

			// This is unfortunate, but since `System.loadLibrary` doesn't return the class handle, we must get it this
			// way :(
			// We must use full name of the library, because dlopen won't accept an undecorated one without kicking up
			// a fuss.
			log_debug (LOG_ASSEMBLY, "Attempting to get library {} handle after System.loadLibrary. Will try to load using '{}'", name, get_file_name (name, name_is_path));
			return log_and_return (dlopen (get_file_name (name, name_is_path).data (), RTLD_NOLOAD), name);
		}

	private:
		static inline jmethodID System_loadLibrary = nullptr;
		static inline jclass systemKlass = nullptr;
		static inline pid_t main_thread_id = 0;
	};
}
