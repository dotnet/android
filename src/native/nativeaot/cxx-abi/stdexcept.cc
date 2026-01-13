#include <cstdio>
#include <stdexcept>
#include <string>

#include <support/runtime/stdexcept_default.ipp>

#include <shared/helpers.hh>

_LIBCPP_BEGIN_NAMESPACE_STD

void __throw_runtime_error (const char* msg)
{
	char *message = nullptr;
	int n = asprintf (&message, "runtime_error was thrown in -fno-exceptions mode with message \"%s\"", msg);
	xamarin::android::Helpers::abort_application (
		n == -1 ? "runtime_error was thrown in -fno-exceptions mode" : message
	);
}

_LIBCPP_END_NAMESPACE_STD
