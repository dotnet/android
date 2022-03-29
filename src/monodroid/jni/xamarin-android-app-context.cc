#include <mono/metadata/class.h>

#include "monodroid-glue-internal.hh"
#include "mono-image-loader.hh"

using namespace xamarin::android::internal;

template<bool NeedsLocking>
force_inline void
MonodroidRuntime::get_function_pointer (uint32_t mono_image_index, uint32_t class_index, uint32_t method_token, void *&target_ptr) noexcept
{
	// We don't check for valid return values from image loader, class and method lookup because if any
	// of them fails to find the requested entity, they will return `null`.  In consequence, we can pass
	// these pointers without checking all the way to `mono_method_get_unmanaged_callers_only_ftnptr`, after
	// which call we check for errors.  This saves some time (not much, but definitely more than zero)
	MonoImage *image = MonoImageLoader::get_from_index (mono_image_index);

	if (XA_UNLIKELY (class_index >= marshal_methods_number_of_classes)) {
		log_fatal (LOG_DEFAULT,
		           "Internal error: invalid index for class cache (expected at most %u, got %u)",
		           marshal_methods_number_of_classes - 1,
		           class_index
		);
		abort ();
	}

	MarshalMethodsManagedClass &klass = marshal_methods_class_cache[class_index];
	if (klass.klass == nullptr) {
		klass.klass = mono_class_get (image, klass.token);
	}

	MonoMethod *method = mono_get_method (image, method_token, klass.klass);

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

	if constexpr (NeedsLocking) {
		// TODO: use atomic write
		target_ptr = ret;
	} else {
		target_ptr = ret;
	}
}

void
MonodroidRuntime::get_function_pointer_at_startup (uint32_t mono_image_index, uint32_t class_index, uint32_t method_token, void *&target_ptr) noexcept
{
	get_function_pointer<false> (mono_image_index, class_index, method_token, target_ptr);
}

void
MonodroidRuntime::get_function_pointer_at_runtime (uint32_t mono_image_index, uint32_t class_index, uint32_t method_token, void *&target_ptr) noexcept
{
	get_function_pointer<true> (mono_image_index, class_index, method_token, target_ptr);
}
