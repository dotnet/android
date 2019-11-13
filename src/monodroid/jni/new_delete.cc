#include <stdlib.h>
#include <new>

#include "java-interop-util.h"

static void*
do_alloc (size_t size)
{
	return ::malloc (size == 0 ? 1 : size);
}

void*
operator new (size_t size)
{
	void* p = do_alloc (size);
	if (p == nullptr) {
		log_fatal (LOG_DEFAULT, "Out of memory in the `new` operator");
		exit (FATAL_EXIT_OUT_OF_MEMORY);
	}

	return p;
}

void*
operator new (size_t size, const std::nothrow_t&)
{
	return do_alloc (size);
}

void*
operator new[] (size_t size)
{
	return ::operator new (size);
}

void*
operator new[] (size_t size, const std::nothrow_t&)
{
	return do_alloc (size);
}

void
operator delete (void* ptr)
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

void
operator delete[] (void* ptr)
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
