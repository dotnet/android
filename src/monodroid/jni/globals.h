// This is a -*- C++ -*- header
#ifndef __GLOBALS_H
#define __GLOBALS_H

#include "dylib-mono.h"
#include "util.h"
#include "debug.h"
#include "monodroid-glue-internal.h"

extern xamarin::android::DylibMono monoFunctions;
extern xamarin::android::Util utils;
extern xamarin::android::internal::AndroidSystem androidSystem;
extern xamarin::android::internal::OSBridge osBridge;

#ifdef DEBUG
extern xamarin::android::Debug debug;
#endif

#endif // !__GLOBALS_H
