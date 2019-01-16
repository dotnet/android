// This is a -*- C++ -*- header
#ifndef __MONODROID_UTIL_H__
#define __MONODROID_UTIL_H__

#ifndef TRUE
#ifdef __cplusplus
constexpr int TRUE = 1;
#else
#define TRUE 1
#endif // __cplusplus
#endif

#ifndef FALSE
#ifdef __cplusplus
constexpr int FALSE = 0;
#else
#define FALSE 0
#endif // __cplusplus
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

#include <stdlib.h>
#include <string.h>
#include <unistd.h>
#include <sys/stat.h>
#include <dirent.h>
#include <stdarg.h>
#include <time.h>
#include <sys/time.h>
#include <jni.h>

#include "monodroid.h"
#include "dylib-mono.h"
#include "jni-wrappers.h"

#ifdef __cplusplus
extern "C" {
#endif // __cplusplus

#include "java-interop-util.h"
#include "logger.h"

#ifdef __cplusplus
}
#endif // __cplusplus

#define DEFAULT_DIRECTORY_MODE S_IRWXU | S_IRGRP | S_IXGRP | S_IROTH | S_IXOTH
#define XA_UNLIKELY(expr) (__builtin_expect ((expr) != 0, 0))

#if __cplusplus >= 201703L
#define UNUSED_ARG [[maybe_unused]]
#else
#if defined (__GNUC__)
#define UNUSED_ARG __attribute__((__unused__))
#else
#define UNUSED_ARG
#endif
#endif

#ifdef __cplusplus
extern "C" {
#endif // __cplusplus
	MONO_API  void    monodroid_strfreev (char **str_array);
	MONO_API  char  **monodroid_strsplit (const char *str, const char *delimiter, int max_tokens);
	MONO_API  char   *monodroid_strdup_printf (const char *format, ...);
	MONO_API  void    monodroid_store_package_name (const char *name);
	MONO_API  int     monodroid_get_namespaced_system_property (const char *name, char **value);
	MONO_API  FILE   *monodroid_fopen (const char* filename, const char* mode);

	MONO_API  int     send_uninterrupted (int fd, void *buf, int len);
	MONO_API  int     recv_uninterrupted (int fd, void *buf, int len);
	MONO_API  void    set_world_accessable (const char *path);
	MONO_API  void    create_public_directory (const char *dir);
	MONO_API  char   *path_combine (const char *path1, const char *path2);
#ifdef __cplusplus
}

namespace xamarin { namespace android
{
	struct timing_point
	{
		time_t sec;
		uint64_t ns;

		void mark ();
	};

	struct timing_period
	{
		timing_point start;
		timing_point end;

		void mark_start ()
		{
			start.mark ();
		}

		void mark_end ()
		{
			end.mark ();
		}
	};

	struct timing_diff
	{
		static constexpr uint32_t ms_in_nsec = 1000000ULL;

		time_t sec;
		uint32_t ms;
		uint32_t ns;

		timing_diff (const timing_period &period);
	};

	class Util
	{
#if defined (ANDROID) || defined (LINUX)
		using timestruct = timespec;
#else
		using timestruct = timeval;
#endif
		static constexpr uint32_t ms_in_nsec = 1000000ULL;

	public:
		FILE            *monodroid_fopen (const char* filename, const char* mode);
		int              monodroid_stat (const char *path, monodroid_stat_t *s);
		monodroid_dir_t *monodroid_opendir (const char *filename);
		int              monodroid_closedir (monodroid_dir_t *dirp);
		int              monodroid_dirent_hasextension (monodroid_dirent_t *e, const char *extension);
		void             monodroid_strfreev (char **str_array);
		char           **monodroid_strsplit (const char *str, const char *delimiter, int max_tokens);
		char            *monodroid_strdup_printf (const char *format, ...);
		char            *monodroid_strdup_vprintf (const char *format, va_list vargs);
		void             monodroid_store_package_name (const char *name);
		int              monodroid_get_namespaced_system_property (const char *name, char **value);
		MonoAssembly    *monodroid_load_assembly (MonoDomain *domain, const char *basename);
		MonoObject      *monodroid_runtime_invoke (MonoDomain *domain, MonoMethod *method, void *obj, void **params, MonoObject **exc);
		MonoClass       *monodroid_get_class_from_name (MonoDomain *domain, const char* assembly, const char *_namespace, const char *type);
		MonoDomain      *monodroid_create_appdomain (MonoDomain *parent_domain, const char *friendly_name, int shadow_copy, const char *shadow_directories);
		MonoClass       *monodroid_get_class_from_image (MonoDomain *domain, MonoImage* image, const char *_namespace, const char *type);
		int              ends_with (const char *str, const char *end);
		char*            path_combine(const char *path1, const char *path2);
		int              send_uninterrupted (int fd, void *buf, int len);
		int              recv_uninterrupted (int fd, void *buf, int len);
		void             create_public_directory (const char *dir);
		int              create_directory (const char *pathname, int mode);
		void             set_world_accessable (const char *path);
		void             set_user_executable (const char *path);
		bool             file_exists (const char *file);
		bool             directory_exists (const char *directory);
		bool             file_copy (const char *to, const char *from);
		jclass           get_class_from_runtime_field (JNIEnv *env, jclass runtime, const char *name, bool make_gref = false);

		char            *strdup_new (const char* s)
		{
			assert (s != nullptr);

			size_t slen = strlen (s);
			char *ret = new char [slen + 1];
			memcpy (ret, s, slen);
			ret[slen] = '\0';

			return ret;
		}
#if !defined (WINDOWS)
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

			concatenate_strings_into (ret, s1, s2, strings...);

			return ret;
		}
#else // def WINDOWS
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
#endif // !def WINDOWS
		bool            is_path_rooted (const char *path);

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

		bool should_log (LogCategories category) const
		{
			return (log_categories & category) != 0;
		}

	private:
		//char *monodroid_strdup_printf (const char *format, va_list vargs);
		void  add_to_vector (char ***vector, int size, char *token);
		void  monodroid_property_set (MonoDomain *domain, MonoProperty *property, void *obj, void **params, MonoObject **exc);

#if WINDOWS
		void package_hash_to_hex (uint32_t hash);
#else
		template<typename IdxType>
		void package_hash_to_hex (IdxType idx);

		template<typename IdxType = size_t, typename ...Indices>
		void package_hash_to_hex (uint32_t hash, IdxType idx, Indices... indices);

		template<typename StringType = const char*, typename ...Strings>
		void concatenate_strings_into (UNUSED_ARG char *dest)
		{}

		template<typename StringType = const char*, typename ...Strings>
		void concatenate_strings_into (char *dest, StringType s1, Strings... strings)
		{
			strcat (dest, s1);
			concatenate_strings_into (dest, strings...);
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
#endif
		int make_directory (const char *path, int mode)
		{
#if WINDOWS
			return mkdir (path);
#else
			return mkdir (path, mode);
#endif
		}

	private:
		char package_property_suffix[9];
	};
} }
#endif // __cplusplus
#endif /* __MONODROID_UTIL_H__ */
