#pragma once

#include <cstdio>

#include <mono/utils/mono-publib.h>
#include <java-interop.h>
#include <jni.h>

#include "log_types.hh"
#include "timing.hh"
#include "xamarin-app.hh"
#include "xamarin_getifaddrs.h"

int _monodroid_getifaddrs (struct _monodroid_ifaddrs **ifap);
void _monodroid_freeifaddrs (struct _monodroid_ifaddrs *ifa);

mono_bool _monodroid_get_network_interface_up_state (const char *ifname, mono_bool *is_up);
mono_bool _monodroid_get_network_interface_supports_multicast (const char *ifname, mono_bool *supports_multicast);
int _monodroid_get_dns_servers (void **dns_servers_array);

int monodroid_get_system_property (const char *name, char **value);
int monodroid_embedded_assemblies_set_assemblies_prefix (const char *prefix);
void monodroid_log (xamarin::android::LogLevel level, LogCategories category, const char *message);
void monodroid_free (void *ptr);
int _monodroid_max_gref_get ();
int _monodroid_gref_get ();
void _monodroid_gref_log (const char *message);
int _monodroid_gref_log_new (jobject curHandle, char curType, jobject newHandle, char newType, const char *threadName, int threadId, const char *from, int from_writable);
void _monodroid_gref_log_delete (jobject handle, char type, const char *threadName, int threadId, const char *from, int from_writable);
int _monodroid_weak_gref_get ();
void _monodroid_weak_gref_new (jobject curHandle, char curType, jobject newHandle, char newType, const char *threadName, int threadId, const char *from, int from_writable);
void _monodroid_weak_gref_delete (jobject handle, char type, const char *threadName, int threadId, const char *from, int from_writable);
void _monodroid_lref_log_new (int lrefc, jobject handle, char type, const char *threadName, int threadId, const char  *from, int from_writable);
void _monodroid_lref_log_delete (int lrefc, jobject handle, char type, const char *threadName, int threadId, const char  *from, int from_writable);
void _monodroid_gc_wait_for_bridge_processing ();
void monodroid_clear_gdb_wait ();
void* _monodroid_timezone_get_default_id ();
void _monodroid_counters_dump ([[maybe_unused]] const char *format, [[maybe_unused]] va_list args);
xamarin::android::managed_timing_sequence* monodroid_timing_start (const char *message);
void monodroid_timing_stop (xamarin::android::managed_timing_sequence *sequence, const char *message);
char** monodroid_strsplit (const char *str, const char *delimiter, size_t max_tokens);
void monodroid_strfreev (char **str_array);
char* monodroid_strdup_printf (const char *format, ...);
char* monodroid_TypeManager_get_java_class_name (jclass klass);
int monodroid_get_namespaced_system_property (const char *name, char **value);
FILE* monodroid_fopen (const char* filename, const char* mode);
int send_uninterrupted (int fd, void *buf, int len);
int recv_uninterrupted (int fd, void *buf, int len);
void set_world_accessable (const char *path);
void create_public_directory (const char *dir);
char* path_combine (const char *path1, const char *path2);
void* monodroid_dylib_mono_new ([[maybe_unused]] const char *libmono_path);
void monodroid_dylib_mono_free ([[maybe_unused]] void *mono_imports);
int monodroid_dylib_mono_init (void *mono_imports, [[maybe_unused]] const char *libmono_path);
void* monodroid_get_dylib ();
const char* _monodroid_lookup_replacement_type (const char *jniSimpleReference);
const JniRemappingReplacementMethod* _monodroid_lookup_replacement_method_info (const char *jniSourceType, const char *jniMethodName, const char *jniMethodSignature);
void monodroid_log_traces (uint32_t kind, const char *first_line);
void _monodroid_detect_cpu_and_architecture (unsigned short *built_for_cpu, unsigned short *running_on_cpu, unsigned char *is64bit);
