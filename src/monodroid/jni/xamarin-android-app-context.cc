#include <mono/metadata/class.h>

#include "monodroid-glue-internal.hh"
#include "mono-image-loader.hh"

using namespace xamarin::android::internal;

static constexpr char Unknown[] = "Unknown";

const char*
MonodroidRuntime::get_method_name (uint32_t mono_image_index, uint32_t method_token) noexcept
{
	uint64_t id = (static_cast<uint64_t>(mono_image_index) << 32) | method_token;

	size_t i = 0;
	while (mm_method_names[i].id != 0) {
		if (mm_method_names[i].id == id) {
			return mm_method_names[i].name;
		}
	}

	return Unknown;
}

const char*
MonodroidRuntime::get_class_name (uint32_t class_index) noexcept
{
	if (class_index >= marshal_methods_number_of_classes) {
		return Unknown;
	}

	return mm_class_names[class_index];
}

template<bool NeedsLocking>
force_inline void
MonodroidRuntime::get_function_pointer (uint32_t mono_image_index, uint32_t class_index, uint32_t method_token, void *&target_ptr) noexcept
{
	log_warn (LOG_DEFAULT, __PRETTY_FUNCTION__);
	log_debug (
		LOG_ASSEMBLY,
		"MM: Trying to look up pointer to method '%s' in class '%s'",
		get_method_name (mono_image_index, method_token),
		get_class_name (class_index)
	);

	if (XA_UNLIKELY (class_index >= marshal_methods_number_of_classes)) {
		log_fatal (LOG_DEFAULT,
		           "Internal error: invalid index for class cache (expected at most %u, got %u)",
		           marshal_methods_number_of_classes - 1,
		           class_index
		);
		abort ();
	}

	// We don't check for valid return values from image loader, class and method lookup because if any
	// of them fails to find the requested entity, they will return `null`.  In consequence, we can pass
	// these pointers without checking all the way to `mono_method_get_unmanaged_callers_only_ftnptr`, after
	// which call we check for errors.  This saves some time (not much, but definitely more than zero)
	MonoImage *image = MonoImageLoader::get_from_index (mono_image_index);
	log_warn (LOG_DEFAULT, "  image == %p", image);

	MarshalMethodsManagedClass &klass = marshal_methods_class_cache[class_index];
	if (klass.klass == nullptr) {
		log_warn (LOG_DEFAULT, "  class not found yet, getting");
		klass.klass = mono_class_get (image, klass.token);
		log_warn (LOG_DEFAULT, "  class == %p", klass.klass);
	}

	MonoMethod *method = mono_get_method (image, method_token, klass.klass);
	log_warn (LOG_DEFAULT, "  method == %p (%s.%s:%s)", method, mono_class_get_namespace (klass.klass), mono_class_get_name (klass.klass), mono_method_get_name (method));

	MonoError error;
	void *ret = mono_method_get_unmanaged_callers_only_ftnptr (method, &error);
	log_warn (LOG_DEFAULT, "  ret == %p", ret);

	if (ret == nullptr || error.error_code != MONO_ERROR_NONE) {
		log_fatal (LOG_DEFAULT,
		           "Failed to obtain function pointer to method '%s' in class '%s'",
		           get_method_name (mono_image_index, method_token),
		           get_class_name (class_index)
		);
		log_fatal (LOG_DEFAULT,
		           "Looked for image index %u, class index %u, method token 0x%x",
		           mono_image_index,
		           class_index,
		           method_token
		);
		if (image == nullptr) {
			log_fatal (LOG_DEFAULT, "Failed to load MonoImage for the assembly");
		} else if (method == nullptr) {
			log_fatal (LOG_DEFAULT, "Failed to load class from the assembly");
		}

		const char *msg = mono_error_get_message (&error);
		if (msg != nullptr) {
			log_fatal (LOG_DEFAULT, msg);
		}

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
