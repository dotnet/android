#pragma once

#include <jni.h>

#include <host/host-common.hh>
#include "managed-interface.hh"

namespace xamarin::android {
	class jstring_wrapper;

	class Host : public HostCommon
	{
	public:
		static void OnInit (jstring_wrapper &language, jstring_wrapper &files_dir, jstring_wrapper &cache_dir, JnienvInitializeArgs *initArgs) noexcept;
	};
}
