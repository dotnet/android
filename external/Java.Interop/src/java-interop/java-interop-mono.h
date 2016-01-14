#ifndef INC_JAVA_INTEROP_MONO_H
#define INC_JAVA_INTEROP_MONO_H

#include "java-interop.h"

#if defined (ANDROID)

	#include "dylib-mono.h"
	#include "monodroid-glue.h"

	#define mono_class_from_name    (monodroid_get_dylib ()->mono_class_from_name)
	#define mono_thread_attach      (monodroid_get_dylib ()->mono_thread_attach)

#else   /* !defined (ANDROID) */

	#include <mono/metadata/assembly.h>
	#include <mono/metadata/class.h>
	#include <mono/metadata/object.h>
	#include <mono/metadata/sgen-bridge.h>
	#include <mono/metadata/threads.h>
	#include <mono/utils/mono-counters.h>
	#include <mono/utils/mono-dl-fallback.h>

#endif  /* !defined (ANDROID) */

JAVA_INTEROP_BEGIN_DECLS

JAVA_INTEROP_END_DECLS

#endif /* ndef INC_JAVA_INTEROP_MONO_H */
