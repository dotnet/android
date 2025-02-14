// Dear Emacs, this is a -*- C++ -*- header
#pragma once

#include <sys/types.h>

#include <shared/xxhash.hh>

namespace xamarin::android {
	class Search final
	{
	public:
		template<class T, bool (*equal) (T const&, hash_t), bool (*less_than) (T const&, hash_t)>
		[[gnu::always_inline]]
		static ssize_t binary_search (hash_t key, const T *arr, size_t n) noexcept
		{
			static_assert (equal != nullptr, "equal is a required template parameter");
			static_assert (less_than != nullptr, "less_than is a required template parameter");

			ssize_t left = -1z;
			ssize_t right = static_cast<ssize_t>(n);

			while (right - left > 1) {
				ssize_t middle = (left + right) >> 1u;
				if (less_than (arr[middle], key)) {
					left = middle;
				} else {
					right = middle;
				}
			}

			return equal (arr[right], key) ? right : -1z;
		}

		[[gnu::always_inline]]
		static ssize_t binary_search (hash_t key, const hash_t *arr, size_t n) noexcept
		{
			auto equal = [](hash_t const& entry, hash_t key) -> bool { return entry == key; };
			auto less_than = [](hash_t const& entry, hash_t key) -> bool { return entry < key; };

			return binary_search<hash_t, equal, less_than> (key, arr, n);
		}

		[[gnu::always_inline]]
		static ptrdiff_t binary_search_branchless (hash_t x, const hash_t *arr, uint32_t len) noexcept
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
