#pragma once

#include <string>
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
			// std::string is needed because we must pass a NUL-terminated string to Java, otherwise
			// strange things happen (and std::string_view is not necessarily such a string)
			const std::string lib_name { undecorated_lib_name };
			log_debug (LOG_ASSEMBLY, "Undecorated library name: {}", lib_name);

			jstring java_lib_name = jni_env->NewStringUTF (lib_name.c_str ());
			if (java_lib_name == nullptr) [[unlikely]] {
				// It's an OOM, there's nothing better we can do
				Helpers::abort_application ("Java string allocation failed while loading a shared library.");
			}
			jni_env->CallStaticVoidMethod (systemKlass, System_loadLibrary, java_lib_name);
			if (jni_env->ExceptionCheck ()) {
				log_debug (LOG_ASSEMBLY, "System.loadLibrary threw a Java exception. Will attempt to log it.");
				jni_env->ExceptionDescribe ();
				jni_env->ExceptionClear ();
				log_debug (LOG_ASSEMBLY, "Java exception cleared");
				return false;
			}

			return true;
		}

	private:
		static inline jmethodID System_loadLibrary = nullptr;
		static inline jclass systemKlass = nullptr;
	};
}
