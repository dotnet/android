// Dear Emacs, this is a -*- C++ -*- header
#ifndef __MONODROID_GLUE_INTERNAL_H
#define __MONODROID_GLUE_INTERNAL_H

#include <jni.h>
#include "dylib-mono.h"

namespace xamarin { namespace android { namespace internal
{
	class MonodroidRuntime
	{
	};

	struct BundledProperty {
		char *name;
		char *value;
		int   value_len;
		struct BundledProperty *next;
	};

	class AndroidSystem
	{
	private:
		static BundledProperty *bundled_properties;

	public:
#ifdef RELEASE
		static constexpr uint32_t MAX_OVERRIDES = 1;
#else
		static constexpr uint32_t MAX_OVERRIDES = 3;
#endif
		static char* override_dirs [MAX_OVERRIDES];
		static const char **app_lib_directories;
		static size_t app_lib_directories_size;

	public:
		void  add_system_property (const char *name, const char *value);
		void  setup_environment (JNIEnv *env, jobjectArray runtimeApks);
		void  setup_process_args (JNIEnv *env, jobjectArray runtimeApks);
		int   monodroid_get_system_property (const char *name, char **value);
		int   monodroid_get_system_property_from_overrides (const char *name, char ** value);
		void  create_update_dir (char *override_dir);
		char* get_libmonosgen_path ();
		char* get_bundled_app (JNIEnv *env, jstring dir);
		int   count_override_assemblies ();
		int   get_gref_gc_threshold ();
		void  setup_apk_directories (JNIEnv *env, unsigned short running_on_cpu, jobjectArray runtimeApks);
		void* load_dso (const char *path, int dl_flags, mono_bool skip_exists_check);
		void* load_dso_from_any_directories (const char *name, int dl_flags);
		char* get_full_dso_path_on_disk (const char *dso_name, mono_bool *needs_free);

		const char* get_override_dir (uint32_t index) const
		{
			if (index >= MAX_OVERRIDES)
				return nullptr;

			return override_dirs [index];
		}

		void set_override_dir (uint32_t index, const char* dir)
		{
			if (index >= MAX_OVERRIDES)
				return;

			override_dirs [index] = const_cast <char*> (dir);
		}

		int get_max_gref_count () const
		{
			return max_gref_count;
		}

		void init_max_gref_count ()
		{
			max_gref_count = get_max_gref_count_from_system ();
		}

	private:
		int  get_max_gref_count_from_system ();
		void setup_environment_from_line (const char *line);
		void setup_environment_from_file (const char *apk, int index, int apk_count, void *user_data);
		BundledProperty* lookup_system_property (const char *name);
		void setup_process_args_apk (const char *apk, int index, int apk_count, void *user_data);
		int  _monodroid__system_property_get (const char *name, char *sp_value, size_t sp_value_len);
		int  _monodroid_get_system_property_from_file (const char *path, char **value);
		void  copy_native_libraries_to_internal_location ();
		void  copy_file_to_internal_location (char *to_dir, char *from_dir, char *file);
		void  add_apk_libdir (const char *apk, int index, int apk_count, void *user_data);
		void  for_each_apk (JNIEnv *env, jobjectArray runtimeApks, void (AndroidSystem::*handler) (const char *apk, int index, int apk_count, void *user_data), void *user_data);
		char* get_full_dso_path (const char *base_dir, const char *dso_path, mono_bool *needs_free);
		void* load_dso_from_specified_dirs (const char **directories, int num_entries, const char *dso_name, int dl_flags);
		void* load_dso_from_app_lib_dirs (const char *name, int dl_flags);
		void* load_dso_from_override_dirs (const char *name, int dl_flags);
		char* get_existing_dso_path_on_disk (const char *base_dir, const char *dso_name, mono_bool *needs_free);
		void  dso_alloc_cleanup (char **dso_path, mono_bool *needs_free);

#if !defined (ANDROID)
		void monodroid_strreplace (char *buffer, char old_char, char new_char);
#endif
	private:
		int max_gref_count = 0;
	};

	class OSBridge
	{
	public:
		struct MonoJavaGCBridgeType
		{
			const char *_namespace;
			const char *_typename;
		};

		/* `mono_java_gc_bridge_info` stores shared global data about the last Monodroid assembly loaded.
		 * Specifically it stores data about the `mono_java_gc_bridge_types` types.
		 * In order for this to work, two rules must be followed.
		 *   1. Only one Monodroid appdomain can be loaded at a time.
		 *   2. Since the Monodroid appdomain unload clears `mono_java_gc_bridge_info`, anything which
		 *      could run at the same time as the domain unload (like gc_bridge_class_kind) must tolerate
		 *      the structure fields being set to zero during run
		 */
		struct MonoJavaGCBridgeInfo
		{
			MonoClass       *klass;
			MonoClassField  *handle;
			MonoClassField  *handle_type;
			MonoClassField  *refs_added;
			MonoClassField  *weak_handle;
		};

		// add_reference can work with objects which are either MonoObjects with java peers, or raw jobjects
		struct AddReferenceTarget
		{
			mono_bool is_mono_object;
			union {
				MonoObject *obj;
				jobject jobj;
			};
		};

		using MonodroidGCTakeRefFunc = mono_bool (OSBridge::*) (JNIEnv *env, MonoObject *obj);

		static const MonoJavaGCBridgeType empty_bridge_type;
		static const MonoJavaGCBridgeType mono_java_gc_bridge_types[];
		static MonoJavaGCBridgeInfo empty_bridge_info;
		static MonoJavaGCBridgeInfo mono_java_gc_bridge_info [];

	public:
		static const uint32_t NUM_GC_BRIDGE_TYPES;

	public:
		void clear_mono_java_gc_bridge_info ();
		jobject	lref_to_gref (JNIEnv *env, jobject lref);

		int get_gc_gref_count () const
		{
			return gc_gref_count;
		}

		const MonoJavaGCBridgeType& get_java_gc_bridge_type (uint32_t index)
		{
			if (index >= NUM_GC_BRIDGE_TYPES)
				return empty_bridge_type; // Not ideal...

			return mono_java_gc_bridge_types [index];
		}

		MonoJavaGCBridgeInfo& get_java_gc_bridge_info (uint32_t index)
		{
			if (index >= NUM_GC_BRIDGE_TYPES)
				return empty_bridge_info; // Not ideal...

			return mono_java_gc_bridge_info [index];
		}

		void _monodroid_gref_log (const char *message);
		int _monodroid_gref_log_new (jobject curHandle, char curType, jobject newHandle, char newType, const char *threadName, int threadId, const char *from, int from_writable);
		void _monodroid_gref_log_delete (jobject handle, char type, const char *threadName, int threadId, const char *from, int from_writable);
		void _monodroid_weak_gref_new (jobject curHandle, char curType, jobject newHandle, char newType, const char *threadName, int threadId, const char *from, int from_writable);
		void _monodroid_weak_gref_delete (jobject handle, char type, const char *threadName, int threadId, const char *from, int from_writable);
		void _monodroid_lref_log_new (int lrefc, jobject handle, char type, const char *threadName, int threadId, const char *from, int from_writable);
		void _monodroid_lref_log_delete (int lrefc, jobject handle, char type, const char *threadName, int threadId, const char *from, int from_writable);
		void monodroid_disable_gc_hooks ();
		void register_gc_hooks ();
		MonoGCBridgeObjectKind gc_bridge_class_kind (MonoClass *klass);
		mono_bool gc_is_bridge_object (MonoObject *object);
		void gc_cross_references (int num_sccs, MonoGCBridgeSCC **sccs, int num_xrefs, MonoGCBridgeXRef *xrefs);
		int get_gref_gc_threshold ();

	private:
		int get_gc_bridge_index (MonoClass *klass);
		MonoJavaGCBridgeInfo* get_gc_bridge_info_for_class (MonoClass *klass);
		MonoJavaGCBridgeInfo* get_gc_bridge_info_for_object (MonoObject *object);
		char get_object_ref_type (JNIEnv *env, void *handle);
		int _monodroid_gref_inc ();
		int _monodroid_gref_dec ();
		char* _get_stack_trace_line_end (char *m);
		void _write_stack_trace (FILE *to, const char *from);
		mono_bool take_global_ref_2_1_compat (JNIEnv *env, MonoObject *obj);
		mono_bool take_weak_global_ref_2_1_compat (JNIEnv *env, MonoObject *obj);
		mono_bool take_global_ref_jni (JNIEnv *env, MonoObject *obj);
		mono_bool take_weak_global_ref_jni (JNIEnv *env, MonoObject *obj);
		mono_bool add_reference_jobject (JNIEnv *env, jobject handle, jobject reffed_handle);
		mono_bool load_reference_target (AddReferenceTarget target, MonoJavaGCBridgeInfo** bridge_info, jobject *handle);
		mono_bool add_reference (JNIEnv *env, AddReferenceTarget target, AddReferenceTarget reffed_target);
		AddReferenceTarget target_from_mono_object (MonoObject *obj);
		AddReferenceTarget target_from_jobject (jobject jobj);
		int scc_get_stashed_index (MonoGCBridgeSCC *scc);
		void scc_set_stashed_index (MonoGCBridgeSCC *scc, int index);
		AddReferenceTarget target_from_scc (MonoGCBridgeSCC **sccs, int idx, JNIEnv *env, jobject temporary_peers);
		void target_release (JNIEnv *env, AddReferenceTarget target);
		mono_bool add_reference_mono_object (JNIEnv *env, MonoObject *obj, MonoObject *reffed_obj);
		void gc_prepare_for_java_collection (JNIEnv *env, int num_sccs, MonoGCBridgeSCC **sccs, int num_xrefs, MonoGCBridgeXRef *xrefs);
		void gc_cleanup_after_java_collection (JNIEnv *env, int num_sccs, MonoGCBridgeSCC **sccs);
		void java_gc (JNIEnv *env);
		int platform_supports_weak_refs ();

#if DEBUG
		char* describe_target (AddReferenceTarget target);
#endif
	private:
		int gc_gref_count = 0;
		int gc_weak_gref_count = 0;
		int gc_disabled = 0;
		MonodroidGCTakeRefFunc take_global_ref = nullptr;
		MonodroidGCTakeRefFunc take_weak_global_ref = nullptr;
	};
} } }
#endif
