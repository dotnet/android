#include <managed-interface.hh>

// Runtime-remapping builds provide a strong definition from their generated LLVM module.
extern "C" {
	[[gnu::weak]] extern const xamarin::android::JniRemappingData jni_remapping_data {};
}
