// Dear Emacs, this is a -*- C++ -*- header
#ifndef __MONODROID_GLUE_INTERNAL_H
#define __MONODROID_GLUE_INTERNAL_H

#include <jni.h>
#include "dylib-mono.h"
#include "android-system.h"
#include "osbridge.h"

namespace xamarin { namespace android { namespace internal
{
	extern char *primary_override_dir;
	extern char *external_override_dir;
	extern char *external_legacy_override_dir;
	extern char *runtime_libdir;

	class MonodroidRuntime
	{
	};
} } }
#endif
