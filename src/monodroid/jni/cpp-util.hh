#ifndef __CPP_UTIL_HH
#define __CPP_UTIL_HH

#include <cppcompat.hh>

namespace xamarin::android
{
	template <typename T>
	class simple_pointer_guard_type
	{
	public:
		simple_pointer_guard_type () = default;
		simple_pointer_guard_type (T* tp)
			: tp (tp)
		{}
		simple_pointer_guard_type (simple_pointer_guard_type &other) = delete;
		simple_pointer_guard_type (const simple_pointer_guard_type &other) = delete;

		T& operator= (simple_pointer_guard_type &other) = delete;
		T& operator= (const simple_pointer_guard_type &other) = delete;
		T* operator= (T* ptr) = delete;
		const T* operator= (const T* ptr) = delete;

		T* get () const noexcept
		{
			return tp;
		}

		T* operator->() const noexcept
		{
			return tp;
		}

		T& operator*() const noexcept
		{
			return *tp;
		}

		explicit operator bool () const noexcept
		{
			return tp != nullptr;
		}

		operator T* () const noexcept
		{
			return tp;
		}

		operator T const* () const noexcept
		{
			return tp;
		}

	protected:
		void set_pointer (T* ptr) noexcept
		{
			tp = ptr;
		}

		void set_pointer (const T* ptr) noexcept
		{
			tp = ptr;
		}

	private:
		T *tp = nullptr;
	};

	template <typename T, bool uses_cpp_alloc = true>
	class simple_pointer_guard : public simple_pointer_guard_type<T>
	{
	public:
		simple_pointer_guard () = default;
		simple_pointer_guard (T* tp)
			: simple_pointer_guard_type<T> (tp)
		{}
		simple_pointer_guard (simple_pointer_guard &other) = delete;
		simple_pointer_guard (const simple_pointer_guard &other) = delete;

		~simple_pointer_guard ()
		{
			T *ptr = simple_pointer_guard_type<T>::get ();
			if constexpr (uses_cpp_alloc) {
				delete ptr;
			} else {
				free (ptr);
			}
		}

		T& operator= (simple_pointer_guard &other) = delete;
		T& operator= (const simple_pointer_guard &other) = delete;
		T* operator= (T* ptr) = delete;
		const T* operator= (const T* ptr) = delete;
	};

	template <typename T, bool uses_cpp_alloc>
	class simple_pointer_guard<T[], uses_cpp_alloc> : public simple_pointer_guard_type<T>
	{
	public:
		simple_pointer_guard () = default;
		simple_pointer_guard (T* tp)
			: simple_pointer_guard_type<T> (tp)
		{}
		simple_pointer_guard (simple_pointer_guard &other) = delete;
		simple_pointer_guard (const simple_pointer_guard &other) = delete;

		~simple_pointer_guard ()
		{
			T *ptr = simple_pointer_guard_type<T>::get ();

			if constexpr (uses_cpp_alloc) {
				delete[] ptr;
			} else {
				free (ptr);
			}
		}

		T& operator= (simple_pointer_guard &other) = delete;
		T& operator= (const simple_pointer_guard &other) = delete;
		const T* operator= (const T (ptr)[]) = delete;
		T* operator= (T (ptr)[]) = delete;
	};
}
#endif // !def __CPP_UTIL_HH
