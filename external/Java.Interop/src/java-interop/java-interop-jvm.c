#include <stdlib.h>
#include <dlfcn.h>

#include "java-interop-jvm.h"
#include "java-interop-logger.h"
#include "java-interop-util.h"

static struct DylibJVM *jvm = NULL;

int java_interop_jvm_load (const char *path)
{
	jvm = calloc (1, sizeof (struct DylibJVM));

	jvm->dl_handle = dlopen (path, RTLD_LAZY);

	if (!jvm->dl_handle)
		return 0;

	int symbols_missing = 0;

#define LOAD_SYMBOL(symbol) \
	jvm->symbol = dlsym (jvm->dl_handle, #symbol); \
	if (!jvm->symbol) { \
		log_error (LOG_DEFAULT, "Failed to load JVM symbol: %s", #symbol); \
		symbols_missing = 1; \
	}

	LOAD_SYMBOL(JNI_CreateJavaVM)
	LOAD_SYMBOL(JNI_GetCreatedJavaVMs)

	if (symbols_missing) {
		log_fatal (LOG_DEFAULT, "Failed to load some Mono symbols, aborting...");
		exit (FATAL_EXIT_JVM_MISSING_SYMBOLS);
	}

	return 1;
}

static inline void
_assert_dl_handle ()
{
	if (!jvm || !jvm->dl_handle) {
		log_fatal (LOG_DEFAULT, "Missing JVM symbols!");
		exit (FATAL_EXIT_JVM_MISSING_SYMBOLS);
	}
}

int java_interop_jvm_create (JavaVM **p_vm, void **p_env, void *vm_args)
{
	_assert_dl_handle ();

	return (*jvm->JNI_CreateJavaVM) (p_vm, p_env, vm_args);
}

int java_interop_jvm_list (JavaVM **vmBuf, int bufLen, int *nVMs)
{
	_assert_dl_handle ();

	return (*jvm->JNI_GetCreatedJavaVMs) (vmBuf, bufLen, nVMs);
}
