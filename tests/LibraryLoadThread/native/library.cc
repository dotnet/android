#include <android/log.h>

extern "C" void
HelloWorld (const char *from)
{
	__android_log_print (ANDROID_LOG_INFO, "XAThreadLoad", "Hello World! From %s", from == nullptr ? "nowhere?" : from);
}
