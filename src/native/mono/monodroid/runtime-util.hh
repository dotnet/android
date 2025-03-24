#if !defined(RUNTIME_UTIL_HH)
#define RUNTIME_UTIL_HH

#include <jni.h>

namespace xamarin::android::internal {
	class RuntimeUtil
	{
	public:
		static jclass get_class_from_runtime_field (JNIEnv *env, jclass runtime, const char *name, bool make_gref);
	};
}
#endif // ndef RUNTIME_UTIL_HH
