// Dear Emacs, this is a -*- C++ -*- header
#pragma once

#include <sys/types.h>

namespace xamarin::android {
	class Search final
	{
	public:
		// Code duplication in the two functions below is lamentable, but avoiding it would require
		// making the code much uglier (and harder to read) with meta programming tricks, just not worth it.
		template<class T, typename TKey, typename TState, bool (*equal) (T const&, TKey, TState&), bool (*less_than) (T const&, TKey, TState&)>
		[[gnu::always_inline, gnu::flatten]]
		static ssize_t binary_search (const TState& state, TKey key, const T *arr, size_t n) noexcept
		{
			static_assert (equal != nullptr, "equal is a required template parameter");
			static_assert (less_than != nullptr, "less_than is a required template parameter");

			ssize_t left = -1z;
			ssize_t right = static_cast<ssize_t>(n);

			while (right - left > 1) {
				ssize_t middle = (left + right) >> 1u;
				if (less_than (arr[middle], key, state)) {
					left = middle;
				} else {
					right = middle;
				}
			}

			return equal (arr[right], key, state) ? right : -1z;
		}

		template<class T, typename TKey, bool (*equal) (T const&, TKey), bool (*less_than) (T const&, TKey)>
		[[gnu::always_inline, gnu::flatten]]
		static ssize_t binary_search (TKey key, const T *arr, size_t n) noexcept
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
	};
}
