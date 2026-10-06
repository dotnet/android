#pragma once

#include <cstdlib>

#include <constants.hh>
#include <managed-interface.hh>
#include <shared/helpers.hh>

namespace xamarin::android {
	inline void load_jni_remapping_asset (JNIEnv *env, JnienvInitializeArgs &args) noexcept
	{
		auto check_exception = [env] {
			if (env->ExceptionCheck ()) [[unlikely]] {
				env->ExceptionDescribe ();
				env->ExceptionClear ();
				Helpers::abort_application ("Failed to load the JNI remapping asset");
			}
		};

		jclass reader = env->FindClass ("net/dot/android/JniRemappingAsset");
		check_exception ();
		abort_unless (reader != nullptr, "Could not find the JNI remapping asset reader");
		jmethodID read = env->GetStaticMethodID (reader, "read", "(Ljava/lang/String;)[B");
		check_exception ();
		abort_unless (read != nullptr, "Could not find the JNI remapping asset reader method");
		jstring rid = env->NewStringUTF (Constants::runtime_identifier.data ());
		check_exception ();
		abort_unless (rid != nullptr, "Could not allocate the JNI remapping runtime identifier");
		auto asset = static_cast<jbyteArray> (env->CallStaticObjectMethod (reader, read, rid));
		check_exception ();
		abort_unless (asset != nullptr, "The JNI remapping asset reader returned no data");
		jsize length = env->GetArrayLength (asset);
		abort_unless (length > 0, "The JNI remapping asset is empty");
		auto data = static_cast<uint8_t*> (std::malloc (static_cast<size_t> (length)));
		abort_unless (data != nullptr, "Could not allocate the JNI remapping asset buffer");
		env->GetByteArrayRegion (asset, 0, length, reinterpret_cast<jbyte*> (data));
		check_exception ();
		env->DeleteLocalRef (asset);
		env->DeleteLocalRef (rid);
		env->DeleteLocalRef (reader);

		// JNIEnvInit copies and validates the bytes, then releases them with monodroid_free.
		args.jniRemappingData = data;
		args.jniRemappingDataLength = static_cast<uint32_t> (length);
	}
}
