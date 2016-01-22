#include <assert.h>
#include <stdlib.h>
#include <string.h>
#include <pthread.h>
#include <inttypes.h>

#include "java-interop.h"
#include "java-interop-gc-bridge.h"
#include "java-interop-mono.h"


typedef struct MonoJavaGCBridgeInfo {
	MonoClass          *klass;
	MonoClassField     *handle;
	MonoClassField     *handle_type;
	MonoClassField     *refs_added;
	MonoClassField     *weak_handle;
} MonoJavaGCBridgeInfo;

#define NUM_GC_BRIDGE_TYPES     (4)

struct JavaInteropGCBridge {

	JavaVM                             *jvm;

	MonoClass                          *BridgeProcessing_type;
	MonoClassField                     *BridgeProcessing_field;

	int                                 num_bridge_types;
	MonoJavaGCBridgeInfo                mono_java_gc_bridge_info [NUM_GC_BRIDGE_TYPES];

	int                                 gc_disabled;

	int                                 gc_gref_count;
	int                                 gc_weak_gref_count;

	jobject                             Runtime_instance;
	jmethodID                           Runtime_gc;

	jclass                              WeakReference_class;
	jmethodID                           WeakReference_init;
	jmethodID                           WeakReference_get;

	JavaInteropGetThreadDescriptionCb   thread_description_cb;
	void                               *thread_description_user_data;

	FILE                               *gref_log,      *lref_log;
	char                               *gref_path,     *lref_path;
	int                                 gref_log_level, lref_log_level;
	int                                 gref_cleanup,   lref_cleanup;
};


static jobject
lref_to_gref (JNIEnv *env, jobject lref)
{
	jobject g;
	if (lref == 0)
		return 0;
	g = (*env)->NewGlobalRef (env, lref);
	(*env)->DeleteLocalRef (env, lref);
	return g;
}

static JNIEnv*
ensure_jnienv (JavaInteropGCBridge *bridge)
{
	JavaVM *jvm = bridge->jvm;
	JNIEnv *env;
	if ((*bridge->jvm)->GetEnv (bridge->jvm, (void**)&env, JNI_VERSION_1_6) != JNI_OK || env == NULL) {
		mono_thread_attach (mono_domain_get ());
		(*jvm)->GetEnv (jvm, (void**)&env, JNI_VERSION_1_6);
	}
	return env;
}

static int
java_interop_gc_bridge_destroy (JavaInteropGCBridge *bridge)
{
	if (bridge == NULL)
		return -1;

	JNIEnv *env = ensure_jnienv (bridge);
	if (env != NULL) {
		(*env)->DeleteGlobalRef (env, bridge->Runtime_instance);
		(*env)->DeleteGlobalRef (env, bridge->WeakReference_class);

		bridge->Runtime_instance    = NULL;
		bridge->WeakReference_class = NULL;
	}

	if (bridge->gref_log != NULL && bridge->gref_cleanup) {
		fclose (bridge->gref_log);
	}
	bridge->gref_log    = NULL;

	free (bridge->gref_path);
	bridge->gref_path   = NULL;

	if (bridge->lref_log != NULL && bridge->lref_cleanup) {
		fclose (bridge->lref_log);
	}
	bridge->lref_log    = NULL;

	free (bridge->lref_path);
	bridge->lref_path   = NULL;

	return 0;
}

static char*
ji_realpath (const char *path)
{
	if (path == NULL)
		return NULL;

	char   *rp = realpath (path, NULL);
	if (rp == NULL) {
		return strdup (path);
	}
	return rp;
}

static FILE *
open_log_file (const char *path, FILE *alt, const char *alt_path, const char *envVar, int *cleanup, char **rpath)
{
	path    = path ? path : getenv (envVar);
	if (path == NULL)
		return NULL;

	*cleanup    = 0;
	if (strlen (path) == 0)
		return stdout;

	char *rp    = ji_realpath (path);
	if (rp != NULL && alt_path != NULL && strcmp (rp, alt_path) == 0) {
	    free (rp);
		return alt;
	}

	FILE *f = fopen (path, "w");
	if (f == NULL) {
		free (rp);
		return NULL;
	}

	if (rpath) {
	    *rpath  = rp;
	} else {
		free (rp);
	}

	*cleanup    = 1;
	return f;
}

JavaInteropGCBridge*
java_interop_gc_bridge_new (JavaVM *jvm)
{
	if (jvm == NULL)
		return NULL;

	JavaInteropGCBridge bridge = {0};

	bridge.jvm  = jvm;

	JNIEnv *env;
	if ((*jvm)->GetEnv (jvm, (void**) &env, JNI_VERSION_1_6) != JNI_OK)
		return NULL;

	jobject     Runtime_class           = (*env)->FindClass (env, "java/lang/Runtime");
	if (Runtime_class != NULL) {
		bridge.Runtime_gc               = (*env)->GetMethodID (env, Runtime_class, "gc", "()V");

		jmethodID   Runtime_getRuntime  = (*env)->GetStaticMethodID (env, Runtime_class, "getRuntime", "()Ljava/lang/Runtime;");
		bridge.Runtime_instance         = Runtime_getRuntime
			? lref_to_gref (env, (*env)->CallStaticObjectMethod (env, Runtime_class, Runtime_getRuntime))
			: NULL;

		(*env)->DeleteLocalRef (env, Runtime_class);
	}

	jobject     WeakReference_class     = (*env)->FindClass (env, "java/lang/ref/WeakReference");
	if (WeakReference_class != NULL) {
		bridge.WeakReference_init       = (*env)->GetMethodID (env, WeakReference_class, "<init>", "(Ljava/lang/Object;)V");
		bridge.WeakReference_get        = (*env)->GetMethodID (env, WeakReference_class, "get", "()Ljava/lang/Object;");
		bridge.WeakReference_class      = lref_to_gref (env, WeakReference_class);
	}

	JavaInteropGCBridge *p  = calloc (1, sizeof (JavaInteropGCBridge));

	if (p == NULL || bridge.jvm == NULL ||
			bridge.Runtime_instance == NULL || bridge.Runtime_gc == NULL ||
			bridge.WeakReference_class == NULL || bridge.WeakReference_init == NULL || bridge.WeakReference_get == NULL) {
		java_interop_gc_bridge_destroy (&bridge);
		free (p);
		return NULL;
	}

	*p  = bridge;

	p->gref_log = open_log_file (NULL,  NULL,           NULL,           "JAVA_INTEROP_GREF_LOG",    &p->gref_cleanup,   &p->gref_path);
	p->lref_log = open_log_file (NULL,  p->gref_log,    p->gref_path,   "JAVA_INTEROP_LREF_LOG",    &p->lref_cleanup,   &p->lref_path);

	return p;
}

int
java_interop_gc_bridge_free (JavaInteropGCBridge *bridge)
{
	if (bridge == NULL)
		return -1;

	int r   = java_interop_gc_bridge_destroy (bridge);
	free (bridge);

	return r;
}

int
java_interop_gc_bridge_enable (JavaInteropGCBridge *bridge, int enable)
{
	if (!bridge)
		return -1;

	bridge->gc_disabled = !enable;

	return 0;
}

int
java_interop_gc_bridge_set_bridge_processing_field (
		JavaInteropGCBridge                            *bridge,
		struct JavaInterop_System_RuntimeTypeHandle     type_handle,
		const char                                     *field_name)
{
	if (bridge == NULL || type_handle.value == NULL || field_name == NULL)
		return -1;

	MonoType *type = type_handle.value;

	bridge->BridgeProcessing_type   = mono_class_from_mono_type (type);
	bridge->BridgeProcessing_field  = mono_class_get_field_from_name (bridge->BridgeProcessing_type,    field_name);

	return 0;
}

int
java_interop_gc_bridge_register_bridgeable_type (
		JavaInteropGCBridge                            *bridge,
		struct JavaInterop_System_RuntimeTypeHandle     type_handle)
{
	if (bridge == NULL || type_handle.value == NULL)
		return -1;

	if (bridge->num_bridge_types >= NUM_GC_BRIDGE_TYPES)
	    return -1;

	MonoType               *type    = type_handle.value;
	int                     i       = bridge->num_bridge_types++;
	MonoJavaGCBridgeInfo   *info    = &bridge->mono_java_gc_bridge_info [i];

	info->klass             = mono_class_from_mono_type (type);
	info->handle            = mono_class_get_field_from_name (info->klass,     "handle");
	info->handle_type       = mono_class_get_field_from_name (info->klass,     "handle_type");
	info->refs_added        = mono_class_get_field_from_name (info->klass,     "refs_added");
	info->weak_handle       = mono_class_get_field_from_name (info->klass,     "weak_handle");

	if (info->klass == NULL || info->handle == NULL || info->handle_type == NULL ||
			info->refs_added == NULL || info->weak_handle == NULL)
		return -1;
	return 0;
}


int
java_interop_gc_bridge_set_thread_description_creator (
		JavaInteropGCBridge                *bridge,
		JavaInteropGetThreadDescriptionCb   creator,
		void                               *user_data)
{
	if (bridge == NULL)
		return -1;

	bridge->thread_description_cb           = creator;
	bridge->thread_description_user_data    = user_data;
	return 0;
}

int
java_interop_gc_bridge_get_gref_count (JavaInteropGCBridge *bridge)
{
	if (bridge == NULL)
		return -1;

	return bridge->gc_gref_count;
}

int
java_interop_gc_bridge_get_weak_gref_count (JavaInteropGCBridge *bridge)
{
	if (bridge == NULL)
		return -1;

	return bridge->gc_weak_gref_count;
}

int
java_interop_gc_bridge_gref_set_log_file (
		JavaInteropGCBridge    *bridge,
		const char             *gref_log_file)
{
	if (bridge == NULL)
		return -1;

	if (bridge->gref_log && bridge->gref_cleanup) {
		fclose (bridge->gref_log);
	}

	bridge->gref_log    = open_log_file (gref_log_file,    bridge->lref_log,    bridge->lref_path,  "JAVA_INTEROP_GREF_LOG",     &bridge->gref_cleanup,     &bridge->gref_path);

	return 0;
}

FILE*
java_interop_gc_bridge_gref_get_log_file (
		JavaInteropGCBridge    *bridge)
{
	if (bridge == NULL)
		return NULL;

	return bridge->gref_log;
}

int
java_interop_gc_bridge_gref_set_log_level (
		JavaInteropGCBridge    *bridge,
		int                     level)
{
	if (bridge == NULL)
		return -1;

	bridge->gref_log_level  = level;
	return 0;
}

void
java_interop_gc_bridge_gref_log_message (
		JavaInteropGCBridge    *bridge,
		int                     level,
		const char             *message)
{
	if (!bridge || !bridge->gref_log || bridge->gref_log_level < level)
		return;
	fprintf (bridge->gref_log, "%s", message);
	fflush (bridge->gref_log);
}

int
java_interop_gc_bridge_lref_set_log_file (
		JavaInteropGCBridge    *bridge,
		const char             *lref_log_file)
{
	if (bridge == NULL)
		return -1;

	if (bridge->lref_log && bridge->lref_cleanup) {
		fclose (bridge->lref_log);
	}

	bridge->lref_log    = open_log_file (lref_log_file,    bridge->gref_log,    bridge->gref_path,  "JAVA_INTEROP_LREF_LOG",     &bridge->lref_cleanup,     &bridge->lref_path);

	return 0;
}

FILE*
java_interop_gc_bridge_lref_get_log_file (
		JavaInteropGCBridge    *bridge)
{
	if (bridge == NULL)
		return NULL;

	return bridge->lref_log;
}

int
java_interop_gc_bridge_lref_set_log_level (
		JavaInteropGCBridge    *bridge,
		int                     level)
{
	if (bridge == NULL)
		return -1;

	bridge->lref_log_level  = level;
	return 0;
}

void
java_interop_gc_bridge_lref_log_message (
		JavaInteropGCBridge    *bridge,
		int                     level,
		const char             *message)
{
	if (!bridge || !bridge->lref_log || bridge->lref_log_level < level)
		return;
	fprintf (bridge->lref_log, "%s", message);
	fflush (bridge->lref_log);
}

static void
log_gref (JavaInteropGCBridge *bridge, const char *format, ...)
{
	va_list args;

	if (!bridge->gref_log)
		return;

	va_start (args, format);
	vfprintf (bridge->gref_log, format, args);
	va_end (args);
}

static char
get_object_ref_type (JNIEnv *env, void *handle)
{
	jobjectRefType value;
	if (handle == NULL)
		return 'I';
	value = (*env)->GetObjectRefType (env, handle);
	switch (value) {
		case JNIInvalidRefType:     return 'I';
		case JNILocalRefType:       return 'L';
		case JNIGlobalRefType:      return 'G';
		case JNIWeakGlobalRefType:  return 'W';
		default:                    return '*';
	}
}

static int
gref_inc (JavaInteropGCBridge *bridge)
{
	return __sync_add_and_fetch (&bridge->gc_gref_count, 1);
}

static int
gref_dec (JavaInteropGCBridge *bridge)
{
	return __sync_sub_and_fetch (&bridge->gc_gref_count, 1);
}

#if defined (ANDROID)
	#define WRITE_ANDROID_MESSAGE_RETURN(ret, category, format, ...) do {  \
		if ((log_categories & category) == 0)                       \
			return ret;                                             \
		log_info (category, format, __VA_ARGS__);                   \
	} while (0)
#else   /* ndef ANDROID */
	#define WRITE_ANDROID_MESSAGE_RETURN(ret, category, format, ...) do { \
	} while (0)
#endif  /* ndef ANDROID */

#define WRITE_LOG_MESSAGE_RETURN(ret, category, to, from, format, ...) do { \
	WRITE_ANDROID_MESSAGE_RETURN(ret, category, format, __VA_ARGS__);   \
	if (!to)                                                            \
		return ret;                                                     \
	fprintf (to, format "\n", __VA_ARGS__);                             \
	fprintf (to, "%s\n", from);                                         \
	fflush (to);                                                        \
} while (0)

int
java_interop_gc_bridge_gref_log_new (
		JavaInteropGCBridge    *bridge,
		jobject                 curHandle,
		char                    curType,
		jobject                 newHandle,
		char                    newType,
		const char             *thread_description,
		const char             *from)
{
	if (!bridge)
	    return -1;

	int c = gref_inc (bridge);

	WRITE_LOG_MESSAGE_RETURN(c, LOG_GREF, bridge->gref_log, from,
			"+g+ grefc %i gwrefc %i obj-handle %p/%c -> new-handle %p/%c from thread %s",
			c,
			bridge->gc_weak_gref_count,
			curHandle,
			curType,
			newHandle,
			newType,
			thread_description);

	return c;
}

int
java_interop_gc_bridge_gref_log_delete (
		JavaInteropGCBridge    *bridge,
		jobject                 handle,
		char                    type,
		const char             *thread_description,
		const char             *from)
{
	if (!bridge)
		return -1;

	int c = gref_dec (bridge);

	WRITE_LOG_MESSAGE_RETURN(c, LOG_GREF, bridge->gref_log, from,
			"-g- grefc %i gwrefc %i handle %p/%c from thread %s",
			c,
			bridge->gc_weak_gref_count,
			handle,
			type,
			thread_description);

	return c;
}

void
java_interop_gc_bridge_lref_log_new (
		JavaInteropGCBridge    *bridge,
		int                     lref_count,
		jobject                 curHandle,
		char                    curType,
		jobject                 newHandle,
		char                    newType,
		const char             *thread_description,
		const char             *from)
{
	if (!bridge)
	    return;

	if (newHandle) {
		WRITE_LOG_MESSAGE_RETURN(, LOG_LREF, bridge->lref_log, from,
				"+l+ lrefc %i obj-handle %p/%c -> new-handle %p/%c from thread %s",
				lref_count,
				curHandle,
				curType,
				newHandle,
				newType,
				thread_description);
	}
	else {
		WRITE_LOG_MESSAGE_RETURN(, LOG_LREF, bridge->lref_log, from,
				"+l+ lrefc %i handle %p/%c from thread %s",
				lref_count,
				curHandle,
				curType,
				thread_description);
	}
}

void
java_interop_gc_bridge_lref_log_delete (
		JavaInteropGCBridge    *bridge,
		int                     lref_count,
		jobject                 handle,
		char                    type,
		const char             *thread_description,
		const char             *from)
{
	if (!bridge)
		return;

	WRITE_LOG_MESSAGE_RETURN(, LOG_LREF, bridge->lref_log, from,
			"-l- lrefc %i handle %p/%c from thread %s",
			lref_count,
			handle,
			type,
			thread_description);
}

int
java_interop_gc_bridge_weak_gref_log_new (
		JavaInteropGCBridge    *bridge,
		jobject                 curHandle,
		char                    curType,
		jobject                 newHandle,
		char                    newType,
		const char             *thread_description,
		const char             *from)
{
	if (!bridge)
		return -1;

	int c = ++bridge->gc_weak_gref_count;

	WRITE_LOG_MESSAGE_RETURN(c, LOG_GREF, bridge->gref_log, from,
			"+w+ grefc %i gwrefc %i obj-handle %p/%c -> new-handle %p/%c from thread %s",
			bridge->gc_gref_count,
			bridge->gc_weak_gref_count,
			curHandle,
			curType,
			newHandle,
			newType,
			thread_description);

	return c;
}

int
java_interop_gc_bridge_weak_gref_log_delete (
		JavaInteropGCBridge    *bridge,
		jobject                 handle,
		char                    type,
		const char             *thread_description,
		const char             *from)
{
	if (!bridge)
		return -1;

	int c = bridge->gc_weak_gref_count--;

	WRITE_LOG_MESSAGE_RETURN(c, LOG_GREF, bridge->gref_log, from,
			"-w- grefc %i gwrefc %i handle %p/%c from thread %s",
			bridge->gc_gref_count,
			bridge->gc_weak_gref_count,
			handle,
			type,
			thread_description);

	return c;
}

static int
get_gc_bridge_index (JavaInteropGCBridge *bridge, MonoClass *klass)
{
	int i;
	int f = 0;

	for (i = 0; i < NUM_GC_BRIDGE_TYPES; ++i) {
		MonoClass *k = bridge->mono_java_gc_bridge_info [i].klass;
		if (k == NULL) {
			f++;
			continue;
		}
		if (klass == k || mono_class_is_subclass_of (klass, k, 0))
			return i;
	}
	return f == NUM_GC_BRIDGE_TYPES
		? (int) -NUM_GC_BRIDGE_TYPES
		: -1;
}

static MonoJavaGCBridgeInfo *
get_gc_bridge_info_for_class (JavaInteropGCBridge *bridge, MonoClass *klass)
{
	int   i;

	if (klass == NULL)
		return NULL;

	i   = get_gc_bridge_index (bridge, klass);
	if (i < 0)
		return NULL;
	return &bridge->mono_java_gc_bridge_info [i];
}

static MonoJavaGCBridgeInfo *
get_gc_bridge_info_for_object (JavaInteropGCBridge *bridge, MonoObject *object)
{
	if (object == NULL)
		return NULL;
	return get_gc_bridge_info_for_class (bridge, mono_object_get_class (object));
}

typedef mono_bool (*MonodroidGCTakeRefFunc) (JavaInteropGCBridge *bridge, JNIEnv *env, MonoObject *obj, const char *thread_description);

static  MonodroidGCTakeRefFunc  take_global_ref;
static  MonodroidGCTakeRefFunc  take_weak_global_ref;

static mono_bool
take_global_ref_java (JavaInteropGCBridge *bridge, JNIEnv *env, MonoObject *obj, const char *thread_description)
{
	MonoJavaGCBridgeInfo    *bridge_info    = get_gc_bridge_info_for_object (bridge, obj);
	if (bridge_info == NULL)
		return 0;

	void *weak;
	mono_field_get_value (obj, bridge_info->weak_handle, &weak);

	void *handle    = (*env)->CallObjectMethod (env, weak, bridge->WeakReference_get);
	log_gref (bridge, "*try_take_global_2_1 obj=%p -> wref=%p handle=%p\n", obj, weak, handle);

	if (handle) {
		void *h     = (*env)->NewGlobalRef (env, handle);
		(*env)->DeleteLocalRef (env, handle);
		handle      = h;
		java_interop_gc_bridge_gref_log_new (bridge, weak, get_object_ref_type (env, weak),
				handle, get_object_ref_type (env, handle), thread_description, "take_global_ref_java");
	}
	java_interop_gc_bridge_weak_gref_log_delete (bridge, weak, get_object_ref_type (env, weak), thread_description, "take_global_ref_java");
	(*env)->DeleteGlobalRef (env, weak);
	weak        = NULL;
	mono_field_set_value (obj, bridge_info->weak_handle, &weak);

	mono_field_set_value (obj, bridge_info->handle, &handle);

	int type    = JNIGlobalRefType;
	mono_field_set_value (obj, bridge_info->handle_type, &type);

	return handle != NULL;
}

static mono_bool
take_weak_global_ref_java (JavaInteropGCBridge *bridge, JNIEnv *env, MonoObject *obj, const char *thread_description)
{
	MonoJavaGCBridgeInfo    *bridge_info    = get_gc_bridge_info_for_object (bridge, obj);
	if (bridge_info == NULL)
		return 0;

	void *handle;
	mono_field_get_value (obj, bridge_info->handle, &handle);

	jobject weaklocal   = (*env)->NewObject (env, bridge->WeakReference_class, bridge->WeakReference_init, handle);
	void   *weakglobal  = (*env)->NewGlobalRef (env, weaklocal);
	(*env)->DeleteLocalRef (env, weaklocal);

	log_gref (bridge, "*take_weak_2_1 obj=%p -> wref=%p handle=%p\n", obj, weakglobal, handle);
	java_interop_gc_bridge_weak_gref_log_new (bridge, handle, get_object_ref_type (env, handle),
			weakglobal, get_object_ref_type (env, weakglobal), thread_description, "take_weak_global_ref_2_1_compat");

	java_interop_gc_bridge_gref_log_delete (bridge, handle, get_object_ref_type (env, handle), thread_description, "take_weak_global_ref_2_1_compat");
	(*env)->DeleteGlobalRef (env, handle);

	mono_field_set_value (obj, bridge_info->weak_handle, &weakglobal);

	return 1;
}

static mono_bool
take_global_ref_jni (JavaInteropGCBridge *bridge, JNIEnv *env, MonoObject *obj, const char *thread_description)
{
	MonoJavaGCBridgeInfo    *bridge_info    = get_gc_bridge_info_for_object (bridge, obj);
	if (bridge_info == NULL)
		return 0;

	void *weak;
	mono_field_get_value (obj, bridge_info->handle, &weak);

	void *handle    = (*env)->NewGlobalRef (env, weak);
	log_gref (bridge, "*try_take_global obj=%p -> wref=%p handle=%p\n", obj, weak, handle);

	if (handle) {
		java_interop_gc_bridge_gref_log_new (bridge, weak, get_object_ref_type (env, weak),
				handle, get_object_ref_type (env, handle),
				thread_description,
				"take_global_ref_jni");
	}

	java_interop_gc_bridge_weak_gref_log_delete (bridge, weak, get_object_ref_type (env, weak),
			thread_description, "take_global_ref_jni");
	(*env)->DeleteWeakGlobalRef (env, weak);

	mono_field_set_value (obj, bridge_info->handle, &handle);

	int type = JNIGlobalRefType;
	mono_field_set_value (obj, bridge_info->handle_type, &type);
	return handle != NULL;
}

static mono_bool
take_weak_global_ref_jni (JavaInteropGCBridge *bridge, JNIEnv *env, MonoObject *obj, const char *thread_description)
{
	MonoJavaGCBridgeInfo    *bridge_info    = get_gc_bridge_info_for_object (bridge, obj);
	if (bridge_info == NULL)
		return 0;

	void *handle;
	mono_field_get_value (obj, bridge_info->handle, &handle);

	log_gref (bridge, "*take_weak obj=%p; handle=%p\n", obj, handle);

	void *weak  = (*env)->NewWeakGlobalRef (env, handle);
	java_interop_gc_bridge_weak_gref_log_new (bridge, handle, get_object_ref_type (env, handle),
			weak, get_object_ref_type (env, weak),
			thread_description, "take_weak_global_ref_jni");

	java_interop_gc_bridge_gref_log_delete (bridge, handle, get_object_ref_type (env, handle),
			thread_description, "take_weak_global_ref_jni");
	(*env)->DeleteGlobalRef (env, handle);

	mono_field_set_value (obj, bridge_info->handle, &weak);

	int type = JNIWeakGlobalRefType;
	mono_field_set_value (obj, bridge_info->handle_type, &type);

	return 1;
}

static mono_bool
add_reference (JavaInteropGCBridge *bridge, JNIEnv *env, MonoObject *obj, MonoJavaGCBridgeInfo *bridge_info, MonoObject *reffed_obj)
{
#if DEBUG
	MonoClass *klass    = mono_object_get_class (obj);
#endif

	void *handle;
	mono_field_get_value (obj, bridge_info->handle, &handle);

	jclass      java_class      = (*env)->GetObjectClass (env, handle);
	jmethodID   add_method_id   = (*env)->GetMethodID (env, java_class, "monodroidAddReference", "(Ljava/lang/Object;)V");
	if (add_method_id) {
	    void *reffed_handle;
		mono_field_get_value (reffed_obj, bridge_info->handle, &reffed_handle);
		(*env)->CallVoidMethod (env, handle, add_method_id, reffed_handle);
		(*env)->DeleteLocalRef (env, java_class);
#if DEBUG
		if (bridge->gref_log_level > 1)
			log_gref (bridge,
					"added reference for object of class %s.%s to object of class %s.%s\n",
					mono_class_get_namespace (klass),
					mono_class_get_name (klass),
					mono_class_get_namespace (mono_object_get_class (reffed_obj)),
					mono_class_get_name (mono_object_get_class (reffed_obj)));
#endif
		return 1;
	}

	(*env)->ExceptionClear (env);
#if DEBUG
	if (bridge->gref_log_level > 1)
		log_gref (bridge,
				"Missing monodroidAddReference method for object of class %s.%s\n",
				mono_class_get_namespace (klass),
				mono_class_get_name (klass));
#endif
	(*env)->DeleteLocalRef (env, java_class);

	return 0;
}

struct  SetStaticFieldValueInfo {
	JavaInteropGCBridge    *bridge;
	mono_bool               value;
};

static void
set_bridge_processing_field (MonoDomain *domain, void* user_data)
{
	struct SetStaticFieldValueInfo  *p = user_data;
	MonoVTable  *v = mono_class_vtable (domain, p->bridge->BridgeProcessing_type);
	if (!v)
	    return;
	mono_field_static_set_value (v, p->bridge->BridgeProcessing_field, &p->value);
}

static void
set_bridge_processing (JavaInteropGCBridge *bridge, mono_bool value)
{
	struct SetStaticFieldValueInfo v = {0};
	v.bridge    = bridge;
	v.value     = value;

	mono_domain_foreach (set_bridge_processing_field, &v);
}

static void
gc_prepare_for_java_collection (JavaInteropGCBridge *bridge, JNIEnv *env, int num_sccs, MonoGCBridgeSCC **sccs, int num_xrefs, MonoGCBridgeXRef *xrefs, const char *thread_description)
{
	set_bridge_processing (bridge, 1);

	int ref_val = 1;
	/* add java refs for items on the list which reference each other */
	for (int i = 0; i < num_sccs; i++) {
		MonoGCBridgeSCC        *scc         = sccs [i];
		MonoJavaGCBridgeInfo   *bridge_info = NULL;
		/* start at the second item, ref j from j-1 */
		for (int j = 1; j < scc->num_objs; j++) {
			bridge_info = get_gc_bridge_info_for_object (bridge, scc->objs [j-1]);
			if (bridge_info != NULL && add_reference (bridge, env, scc->objs [j-1], bridge_info, scc->objs [j])) {
				mono_field_set_value (scc->objs [j-1], bridge_info->refs_added, &ref_val);
			}
		}
		/* ref the first from the last */
		if (scc->num_objs > 1) {
			bridge_info = get_gc_bridge_info_for_object (bridge, scc->objs [scc->num_objs-1]);
			if (bridge_info != NULL && add_reference (bridge, env, scc->objs [scc->num_objs-1], bridge_info, scc->objs [0])) {
				mono_field_set_value (scc->objs [scc->num_objs-1], bridge_info->refs_added, &ref_val);
			}
		}
	}

	/* add the cross scc refs */
	for (int i = 0; i < num_xrefs; i++) {
		MonoJavaGCBridgeInfo    *bridge_info    = get_gc_bridge_info_for_object (bridge, sccs [xrefs [i].src_scc_index]->objs [0]);
		if (bridge_info != NULL && add_reference (bridge, env, sccs [xrefs [i].src_scc_index]->objs [0], bridge_info, sccs [xrefs [i].dst_scc_index]->objs [0])) {
			mono_field_set_value (sccs [xrefs [i].src_scc_index]->objs [0], bridge_info->refs_added, &ref_val);
		}
	}

	// switch to weak refs
	for (int i = 0; i < num_sccs; i++)
		for (int j = 0; j < sccs [i]->num_objs; j++)
			take_weak_global_ref (bridge, env, sccs [i]->objs [j], thread_description);
}

static void
gc_cleanup_after_java_collection (JavaInteropGCBridge *bridge, JNIEnv *env, int num_sccs, MonoGCBridgeSCC **sccs, const char *thread_description)
{
	int total   = 0;
	int alive   = 0;

	for (int i = 0; i < num_sccs; i++)
		for (int j = 0; j < sccs [i]->num_objs; j++, total++)
			take_global_ref (bridge, env, sccs [i]->objs [j], thread_description);

	/* clear the cross references on any remaining items */
	for (int i = 0; i < num_sccs; i++) {
		sccs [i]->is_alive = 0;
		for (int j = 0; j < sccs [i]->num_objs; j++) {
			MonoObject             *obj         = sccs [i]->objs [j];
			MonoJavaGCBridgeInfo   *bridge_info = get_gc_bridge_info_for_object (bridge, obj);
			if (bridge_info == NULL)
				continue;

			jobject jref;
			mono_field_get_value (obj, bridge_info->handle, &jref);
			if (jref) {
				alive++;
				if (j > 0)
					assert (sccs [i]->is_alive);
				sccs [i]->is_alive = 1;
				int refs_added;
				mono_field_get_value (obj, bridge_info->refs_added, &refs_added);
				if (refs_added) {
					jclass      java_class      = (*env)->GetObjectClass (env, jref);
					jmethodID   clear_method_id = (*env)->GetMethodID (env, java_class, "monodroidClearReferences", "()V");
					if (clear_method_id) {
						(*env)->CallVoidMethod (env, jref, clear_method_id);
					} else {
						(*env)->ExceptionClear (env);
#if DEBUG
						if (bridge->gref_log_level > 1) {
							MonoClass *klass = mono_object_get_class (obj);
							log_gref (bridge,
									"Missing monodroidClearReferences method for object of class %s.%s\n",
									mono_class_get_namespace (klass),
									mono_class_get_name (klass));
						}
#endif
					}
					(*env)->DeleteLocalRef (env, java_class);
				}
			} else {
				assert (!sccs [i]->is_alive);
			}
		}
	}
#if DEBUG
	log_gref (bridge, "GC cleanup summary: %d objects tested - resurrecting %d.\n", total, alive);
#endif

	set_bridge_processing (bridge, 0);
}

static void
java_gc (JavaInteropGCBridge *bridge, JNIEnv *env)
{
	(*env)->CallVoidMethod (env, bridge->Runtime_instance, bridge->Runtime_gc);
}

static char *
get_thread_description (JavaInteropGCBridge *bridge)
{
	JavaInteropGetThreadDescriptionCb cb = bridge->thread_description_cb;
	if (cb) {
		return cb (bridge->thread_description_user_data);
	}

#if __linux__
	int64_t tid = gettid ();
#else
	int64_t tid = (int64_t) pthread_self ();
#endif
	char *b;
	asprintf (&b, "'finalizer'(%" PRId64 ")", tid);
	return b;
}

static  JavaInteropGCBridge    *mono_bridge;

JavaInteropGCBridge*
java_interop_gc_bridge_get_current (void)
{
	return mono_bridge;
}

int
java_interop_gc_bridge_set_current_once (JavaInteropGCBridge *bridge)
{
	if (bridge == NULL)
		return -1;
	mono_bridge = bridge;
	return 0;
}

static MonoGCBridgeObjectKind
gc_bridge_class_kind (MonoClass *klass)
{
	int i;
	if (mono_bridge->gc_disabled)
		return GC_BRIDGE_TRANSPARENT_CLASS;

	i = get_gc_bridge_index (mono_bridge, klass);
	if (i == -NUM_GC_BRIDGE_TYPES) {
		log_gref (mono_bridge,
				"asked if a class %s.%s is a bridge before we inited GC Bridge Types!\n",
				mono_class_get_namespace (klass),
				mono_class_get_name (klass));
		return GC_BRIDGE_TRANSPARENT_CLASS;
	}

	if (i >= 0) {
		return GC_BRIDGE_TRANSPARENT_BRIDGE_CLASS;
	}

	return GC_BRIDGE_TRANSPARENT_CLASS;
}

static mono_bool
gc_is_bridge_object (MonoObject *object)
{
	void *handle;

	MonoJavaGCBridgeInfo    *bridge_info    = get_gc_bridge_info_for_object (mono_bridge, object);
	if (bridge_info == NULL)
		return 0;

	mono_field_get_value (object, bridge_info->handle, &handle);
	if (handle == NULL) {
#if DEBUG
		MonoClass *mclass = mono_object_get_class (object);
		log_gref (mono_bridge,
				"object of class %s.%s with null handle\n",
				mono_class_get_namespace (mclass),
				mono_class_get_name (mclass));
#endif
		return 0;
	}

	return 1;
}

static void
gc_cross_references (int num_sccs, MonoGCBridgeSCC **sccs, int num_xrefs, MonoGCBridgeXRef *xrefs)
{
	if (mono_bridge->gc_disabled)
		return;

	JavaInteropGCBridge    *bridge  = mono_bridge;

	char   *thread_description  = get_thread_description (bridge);

#if DEBUG
	if (bridge->gref_log_level > 1) {
		log_gref (bridge, "cross references callback invoked with %d sccs and %d xrefs.\n", num_sccs, num_xrefs);

		for (int i = 0; i < num_sccs; ++i) {
			log_gref (bridge, "group %d with %d objects\n", i, sccs [i]->num_objs);
			for (int j = 0; j < sccs [i]->num_objs; ++j) {
				MonoObject *obj     = sccs [i]->objs [j];
				MonoClass  *klass   = mono_object_get_class (obj);
				log_gref (bridge,
						"\tobj %p [%s::%s]\n",
						obj,
						mono_class_get_namespace (klass),
						mono_class_get_name (klass));
			}
		}

		for (int i = 0; i < num_xrefs; ++i)
			log_gref (bridge, "xref [%d] %d -> %d\n", i, xrefs [i].src_scc_index, xrefs [i].dst_scc_index);
	}
#endif

	JNIEnv *env = ensure_jnienv (bridge);
	gc_prepare_for_java_collection (bridge, env, num_sccs, sccs, num_xrefs, xrefs, thread_description);
	java_gc (bridge, env);
	gc_cleanup_after_java_collection (bridge, env, num_sccs, sccs, thread_description);

	free (thread_description);
}

int
java_interop_gc_bridge_register_hooks_once (int weak_ref_kind)
{
	if (mono_bridge == NULL)
		return -1;

	const char *message = NULL;

	MonoGCBridgeCallbacks   bridge_cbs = {0};

	switch (weak_ref_kind) {
	case JAVA_INTEROP_GC_BRIDGE_USE_WEAK_REFERENCE_KIND_JAVA:
		message = "Using java.lang.ref.WeakReference for JNI Weak References.";
		take_global_ref         = take_global_ref_java;
		take_weak_global_ref    = take_weak_global_ref_java;
		break;
	case JAVA_INTEROP_GC_BRIDGE_USE_WEAK_REFERENCE_KIND_JNI:
		message = "Using JNIEnv::NewWeakGlobalRef() for JNI Weak References.";
		take_global_ref         = take_global_ref_jni;
		take_weak_global_ref    = take_weak_global_ref_jni;
		break;
	default:
		return -1;
	}

	log_gref (mono_bridge, "%s\n", message);

	bridge_cbs.bridge_version       = SGEN_BRIDGE_VERSION;
	bridge_cbs.bridge_class_kind    = gc_bridge_class_kind;
	bridge_cbs.is_bridge_object     = gc_is_bridge_object;
	bridge_cbs.cross_references     = gc_cross_references;

	mono_gc_register_bridge_callbacks (&bridge_cbs);

	return 0;
}

void
java_interop_gc_bridge_wait_for_bridge_processing (void)
{
	if (mono_bridge == NULL)
		return;

	mono_gc_wait_for_bridge_processing ();
}
