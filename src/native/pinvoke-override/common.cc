#include "pinvoke-override-api.hh"

using namespace xamarin::android;

PinvokeOverride::pinvoke_library_map PinvokeOverride::other_pinvoke_map (PinvokeOverride::LIBRARY_MAP_INITIAL_BUCKET_COUNT);
<<<<<<< HEAD
<<<<<<< HEAD
xamarin::android::mutex PinvokeOverride::pinvoke_map_write_lock;
=======
std::mutex PinvokeOverride::pinvoke_map_write_lock;
>>>>>>> 13ba4b152 (Let's see what breaks)
=======
xamarin::android::mutex PinvokeOverride::pinvoke_map_write_lock;
>>>>>>> da87011a5 (Fix post-rebase issues and conflicts)
