#pragma once

#include <cstdlib>
#include <cstring>
#include <string_view>

#include <shared/helpers.hh>
#include <shared/log_types.hh>

namespace xamarin::android {
	// Storage for a path which is set once, early during startup, and then only read for as long as
	// the process lives. Paths that fit in the inline buffer (the overwhelming majority of them) need
	// no allocation at all, longer ones are moved to the heap, so there is no hard limit on the path
	// length.
	//
	// The type is a trivial aggregate on purpose. Static instances of it are constant-initialized,
	// which means that the compiler emits neither a guard variable nor an `atexit` registration for
	// them - unlike for a `std::string`, which needs both in every translation unit that includes the
	// declaration. The heap buffer is intentionally never released for the last assigned value, the
	// instances are expected to live for as long as the process does.
	template<size_t InlineCapacity>
	struct path_buffer
	{
		static_assert (InlineCapacity > 0, "Inline capacity must not be zero");

		char   inline_buffer [InlineCapacity];
		char  *heap_buffer;

		auto get () const noexcept -> const char*
		{
			return heap_buffer != nullptr ? heap_buffer : inline_buffer;
		}

		void assign (std::string_view const& value) noexcept
		{
			// The previous heap buffer, if any, is released here. Values are assigned at most a
			// handful of times, so there's no point in trying to reuse an allocation.
			std::free (heap_buffer);
			heap_buffer = nullptr;

			char *destination = inline_buffer;
			if (value.length () >= InlineCapacity) {
				size_t capacity = Helpers::add_with_overflow_check<size_t> (value.length (), 1uz);
				heap_buffer = static_cast<char*> (std::malloc (capacity));
				if (heap_buffer == nullptr) [[unlikely]] {
					Helpers::abort_application (LOG_DEFAULT, "Unable to allocate memory for a path");
				}
				destination = heap_buffer;
			}

			memcpy (destination, value.data (), value.length ());
			destination [value.length ()] = '\0';
		}

		void assign (const char *value) noexcept
		{
			assign (std::string_view { value != nullptr ? value : "" });
		}
	};
}
