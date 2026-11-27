#pragma once

namespace xamarin::android {
	class PinvokeOverride
	{
	public:
		static auto load_symbol (const char *library_name, const char *symbol_name) noexcept -> const void*;
	};
}
