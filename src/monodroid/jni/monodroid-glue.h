// This is a -*- c++ -*- header
#ifndef __MONODROID_GLUE_H
#define __MONODROID_GLUE_H

#include <stdio.h>
#include <jni.h>

#include "dylib-mono.h"

#ifdef __cplusplus
extern "C" {
#endif
	MONO_API  int monodroid_get_system_property (const char *name, char **value);
	MONO_API  int monodroid_getpagesize (void);
#ifdef __cplusplus
}
#endif
int monodroid_get_system_property_from_overrides (const char *name, char ** value);
JNIEnv* get_jnienv (void);

extern  FILE  *gref_log;
extern  FILE  *lref_log;

#endif /* __MONODROID_GLUE_H */
