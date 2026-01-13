#include <cstdio>
#include <stdexcept>

#include <__system_error/throw_system_error.h>

#include <shared/helpers.hh>

_LIBCPP_BEGIN_NAMESPACE_STD

void __throw_system_error (int ev, const char* what_arg)
{
	char *message = nullptr;
	int n = asprintf (&message, "system_error was thrown in -fno-exceptions mode with error %i and message \"%s\"", ev, what_arg);
	xamarin::android::Helpers::abort_application (
		n == -1 ? "system_error was thrown in -fno-exceptions mode" : message
	);
}

_LIBCPP_END_NAMESPACE_STD
