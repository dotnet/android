#if !defined (__BUILD_INFO_HH)
#define __BUILD_INFO_HH

#include <string_view>

#if defined (ANDROID)
#include <android/ndk-version.h>
#endif // def ANDROID

namespace xamarin::android::internal
{
#define STRINGIFY(_val_) #_val_
#define API_STRING(_api_) STRINGIFY(_api_)
#define VERSION_STRING(_major_, _minor_, _build_) STRINGIFY(_major_) "." STRINGIFY(_minor_) "." STRINGIFY(_build_)

	class BuildInfo final
	{
	public:
		static constexpr std::string_view xa_version { XA_VERSION };
		static constexpr std::string_view date { __DATE__ };

		static constexpr std::string_view kind {
#if defined (DEBUG)
			"Debug" };
#else // ndef DEBUG
			"Release" };
#endif

		static constexpr std::string_view architecture {
#if defined (__aarch64__)
			"ARM64" };
#elif defined (__arm__)
			"ARM32" };
#elif defined (__amd64__) || defined (__x86_64__)
			"X86_64" };
#elif defined (__i386__)
			"X86" };
#endif

		static constexpr std::string_view ndk_version {
#if defined (ANDROID)
		VERSION_STRING (__NDK_MAJOR__, __NDK_MINOR__, __NDK_BUILD__) };
#else // def ANDROID
		"" };
#endif // // ndef ANDROID

		static constexpr std::string_view ndk_api_level {
#if defined (__ANDROID_API__)
		API_STRING(__ANDROID_API__) };
#else // def __ANDROID_API__
		"" };
#endif // ndef __ANDROID_API__
	};
}
#endif // ndef __BUILD_INFO_HH
