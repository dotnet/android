// This is a -*- C++ -*- header
#ifndef __CPP_COMPAT_H
#define __CPP_COMPAT_H

#include <pthread.h>

#undef HAVE_WORKING_MUTEX

// On desktop builds we can include the actual C++ standard library files which declare type traits
// as well as the `lock_guard` and `mutex` classes. However, some versions of MinGW, even though
// they have the required files, do not declare `mutex` because the `gthreads` feature is not
// enabled. Thus the complicated `#if` below.
#if (!defined (WINDOWS) || (defined (WINDOWS) && defined (_GLIBCXX_HAS_GTHREADS)))
#define HAVE_WORKING_MUTEX 1
#endif

#include <type_traits>
#include <mutex> // Also declares `lock_guard` even if it doesn't declare `mutex`

#endif
