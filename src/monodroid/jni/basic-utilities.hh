#ifndef __BASIC_UTILITIES_HH
#define __BASIC_UTILITIES_HH

#include <cstddef>
#include <cstdint>
#include <cassert>
#include <cstdlib>
#include <cstdio>
#include <cstring>
#include <type_traits>

#include <unistd.h>

#include <sys/stat.h>
#include <sys/types.h>
#include <dirent.h>

#include "java-interop-util.h"
#include "helpers.hh"
#include "cpp-util.hh"
#include "platform-compat.hh"
#include "strings.hh"

namespace xamarin::android
{
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
		char*            path_combine (const char *path1, const char *path2);
		void             create_public_directory (const char *dir);
		int              create_directory (const char *pathname, mode_t mode);
		void             set_world_accessable (const char *path);
		void             set_user_executable (const char *path);
		bool             file_exists (const char *file);
		bool             directory_exists (const char *directory);
		bool             file_copy (const char *to, const char *from);


		// Make sure that `buf` has enough space! This is by design, the methods are supposed to be fast.
		template<size_t MaxStackSpace, typename TBuffer>
		void path_combine (TBuffer& buf, const char* path1, const char* path2) noexcept
		{
			path_combine (buf, path1, path1 == nullptr ? 0 : strlen (path1), path2, path2 == nullptr ? 0 : strlen (path2));
		}

		// internal::static_local_string<MaxStackSpace>
		template<size_t MaxStackSpace, typename TBuffer>
		void path_combine (TBuffer& buf, const char* path1, size_t path1_len, const char* path2, size_t path2_len) noexcept
		{
			assert (path1 != nullptr || path2 != nullptr);

			if (path1 == nullptr) {
				buf.append (path2);
				return;
			}

			if (path2 == nullptr) {
				buf.append (path1);
				return;
			}

			buf.append (path1, path1_len);
			buf.append (MONODROID_PATH_SEPARATOR, MONODROID_PATH_SEPARATOR_LENGTH);
			buf.append (path2, path2_len);
		}

		template<size_t MaxStackSpace>
		void path_combine (internal::static_local_string<MaxStackSpace>& buf, const char* path1, const char* path2) noexcept
		{
			path_combine <MaxStackSpace, decltype(buf)> (buf, path1, path2);
		}

		template<size_t MaxStackSpace>
		void path_combine (internal::static_local_string<MaxStackSpace>& buf, const char* path1, size_t path1_len, const char* path2, size_t path2_len) noexcept
		{
			path_combine <MaxStackSpace, decltype(buf)> (buf, path1, path1_len, path2, path2_len);
		}

		template<size_t MaxStackSpace>
		void path_combine (internal::dynamic_local_string<MaxStackSpace>& buf, const char* path1, const char* path2) noexcept
		{
			path_combine <MaxStackSpace, decltype(buf)> (buf, path1, path2);
		}

		template<size_t MaxStackSpace>
		void path_combine (internal::dynamic_local_string<MaxStackSpace>& buf, const char* path1, size_t path1_len, const char* path2, size_t path2_len) noexcept
		{
			path_combine <MaxStackSpace, decltype(buf)> (buf, path1, path1_len, path2, path2_len);
		}

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

			size_t alloc_size = ADD_WITH_OVERFLOW_CHECK (size_t, len, 1);
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

		template<typename CharType = char, typename ...Strings>
		char* string_concat (const char *s1, const CharType* s2, Strings... strings)
		{
			assert_char_type<CharType> ();

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

		template<typename CharType = char>
		size_t calculate_length (const CharType* s)
		{
			return strlen (s);
		}

		template<typename CharType = char, typename ...Strings>
		size_t calculate_length (const CharType* s1, Strings... strings)
		{
			assert_char_type<CharType> ();

			return strlen (s1) + calculate_length (strings...);
		}

	protected:
		template<typename CharType = char, typename ...Strings>
		void concatenate_strings_into (UNUSED_ARG size_t len, UNUSED_ARG char *dest)
		{}

		template<typename CharType = char, typename ...Strings>
		void concatenate_strings_into (size_t len, char *dest, const CharType* s1, Strings... strings)
		{
			assert_char_type<CharType> ();

			strcat (dest, s1);
			concatenate_strings_into (len, dest, strings...);
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

		template<typename CharType>
		static constexpr void assert_char_type ()
		{
			static_assert (std::is_same_v<CharType, char>, "CharType must be an 8-bit character type");
		}
	};
}
#endif // !__BASIC_UTILITIES_HH
