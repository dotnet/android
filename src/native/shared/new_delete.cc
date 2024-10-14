#include <stdlib.h>

#include "helpers.hh"

namespace std
{
	struct nothrow_t {};
	extern const nothrow_t nothrow;
}

#include "java-interop-util.h"

static void*
do_alloc (size_t size)
{
	return ::malloc (size == 0 ? 1 : size);
}

__attribute__((__weak__))
void*
operator new (size_t size)
{
	void* p = do_alloc (size);
	if (p == nullptr) {
		xamarin::android::Helpers::abort_application ("Out of memory in the `new` operator");
	}

	return p;
}

void*
operator new (size_t size, const std::nothrow_t&) noexcept
{
	return do_alloc (size);
}

__attribute__((__weak__))
void*
operator new[] (size_t size)
{
	return ::operator new (size);
}

void*
operator new[] (size_t size, const std::nothrow_t&) noexcept
{
	return do_alloc (size);
}

__attribute__((__weak__))
void
operator delete (void* ptr) noexcept
{
	if (ptr)
		::free (ptr);
}

void
operator delete (void* ptr, const std::nothrow_t&)
{
	::operator delete (ptr);
}

void
operator delete (void* ptr, size_t) noexcept
{
	::operator delete (ptr);
}

__attribute__((__weak__))
void
operator delete[] (void* ptr) noexcept
{
	::operator delete (ptr);
}

void
operator delete[] (void* ptr, const std::nothrow_t&) noexcept
{
	::operator delete[] (ptr);
}

void
operator delete[] (void* ptr, size_t) noexcept
{
	::operator delete[] (ptr);
}
