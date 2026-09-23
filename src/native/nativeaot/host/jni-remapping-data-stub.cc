#include <managed-interface.hh>

using namespace xamarin::android;

// Apps without remapping data use this empty descriptor. A later application-link step can
// override it with the strong definition generated from the application's remapping tables.
extern "C" {
	[[gnu::weak]] extern const JniRemappingData jni_remapping_data {};
}
