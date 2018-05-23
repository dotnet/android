#ifndef __MONODROID_GLUE_H
#define __MONODROID_GLUE_H

#include <stdio.h>
#include <jni.h>

int monodroid_get_system_property_from_overrides (const char *name, char ** value);

struct DylibMono;

struct DylibMono  *monodroid_get_dylib (void);

extern  FILE  *gref_log;
extern  FILE  *lref_log;

#endif /* __MONODROID_GLUE_H */
