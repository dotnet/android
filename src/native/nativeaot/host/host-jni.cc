#include <host/host-jni.hh>
#include <host/host-nativeaot.hh>
#include <runtime-base/logger.hh>

using namespace xamarin::android;

auto XA_Host_NativeAOT_JNI_OnLoad (JavaVM *vm, void *reserved) -> int
{
	return Host::Java_JNI_OnLoad (vm, reserved);
}

void XA_Host_NativeAOT_OnInit ()
{
	Host::OnInit ();
}
