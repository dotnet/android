#if INSIDE_MONO_ANDROID_RUNTIME
namespace Android.Runtime
{
	// Keep in sync with the LogLevel enum in
	// monodroid/libmonodroid/logger.{c,h}
	internal enum LogLevel {
		Unknown = 0x00,
		Default = 0x01,
		Verbose = 0x02,
		Debug   = 0x03,
		Info    = 0x04,
		Warn    = 0x05,
		Error   = 0x06,
		Fatal   = 0x07,
		Silent  = 0x08
	}
}
#endif // INSIDE_MONO_ANDROID_RUNTIME
