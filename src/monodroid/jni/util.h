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

void create_public_directory (const char *dir);
int create_directory (const char *pathname, int mode);
void set_world_accessable (const char *path);

struct        DylibMono;

MonoAssembly    *monodroid_load_assembly (struct DylibMono *mono, MonoDomain *domain, const char *basename);
void            *monodroid_runtime_invoke (struct DylibMono *mono, MonoDomain *domain, MonoMethod *method, void *obj, void **params, MonoObject **exc);
MonoClass       *monodroid_get_class_from_name (struct DylibMono *mono, MonoDomain *domain, const char* assembly, const char *namespace, const char *type);
MonoDomain      *monodroid_create_appdomain (struct DylibMono *mono, MonoDomain *parent_domain, const char *friendly_name, int shadow_copy, const char *shadow_directories);
MonoClass       *monodroid_get_class_from_image (struct DylibMono *mono, MonoDomain *domain, MonoImage* image, const char *namespace, const char *type);

#endif /* __MONODROID_UTIL_H__ */
