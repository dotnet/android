#include <cstddef>
#include <cstdlib>

// NativeAOT compiles shared CoreCLR code that uses C++ allocation, but should not require libc++
// only to provide these operators. Keep this list limited to symbols required by the host archive.
namespace {
	void *allocate_or_abort (std::size_t size) noexcept
	{
		void *memory = std::malloc (size == 0 ? 1 : size);
		if (memory == nullptr) {
			std::abort ();
		}
		return memory;
	}
}

void *operator new[] (std::size_t size)
{
	return allocate_or_abort (size);
}

void operator delete (void *memory) noexcept
{
	std::free (memory);
}

void operator delete[] (void *memory) noexcept
{
	std::free (memory);
}
