#include "mono-image-loader.hh"

#if defined (USE_CACHE)
size_t xamarin::android::internal::MonoImageLoader::number_of_cache_index_entries = application_config.number_of_assemblies_in_apk * number_of_assembly_name_forms_in_image_cache;
#endif // def USE_CACHE
