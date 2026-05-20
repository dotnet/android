#include <stddef.h>
#include <stdlib.h>

#include <new>

namespace std {
	const nothrow_t nothrow {};
}

static void *allocate (size_t size) noexcept
{
	return malloc (size == 0 ? 1 : size);
}

static void *allocate_or_abort (size_t size) noexcept
{
	void *ret = allocate (size);
	if (ret == nullptr) {
		abort ();
	}
	return ret;
}

void *operator new (size_t size)
{
	return allocate_or_abort (size);
}

void *operator new[] (size_t size)
{
	return allocate_or_abort (size);
}

void *operator new (size_t size, std::nothrow_t const&) noexcept
{
	return allocate (size);
}

void *operator new[] (size_t size, std::nothrow_t const&) noexcept
{
	return allocate (size);
}

void operator delete (void *ptr) noexcept
{
	free (ptr);
}

void operator delete[] (void *ptr) noexcept
{
	free (ptr);
}

void operator delete (void *ptr, size_t) noexcept
{
	free (ptr);
}

void operator delete[] (void *ptr, size_t) noexcept
{
	free (ptr);
}
