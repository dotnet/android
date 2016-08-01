#ifndef __MONODROID_UTIL_H__
#define __MONODROID_UTIL_H__

#ifndef TRUE
#define TRUE 1
#endif

#ifndef FALSE
#define FALSE 0
#endif

#if WINDOWS
#define MONODROID_PATH_SEPARATOR "\\"
#else
#define MONODROID_PATH_SEPARATOR "/"
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
#include <unistd.h>
#include <sys/stat.h>
#include <dirent.h>

#include "dylib-mono.h"
#include "monodroid.h"
#include "logger.h"

#define DEFAULT_DIRECTORY_MODE S_IRWXU | S_IRGRP | S_IXGRP | S_IROTH | S_IXOTH

MONO_API  void    monodroid_strfreev (char **str_array);
MONO_API  char  **monodroid_strsplit (const char *string, const char *delimiter, int max_tokens);
MONO_API  char   *monodroid_strdup_printf (const char *format, ...);
MONO_API  void    monodroid_store_package_name (const char *name);
MONO_API  int     monodroid_get_namespaced_system_property (const char *name, char **value);

MONO_API  int     send_uninterrupted (int fd, void *buf, int len);
MONO_API  int     recv_uninterrupted (int fd, void *buf, int len);

int ends_with (const char *str, const char *end);
char* path_combine(const char *path1, const char *path2);
FILE *monodroid_fopen (const char* filename, const char* mode);
int monodroid_stat (const char *path, monodroid_stat_t *s);
monodroid_dir_t* monodroid_opendir (const char *filename);
int monodroid_closedir (monodroid_dir_t *dirp);
int monodroid_dirent_hasextension (monodroid_dirent_t *e, const char *extension);

static inline void*
_assert_valid_pointer (void *p, size_t size)
{
	if (!p) {
		if (size == 0) {
			/* In this case it's "ok" to return NULL, although a malloc
			 * implementation may choose to do something else
			 */
			return p;
		}

		log_fatal (LOG_DEFAULT, "Out of memory!");
		exit (FATAL_EXIT_OUT_OF_MEMORY);
	}

	return p;
}

static inline void*
xmalloc (size_t size)
{
	return _assert_valid_pointer (malloc (size), size);
}

static inline void*
xrealloc (void *ptr, size_t size)
{
	return _assert_valid_pointer (realloc (ptr, size), size);
}

static inline void*
xcalloc (size_t nmemb, size_t size)
{
	return _assert_valid_pointer (calloc (nmemb, size), nmemb * size);
}

#ifdef WINDOWS
/* Those two conversion functions are only properly implemented on Windows
 * because that's the only place where they should be useful.
 */
char* utf16_to_utf8 (const wchar_t *widestr);
wchar_t* utf8_to_utf16 (const char *mbstr);
#endif // def WINDOWS

void create_public_directory (const char *dir);
int create_directory (const char *pathname, int mode);
void set_world_accessable (const char *path);

typedef void  MonoAssembly;
struct        DylibMono;

MonoAssembly    *monodroid_load_assembly (struct DylibMono *mono, MonoDomain *domain, const char *basename);
void            *monodroid_runtime_invoke (struct DylibMono *mono, MonoDomain *domain, MonoMethod *method, void *obj, void **params, MonoObject **exc);
MonoClass       *monodroid_get_class_from_name (struct DylibMono *mono, MonoDomain *domain, const char* assembly, const char *namespace, const char *type);

#endif /* __MONODROID_UTIL_H__ */
