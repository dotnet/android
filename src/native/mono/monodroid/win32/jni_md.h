#ifndef __MONODROID_WIN32_JNI_MD_H__
#define __MONODROID_WIN32_JNI_MD_H__

#define JNICALL __stdcall
#define JNIEXPORT __declspec(dllexport)
#define JNIIMPORT __declspec(dllimport)

typedef signed char jbyte;
typedef __int32     jint;
typedef __int64     jlong;

#endif
