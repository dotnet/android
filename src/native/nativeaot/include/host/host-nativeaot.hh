#pragma once

#include <jni.h>

#include <host/host-common.hh>
#include "managed-interface.hh"

namespace xamarin::android {
	class Host : public HostCommon
	{
	public:
		static void OnInit (jstring language, jstring filesDir, jstring cacheDir, JnienvInitializeArgs *initArgs) noexcept;
	};
}
