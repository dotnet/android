#include <host/host.hh>
#include <host/host-jni.hh>

using namespace xamarin::android;

JNIEXPORT jint JNICALL
XA_Host_NativeAOT_JNI_OnLoad (JavaVM *vm, void *reserved)
{
	return Host::Java_JNI_OnLoad (vm, reserved);
}
