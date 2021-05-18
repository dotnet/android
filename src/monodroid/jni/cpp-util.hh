#ifndef __CPP_UTIL_HH
#define __CPP_UTIL_HH

#include <cstdarg>
#include <cstdlib>

#if defined (ANDROID)
#include <android/log.h>
#else
#include <cstdio>
#endif

#include "cppcompat.hh"
#include "platform-compat.hh"

static inline void
do_abort_unless (bool condition, const char* fmt, ...)
{
	if (XA_LIKELY (condition)) {
		return;
	}

	va_list ap;

	va_start (ap, fmt);
#if defined (ANDROID)
	__android_log_vprint (ANDROID_LOG_FATAL, "monodroid", fmt, ap);
#else // def ANDROID
	vfprintf (stderr, fmt, ap);
	fprintf (stderr, "\n");
#endif // ndef ANDROID
	va_end (ap);

	std::abort ();
}

#define abort_unless(_condition_, _fmt_, ...) do_abort_unless (_condition_, "%s:%d (%s): " _fmt_, __FILE__, __LINE__, __FUNCTION__, ## __VA_ARGS__)
#define abort_if_invalid_pointer_argument(_ptr_) abort_unless ((_ptr_) != NULL, "Parameter '%s' must be a valid pointer", #_ptr_)
#define abort_if_negative_integer_argument(_arg_) abort_unless ((_arg_) > 0, "Parameter '%s' must be larger than 0", #_arg_)

namespace xamarin::android
{
	template <typename T>
	class simple_pointer_guard_type
	{
	public:
		simple_pointer_guard_type () = default;
		simple_pointer_guard_type (T* _tp)
			: tp (_tp)
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

	private:
		T *tp = nullptr;
	};

	template <typename T, bool uses_cpp_alloc = true>
	class simple_pointer_guard : public simple_pointer_guard_type<T>
	{
	public:
		simple_pointer_guard () = default;
		simple_pointer_guard (T* _tp)
			: simple_pointer_guard_type<T> (_tp)
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
		simple_pointer_guard (T* _tp)
			: simple_pointer_guard_type<T> (_tp)
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
