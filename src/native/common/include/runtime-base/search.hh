// Dear Emacs, this is a -*- C++ -*- header
#pragma once

#include <cstddef>

namespace xamarin::android {
	class Search final
	{
	public:
		template<class T, typename TKey, bool (*less_than) (T const&, TKey)>
		[[gnu::always_inline, gnu::flatten]]
		static size_t lower_bound (TKey key, const T *arr, size_t n) noexcept
		{
			static_assert (less_than != nullptr, "less_than is a required template parameter");

			size_t left = 0;
			size_t right = n;

			while (left < right) {
				size_t middle = (left + right) >> 1u;
				if (less_than (arr[middle], key)) {
					left = middle + 1;
				} else {
					right = middle;
				}
			}

			return left;
		}
	};
}
