#include <managed-interface.hh>

using namespace xamarin::android;

// The post-ILC remapping object supplies a strong definition when remapping is enabled.
extern "C" [[gnu::weak]] extern const JniRemappingData jni_remapping_data {};
