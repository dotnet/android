#pragma once

#include <cstdlib>
#include <cstring>
#include <string_view>

#include <jni.h>

#include <shared/helpers.hh>
#include <runtime-base/logger.hh>

namespace xamarin::android {
	class SystemLoadLibraryWrapper
	{
	public:
		[[gnu::flatten]]
		static void init (JNIEnv *env, jclass systemClass) noexcept
		{
			systemKlass = systemClass;
			System_loadLibrary = env->GetStaticMethodID (systemClass, "loadLibrary", "(Ljava/lang/String;)V");
			if (System_loadLibrary == nullptr) [[unlikely]] {
				Helpers::abort_application ("Failed to look up the Java System.loadLibrary method.");
			}
		}

		[[gnu::flatten]]
		static auto load (JNIEnv *jni_env, std::string_view const& undecorated_lib_name) noexcept -> bool
		{
			if (systemKlass == nullptr) [[unlikely]] {
				Helpers::abort_application ("System.loadeLibrary wrapper class not initialized properly."sv);
			}

			// We must pass a NUL-terminated string to Java, otherwise strange things happen, and a
			// `std::string_view` is not necessarily such a string. Library names are short, so the copy
			// will practically always fit in the stack buffer.
			constexpr size_t StackBufferSize = 256uz;

			char stack_buffer[StackBufferSize];
			size_t needed_size = Helpers::add_with_overflow_check<size_t> (undecorated_lib_name.length (), 1uz);
			char *lib_name = stack_buffer;

			if (needed_size > StackBufferSize) [[unlikely]] {
				lib_name = static_cast<char*> (std::malloc (needed_size));
				if (lib_name == nullptr) [[unlikely]] {
					Helpers::abort_application ("Unable to allocate memory for the shared library name."sv);
				}
			}

			std::memcpy (lib_name, undecorated_lib_name.data (), undecorated_lib_name.length ());
			lib_name[undecorated_lib_name.length ()] = '\0';

			bool ret = load (jni_env, lib_name);

			if (lib_name != stack_buffer) {
				std::free (lib_name);
			}

			return ret;
		}

	private:
		static auto load (JNIEnv *jni_env, const char *lib_name) noexcept -> bool
		{
			log_debugf (LOG_ASSEMBLY, "Undecorated library name: %s", lib_name);

			jstring java_lib_name = jni_env->NewStringUTF (lib_name);
			if (java_lib_name == nullptr) [[unlikely]] {
				// It's an OOM, there's nothing better we can do
				Helpers::abort_application ("Java string allocation failed while loading a shared library.");
			}
			jni_env->CallStaticVoidMethod (systemKlass, System_loadLibrary, java_lib_name);
			if (jni_env->ExceptionCheck ()) {
				log_debugf (LOG_ASSEMBLY, "System.loadLibrary threw a Java exception. Will attempt to log it.");
				jni_env->ExceptionDescribe ();
				jni_env->ExceptionClear ();
				log_debugf (LOG_ASSEMBLY, "Java exception cleared");
				return false;
			}

			return true;
		}

		static inline jmethodID System_loadLibrary = nullptr;
		static inline jclass systemKlass = nullptr;
	};
}
