#ifndef INC_JAVA_INTEROP_MONO_H
#define INC_JAVA_INTEROP_MONO_H

#include "java-interop.h"

#if defined (ANDROID)

	#include "dylib-mono.h"
	#include "monodroid-glue.h"

	#define mono_class_from_mono_type               (mono.mono_class_from_mono_type)
	#define mono_class_from_name                    (mono.mono_class_from_name)
	#define mono_class_get_field_from_name          (mono.mono_class_get_field_from_name)
	#define mono_class_get_name                     (mono.mono_class_get_name)
	#define mono_class_get_namespace                (mono.mono_class_get_namespace)
	#define mono_class_is_subclass_of               (mono.mono_class_is_subclass_of)
	#define mono_class_vtable                       (mono.mono_class_vtable)
	#define mono_domain_foreach                     (mono.mono_domain_foreach)
	#define mono_domain_get                         (mono.mono_domain_get)
	#define mono_field_get_value                    (mono.mono_field_get_value)
	#define mono_field_set_value                    (mono.mono_field_set_value)
	#define mono_field_static_set_value             (mono.mono_field_static_set_value)
	#define mono_object_get_class                   (mono.mono_object_get_class)
	#define mono_thread_attach                      (mono.mono_thread_attach)
	#define mono_gc_register_bridge_callbacks       (mono.mono_gc_register_bridge_callbacks)
	#define mono_gc_wait_for_bridge_processing      (mono.mono_gc_wait_for_bridge_processing)

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
