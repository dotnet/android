#ifndef INC_JAVA_INTEROP_JVM_H
#define INC_JAVA_INTEROP_JVM_H

#include "java-interop.h"

typedef void JavaVM;

typedef int (*java_interop_JNI_CreateJavaVM_fptr) (JavaVM **p_vm, void **p_env, void *vm_args);
typedef int (*java_interop_JNI_GetCreatedJavaVMs_fptr) (JavaVM **vmBuf, int bufLen, int *nVMs);

/* NOTE: structure members MUST NOT CHANGE ORDER. */
struct DylibJVM {
	void						       *dl_handle;
	java_interop_JNI_CreateJavaVM_fptr			JNI_CreateJavaVM;
	java_interop_JNI_GetCreatedJavaVMs_fptr			JNI_GetCreatedJavaVMs;
};

JAVA_INTEROP_BEGIN_DECLS

MONO_API    int java_interop_jvm_load (const char *path);
MONO_API    int java_interop_jvm_create (JavaVM **p_vm, void **p_env, void *vm_args);
MONO_API    int java_interop_jvm_list (JavaVM **vmBuf, int bufLen, int *nVMs);

JAVA_INTEROP_END_DECLS


#endif /* ndef INC_JAVA_INTEROP_JVM_H */
