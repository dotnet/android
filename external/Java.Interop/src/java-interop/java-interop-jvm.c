#include <stdlib.h>
#include <dlfcn.h>

#include "java-interop-jvm.h"
#include "java-interop-logger.h"
#include "java-interop-util.h"


typedef int (*java_interop_JNI_CreateJavaVM_fptr) (JavaVM **p_vm, void **p_env, void *vm_args);
typedef int (*java_interop_JNI_GetCreatedJavaVMs_fptr) (JavaVM **vmBuf, int bufLen, int *nVMs);

struct DylibJVM {
	void                                       *dl_handle;
	java_interop_JNI_CreateJavaVM_fptr          JNI_CreateJavaVM;
	java_interop_JNI_GetCreatedJavaVMs_fptr     JNI_GetCreatedJavaVMs;
};

static struct DylibJVM *jvm;

int
java_interop_jvm_load (const char *path)
{
	if (jvm != NULL) {
		return JAVA_INTEROP_JVM_FAILED_ALREADY_LOADED;
	}

	jvm = calloc (1, sizeof (struct DylibJVM));
	if (!jvm) {
		return JAVA_INTEROP_JVM_FAILED_OOM;
	}

	jvm->dl_handle = dlopen (path, RTLD_LAZY);
	if (!jvm->dl_handle) {
		free (jvm);
		jvm = NULL;
		return JAVA_INTEROP_JVM_FAILED_NOT_LOADED;
	}

	int symbols_missing = 0;

#define LOAD_SYMBOL(symbol) do { \
		jvm->symbol = dlsym (jvm->dl_handle, #symbol); \
		if (!jvm->symbol) { \
			log_error (LOG_DEFAULT, "Failed to load JVM symbol: %s", #symbol); \
			symbols_missing = 1; \
		} \
	} while (0)

	LOAD_SYMBOL(JNI_CreateJavaVM);
	LOAD_SYMBOL(JNI_GetCreatedJavaVMs);

#undef LOAD_SYMBOL

	if (symbols_missing) {
		free (jvm);
		jvm = NULL;
		return JAVA_INTEROP_JVM_FAILED_SYMBOL_MISSING;
	}

	return 0;
}

#define ji_return_val_if_fail(expr, val) do { if (!(expr)) return (val); } while (0)

int java_interop_jvm_create (JavaVM **p_vm, void **p_env, void *vm_args)
{
	ji_return_val_if_fail (jvm != NULL, JAVA_INTEROP_JVM_FAILED_NOT_LOADED);

	return (*jvm->JNI_CreateJavaVM) (p_vm, p_env, vm_args);
}

int java_interop_jvm_list (JavaVM **vmBuf, int bufLen, int *nVMs)
{
	ji_return_val_if_fail (jvm != NULL, JAVA_INTEROP_JVM_FAILED_NOT_LOADED);

	return (*jvm->JNI_GetCreatedJavaVMs) (vmBuf, bufLen, nVMs);
}
