// Dear Emacs, this is a -*- C++ -*- header
#pragma once

#include <cstddef>
#include <cstdint>

#include <runtime-base/search.hh>
#include <shared/xxhash.hh>

namespace xamarin::android {
	class SearchXxHash final
	{
	public:
		template<class T, bool (*equal) (T const&, hash_t), bool (*less_than) (T const&, hash_t)>
		[[gnu::always_inline, gnu::flatten]]
		static ssize_t binary_search (hash_t key, const T *arr, size_t n) noexcept
		{
			return Search::binary_search<T, hash_t, equal, less_than> (key, arr, n);
		}

		[[gnu::always_inline, gnu::flatten]]
		static ssize_t binary_search (hash_t key, const hash_t *arr, size_t n) noexcept
		{
			auto equal = [](hash_t const& entry, hash_t key) -> bool { return entry == key; };
			auto less_than = [](hash_t const& entry, hash_t key) -> bool { return entry < key; };

			return Search::binary_search<hash_t, hash_t, equal, less_than> (key, arr, n);
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
