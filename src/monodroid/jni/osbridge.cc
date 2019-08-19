#include <assert.h>
#include <string.h>

#include <sys/types.h>
#if defined (LINUX) || defined (__linux__) || defined (__linux)
#include <sys/syscall.h>
#endif

#if defined (WINDOWS)
#include <windef.h>
#include <winbase.h>
#include <shlobj.h>
#include <objbase.h>
#include <knownfolders.h>
#include <shlwapi.h>
#endif

#include "globals.h"
#include "osbridge.h"

// These two must stay here until JavaInterop is converted to C++
FILE  *gref_log;
FILE  *lref_log;

using namespace xamarin::android;
using namespace xamarin::android::internal;

const OSBridge::MonoJavaGCBridgeType OSBridge::mono_java_gc_bridge_types[] = {
	{ "Java.Lang",  "Object" },
	{ "Java.Lang",  "Throwable" },
};

const OSBridge::MonoJavaGCBridgeType OSBridge::empty_bridge_type = {
	"",
	""
};

const uint32_t OSBridge::NUM_GC_BRIDGE_TYPES = (sizeof (mono_java_gc_bridge_types)/sizeof (mono_java_gc_bridge_types [0]));
OSBridge::MonoJavaGCBridgeInfo OSBridge::mono_java_gc_bridge_info [NUM_GC_BRIDGE_TYPES];

OSBridge::MonoJavaGCBridgeInfo OSBridge::empty_bridge_info = {
	nullptr,
	nullptr,
	nullptr,
	nullptr,
	nullptr
};

extern "C" MonoGCBridgeObjectKind
gc_bridge_class_kind_cb (MonoClass* klass)
{
	return osBridge.gc_bridge_class_kind (klass);
}

extern "C" mono_bool
gc_is_bridge_object_cb (MonoObject* object)
{
	return osBridge.gc_is_bridge_object (object);
}

extern "C" void
gc_cross_references_cb (int num_sccs, MonoGCBridgeSCC **sccs, int num_xrefs, MonoGCBridgeXRef *xrefs)
{
	osBridge.gc_cross_references (num_sccs, sccs, num_xrefs, xrefs);
}

#ifdef WINDOWS
typedef int tid_type;
#else
typedef pid_t tid_type;
#endif
// glibc does *not* have a wrapper for the gettid syscall, Android NDK has it
#if !defined (ANDROID)
static tid_type gettid ()
{
#ifdef WINDOWS
	return GetCurrentThreadId ();
#elif defined (LINUX) || defined (__linux__) || defined (__linux)
	return static_cast<tid_type>(syscall (SYS_gettid));
#else
	uint64_t tid;
	pthread_threadid_np (nullptr, &tid);
	return static_cast<tid_type>(tid);
#endif
}
#endif // ANDROID

// Do this instead of using memset so that individual pointers are set atomically
void
OSBridge::clear_mono_java_gc_bridge_info ()
{
	for (uint32_t c = 0; c < NUM_GC_BRIDGE_TYPES; c++) {
		MonoJavaGCBridgeInfo *info = &mono_java_gc_bridge_info [c];
		info->klass = nullptr;
		info->handle = nullptr;
		info->handle_type = nullptr;
		info->refs_added = nullptr;
		info->weak_handle = nullptr;
	}
}

int
OSBridge::get_gc_bridge_index (MonoClass *klass)
{
	uint32_t f = 0;

	for (size_t i = 0; i < NUM_GC_BRIDGE_TYPES; ++i) {
		MonoClass *k = mono_java_gc_bridge_info [i].klass;
		if (k == nullptr) {
			f++;
			continue;
		}
		if (klass == k || monoFunctions.class_is_subclass_of (klass, k, 0))
			return static_cast<int>(i);
	}
	return f == NUM_GC_BRIDGE_TYPES
		? static_cast<int>(-NUM_GC_BRIDGE_TYPES)
		: -1;
}

OSBridge::MonoJavaGCBridgeInfo *
OSBridge::get_gc_bridge_info_for_class (MonoClass *klass)
{
	int   i;

	if (klass == nullptr)
		return nullptr;

	i   = get_gc_bridge_index (klass);
	if (i < 0)
		return nullptr;
	return &mono_java_gc_bridge_info [i];
}

OSBridge::MonoJavaGCBridgeInfo *
OSBridge::get_gc_bridge_info_for_object (MonoObject *object)
{
	if (object == nullptr)
		return nullptr;
	return get_gc_bridge_info_for_class (monoFunctions.object_get_class (object));
}

jobject
OSBridge::lref_to_gref (JNIEnv *env, jobject lref)
{
	jobject g;
	if (lref == 0)
		return 0;
	g = env->NewGlobalRef (lref);
	env->DeleteLocalRef (lref);
	return g;
}

char
OSBridge::get_object_ref_type (JNIEnv *env, void *handle)
{
	jobjectRefType value;
	if (handle == nullptr)
		return 'I';
	value = env->GetObjectRefType (reinterpret_cast<jobject> (handle));
	switch (value) {
		case JNIInvalidRefType:     return 'I';
		case JNILocalRefType:       return 'L';
		case JNIGlobalRefType:      return 'G';
		case JNIWeakGlobalRefType:  return 'W';
		default:                    return '*';
	}
}

int
OSBridge::_monodroid_gref_inc ()
{
	return __sync_add_and_fetch (&gc_gref_count, 1);
}

int
OSBridge::_monodroid_gref_dec ()
{
	return __sync_fetch_and_sub (&gc_gref_count, 1);
}

char*
OSBridge::_get_stack_trace_line_end (char *m)
{
	while (*m && *m != '\n')
		m++;
	return m;
}

void
OSBridge::_write_stack_trace (FILE *to, const char *from)
{
	char *n	= const_cast<char*> (from);

	char c;
	do {
		char *m     = n;
		char *end   = _get_stack_trace_line_end (m);

		n       = end + 1;
		c       = *end;
		*end    = '\0';
		fprintf (to, "%s\n", m);
		fflush (to);
		*end    = c;
	} while (c);
}

void
OSBridge::_monodroid_gref_log (const char *message)
{
	if (!gref_log)
		return;
	fprintf (gref_log, "%s", message);
	fflush (gref_log);
}

int
OSBridge::_monodroid_gref_log_new (jobject curHandle, char curType, jobject newHandle, char newType, const char *threadName, int threadId, const char *from, int from_writable)
{
	int c = _monodroid_gref_inc ();
	if ((log_categories & LOG_GREF) == 0)
		return c;
	log_info (LOG_GREF, "+g+ grefc %i gwrefc %i obj-handle %p/%c -> new-handle %p/%c from thread '%s'(%i)",
	          c,
	          gc_weak_gref_count,
	          curHandle,
	          curType,
	          newHandle,
	          newType,
	          threadName,
	          threadId);
	if (!gref_log)
		return c;
	fprintf (gref_log, "+g+ grefc %i gwrefc %i obj-handle %p/%c -> new-handle %p/%c from thread '%s'(%i)\n",
	         c,
	         gc_weak_gref_count,
	         curHandle,
	         curType,
	         newHandle,
	         newType,
	         threadName,
	         threadId);
	if (from_writable)
		_write_stack_trace (gref_log, const_cast<char*>(from));
	else
		fprintf (gref_log, "%s\n", from);

	fflush (gref_log);

	return c;
}

void
OSBridge::_monodroid_gref_log_delete (jobject handle, char type, const char *threadName, int threadId, const char *from, int from_writable)
{
	int c = _monodroid_gref_dec ();
	if ((log_categories & LOG_GREF) == 0)
		return;
	log_info (LOG_GREF, "-g- grefc %i gwrefc %i handle %p/%c from thread '%s'(%i)",
	          c,
	          gc_weak_gref_count,
	          handle,
	          type,
	          threadName,
	          threadId);
	if (!gref_log)
		return;
	fprintf (gref_log, "-g- grefc %i gwrefc %i handle %p/%c from thread '%s'(%i)\n",
	         c,
	         gc_weak_gref_count,
	         handle,
	         type,
	         threadName,
	         threadId);
	if (from_writable)
		_write_stack_trace (gref_log, from);
	else
		fprintf (gref_log, "%s\n", from);

	fflush (gref_log);
}

void
OSBridge::_monodroid_weak_gref_new (jobject curHandle, char curType, jobject newHandle, char newType, const char *threadName, int threadId, const char *from, int from_writable)
{
	++gc_weak_gref_count;
	if ((log_categories & LOG_GREF) == 0)
		return;
	log_info (LOG_GREF, "+w+ grefc %i gwrefc %i obj-handle %p/%c -> new-handle %p/%c from thread '%s'(%i)",
	          gc_gref_count,
	          gc_weak_gref_count,
	          curHandle,
	          curType,
	          newHandle,
	          newType,
	          threadName,
	          threadId);
	if (!gref_log)
		return;
	fprintf (gref_log, "+w+ grefc %i gwrefc %i obj-handle %p/%c -> new-handle %p/%c from thread '%s'(%i)\n",
	         gc_gref_count,
	         gc_weak_gref_count,
	         curHandle,
	         curType,
	         newHandle,
	         newType,
	         threadName,
	         threadId);
	if (from_writable)
		_write_stack_trace (gref_log, from);
	else
		fprintf (gref_log, "%s\n", from);

	fflush (gref_log);
}

void
OSBridge::_monodroid_weak_gref_delete (jobject handle, char type, const char *threadName, int threadId, const char *from, int from_writable)
{
	--gc_weak_gref_count;
	if ((log_categories & LOG_GREF) == 0)
		return;
	log_info (LOG_GREF, "-w- grefc %i gwrefc %i handle %p/%c from thread '%s'(%i)",
	          gc_gref_count,
	          gc_weak_gref_count,
	          handle,
	          type,
	          threadName,
	          threadId);
	if (!gref_log)
		return;
	fprintf (gref_log, "-w- grefc %i gwrefc %i handle %p/%c from thread '%s'(%i)\n",
	         gc_gref_count,
	         gc_weak_gref_count,
	         handle,
	         type,
	         threadName,
	         threadId);
	if (from_writable)
		_write_stack_trace (gref_log, from);
	else
		fprintf (gref_log, "%s\n", from);

	fflush (gref_log);
}

void
OSBridge::_monodroid_lref_log_new (int lrefc, jobject handle, char type, const char *threadName, int threadId, const char *from, int from_writable)
{
	if ((log_categories & LOG_LREF) == 0)
		return;
	log_info (LOG_LREF, "+l+ lrefc %i handle %p/%c from thread '%s'(%i)",
	          lrefc,
	          handle,
	          type,
	          threadName,
	          threadId);
	if (!lref_log)
		return;
	fprintf (lref_log, "+l+ lrefc %i handle %p/%c from thread '%s'(%i)\n",
	         lrefc,
	         handle,
	         type,
	         threadName,
	         threadId);
	if (from_writable)
		_write_stack_trace (lref_log, from);
	else
		fprintf (lref_log, "%s\n", from);

	fflush (lref_log);
}

void
OSBridge::_monodroid_lref_log_delete (int lrefc, jobject handle, char type, const char *threadName, int threadId, const char *from, int from_writable)
{
	if ((log_categories & LOG_LREF) == 0)
		return;
	log_info (LOG_LREF, "-l- lrefc %i handle %p/%c from thread '%s'(%i)",
	          lrefc,
	          handle,
	          type,
	          threadName,
	          threadId);
	if (!lref_log)
		return;
	fprintf (lref_log, "-l- lrefc %i handle %p/%c from thread '%s'(%i)\n",
	         lrefc,
	         handle,
	         type,
	         threadName,
	         threadId);
	if (from_writable)
		_write_stack_trace (lref_log, from);
	else
		fprintf (lref_log, "%s\n", from);

	fflush (lref_log);
}

void
OSBridge::monodroid_disable_gc_hooks ()
{
	gc_disabled = 1;
}

mono_bool
OSBridge::take_global_ref_2_1_compat (JNIEnv *env, MonoObject *obj)
{
	jobject handle, weak;
	int type = JNIGlobalRefType;

	MonoJavaGCBridgeInfo    *bridge_info    = get_gc_bridge_info_for_object (obj);
	if (bridge_info == nullptr)
		return 0;

	monoFunctions.field_get_value (obj, bridge_info->weak_handle, &weak);
	handle = env->CallObjectMethod (weak, weakrefGet);
	if (gref_log) {
		fprintf (gref_log, "*try_take_global_2_1 obj=%p -> wref=%p handle=%p\n", obj, weak, handle);
		fflush (gref_log);
	}
	if (handle) {
		void* h = env->NewGlobalRef (handle);
		env->DeleteLocalRef (handle);
		handle = reinterpret_cast <jobject> (h);
		_monodroid_gref_log_new (weak, get_object_ref_type (env, weak),
		                         handle, get_object_ref_type (env, handle), "finalizer", gettid (), __PRETTY_FUNCTION__, 0);
	}
	_monodroid_weak_gref_delete (weak, get_object_ref_type (env, weak), "finalizer", gettid(), __PRETTY_FUNCTION__, 0);
	env->DeleteGlobalRef (weak);
	weak = nullptr;
	monoFunctions.field_set_value (obj, bridge_info->weak_handle, &weak);

	monoFunctions.field_set_value (obj, bridge_info->handle, &handle);
	monoFunctions.field_set_value (obj, bridge_info->handle_type, &type);
	return handle != nullptr;
}

mono_bool
OSBridge::take_weak_global_ref_2_1_compat (JNIEnv *env, MonoObject *obj)
{
	jobject weaklocal;
	jobject handle, weakglobal;

	MonoJavaGCBridgeInfo    *bridge_info    = get_gc_bridge_info_for_object (obj);
	if (bridge_info == nullptr)
		return 0;

	monoFunctions.field_get_value (obj, bridge_info->handle, &handle);
	weaklocal = env->NewObject (weakrefClass, weakrefCtor, handle);
	weakglobal = env->NewGlobalRef (weaklocal);
	env->DeleteLocalRef (weaklocal);
	if (gref_log) {
		fprintf (gref_log, "*take_weak_2_1 obj=%p -> wref=%p handle=%p\n", obj, weakglobal, handle);
		fflush (gref_log);
	}
	_monodroid_weak_gref_new (handle, get_object_ref_type (env, handle),
	                          weakglobal, get_object_ref_type (env, weakglobal), "finalizer", gettid (), __PRETTY_FUNCTION__, 0);

	_monodroid_gref_log_delete (handle, get_object_ref_type (env, handle), "finalizer", gettid (), __PRETTY_FUNCTION__, 0);

	env->DeleteGlobalRef (handle);
	monoFunctions.field_set_value (obj, bridge_info->weak_handle, &weakglobal);
	return 1;
}

mono_bool
OSBridge::take_global_ref_jni (JNIEnv *env, MonoObject *obj)
{
	jobject handle, weak;
	int type = JNIGlobalRefType;

	MonoJavaGCBridgeInfo    *bridge_info    = get_gc_bridge_info_for_object (obj);
	if (bridge_info == nullptr)
		return 0;

	monoFunctions.field_get_value (obj, bridge_info->handle, &weak);
	handle = env->NewGlobalRef (weak);
	if (gref_log) {
		fprintf (gref_log, "*try_take_global obj=%p -> wref=%p handle=%p\n", obj, weak, handle);
		fflush (gref_log);
	}
	if (handle) {
		_monodroid_gref_log_new (weak, get_object_ref_type (env, weak),
				handle, get_object_ref_type (env, handle),
				"finalizer", gettid (),
				"take_global_ref_jni", 0);
	}
	_monodroid_weak_gref_delete (weak, get_object_ref_type (env, weak),
			"finalizer", gettid (), "take_global_ref_jni", 0);
	env->DeleteWeakGlobalRef (weak);
	if (!handle) {
		void *old_handle = nullptr;

		monoFunctions.field_get_value (obj, bridge_info->handle, &old_handle);
	}
	monoFunctions.field_set_value (obj, bridge_info->handle, &handle);
	monoFunctions.field_set_value (obj, bridge_info->handle_type, &type);
	return handle != nullptr;
}

mono_bool
OSBridge::take_weak_global_ref_jni (JNIEnv *env, MonoObject *obj)
{
	jobject handle, weak;
	int type = JNIWeakGlobalRefType;

	MonoJavaGCBridgeInfo    *bridge_info    = get_gc_bridge_info_for_object (obj);
	if (bridge_info == nullptr)
		return 0;

	monoFunctions.field_get_value (obj, bridge_info->handle, &handle);
	if (gref_log) {
		fprintf (gref_log, "*take_weak obj=%p; handle=%p\n", obj, handle);
		fflush (gref_log);
	}

	weak = env->NewWeakGlobalRef (handle);
	_monodroid_weak_gref_new (handle, get_object_ref_type (env, handle),
			weak, get_object_ref_type (env, weak),
			"finalizer", gettid (), "take_weak_global_ref_jni", 0);

	_monodroid_gref_log_delete (handle, get_object_ref_type (env, handle),
			"finalizer", gettid (), "take_weak_global_ref_jni", 0);
	env->DeleteGlobalRef (handle);
	monoFunctions.field_set_value (obj, bridge_info->handle, &weak);
	monoFunctions.field_set_value (obj, bridge_info->handle_type, &type);
	return 1;
}

MonoGCBridgeObjectKind
OSBridge::gc_bridge_class_kind (MonoClass *klass)
{
	int i;
	if (gc_disabled)
		return MonoGCBridgeObjectKind::GC_BRIDGE_TRANSPARENT_CLASS;

	i = get_gc_bridge_index (klass);
	if (i == static_cast<int> (-NUM_GC_BRIDGE_TYPES)) {
		log_info (LOG_GC, "asked if a class %s.%s is a bridge before we inited java.lang.Object",
			monoFunctions.class_get_namespace (klass),
			monoFunctions.class_get_name (klass));
		return MonoGCBridgeObjectKind::GC_BRIDGE_TRANSPARENT_CLASS;
	}

	if (i >= 0) {
		return MonoGCBridgeObjectKind::GC_BRIDGE_TRANSPARENT_BRIDGE_CLASS;
	}

	return MonoGCBridgeObjectKind::GC_BRIDGE_TRANSPARENT_CLASS;
}

mono_bool
OSBridge::gc_is_bridge_object (MonoObject *object)
{
	void *handle;

	MonoJavaGCBridgeInfo    *bridge_info    = get_gc_bridge_info_for_object (object);
	if (bridge_info == nullptr)
		return 0;

	monoFunctions.field_get_value (object, bridge_info->handle, &handle);
	if (handle == nullptr) {
#if DEBUG
		MonoClass *mclass = monoFunctions.object_get_class (object);
		log_info (LOG_GC, "object of class %s.%s with null handle",
				monoFunctions.class_get_namespace (mclass),
				monoFunctions.class_get_name (mclass));
#endif
		return 0;
	}

	return 1;
}

// Add a reference from an IGCUserPeer jobject to another jobject
mono_bool
OSBridge::add_reference_jobject (JNIEnv *env, jobject handle, jobject reffed_handle)
{
	jclass java_class;
	jmethodID add_method_id;

	java_class = env->GetObjectClass (handle);
	add_method_id = env->GetMethodID (java_class, "monodroidAddReference", "(Ljava/lang/Object;)V");
	if (add_method_id) {
		env->CallVoidMethod (handle, add_method_id, reffed_handle);
		env->DeleteLocalRef (java_class);

		return 1;
	}

	env->ExceptionClear ();
	env->DeleteLocalRef (java_class);
	return 0;
}

// Given a target, extract the bridge_info (if a mono object) and handle. Return success.
mono_bool
OSBridge::load_reference_target (OSBridge::AddReferenceTarget target, OSBridge::MonoJavaGCBridgeInfo** bridge_info, jobject *handle)
{
	if (target.is_mono_object) {
		*bridge_info = get_gc_bridge_info_for_object (target.obj);
		if (!*bridge_info)
			return FALSE;
		monoFunctions.field_get_value (target.obj, (*bridge_info)->handle, handle);
	} else {
		*handle = target.jobj;
	}
	return TRUE;
}

#if DEBUG
// Allocate and return a string describing a target
char*
OSBridge::describe_target (OSBridge::AddReferenceTarget target)
{
	if (target.is_mono_object) {
		MonoClass *klass = monoFunctions.object_get_class (target.obj);
		return utils.monodroid_strdup_printf ("object of class %s.%s",
			monoFunctions.class_get_namespace (klass),
			monoFunctions.class_get_name (klass));
	}
	else
		return utils.monodroid_strdup_printf ("<temporary object %p>", target.jobj);
}
#endif

// Add a reference from one target to another. If the "from" target is a mono_object, it must be a user peer
mono_bool
OSBridge::add_reference (JNIEnv *env, OSBridge::AddReferenceTarget target, OSBridge::AddReferenceTarget reffed_target)
{
	MonoJavaGCBridgeInfo *bridge_info = nullptr, *reffed_bridge_info = nullptr;
	jobject handle, reffed_handle;

	if (!load_reference_target (target, &bridge_info, &handle))
		return FALSE;

	if (!load_reference_target (reffed_target, &reffed_bridge_info, &reffed_handle))
		return FALSE;

	mono_bool success = add_reference_jobject (env, handle, reffed_handle);

	// Flag MonoObjects so they can be cleared in gc_cleanup_after_java_collection.
	// Java temporaries do not need this because the entire GCUserPeer is discarded.
	if (success && target.is_mono_object) {
		int ref_val = 1;
		monoFunctions.field_set_value (target.obj, bridge_info->refs_added, &ref_val);
	}

#if DEBUG
	if (gc_spew_enabled) {
		char *description = describe_target (target),
			 *reffed_description = describe_target (reffed_target);

		if (success)
			log_warn (LOG_GC, "Added reference for %s to %s", description, reffed_description);
		else
			log_error (LOG_GC, "Missing monodroidAddReference method for %s", description);

		free (description);
		free (reffed_description);
	}
#endif

	return success;
}

// Create a target
OSBridge::AddReferenceTarget
OSBridge::target_from_mono_object (MonoObject *obj)
{
	OSBridge::AddReferenceTarget result;
	result.is_mono_object = TRUE;
	result.obj = obj;
	return result;
}

// Create a target
OSBridge::AddReferenceTarget
OSBridge::target_from_jobject (jobject jobj)
{
	OSBridge::AddReferenceTarget result;
	result.is_mono_object = FALSE;
	result.jobj = jobj;
	return result;
}

/* During the xref phase of gc_prepare_for_java_collection, we need to be able to map bridgeless
 * SCCs to their index in temporary_peers. Because for all bridgeless SCCs the num_objs field of
 * MonoGCBridgeSCC is known 0, we can temporarily stash this index as a negative value in the SCC
 * object. This does mean we have to erase our vandalism at the end of the function.
 */
int
OSBridge::scc_get_stashed_index (MonoGCBridgeSCC *scc)
{
	assert ( (scc->num_objs < 0) || "Attempted to load stashed index from an object which does not contain one." == nullptr);
	return -scc->num_objs - 1;
}

void
OSBridge::scc_set_stashed_index (MonoGCBridgeSCC *scc, int index)
{
	scc->num_objs = -index - 1;
}

// Extract the root target for an SCC. If the SCC has bridged objects, this is the first object. If not, it's stored in temporary_peers.
OSBridge::AddReferenceTarget
OSBridge::target_from_scc (MonoGCBridgeSCC **sccs, int idx, JNIEnv *env, jobject temporary_peers)
{
	MonoGCBridgeSCC *scc = sccs [idx];
	if (scc->num_objs > 0)
		return target_from_mono_object (scc->objs [0]);

	jobject jobj = env->CallObjectMethod (temporary_peers, ArrayList_get, scc_get_stashed_index (scc));
	return target_from_jobject (jobj);
}

// Must call this on any AddReferenceTarget returned by target_from_scc once done with it
void
OSBridge::target_release (JNIEnv *env, OSBridge::AddReferenceTarget target)
{
	if (!target.is_mono_object)
		env->DeleteLocalRef (target.jobj);
}

// Add a reference between objects if both are already known to be MonoObjects which are user peers
mono_bool
OSBridge::add_reference_mono_object (JNIEnv *env, MonoObject *obj, MonoObject *reffed_obj)
{
	return add_reference (env, target_from_mono_object (obj), target_from_mono_object (reffed_obj));
}

void
OSBridge::gc_prepare_for_java_collection (JNIEnv *env, int num_sccs, MonoGCBridgeSCC **sccs, int num_xrefs, MonoGCBridgeXRef *xrefs)
{
	/* Some SCCs might have no IGCUserPeers associated with them, so we must create one */
	jobject temporary_peers = nullptr;     // This is an ArrayList
	int temporary_peer_count = 0;       // Number of items in temporary_peers

	/* Before looking at xrefs, scan the SCCs. During collection, an SCC has to behave like a
	 * single object. If the number of objects in the SCC is anything other than 1, the SCC
	 * must be doctored to mimic that one-object nature.
	 */
	for (int i = 0; i < num_sccs; i++) {
		MonoGCBridgeSCC *scc = sccs [i];

		/* num_objs < 0 case: This is a violation of the bridge API invariants. */
		assert ( (scc->num_objs >= 0) || "Bridge processor submitted an SCC with a negative number of objects." == nullptr);

		/* num_objs > 1 case: The SCC contains many objects which must be collected as one.
		 * Solution: Make all objects within the SCC directly or indirectly reference each other
		 */
		if (scc->num_objs > 1) {
			MonoObject *first = scc->objs [0];
			MonoObject *prev = first;

			/* start at the second item, ref j from j-1 */
			for (int j = 1; j < scc->num_objs; j++) {
				MonoObject *current = scc->objs [j];

				add_reference_mono_object (env, prev, current);
				prev = current;
			}

			/* ref the first from the final */
			add_reference_mono_object (env, prev, first);

		/* num_objs == 0 case: The SCC contains *no* objects (or rather contains only C# objects).
		 * Solution: Create a temporary Java object to stand in for the SCC.
		 */
		} else if (scc->num_objs == 0) {
			/* Once per process boot, look up JNI metadata we need to make temporary objects */
			if (!ArrayList_class) {
				ArrayList_class = reinterpret_cast<jclass> (lref_to_gref (env, env->FindClass ("java/util/ArrayList")));
				ArrayList_ctor = env->GetMethodID (ArrayList_class, "<init>", "()V");
				ArrayList_add = env->GetMethodID (ArrayList_class, "add", "(Ljava/lang/Object;)Z");
				ArrayList_get = env->GetMethodID (ArrayList_class, "get", "(I)Ljava/lang/Object;");

				assert ( (ArrayList_class && ArrayList_ctor && ArrayList_get) || "Failed to load classes required for JNI" == nullptr);
			}

			/* Once per gc_prepare_for_java_collection call, create a list to hold the temporary
			 * objects we create. This will protect them from collection while we build the list.
			 */
			if (!temporary_peers) {
				temporary_peers = env->NewObject (ArrayList_class, ArrayList_ctor);
			}

			/* Create this SCC's temporary object */
			jobject peer = env->NewObject (GCUserPeer_class, GCUserPeer_ctor);
			env->CallBooleanMethod (temporary_peers, ArrayList_add, peer);
			env->DeleteLocalRef (peer);

			/* See note on scc_get_stashed_index */
			scc_set_stashed_index (scc, temporary_peer_count);
			temporary_peer_count++;
		}
	}

	/* add the cross scc refs */
	for (int i = 0; i < num_xrefs; i++) {
		AddReferenceTarget src_target = target_from_scc (sccs, xrefs [i].src_scc_index, env, temporary_peers);
		AddReferenceTarget dst_target = target_from_scc (sccs, xrefs [i].dst_scc_index, env, temporary_peers);

		add_reference (env, src_target, dst_target);

		target_release (env, src_target);
		target_release (env, dst_target);
	}

	/* With xrefs processed, the temporary peer list can be released */
	env->DeleteLocalRef (temporary_peers);

	/* Post-xref cleanup on SCCs: Undo memoization, switch to weak refs */
	for (int i = 0; i < num_sccs; i++) {
		/* See note on scc_get_stashed_index */
		if (sccs [i]->num_objs < 0)
			sccs [i]->num_objs = 0;

		for (int j = 0; j < sccs [i]->num_objs; j++) {
			(this->*take_weak_global_ref) (env, sccs [i]->objs [j]);
		}
	}
}

void
OSBridge::gc_cleanup_after_java_collection (JNIEnv *env, int num_sccs, MonoGCBridgeSCC **sccs)
{
#if DEBUG
	MonoClass *klass;
#endif
	MonoObject *obj;
	jclass java_class;
	jobject jref;
	jmethodID clear_method_id;
	int i, j, total, alive, refs_added;

	total = alive = 0;

	/* try to switch back to global refs to analyze what stayed alive */
	for (i = 0; i < num_sccs; i++)
		for (j = 0; j < sccs [i]->num_objs; j++, total++)
			(this->*take_global_ref) (env, sccs [i]->objs [j]);

	/* clear the cross references on any remaining items */
	for (i = 0; i < num_sccs; i++) {
		sccs [i]->is_alive = 0;

		for (j = 0; j < sccs [i]->num_objs; j++) {
			MonoJavaGCBridgeInfo    *bridge_info;

			obj = sccs [i]->objs [j];

			bridge_info = get_gc_bridge_info_for_object (obj);
			if (bridge_info == nullptr)
				continue;
			monoFunctions.field_get_value (obj, bridge_info->handle, &jref);
			if (jref) {
				alive++;
				if (j > 0)
					assert (sccs [i]->is_alive);
				sccs [i]->is_alive = 1;
				monoFunctions.field_get_value (obj, bridge_info->refs_added, &refs_added);
				if (refs_added) {
					java_class = env->GetObjectClass (jref);
					clear_method_id = env->GetMethodID (java_class, "monodroidClearReferences", "()V");
					if (clear_method_id) {
						env->CallVoidMethod (jref, clear_method_id);
					} else {
						env->ExceptionClear ();
#if DEBUG
						if (gc_spew_enabled) {
							klass = monoFunctions.object_get_class (obj);
							log_error (LOG_GC, "Missing monodroidClearReferences method for object of class %s.%s",
									monoFunctions.class_get_namespace (klass),
									monoFunctions.class_get_name (klass));
						}
#endif
					}
					env->DeleteLocalRef (java_class);
				}
			} else {
				assert (!sccs [i]->is_alive);
			}
		}
	}
#if DEBUG
	log_info (LOG_GC, "GC cleanup summary: %d objects tested - resurrecting %d.", total, alive);
#endif
}

void
OSBridge::java_gc (JNIEnv *env)
{
	env->CallVoidMethod (Runtime_instance, Runtime_gc);
}

void
OSBridge::set_bridge_processing_field (MonodroidBridgeProcessingInfo *list, mono_bool value)
{
	for ( ; list != nullptr; list = list->next) {
		MonoClassField *bridge_processing_field = list->bridge_processing_field;
		MonoVTable *jnienv_vtable = list->jnienv_vtable;
		monoFunctions.field_static_set_value (jnienv_vtable, bridge_processing_field, &value);
	}
}

void
OSBridge::gc_cross_references (int num_sccs, MonoGCBridgeSCC **sccs, int num_xrefs, MonoGCBridgeXRef *xrefs)
{
	JNIEnv *env;

	if (gc_disabled)
		return;

#if DEBUG
	if (gc_spew_enabled) {
		int i, j;
		log_info (LOG_GC, "cross references callback invoked with %d sccs and %d xrefs.", num_sccs, num_xrefs);

		for (i = 0; i < num_sccs; ++i) {
			log_info (LOG_GC, "group %d with %d objects", i, sccs [i]->num_objs);
			for (j = 0; j < sccs [i]->num_objs; ++j) {
				MonoObject *obj = sccs [i]->objs [j];
				MonoClass *klass = monoFunctions.object_get_class (obj);
				log_info (LOG_GC, "\tobj %p [%s::%s]",
						obj,
						monoFunctions.class_get_namespace (klass),
						monoFunctions.class_get_name (klass));
			}
		}

		if (utils.should_log (LOG_GC)) {
			for (i = 0; i < num_xrefs; ++i)
				log_info_nocheck (LOG_GC, "xref [%d] %d -> %d", i, xrefs [i].src_scc_index, xrefs [i].dst_scc_index);
		}
	}
#endif

	env = ensure_jnienv ();

	set_bridge_processing_field (domains_list, 1);
	gc_prepare_for_java_collection (env, num_sccs, sccs, num_xrefs, xrefs);

	java_gc (env);

	gc_cleanup_after_java_collection (env, num_sccs, sccs);
	set_bridge_processing_field (domains_list, 0);
}

int
OSBridge::platform_supports_weak_refs (void)
{
	char *value;
	int api_level = 0;

	if (androidSystem.monodroid_get_system_property ("ro.build.version.sdk", &value) > 0) {
		api_level = atoi (value);
		free (value);
	}

	if (utils.monodroid_get_namespaced_system_property (Debug::DEBUG_MONO_WREF_PROPERTY, &value) > 0) {
		int use_weak_refs = 0;
		if (!strcmp ("jni", value))
			use_weak_refs = 1;
		else if (!strcmp ("java", value))
			use_weak_refs = 0;
		else {
			use_weak_refs = -1;
			log_warn (LOG_GC, "Unsupported debug.mono.wref value '%s'; "
					"supported values are 'jni' and 'java'. Ignoring...",
					value);
		}
		free (value);

		if (use_weak_refs && api_level < 8)
			log_warn (LOG_GC, "Using JNI weak references instead of "
					"java.lang.WeakReference on API-%i. Are you sure you want to do this? "
					"The GC may be compromised.",
					api_level);

		if (use_weak_refs >= 0)
			return use_weak_refs;
	}

	if (utils.monodroid_get_namespaced_system_property ("persist.sys.dalvik.vm.lib", &value) > 0) {
		int art = 0;
		if (!strcmp ("libart.so", value))
			art = 1;
		free (value);
		if (art) {
			int use_java = 0;
			if (utils.monodroid_get_namespaced_system_property ("ro.build.version.release", &value) > 0) {
				// Android 4.x ART is busted; see https://code.google.com/p/android/issues/detail?id=63929
				if (value [0] != 0 && value [0] == '4' && value [1] != 0 && value [1] == '.') {
					use_java = 1;
				}
				free (value);
			}
			if (use_java) {
				log_warn (LOG_GC, "JNI weak global refs are broken on Android with the ART runtime.");
				log_warn (LOG_GC, "Trying to use java.lang.WeakReference instead, but this may fail as well.");
				log_warn (LOG_GC, "App stability may be compromised.");
				log_warn (LOG_GC, "See: https://code.google.com/p/android/issues/detail?id=63929");
				return 0;
			}
		}
	}

	if (api_level > 7)
		return 1;
	return 0;
}

void
OSBridge::register_gc_hooks (void)
{
	MonoGCBridgeCallbacks bridge_cbs;

	if (platform_supports_weak_refs ()) {
		take_global_ref = &OSBridge::take_global_ref_jni;
		take_weak_global_ref = &OSBridge::take_weak_global_ref_jni;
		log_info (LOG_GC, "environment supports jni NewWeakGlobalRef");
	} else {
		take_global_ref = &OSBridge::take_global_ref_2_1_compat;
		take_weak_global_ref = &OSBridge::take_weak_global_ref_2_1_compat;
		log_info (LOG_GC, "environment does not support jni NewWeakGlobalRef");
	}

	bridge_cbs.bridge_version = SGEN_BRIDGE_VERSION;
	bridge_cbs.bridge_class_kind = gc_bridge_class_kind_cb;
	bridge_cbs.is_bridge_object = gc_is_bridge_object_cb;
	bridge_cbs.cross_references = gc_cross_references_cb;
	monoFunctions.gc_register_bridge_callbacks (&bridge_cbs);
}

JNIEnv*
OSBridge::ensure_jnienv (void)
{
	JNIEnv *env;
	jvm->GetEnv ((void**)&env, JNI_VERSION_1_6);
	if (env == nullptr) {
		monoFunctions.thread_attach (monoFunctions.domain_get ());
		jvm->GetEnv ((void**)&env, JNI_VERSION_1_6);
	}
	return env;
}

void
OSBridge::initialize_on_onload (JavaVM *vm, JNIEnv *env)
{
	assert (env != nullptr);
	assert (vm != nullptr);

	jvm = vm;
	jclass lref = env->FindClass ("java/lang/Runtime");
	jmethodID Runtime_getRuntime = env->GetStaticMethodID (lref, "getRuntime", "()Ljava/lang/Runtime;");

	Runtime_gc          = env->GetMethodID (lref, "gc", "()V");
	Runtime_instance    = lref_to_gref (env, env->CallStaticObjectMethod (lref, Runtime_getRuntime));
	env->DeleteLocalRef (lref);
	lref = env->FindClass ("java/lang/ref/WeakReference");
	weakrefClass = reinterpret_cast<jclass> (env->NewGlobalRef (lref));
	env->DeleteLocalRef (lref);
	weakrefCtor = env->GetMethodID (weakrefClass, "<init>", "(Ljava/lang/Object;)V");
	weakrefGet = env->GetMethodID (weakrefClass, "get", "()Ljava/lang/Object;");
}

void
OSBridge::initialize_on_runtime_init (JNIEnv *env, jclass runtimeClass)
{
	assert (env != nullptr);
	GCUserPeer_class      = utils.get_class_from_runtime_field(env, runtimeClass, "mono_android_GCUserPeer", true);
	GCUserPeer_ctor       = env->GetMethodID (GCUserPeer_class, "<init>", "()V");
	assert ( (GCUserPeer_class && GCUserPeer_ctor) || "Failed to load mono.android.GCUserPeer!" == nullptr);
}

void
OSBridge::add_monodroid_domain (MonoDomain *domain)
{
	MonodroidBridgeProcessingInfo *node = new MonodroidBridgeProcessingInfo (); //calloc (1, sizeof (MonodroidBridgeProcessingInfo));

	/* We need to prefetch all these information prior to using them in gc_cross_reference as all those functions
	 * use GC API to allocate memory and thus can't be called from within the GC callback as it causes a deadlock
	 * (the routine allocating the memory waits for the GC round to complete first)
	 */
	MonoClass *jnienv = utils.monodroid_get_class_from_name (domain, "Mono.Android", "Android.Runtime", "JNIEnv");;
	node->domain = domain;
	node->bridge_processing_field = monoFunctions.class_get_field_from_name (jnienv, const_cast<char*> ("BridgeProcessing"));
	node->jnienv_vtable = monoFunctions.class_vtable (domain, jnienv);
	node->next = domains_list;

	domains_list = node;
}

void
OSBridge::remove_monodroid_domain (MonoDomain *domain)
{
	MonodroidBridgeProcessingInfo *node = domains_list;
	MonodroidBridgeProcessingInfo *prev = nullptr;

	while (node != nullptr) {
		if (node->domain != domain) {
			prev = node;
			node = node->next;
			continue;
		}

		if (prev != nullptr)
			prev->next = node->next;
		else
			domains_list = node->next;

		free (node);

		break;
	}
}

void
OSBridge::on_destroy_contexts ()
{
	/* If domains_list is now empty, we are about to unload Monodroid.dll.
	 * Clear the global bridge info structure since it's pointing into soon-invalid memory.
	 * FIXME: It is possible for a thread to get into `gc_bridge_class_kind` after this clear
	 *        occurs, but before the stop-the-world during mono_domain_unload. If this happens,
	 *        it can falsely mark a class as transparent. This is considered acceptable because
	 *        this case is *very* rare and the worst case scenario is a resource leak.
	 *        The real solution would be to add a new callback, called while the world is stopped
	 *        during `mono_gc_clear_domain`, and clear the bridge info during that.
	 */
	if (!domains_list)
		osBridge.clear_mono_java_gc_bridge_info ();
}
