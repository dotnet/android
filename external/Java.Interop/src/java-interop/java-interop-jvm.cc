#include <stdlib.h>

#include "java-interop-jvm.h"
#include "java-interop-dlfcn.h"
#include "java-interop-logger.h"
#include "java-interop-util.h"

using namespace microsoft::java_interop;

typedef int (JNICALL *java_interop_JNI_CreateJavaVM_fptr) (JavaVM **p_vm, void **p_env, void *vm_args);
typedef int (JNICALL *java_interop_JNI_GetCreatedJavaVMs_fptr) (JavaVM **vmBuf, int bufLen, int *nVMs);

struct DylibJVM {
	void                                       *dl_handle;
	java_interop_JNI_CreateJavaVM_fptr          JNI_CreateJavaVM;
	java_interop_JNI_GetCreatedJavaVMs_fptr     JNI_GetCreatedJavaVMs;
};

static struct DylibJVM *jvm;

int
java_interop_jvm_load_with_error_message (const char *path, char **error_message)
{
	if (error_message) {
		*error_message  = NULL;
	}

	if (jvm != NULL) {
		return JAVA_INTEROP_JVM_FAILED_ALREADY_LOADED;
	}

	jvm = static_cast<DylibJVM*>(calloc (1, sizeof (DylibJVM)));
	if (!jvm) {
		return JAVA_INTEROP_JVM_FAILED_OOM;
	}

	char *error    = nullptr;
	jvm->dl_handle = java_interop_lib_load (path, JAVA_INTEROP_LIB_LOAD_LOCALLY, &error);
	if (!jvm->dl_handle) {
		if (error_message) {
			*error_message = error;
			error          = nullptr;
		}
		java_interop_free (error);
		free (jvm);
		jvm = NULL;
		return JAVA_INTEROP_JVM_FAILED_NOT_LOADED;
	}

	int symbols_missing = 0;

#define LOAD_SYMBOL_CAST(symbol, Type) do { \
		error = nullptr; \
		jvm->symbol = reinterpret_cast<Type>(java_interop_lib_symbol (jvm->dl_handle, #symbol, &error)); \
		if (!jvm->symbol) { \
			log_error (LOG_DEFAULT, "Failed to load JVM symbol: %s: %s", #symbol, error); \
			symbols_missing = 1; \
			java_interop_free (error); \
			error = nullptr; \
		} \
	} while (0)
#define LOAD_SYMBOL(symbol) LOAD_SYMBOL_CAST(symbol, java_interop_ ## symbol ## _fptr)

	LOAD_SYMBOL(JNI_CreateJavaVM);
	LOAD_SYMBOL(JNI_GetCreatedJavaVMs);

#undef LOAD_SYMBOL_CAST
#undef LOAD_SYMBOL

	if (symbols_missing) {
		java_interop_lib_close (jvm->dl_handle, nullptr);
		free (jvm);
		jvm = NULL;
		return JAVA_INTEROP_JVM_FAILED_SYMBOL_MISSING;
	}

	return 0;
}

int
java_interop_jvm_load (const char *path)
{
	return java_interop_jvm_load_with_error_message (path, NULL);
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
