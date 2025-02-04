#include <host/os-bridge.hh>
#include <runtime-base/internal-pinvokes.hh>

using namespace xamarin::android;

int
_monodroid_gref_get ()
{
    return OSBridge::get_gc_gref_count ();
}

int
_monodroid_gref_log_new (jobject curHandle, char curType, jobject newHandle, char newType, const char *threadName, int threadId, const char *from, int from_writable)
{
    return OSBridge::_monodroid_gref_log_new (curHandle, curType, newHandle, newType, threadName, threadId, from, from_writable);
}
