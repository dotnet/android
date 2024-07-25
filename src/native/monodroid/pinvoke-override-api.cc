#include <compare>
#include <unistd.h>
#include <stdarg.h>
#include <mono/utils/mono-publib.h>
#include <mono/utils/mono-dl-fallback.h>

#include "globals.hh"
#include "monodroid-dl.hh"
#include "monodroid-glue.hh"
#include "monodroid-glue-internal.hh"
#include "timing.hh"
#include "java-interop.h"
#include "cpu-arch.hh"
#include "xxhash.hh"
#include "startup-aware-lock.hh"
#include "jni-remapping.hh"
#include "internal-pinvokes.hh"

using namespace xamarin::android;
using namespace xamarin::android::internal;
