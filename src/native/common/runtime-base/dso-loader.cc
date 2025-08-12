#include <runtime-base/dso-loader.hh>
#include <runtime-base/mainthread-dso-loader.hh>

using namespace xamarin::android;

auto DsoLoader::load_jni_on_main_thread (std::string_view const& full_name, std::string const& undecorated_name) noexcept -> void*
{
	return nullptr;
}
