// Dear Emacs, this is a -*- C++ -*- header
#if !defined (__SEARCH_HH)
#define __SEARCH_HH

#include <sys/types.h>

#include "platform-compat.hh"
#include "xxhash.hh"

namespace xamarin::android::internal {
	class Search final
	{
	public:
		force_inline static ssize_t binary_search (hash_t key, const hash_t *arr, size_t n) noexcept
		{
			ssize_t left = -1;
			ssize_t right = static_cast<ssize_t>(n);

			while (right - left > 1) {
				ssize_t middle = (left + right) >> 1;
				if (arr[middle] < key) {
					left = middle;
				} else {
					right = middle;
				}
			}

			return arr[right] == key ? right : -1;
		}

		force_inline static ptrdiff_t binary_search_branchless (hash_t x, const hash_t *arr, uint32_t len) noexcept
		{
			const hash_t *base = arr;
			while (len > 1) {
				uint32_t half = len >> 1;
				// __builtin_prefetch(&base[(len - half) / 2]);
				// __builtin_prefetch(&base[half + (len - half) / 2]);
				base = (base[half] < x ? &base[half] : base);
				len -= half;
			}

			//return *(base + (*base < x));
			ptrdiff_t ret = (base + (*base < x)) - arr;
			return arr[ret] == x ? ret : -1;
		}
	};
}
#endif // ndef __SEARCH_HH
