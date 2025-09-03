#pragma once

#include <host/host-common.hh>

namespace xamarin::android {
	class Host : public HostCommon
	{
	public:
		static void OnInit () noexcept;
	};
}
