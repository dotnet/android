// Dear Emacs, this is a -*- C++ -*- header
#if !defined (__XAMARIN_APP_MARSHALING_PRIVATE_HH)
#define __XAMARIN_APP_MARSHALING_PRIVATE_HH

#include <cstddef>
#include <cstdint>
#include <cstdlib>

#if !defined (__FOR_GENERATOR_ONLY)
#include <jni.h>
#include <mono/metadata/appdomain.h>
#include <mono/metadata/loader.h>
#include <mono/utils/mono-publib.h>
#include <mono/metadata/object.h>

#include "platform-compat.hh"
#include "xamarin-app-marshaling.hh"

//
// Functions here must be in the global namespace and have C linkage since they are referenced from the generated native
// assembler code.
//
extern "C" {
	//
	// module_uuid: points to a 16-byte array with the UUID, used to look up in typemap
	// module_id: unique module ID generated at build time, used for hashing together with type_token and method_token
	//
	MonoObject* monodroid_invoke_managed_method (uint8_t *module_uuid, uint32_t module_id, uint32_t type_token, uint32_t method_token, void **params);
	jboolean monodroid_unbox_value_boolean (MonoObject *value);
	jbyte monodroid_unbox_value_byte (MonoObject *value);
	jchar monodroid_unbox_value_char (MonoObject *value);
	jdouble monodroid_unbox_value_double (MonoObject *value);
	jfloat monodroid_unbox_value_float (MonoObject *value);
	jint monodroid_unbox_value_int (MonoObject *value);
	jlong monodroid_unbox_value_long (MonoObject *value);
	void* monodroid_unbox_value_pointer (MonoObject *value);
	jshort monodroid_unbox_value_short (MonoObject *value);
}
#endif // ndef __FOR_GENERATOR_ONLY

namespace xamarin::android::internal
{
	enum class MarshalingTypes
	{
		Boolean,
		Byte,
		Char,
		Double,
		Float,
		Int,
		Long,
		Pointer,
		Short,
	};

#if !defined (__FOR_GENERATOR_ONLY)
	class XamarinAppMarshaling
	{
	public:
		void init (AppContext *context) noexcept;

	public:
		template<class TRet>
		force_inline
		static TRet unbox_value (MonoObject *value) noexcept
		{
			void *ret = mono_object_unbox (value);
			if (ret == nullptr) [[unlikely]] {
				// TODO: log
				abort ();
			}

			return *reinterpret_cast<TRet*>(ret);
		}

		MonoObject* invoke_managed_method (uint8_t *module_uuid, uint32_t module_id, uint32_t type_token, uint32_t method_token, void **params) const noexcept;

	private:
		AppContext *context      = nullptr;
	};
#endif // ndef __FOR_GENERATOR_ONLY
}

#endif // ndef __XAMARIN_APP_MARSHALING_HH
