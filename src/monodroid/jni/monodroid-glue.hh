// This is a -*- c++ -*- header
#ifndef __MONODROID_GLUE_H
#define __MONODROID_GLUE_H

#include <stdio.h>
#include <jni.h>

#include <mono/metadata/appdomain.h>
#include <mono/utils/mono-publib.h>

#if !defined (NET6)
#ifdef __cplusplus
extern "C" {
#endif // __cplusplus
	MONO_API  int monodroid_get_system_property (const char *name, char **value);
	MONO_API  int monodroid_getpagesize (void);
#ifdef __cplusplus
}
#endif // __cplusplus
#endif // NET6

int monodroid_get_system_property_from_overrides (const char *name, char ** value);
JNIEnv* get_jnienv (void);

extern  FILE  *gref_log;
extern  FILE  *lref_log;
#endif /* __MONODROID_GLUE_H */
