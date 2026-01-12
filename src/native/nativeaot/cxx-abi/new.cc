//
// Code based on the original libc++ `libcxx/src/new.cpp` source, modified
// heavily for our use
//

//===----------------------------------------------------------------------===//
//
// Part of the LLVM Project, under the Apache License v2.0 with LLVM Exceptions.
// See https://llvm.org/LICENSE.txt for license information.
// SPDX-License-Identifier: Apache-2.0 WITH LLVM-exception
//
//===----------------------------------------------------------------------===//

#include <__assert>
#include <__memory/aligned_alloc.h>
#include <cstddef>
#include <cstdlib>
#include <new>

#include <shared/helpers.hh>

#define _LIBCPP_MAKE_OVERRIDABLE_FUNCTION_DETECTABLE [[gnu::section ("__TEXT,__lcxx_override,regular,pure_instructions")]]

namespace {
	void* operator_new_impl(std::size_t size)
	{
		if (size == 0) {
			size = 1;
		}

		return std::malloc (size);
	}
}

_LIBCPP_MAKE_OVERRIDABLE_FUNCTION_DETECTABLE
[[gnu::weak]]
void* operator new (std::size_t size)
{
	void *p = operator_new_impl (size);
	if (p == nullptr) {
		xamarin::android::Helpers::abort_application ("Out of memory in the `new` operator");
	}

	return p;
}

[[gnu::weak]]
void* operator new (size_t size, const std::nothrow_t&) noexcept
{
	return operator_new_impl (size);
}

_LIBCPP_MAKE_OVERRIDABLE_FUNCTION_DETECTABLE
[[gnu::weak]]
void* operator new[] (size_t size)
{
	return ::operator new (size);
}

[[gnu::weak]]
void* operator new[] (size_t size, const std::nothrow_t&) noexcept
{
	return operator_new_impl (size);
}

[[gnu::weak]]
void operator delete(void* ptr) noexcept
{
	std::free (ptr);
}

[[gnu::weak]]
void operator delete (void* ptr, const std::nothrow_t&) noexcept
{
	::operator delete(ptr);
}

[[gnu::weak]]
void operator delete (void* ptr, size_t) noexcept
{
	::operator delete(ptr);
}

[[gnu::weak]]
void operator delete[] (void* ptr) noexcept
{
	::operator delete(ptr);
}

[[gnu::weak]]
void operator delete[] (void* ptr, const std::nothrow_t&) noexcept
{
	::operator delete[](ptr);
}

[[gnu::weak]]
void operator delete[] (void* ptr, size_t) noexcept
{
	::operator delete[](ptr);
}

namespace {
	void* operator_new_aligned_impl (std::size_t size, std::align_val_t alignment)
	{
		if (size == 0) {
			size = 1;
		}

		if (static_cast<size_t>(alignment) < sizeof(void*)) {
			alignment = std::align_val_t(sizeof(void*));
		}

		return std::__libcpp_aligned_alloc (static_cast<std::size_t>(alignment), size);
	}
}

_LIBCPP_MAKE_OVERRIDABLE_FUNCTION_DETECTABLE
[[gnu::weak]]
void* operator new (std::size_t size, std::align_val_t alignment)
{
	void* p = operator_new_aligned_impl (size, alignment);
	if (p == nullptr) {
		xamarin::android::Helpers::abort_application ("Out of memory in the aligned `new` operator");
	}

	return p;
}

[[gnu::weak]]
void* operator new (size_t size, std::align_val_t alignment, const std::nothrow_t&) noexcept
{
	return operator_new_aligned_impl (size, alignment);
}

_LIBCPP_MAKE_OVERRIDABLE_FUNCTION_DETECTABLE
[[gnu::weak]]
void* operator new[] (size_t size, std::align_val_t alignment)
{
	return ::operator new (size, alignment);
}

[[gnu::weak]]
void* operator new[] (size_t size, std::align_val_t alignment, const std::nothrow_t&) noexcept
{
	return operator_new_aligned_impl (size, alignment);
}

[[gnu::weak]]
void operator delete (void* ptr, std::align_val_t) noexcept
{
	std::__libcpp_aligned_free(ptr);
}

[[gnu::weak]]
void operator delete (void* ptr, std::align_val_t alignment, const std::nothrow_t&) noexcept
{
	::operator delete(ptr, alignment);
}

[[gnu::weak]]
void operator delete (void* ptr, size_t, std::align_val_t alignment) noexcept
{
	::operator delete(ptr, alignment);
}

[[gnu::weak]]
void operator delete[] (void* ptr, std::align_val_t alignment) noexcept
{
	::operator delete(ptr, alignment);
}

[[gnu::weak]]
void operator delete[] (void* ptr, std::align_val_t alignment, const std::nothrow_t&) noexcept
{
	::operator delete[](ptr, alignment);
}

[[gnu::weak]] void operator delete[] (void* ptr, size_t, std::align_val_t alignment) noexcept
{
	::operator delete[](ptr, alignment);
}
