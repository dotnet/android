#pragma once

#include <jni.h>

int _monodroid_gref_get ();
int _monodroid_gref_log_new (jobject curHandle, char curType, jobject newHandle, char newType, const char *threadName, int threadId, const char *from, int from_writable);
