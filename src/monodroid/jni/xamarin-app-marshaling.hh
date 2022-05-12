// Dear Emacs, this is a -*- C++ -*- header
#if !defined (__XAMARIN_APP_MARSHALING_HH)
#define __XAMARIN_APP_MARSHALING_HH

#include <cstddef>
#include <cstdint>

#include <mono/metadata/image.h>
#include <mono/utils/mono-publib.h>

namespace xamarin::android::internal
{
	class AppContext
	{
	public:
		virtual void* get_function_pointer (uint32_t mono_image_index, uint32_t class_token, uint32_t method_token) = 0;
	};
}

//
// Called by libmonodroid.so on init
//
MONO_API_EXPORT void xamarin_app_init (xamarin::android::internal::AppContext *ctx);

#endif // ndef __XAMARIN_APP_MARSHALING_HH
