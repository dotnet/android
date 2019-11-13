#include "globals.hh"

using namespace xamarin::android;
using namespace xamarin::android::internal;

Util utils;
AndroidSystem androidSystem;
OSBridge osBridge;
EmbeddedAssemblies embeddedAssemblies;
MonodroidRuntime monodroidRuntime;
Timing *timing = nullptr;
#ifndef ANDROID
DesignerAssemblies designerAssemblies;
#endif
Debug debug;
