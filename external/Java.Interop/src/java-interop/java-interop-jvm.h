#ifndef INC_JAVA_INTEROP_JVM_H
#define INC_JAVA_INTEROP_JVM_H

#include <jni.h>

#include "java-interop.h"

JAVA_INTEROP_BEGIN_DECLS

#define JAVA_INTEROP_JVM_FAILED                 (-1000)
#define JAVA_INTEROP_JVM_FAILED_ALREADY_LOADED  (JAVA_INTEROP_JVM_FAILED-1)
#define JAVA_INTEROP_JVM_FAILED_NOT_LOADED      (JAVA_INTEROP_JVM_FAILED-2)
#define JAVA_INTEROP_JVM_FAILED_OOM             (JAVA_INTEROP_JVM_FAILED-3)
#define JAVA_INTEROP_JVM_FAILED_SYMBOL_MISSING  (JAVA_INTEROP_JVM_FAILED-4)

JAVA_INTEROP_API    int java_interop_jvm_load (const char *path);
JAVA_INTEROP_API    int java_interop_jvm_load_with_error_message (const char *path, char **error);
JAVA_INTEROP_API    int java_interop_jvm_create (JavaVM **p_vm, void **p_env, void *vm_args);
JAVA_INTEROP_API    int java_interop_jvm_list (JavaVM **vmBuf, int bufLen, int *nVMs);

JAVA_INTEROP_END_DECLS


#endif /* ndef INC_JAVA_INTEROP_JVM_H */
