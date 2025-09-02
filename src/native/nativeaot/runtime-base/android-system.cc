#include <string_view>

#include <runtime-base/android-system.hh>

using namespace xamarin::android;

auto
AndroidSystem::lookup_system_property ([[maybe_unused]] std::string_view const& name, [[maybe_unused]] size_t &value_len) noexcept -> const char*
{
	return nullptr; // No-op in NativeAOT
}
