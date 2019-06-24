// This is a -*- C++ -*- header
#ifndef __GLOBALS_H
#define __GLOBALS_H

#include "util.hh"
#include "debug.hh"
#include "embedded-assemblies.hh"
#include "inmemory-assemblies.hh"
#include "monodroid-glue-internal.hh"
#include "cppcompat.hh"

extern xamarin::android::Util utils;
extern xamarin::android::internal::AndroidSystem androidSystem;
extern xamarin::android::internal::OSBridge osBridge;
extern xamarin::android::internal::EmbeddedAssemblies embeddedAssemblies;
#ifndef ANDROID
extern xamarin::android::internal::InMemoryAssemblies inMemoryAssemblies;
#endif

#ifdef DEBUG
extern xamarin::android::Debug debug;
#endif

#endif // !__GLOBALS_H
