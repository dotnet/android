#include <cstdlib>
#include <android/log.h>

#include <mono/metadata/appdomain.h>
#include <mono/metadata/object.h>

#include "xamarin-app-marshaling-private.hh"

using namespace xamarin::android::internal;

static XamarinAppMarshaling xam;

void XamarinAppMarshaling::init (AppContext *ctx) noexcept
{
	context = ctx;
}

force_inline
MonoObject* XamarinAppMarshaling::invoke_managed_method (uint8_t *module_uuid, uint32_t module_id, uint32_t type_token, uint32_t method_token, void **params) const noexcept
{
	// TODO: caching
	// TODO: should we abort() if we can't find the image, class or method?
	MonoImage  *image = context->lookup_mono_image (module_uuid);
	MonoClass  *klass = mono_class_get (image, type_token);
	MonoMethod *method = mono_get_method (image, method_token, klass);
	MonoObject *exc = nullptr;
	MonoObject *ret = mono_runtime_invoke (method, nullptr, params, &exc);

	if (exc != nullptr) {
		// TODO: call AndroidEnvironment.UnhandledException(exc)
	}

	return ret;
}

MonoObject* monodroid_invoke_managed_method (uint8_t *module_uuid, uint32_t module_id,  uint32_t type_token, uint32_t method_token, void **params)
{
	return xam.invoke_managed_method (module_uuid, module_id, type_token, method_token, params);
}

jboolean monodroid_unbox_value_boolean (MonoObject *value)
{
	return xam.unbox_value<jboolean> (value);
}

jbyte monodroid_unbox_value_byte (MonoObject *value)
{
	return xam.unbox_value<jbyte> (value);
}

jchar monodroid_unbox_value_char (MonoObject *value)
{
	return xam.unbox_value<jchar> (value);
}

jdouble monodroid_unbox_value_double (MonoObject *value)
{
	return xam.unbox_value<jdouble> (value);
}

jfloat monodroid_unbox_value_float (MonoObject *value)
{
	return xam.unbox_value<jfloat> (value);
}

jint monodroid_unbox_value_int (MonoObject *value)
{
	return xam.unbox_value<jint> (value);
}

jlong monodroid_unbox_value_long (MonoObject *value)
{
	return xam.unbox_value<jlong> (value);
}

void* monodroid_unbox_value_pointer (MonoObject *value)
{
	return xam.unbox_value<void*> (value);
}

jshort monodroid_unbox_value_short (MonoObject *value)
{
	return xam.unbox_value<jshort> (value);
}

void xamarin_app_init (AppContext *context)
{
	xam.init (context);
}
