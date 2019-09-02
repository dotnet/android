// Dear Emacs, this is a -*- C++ -*- header
#ifndef __MONODROID_GLUE_INTERNAL_H
#define __MONODROID_GLUE_INTERNAL_H

#include <jni.h>
#include "android-system.hh"
#include "osbridge.hh"

namespace xamarin::android::internal
{
	extern char *primary_override_dir;
	extern char *external_override_dir;
	extern char *external_legacy_override_dir;
	extern char *runtime_libdir;

	class MonodroidRuntime
	{
	};
}
#endif
