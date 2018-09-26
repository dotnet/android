#ifndef __MONODROID_GLUE_H
#define __MONODROID_GLUE_H

#include <stdio.h>
#include <jni.h>

#include "dylib-mono.h"

MONO_API  int monodroid_get_system_property (const char *name, char **value);
int monodroid_get_system_property_from_overrides (const char *name, char ** value);
JNIEnv* get_jnienv (void);

struct DylibMono;

struct DylibMono  *monodroid_get_dylib (void);

void  *monodroid_load_dso (const char *path, int dl_flags, mono_bool skip_exists_check);

extern  FILE  *gref_log;
extern  FILE  *lref_log;

#endif /* __MONODROID_GLUE_H */
