#include "pinvoke-override-api.hh"

using namespace xamarin::android;

PinvokeOverride::pinvoke_library_map PinvokeOverride::other_pinvoke_map (PinvokeOverride::LIBRARY_MAP_INITIAL_BUCKET_COUNT);
xamarin::android::mutex PinvokeOverride::pinvoke_map_write_lock;
