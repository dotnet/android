#include <mono/metadata/class.h>

#include "monodroid-glue-internal.hh"
#include "mono-image-loader.hh"

using namespace xamarin::android::internal;

void MonodroidRuntime::get_function_pointer (uint32_t mono_image_index, uint32_t class_index, uint32_t method_token, void *&target_ptr) noexcept
{
	MonoImage *image = MonoImageLoader::get_from_index (mono_image_index);

	// TODO: implement MonoClassLoader with caching. Best to use indexes instead of keying on tokens.
	MonoClass *method_klass = mono_class_get (image, class_index);
	MonoMethod *method = mono_get_method (image, method_token, method_klass);

	MonoError error;
	void *ret = mono_method_get_unmanaged_callers_only_ftnptr (method, &error);
	if (ret == nullptr || error.error_code != MONO_ERROR_NONE) {
		// TODO: make the error message friendlier somehow (class, method and assembly names)
		log_fatal (LOG_DEFAULT,
		           "Failed to obtain function pointer to method with token 0x%x; class token: 0x%x; assembly index: %u",
		           method_token, class_index, mono_images_cleanup
		);
		abort ();
	}

	target_ptr = ret;
}
