#ifndef __PLATFORM_COMPAT_HH
#define __PLATFORM_COMPAT_HH

#include <fcntl.h>

static inline constexpr int DEFAULT_DIRECTORY_MODE = S_IRWXU | S_IRGRP | S_IXGRP | S_IROTH | S_IXOTH;

#define force_inline inline __attribute__((always_inline))
#define never_inline __attribute__((noinline))

#endif // __PLATFORM_COMPAT_HH
