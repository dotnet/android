// This is a -*- c++ -*- header
#ifndef __MONODROID_GLUE_H
#define __MONODROID_GLUE_H

#include <stdio.h>
#include <jni.h>

#include <mono/metadata/appdomain.h>
#include <mono/utils/mono-publib.h>

#if !defined (NET)
#ifdef __cplusplus
extern "C" {
#endif // __cplusplus
	MONO_API  int monodroid_get_system_property (const char *name, char **value);
	MONO_API  int monodroid_getpagesize (void);
#ifdef __cplusplus
}
#endif // __cplusplus
#endif // NET

int monodroid_get_system_property_from_overrides (const char *name, char ** value);
JNIEnv* get_jnienv (void);

extern  FILE  *gref_log;
extern  FILE  *lref_log;
extern  bool    gref_to_logcat;
extern  bool    lref_to_logcat;
#endif /* __MONODROID_GLUE_H */
