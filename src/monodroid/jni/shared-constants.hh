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
#if ANDROID || LINUX
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
	};
}
#endif // __SHARED_CONSTANTS_HH
