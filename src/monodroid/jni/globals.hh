// This is a -*- C++ -*- header
#ifndef __GLOBALS_H
#define __GLOBALS_H

#if !defined (DEBUG_APP_HELPER)
#include "util.hh"
#else
#include "basic-utilities.hh"
#endif
#include "timing.hh"

#include "debug.hh"
#include "embedded-assemblies.hh"
#include "designer-assemblies.hh"
#include "monodroid-glue-internal.hh"
#include "cppcompat.hh"

extern xamarin::android::Debug debug;
#if !defined (DEBUG_APP_HELPER)
extern xamarin::android::Util utils;
#else
extern xamarin::android::BasicUtilities utils;
#endif
extern xamarin::android::internal::AndroidSystem androidSystem;
extern xamarin::android::internal::OSBridge osBridge;
extern xamarin::android::internal::EmbeddedAssemblies embeddedAssemblies;
extern xamarin::android::internal::MonodroidRuntime monodroidRuntime;
extern xamarin::android::Timing *timing;

#ifndef ANDROID
extern xamarin::android::internal::DesignerAssemblies designerAssemblies;
#endif

#endif // !__GLOBALS_H
