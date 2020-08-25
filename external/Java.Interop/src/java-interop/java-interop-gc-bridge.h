#ifndef INC_JAVA_INTEROP_GC_BRIDGE_H
#define INC_JAVA_INTEROP_GC_BRIDGE_H

#include <stdio.h>
#include "java-interop.h"

#include <jni.h>

JAVA_INTEROP_BEGIN_DECLS

typedef struct  JavaInteropGCBridge     JavaInteropGCBridge;

typedef enum    JavaInteropGCBridgeUseWeakReferenceKind {
	JAVA_INTEROP_GC_BRIDGE_USE_WEAK_REFERENCE_KIND_JAVA,
	JAVA_INTEROP_GC_BRIDGE_USE_WEAK_REFERENCE_KIND_JNI,
} JavaInteropGCBridgeUseWeakReferenceKind;

struct JavaInterop_System_RuntimeTypeHandle {
	void   *value;
};

JAVA_INTEROP_API    JavaInteropGCBridge    *java_interop_gc_bridge_get_current                  (void);
JAVA_INTEROP_API    int                     java_interop_gc_bridge_set_current_once             (JavaInteropGCBridge *bridge);

JAVA_INTEROP_API    JavaInteropGCBridge    *java_interop_gc_bridge_new                          (JavaVM *jvm);
JAVA_INTEROP_API    int                     java_interop_gc_bridge_free                         (JavaInteropGCBridge *bridge);

JAVA_INTEROP_API    int                     java_interop_gc_bridge_register_hooks               (JavaInteropGCBridge *bridge, int weak_ref_kind);
JAVA_INTEROP_API    int                     java_interop_gc_bridge_wait_for_bridge_processing   (JavaInteropGCBridge *bridge);

JAVA_INTEROP_API    int                     java_interop_gc_bridge_add_current_app_domain       (JavaInteropGCBridge *bridge);
JAVA_INTEROP_API    int                     java_interop_gc_bridge_remove_current_app_domain    (JavaInteropGCBridge *bridge);

JAVA_INTEROP_API    int                     java_interop_gc_bridge_set_bridge_processing_field  (JavaInteropGCBridge *bridge,   struct JavaInterop_System_RuntimeTypeHandle type_handle,    char *field_name);
JAVA_INTEROP_API    int                     java_interop_gc_bridge_register_bridgeable_type     (JavaInteropGCBridge *bridge,   struct JavaInterop_System_RuntimeTypeHandle type_handle);
JAVA_INTEROP_API    int                     java_interop_gc_bridge_enable                       (JavaInteropGCBridge *bridge,   int enable);

JAVA_INTEROP_API    int                     java_interop_gc_bridge_get_gref_count           (JavaInteropGCBridge *bridge);
JAVA_INTEROP_API    int                     java_interop_gc_bridge_get_weak_gref_count      (JavaInteropGCBridge *bridge);

JAVA_INTEROP_API    int                     java_interop_gc_bridge_lref_set_log_file        (JavaInteropGCBridge *bridge,   const char *gref_log_file);
JAVA_INTEROP_API    FILE*                   java_interop_gc_bridge_lref_get_log_file        (JavaInteropGCBridge *bridge);
JAVA_INTEROP_API    int                     java_interop_gc_bridge_lref_set_log_level       (JavaInteropGCBridge *bridge,   int level);
JAVA_INTEROP_API    void                    java_interop_gc_bridge_lref_log_message         (JavaInteropGCBridge *bridge,   int level, const char *message);
JAVA_INTEROP_API    void                    java_interop_gc_bridge_lref_log_new             (JavaInteropGCBridge *bridge,   int lref_count,     jobject curHandle,  char curType,   jobject newHandle,  char newType,   const char *thread_name,   int64_t thread_id,  const char *from);
JAVA_INTEROP_API    void                    java_interop_gc_bridge_lref_log_delete          (JavaInteropGCBridge *bridge,   int lref_count,     jobject handle,     char type,                                          const char *thread_name,   int64_t thread_id,  const char *from);

JAVA_INTEROP_API    int                     java_interop_gc_bridge_gref_set_log_file        (JavaInteropGCBridge *bridge,   const char *gref_log_file);
JAVA_INTEROP_API    FILE*                   java_interop_gc_bridge_gref_get_log_file        (JavaInteropGCBridge *bridge);
JAVA_INTEROP_API    int                     java_interop_gc_bridge_gref_set_log_level       (JavaInteropGCBridge *bridge,   int level);
JAVA_INTEROP_API    void                    java_interop_gc_bridge_gref_log_message         (JavaInteropGCBridge *bridge,   int level, const char *message);
JAVA_INTEROP_API    int                     java_interop_gc_bridge_gref_log_new             (JavaInteropGCBridge *bridge,   jobject curHandle,  char curType,   jobject newHandle,  char newType,   const char *thread_name,    int64_t thread_id,  const char *from);
JAVA_INTEROP_API    int                     java_interop_gc_bridge_gref_log_delete          (JavaInteropGCBridge *bridge,   jobject handle,     char type,                                          const char *thread_name,    int64_t thread_id,  const char *from);

JAVA_INTEROP_API    int                     java_interop_gc_bridge_weak_gref_log_new        (JavaInteropGCBridge *bridge,   jobject curHandle,  char curType,   jobject newHandle,  char newType,   const char *thread_name,    int64_t thread_id,  const char *from);
JAVA_INTEROP_API    int                     java_interop_gc_bridge_weak_gref_log_delete     (JavaInteropGCBridge *bridge,   jobject handle,     char type,                                          const char *thread_name,    int64_t thread_id,  const char *from);

JAVA_INTEROP_END_DECLS

#endif  /* ndef INC_JAVA_INTEROP_GC_BRIDGE_H */

