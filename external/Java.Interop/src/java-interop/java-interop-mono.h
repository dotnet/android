#ifndef INC_JAVA_INTEROP_MONO_H
#define INC_JAVA_INTEROP_MONO_H

#include "java-interop.h"

#if defined (XAMARIN_ANDROID_DYLIB_MONO)

	#include "dylib-mono.h"
	#include "monodroid-glue.h"

	#define mono_class_from_mono_type               (monodroid_get_dylib ()->class_from_mono_type)
	#define mono_class_from_name                    (monodroid_get_dylib ()->class_from_name)
	#define mono_class_get_field_from_name          (monodroid_get_dylib ()->class_get_field_from_name)
	#define mono_class_get_name                     (monodroid_get_dylib ()->class_get_name)
	#define mono_class_get_namespace                (monodroid_get_dylib ()->class_get_namespace)
	#define mono_class_is_subclass_of               (monodroid_get_dylib ()->class_is_subclass_of)
	#define mono_class_vtable                       (monodroid_get_dylib ()->class_vtable)
	#define mono_domain_get                         (monodroid_get_dylib ()->domain_get)
	#define mono_field_get_value                    (monodroid_get_dylib ()->field_get_value)
	#define mono_field_set_value                    (monodroid_get_dylib ()->field_set_value)
	#define mono_field_static_set_value             (monodroid_get_dylib ()->field_static_set_value)
	#define mono_object_get_class                   (monodroid_get_dylib ()->object_get_class)
	#define mono_thread_attach                      (monodroid_get_dylib ()->thread_attach)
	#define mono_thread_current                     (monodroid_get_dylib ()->thread_current)
	#define mono_gc_register_bridge_callbacks       (monodroid_get_dylib ()->gc_register_bridge_callbacks)
	#define mono_gc_wait_for_bridge_processing      (monodroid_get_dylib ()->gc_wait_for_bridge_processing)

#else   /* !defined (XAMARIN_ANDROID_DYLIB_MONO) */

	#undef MONO_API_EXPORT
	#undef MONO_API_IMPORT
	#undef MONO_API

	#include <mono/metadata/assembly.h>
	#include <mono/metadata/class.h>
	#include <mono/metadata/object.h>
	#include <mono/metadata/sgen-bridge.h>
	#include <mono/metadata/threads.h>
	#include <mono/utils/mono-counters.h>
	#include <mono/utils/mono-dl-fallback.h>

#endif  /* !defined (XAMARIN_ANDROID_DYLIB_MONO) */

JAVA_INTEROP_BEGIN_DECLS

JAVA_INTEROP_END_DECLS

#endif /* ndef INC_JAVA_INTEROP_MONO_H */
