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

	private:
		T *tp;
	};

	template <typename T>
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
			delete simple_pointer_guard_type<T>::get ();
		}

		T& operator= (simple_pointer_guard &other) = delete;
		T& operator= (const simple_pointer_guard &other) = delete;
	};

	template <typename T>
	class simple_pointer_guard<T[]> : public simple_pointer_guard_type<T>
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
			delete[] simple_pointer_guard_type<T>::get ();
		}

		T& operator= (simple_pointer_guard &other) = delete;
		T& operator= (const simple_pointer_guard &other) = delete;
	};
}
#endif // !def __CPP_UTIL_HH
