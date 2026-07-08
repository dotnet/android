// Dear Emacs, this is a -*- C++ -*- header
#pragma once

#include <cstddef>
#include <sys/types.h>

namespace xamarin::android {
	class Search final
	{
	public:
		template<class T, typename TKey, typename TState, bool (*less_than) (T const&, TKey, TState const&)>
		[[gnu::always_inline, gnu::flatten]]
		static size_t lower_bound (const TState& state, TKey key, const T *arr, size_t n) noexcept
		{
			static_assert (less_than != nullptr, "less_than is a required template parameter");

			size_t left = 0;
			size_t right = n;

			while (left < right) {
				size_t middle = (left + right) >> 1u;
				if (less_than (arr[middle], key, state)) {
					left = middle + 1;
				} else {
					right = middle;
				}
			}

			return left;
		}

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

		template<class T, typename TKey, typename TState, bool (*equal) (T const&, TKey, TState const&), bool (*less_than) (T const&, TKey, TState const&)>
		[[gnu::always_inline, gnu::flatten]]
		static ssize_t binary_search (const TState& state, TKey key, const T *arr, size_t n) noexcept
		{
			static_assert (equal != nullptr, "equal is a required template parameter");

			size_t idx = lower_bound<T, TKey, TState, less_than> (state, key, arr, n);
			return idx < n && equal (arr[idx], key, state) ? static_cast<ssize_t>(idx) : -1z;
		}

		template<class T, typename TKey, bool (*equal) (T const&, TKey), bool (*less_than) (T const&, TKey)>
		[[gnu::always_inline, gnu::flatten]]
		static ssize_t binary_search (TKey key, const T *arr, size_t n) noexcept
		{
			static_assert (equal != nullptr, "equal is a required template parameter");

			size_t idx = lower_bound<T, TKey, less_than> (key, arr, n);
			return idx < n && equal (arr[idx], key) ? static_cast<ssize_t>(idx) : -1z;
		}
	};
}
