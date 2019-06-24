#include "globals.hh"

using namespace xamarin::android;
using namespace xamarin::android::internal;

Util utils;
AndroidSystem androidSystem;
OSBridge osBridge;
EmbeddedAssemblies embeddedAssemblies;
#ifndef ANDROID
InMemoryAssemblies inMemoryAssemblies;
#endif

#ifdef DEBUG
Debug debug;
#endif
