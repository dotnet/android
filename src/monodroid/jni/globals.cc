#include "globals.hh"
#include "gsl.hh"

using namespace xamarin::android;
using namespace xamarin::android::internal;

Util utils;
OSBridge osBridge;
EmbeddedAssemblies embeddedAssemblies;
gsl::owner<Timing*> timing = nullptr;
#ifndef ANDROID
DesignerAssemblies designerAssemblies;
#endif
Debug debug;
