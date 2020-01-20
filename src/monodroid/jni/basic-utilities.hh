#ifndef __BASIC_UTILITIES_HH
#define __BASIC_UTILITIES_HH

#include <cstddef>
#include <cstdint>
#include <cassert>
#include <cstdlib>
#include <cstdio>
#include <cstring>

#include <unistd.h>

#include <sys/stat.h>
#include <sys/types.h>
#include <dirent.h>

#include "java-interop-util.h"

#if __cplusplus >= 201703L
#define UNUSED_ARG [[maybe_unused]]
#else
#if defined (__GNUC__)
#define UNUSED_ARG __attribute__((__unused__))
#else
#define UNUSED_ARG
#endif
#endif

#if WINDOWS
#define MONODROID_PATH_SEPARATOR      "\\"
#define MONODROID_PATH_SEPARATOR_CHAR '\\'
#else
#define MONODROID_PATH_SEPARATOR      "/"
#define MONODROID_PATH_SEPARATOR_CHAR '/'
#endif

#if WINDOWS
typedef struct _stat monodroid_stat_t;
#define monodroid_dir_t _WDIR
typedef struct _wdirent monodroid_dirent_t;
#else
typedef struct stat monodroid_stat_t;
#define monodroid_dir_t DIR
typedef struct dirent monodroid_dirent_t;
#endif

#define DEFAULT_DIRECTORY_MODE S_IRWXU | S_IRGRP | S_IXGRP | S_IROTH | S_IXOTH
#define XA_UNLIKELY(expr) (__builtin_expect ((expr) != 0, 0))

namespace xamarin::android
{
#define ADD_WITH_OVERFLOW_CHECK(__ret_type__, __a__, __b__) utils.add_with_overflow_check<__ret_type__>(__FILE__, __LINE__, (__a__), (__b__))
#define MULTIPLY_WITH_OVERFLOW_CHECK(__ret_type__, __a__, __b__) utils.multiply_with_overflow_check<__ret_type__>(__FILE__, __LINE__, (__a__), (__b__))

	class BasicUtilities
	{
	public:
		FILE            *monodroid_fopen (const char* filename, const char* mode);
		int              monodroid_stat (const char *path, monodroid_stat_t *s);
		monodroid_dir_t *monodroid_opendir (const char *filename);
		int              monodroid_closedir (monodroid_dir_t *dirp);
		int              monodroid_dirent_hasextension (monodroid_dirent_t *e, const char *extension);
		void             monodroid_strfreev (char **str_array);
		char           **monodroid_strsplit (const char *str, const char *delimiter, size_t max_tokens);
		char            *monodroid_strdup_printf (const char *format, ...);
		char            *monodroid_strdup_vprintf (const char *format, va_list vargs);
		char*            path_combine(const char *path1, const char *path2);
		void             create_public_directory (const char *dir);
		int              create_directory (const char *pathname, mode_t mode);
		void             set_world_accessable (const char *path);
		void             set_user_executable (const char *path);
		bool             file_exists (const char *file);
		bool             directory_exists (const char *directory);
		bool             file_copy (const char *to, const char *from);

		bool ends_with_slow (const char *str, const char *end)
		{
			char *p = const_cast<char*> (strstr (str, end));
			return p != nullptr && p [strlen (end)] == '\0';
		}

		template<size_t N>
		bool ends_with (const char *str, const char (&end)[N])
		{
			char *p = const_cast<char*> (strstr (str, end));
			return p != nullptr && p [N - 1] == '\0';
		}

		void *xmalloc (size_t size)
		{
			return ::xmalloc (size);
		}

		void *xrealloc (void *ptr, size_t size)
		{
			return ::xrealloc (ptr, size);
		}

		void *xcalloc (size_t nmemb, size_t size)
		{
			return ::xcalloc (nmemb, size);
		}

		char *strdup_new (const char* s, size_t len)
		{
			if (XA_UNLIKELY (len == 0 || s == nullptr)) {
				return nullptr;
			}

			size_t alloc_size = add_with_overflow_check<size_t>(__FILE__, __LINE__, len, 1);
			auto ret = new char[alloc_size];
			memcpy (ret, s, len);
			ret[len] = '\0';

			return ret;
		}

		char *strdup_new (const char* s)
		{
			if (XA_UNLIKELY (s == nullptr)) {
				return nullptr;
			}

			return strdup_new (s, strlen (s));
		}

		// Without <type_traits> it's a little bit open for abuse (bad stuff will happen if
		// a type different than `char*` is used to specialize the function and we can't
		// assert this condition on compile time), but we can take that risk since it's
		// internal, controlled API
		template<typename StringType = const char*, typename ...Strings>
		char* string_concat (const char *s1, StringType s2, Strings... strings)
		{
			size_t len = calculate_length (s1, s2, strings...);

			char *ret = new char [len + 1];
			*ret = '\0';

			concatenate_strings_into (len, ret, s1, s2, strings...);

			return ret;
		}
#if defined (WINDOWS)
		/* Those two conversion functions are only properly implemented on Windows
		 * because that's the only place where they should be useful.
		 */
		char            *utf16_to_utf8 (const wchar_t *widestr)
		{
			return ::utf16_to_utf8 (widestr);
		}

		wchar_t         *utf8_to_utf16 (const char *mbstr)
		{
			return ::utf8_to_utf16 (mbstr);
		}
#endif // def WINDOWS
		bool            is_path_rooted (const char *path);

		template<typename Ret, typename P1, typename P2>
		inline Ret add_with_overflow_check (const char *file, uint32_t line, P1 a, P2 b) const
		{
			Ret ret;

			if (XA_UNLIKELY (__builtin_add_overflow (a, b, &ret))) {
				log_fatal (LOG_DEFAULT, "Integer overflow on addition at %s:%u", file, line);
				exit (FATAL_EXIT_OUT_OF_MEMORY);
				return static_cast<Ret>(0);
			}

			return ret;
		}

		// Can't use templates as above with add_with_oveflow because of a bug in the clang compiler
		// shipped with the NDK:
		//
		//   https://github.com/android-ndk/ndk/issues/294
		//   https://github.com/android-ndk/ndk/issues/295
		//   https://bugs.llvm.org/show_bug.cgi?id=16404
		//
		// Using templated parameter types for `a` and `b` would make clang generate that tries to
		// use 128-bit integers and thus output code that calls `__muloti4` and so linking would
		// fail
		//
		template<typename Ret>
		inline Ret multiply_with_overflow_check (const char *file, uint32_t line, size_t a, size_t b) const
		{
			Ret ret;

			if (XA_UNLIKELY (__builtin_mul_overflow (a, b, &ret))) {
				log_fatal (LOG_DEFAULT, "Integer overflow on multiplication at %s:%u", file, line);
				exit (FATAL_EXIT_OUT_OF_MEMORY);
				return static_cast<Ret>(0);
			}

			return ret;
		}

	protected:
		template<typename StringType = const char*, typename ...Strings>
		void concatenate_strings_into (UNUSED_ARG size_t len, UNUSED_ARG char *dest)
		{}

		template<typename StringType = const char*, typename ...Strings>
		void concatenate_strings_into (size_t len, char *dest, StringType s1, Strings... strings)
		{
			strcat (dest, s1);
			concatenate_strings_into (len, dest, strings...);
		}

		template<typename StringType = const char*>
		size_t calculate_length (StringType s)
		{
			return strlen (s);
		}

		template<typename StringType = const char*, typename ...Strings>
		size_t calculate_length (StringType s1, Strings... strings)
		{
			return strlen (s1) + calculate_length (strings...);
		}

		int make_directory (const char *path, [[maybe_unused]] mode_t mode)
		{
#if WINDOWS
			return mkdir (path);
#else
			return mkdir (path, mode);
#endif
		}

	private:
		void  add_to_vector (char ***vector, size_t size, char *token);
	};
}
#endif // !__BASIC_UTILITIES_HH
