#pragma once

#include <string_view>

namespace xamarin::android {
	class Constants
	{
	public:
		static constexpr std::string_view LOG_CATEGORY_NAME_NONE { "*none*" };
		static constexpr std::string_view LOG_CATEGORY_NAME_MONODROID { "monodroid" };
		static constexpr std::string_view LOG_CATEGORY_NAME_MONODROID_ASSEMBLY { "monodroid-assembly" };
		static constexpr std::string_view LOG_CATEGORY_NAME_MONODROID_DEBUG { "monodroid-debug" };
		static constexpr std::string_view LOG_CATEGORY_NAME_MONODROID_GC { "monodroid-gc" };
		static constexpr std::string_view LOG_CATEGORY_NAME_MONODROID_GREF { "monodroid-gref" };
		static constexpr std::string_view LOG_CATEGORY_NAME_MONODROID_LREF { "monodroid-lref" };
		static constexpr std::string_view LOG_CATEGORY_NAME_MONODROID_TIMING { "monodroid-timing" };
		static constexpr std::string_view LOG_CATEGORY_NAME_MONODROID_BUNDLE { "monodroid-bundle" };
		static constexpr std::string_view LOG_CATEGORY_NAME_MONODROID_NETWORK { "monodroid-network" };
		static constexpr std::string_view LOG_CATEGORY_NAME_MONODROID_NETLINK { "monodroid-netlink" };
		static constexpr std::string_view LOG_CATEGORY_NAME_ERROR { "*error*" };
	};
}
