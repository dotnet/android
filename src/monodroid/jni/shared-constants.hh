#ifndef __SHARED_CONSTANTS_HH
#define __SHARED_CONSTANTS_HH

namespace xamarin::android::internal
{
// _WIN32 is defined with _WIN64 so _WIN64 must be checked first.
#if __SIZEOF_POINTER__ == 8 || defined (_WIN64)
#define __BITNESS__ "64bit"
#elif __SIZEOF_POINTER__ == 4 || defined (_WIN32)
#define __BITNESS__ "32bit"
#else
#error Unknown pointer size for this platform
#endif

	class SharedConstants
	{
	public:
		static constexpr char MONO_ANDROID_ASSEMBLY_NAME[] = "Mono.Android";
		static constexpr char JAVA_INTEROP_ASSEMBLY_NAME[] = "Java.Interop";

		static constexpr char ANDROID_RUNTIME_NS_NAME[] = "Android.Runtime";
		static constexpr char JNIENV_CLASS_NAME[] = "JNIEnv";
		static constexpr char ANDROID_ENVIRONMENT_CLASS_NAME[] = "AndroidEnvironment";

		static constexpr char DLL_EXTENSION[] = ".dll";

#if defined (NET6)
		static constexpr char RUNTIME_CONFIG_BLOB_NAME[] = "rc.bin";
#endif // def NET6

#if defined (ANDROID) || defined (__linux__) || defined (__linux)
		static constexpr char MONO_SGEN_SO[]      = "libmonosgen-2.0.so";
		static constexpr char MONO_SGEN_ARCH_SO[] = "libmonosgen-" __BITNESS__ "-2.0.so";
#elif APPLE_OS_X
		static constexpr char MONO_SGEN_SO[]      = "libmonosgen-2.0.dylib";
		static constexpr char MONO_SGEN_ARCH_SO[] = "libmonosgen-" __BITNESS__ "-2.0.dylib";
#elif WINDOWS
		static constexpr char MONO_SGEN_SO[]      = "libmonosgen-2.0.dll";
		static constexpr char MONO_SGEN_ARCH_SO[] = "libmonosgen-" __BITNESS__ "-2.0.dll";
#else
		static constexpr char MONO_SGEN_SO[]      = "monosgen-2.0";
		static constexpr char MONO_SGEN_ARCH_SO[] = "monosgen-" __BITNESS__ "-2.0";
#endif

#if __arm__
		static constexpr char android_abi[] = "armeabi_v7a";
#elif __aarch64__
		static constexpr char android_abi[] = "arm64_v8a";
#elif __x86_64__
		static constexpr char android_abi[] = "x86_64";
#elif __i386__
		static constexpr char android_abi[] = "x86";
#endif
	};
}
#endif // __SHARED_CONSTANTS_HH
