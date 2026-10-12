#include <cerrno>
#include <cstdlib>
#include <cstring>

#include <runtime-base/app-system-properties.hh>
#include <runtime-base/binary-blob-loader.hh>
#include <shared/helpers.hh>

using namespace xamarin::android;

extern "C" {
#if defined (DYNAMIC_RUNTIME_LINKING)
	extern const uint8_t nativeaot_bootstrap[] __attribute__((visibility("hidden")));
	extern const uint32_t nativeaot_bootstrap_size __attribute__((visibility("hidden")));
#else
	extern const uint8_t nativeaot_bootstrap[] __attribute__((weak, visibility("hidden")));
	extern const uint32_t nativeaot_bootstrap_size __attribute__((weak, visibility("hidden")));
#endif
	extern const uint8_t remapping_data[] __attribute__((weak, visibility("hidden")));
	extern const uint32_t remapping_data_size __attribute__((weak, visibility("hidden")));
}

// Selected by the application link's DT_INIT, before its NativeAOT/C++ .init_array.
// Dependencies have already run their constructors; this routine must not need JNI or managed code.
extern "C" __attribute__((visibility("hidden"))) void XA_Host_NativeAOT_InitializeEnvironment () noexcept
{
	static bool initialized = false;
	if (initialized) {
		return;
	}
#if !defined (DYNAMIC_RUNTIME_LINKING)
	if (nativeaot_bootstrap == nullptr || &nativeaot_bootstrap_size == nullptr) {
		Helpers::abort_application ("Missing required NativeAOT bootstrap symbols");
	}
#endif
	AppSystemProperties::initialize (BinaryBlobLoader::decode (
		nativeaot_bootstrap, nativeaot_bootstrap_size, "nativeaot_bootstrap"));
	for (const auto &variable : AppSystemProperties::environment ()) {
		if (::setenv (variable.name, variable.value, 1) != 0) {
			Helpers::abort_applicationf (LOG_DEFAULT, std::source_location::current (),
				"Unable to set NativeAOT environment variable '%s': %s", variable.name, std::strerror (errno));
		}
	}
	initialized = true;
}

auto xamarin::android::load_nativeaot_remapping () noexcept -> BinaryBlobPayload
{
	static BinaryBlobPayload payload = {};
	static bool initialized = false;
	if (!initialized) {
		if (remapping_data != nullptr && &remapping_data_size != nullptr) {
			payload = BinaryBlobLoader::decode (remapping_data, remapping_data_size, "remapping_data");
		} else if (remapping_data != nullptr || &remapping_data_size != nullptr) {
			Helpers::abort_application ("Incomplete NativeAOT remapping symbols");
		}
		initialized = true;
	}
	return payload;
}
