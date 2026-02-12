//
// Simple implementation of std::terminate() for .NET for Android
//
//  Does NOT support terminate handlers, since we don't use them.
//
#include <cstdlib>
#include <android/log.h>

#include <shared/helpers.hh>

namespace std {
       [[noreturn]] void
       terminate () noexcept
       {
               xamarin::android::Helpers::abort_application ("std::terminate() called. Aborting.");
       }
}
