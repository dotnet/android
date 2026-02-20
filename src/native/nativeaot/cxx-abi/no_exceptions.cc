#include <exception>

#include <shared/helpers.hh>

namespace std {
	exception_ptr::~exception_ptr () noexcept
	{
		xamarin::android::Helpers::abort_application ("exception_ptr not implemented\n"sv);
	}

	exception_ptr::exception_ptr (const exception_ptr& other) noexcept : __ptr_(other.__ptr_)
	{
		xamarin::android::Helpers::abort_application ("exception_ptr not implemented\n");
	}

	exception_ptr& exception_ptr::operator= ([[maybe_unused]] const exception_ptr& other) noexcept
	{
		xamarin::android::Helpers::abort_application ("exception_ptr not yet implemented\n");
	}

	[[noreturn]]
	void rethrow_exception ([[maybe_unused]] exception_ptr p)
	{
		xamarin::android::Helpers::abort_application ("exception_ptr not yet implemented\n");
	}
} // namespace std
