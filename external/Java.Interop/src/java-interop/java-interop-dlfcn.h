#ifndef INC_JAVA_INTEROP_DLFCN_H
#define INC_JAVA_INTEROP_DLFCN_H

#include "java-interop.h"

namespace microsoft::java_interop {

// Possible flags values for java_interop_lib_load
constexpr   unsigned int    JAVA_INTEROP_LIB_LOAD_GLOBALLY      = (1 << 0);
constexpr   unsigned int    JAVA_INTEROP_LIB_LOAD_LOCALLY       = (1 << 1);


// Possible error codes from java_interop_lib_close
constexpr   int JAVA_INTEROP_LIB_FAILED             = -1000;
constexpr   int JAVA_INTEROP_LIB_CLOSE_FAILED       = JAVA_INTEROP_LIB_FAILED-1;
constexpr   int JAVA_INTEROP_LIB_INVALID_PARAM      = JAVA_INTEROP_LIB_FAILED-2;

JAVA_INTEROP_BEGIN_DECLS

JAVA_INTEROP_API    void*   java_interop_lib_load (const char *path, unsigned int flags, char **error);
JAVA_INTEROP_API    void*   java_interop_lib_symbol (void* library, const char *symbol, char **error);
JAVA_INTEROP_API    int     java_interop_lib_close (void* library, char **error);

JAVA_INTEROP_END_DECLS

}

#endif /* INC_JAVA_INTEROP_DLFCN_H */
