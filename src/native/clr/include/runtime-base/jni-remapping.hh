#pragma once

#include "xamarin-app.hh"

namespace xamarin::android
{
	class JniRemapping final
	{
	public:
		static auto lookup_replacement_type (const char *jniSimpleReference) noexcept -> const char*;
		static auto lookup_replacement_method_info (const char *jniSourceType, const char *jniMethodName, const char *jniMethodSignature) noexcept -> const JniRemappingReplacementMethod*;

	private:
		[[gnu::nonnull (2)]]
		static auto equal (JniRemappingString const& left, const char *right, size_t right_len) noexcept -> bool;
	};
}
