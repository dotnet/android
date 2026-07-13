#pragma once

#include <string_view>

#include <jni.h>

#include "../runtime-base/monodroid-dl.hh"

namespace xamarin::android {
	class PinvokeOverride
	{
	public:
		static auto load_library_symbol (std::string_view const& library_name, std::string_view const& symbol_name) noexcept -> void*;
		static auto monodroid_pinvoke_override (const char *library_name, const char *entrypoint_name) noexcept -> void*;
	};
}
