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
		virtual MonoImage *lookup_mono_image (uint8_t *module_uuid) = 0;
	};
}

//
// Called by libmonodroid.so on init
//
MONO_API_EXPORT void xamarin_app_init (xamarin::android::internal::AppContext *ctx);

#endif // ndef __XAMARIN_APP_MARSHALING_HH
