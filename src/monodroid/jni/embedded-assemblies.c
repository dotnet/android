#include <assert.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/stat.h>
#include <sys/mman.h>
#include <fcntl.h>
#include <ctype.h>
#include <libgen.h>

#include "java-interop-util.h"

#include "monodroid.h"
#include "dylib-mono.h"
#include "util.h"
#include "unzip.h"
#include "ioapi.h"
#include "embedded-assemblies.h"

struct TypeMappingInfo {
	char                     *source_apk;
	char                     *source_entry;
	int                       num_entries;
	int                       entry_length;
	int                       value_offset;
	const   char             *mapping;
	struct  TypeMappingInfo  *next;
};

static  MonoBundledAssembly         **bundled_assemblies;
static  int                           bundled_assemblies_count;
static  monodroid_should_register     should_register;
static  void                         *should_register_data;
static  int                           register_debug_symbols;
static  char                         *assemblies_prefix;

static  struct TypeMappingInfo       *java_to_managed_maps;
static  struct TypeMappingInfo       *managed_to_java_maps;

MONO_API void
monodroid_embedded_assemblies_set_should_register (monodroid_should_register r, void *d)
{
	should_register       = r;
	should_register_data  = d;
}

MONO_API int
monodroid_embedded_assemblies_set_register_debug_symbols (int new_value)
{
	int cur                 = register_debug_symbols;
	register_debug_symbols  = new_value;
	return cur;
}

static MonoAssembly*
open_from_bundles (MonoAssemblyName *aname, char **assemblies_path, void *user_data, mono_bool ref_only)
{
	struct DylibMono  *mono     = user_data;
	const char        *culture  = mono->mono_assembly_name_get_culture (aname);
	char *name;
	char *ename;
	int si;

	MonoAssembly *a = NULL;

	int name_len = culture == NULL ? 0 : strlen (culture) + 1;
	name_len += strlen (mono->mono_assembly_name_get_name (aname));

	name = xmalloc (name_len + sizeof (".exe") + 1);
	if (culture != NULL && strlen (culture) > 0)
		sprintf (name, "%s/%s", culture, (const char*) mono->mono_assembly_name_get_name (aname));
	else
		sprintf (name, "%s", (const char*) mono->mono_assembly_name_get_name (aname));
	ename = name + strlen (name);

	static const char *suffixes[] = {
		"",
		".dll",
		".exe",
	};

	for (si = 0; si < sizeof (suffixes)/sizeof (suffixes [0]) && a == NULL; ++si) {
		MonoBundledAssembly **p;

		*ename = '\0';
		strcat (ename, suffixes [si]);

		log_info (LOG_ASSEMBLY, "open_from_bundles: looking for bundled name: '%s'", name);

		for (p = bundled_assemblies; p != NULL && *p; ++p) {
			MonoImage *image = NULL;
			MonoImageOpenStatus status;
			const MonoBundledAssembly *e = *p;

			if (strcmp (e->name, name) == 0 &&
					(image  = mono->mono_image_open_from_data_with_name ((char*) e->data, e->size, 0, NULL, ref_only, name)) != NULL &&
					(a      = mono->mono_assembly_load_from_full (image, name, &status, ref_only)) != NULL) {
				mono->mono_config_for_assembly (image);
				break;
			}
		}
	}
	free (name);
	if (a) {
		log_info (LOG_ASSEMBLY, "open_from_bundles: loaded assembly: %p\n", a);
	}
	return a;
}

static MonoAssembly*
open_from_bundles_full (MonoAssemblyName *aname, char **assemblies_path, void *user_data)
{
	return open_from_bundles (aname, assemblies_path, user_data, 0);
}

static MonoAssembly*
open_from_bundles_refonly (MonoAssemblyName *aname, char **assemblies_path, void *user_data)
{
	return open_from_bundles (aname, assemblies_path, user_data, 1);
}

MONO_API int
monodroid_embedded_assemblies_install_preload_hook (struct DylibMono *imports)
{
	if (!imports)
		return FALSE;
	imports->mono_install_assembly_preload_hook (open_from_bundles_full, imports);
	imports->mono_install_assembly_refonly_preload_hook (open_from_bundles_refonly, imports);
	return TRUE;
}

static int
TypeMappingInfo_compare_key (const void *a, const void *b)
{
	return strcmp (a, b);
}

MONO_API const char *
monodroid_typemap_java_to_managed (const char *java)
{
	struct TypeMappingInfo *info;
	for (info = java_to_managed_maps; info != NULL; info = info->next) {
		/* log_warn (LOG_DEFAULT, "# jonp: checking file: %s!%s for type '%s'", info->source_apk, info->source_entry, java); */
		const char *e = bsearch (java, info->mapping, info->num_entries, info->entry_length, TypeMappingInfo_compare_key);
		if (e == NULL)
			continue;
		return e + info->value_offset;
	}
	return NULL;
}

MONO_API const char *
monodroid_typemap_managed_to_java (const char *managed)
{
	struct TypeMappingInfo *info;
	for (info = managed_to_java_maps; info != NULL; info = info->next) {
		/* log_warn (LOG_DEFAULT, "# jonp: checking file: %s!%s for type '%s'", info->source_apk, info->source_entry, managed); */
		const char *e = bsearch (managed, info->mapping, info->num_entries, info->entry_length, TypeMappingInfo_compare_key);
		if (e == NULL)
			continue;
		return e + info->value_offset;
	}
	return NULL;
}

static void
extract_int (const char **header, const char *source_apk, const char *source_entry, const char *key_name, int *value)
{
	int   read              = 0;
	int   consumed          = 0;
	int   key_name_len      = 0;
	char  scanf_format [20] = { 0, };

	if (header == NULL || *header == NULL)
		return;

	key_name_len    = strlen (key_name);
	if (key_name_len >= (sizeof (scanf_format) - sizeof ("=%d%n"))) {
		*header = NULL;
		return;
	}

	snprintf (scanf_format, sizeof (scanf_format), "%s=%%d%%n", key_name);

	read = sscanf (*header, scanf_format, value, &consumed);
	if (read != 1) {
		log_warn (LOG_DEFAULT, "Could not read header '%s' value from '%s!%s': read %i elements, expected 1 element. Contents: '%s'",
				key_name, source_apk, source_entry, read, *header);
		*header = NULL;
		return;
	}
	*header = *header + consumed + 1;
}

static void
add_type_mapping (struct TypeMappingInfo **info, const char *source_apk, const char *source_entry, const char *addr)
{
	struct TypeMappingInfo  *p        = calloc (1, sizeof (struct TypeMappingInfo));
	int                      version  = 0;
	const char              *data     = addr;

	if (!p)
		return;

	extract_int (&data, source_apk, source_entry, "version",   &version);
	if (version != 1) {
		log_warn (LOG_DEFAULT, "Unsupported version '%i' within type mapping file '%s!%s'. Ignoring...", version, source_apk, source_entry);
		return;
	}

	extract_int (&data, source_apk, source_entry, "entry-count",  &p->num_entries);
	extract_int (&data, source_apk, source_entry, "entry-len",    &p->entry_length);
	extract_int (&data, source_apk, source_entry, "value-offset", &p->value_offset);
	p->mapping      = data;

	if ((p->mapping == 0) ||
			(p->num_entries <= 0) ||
			(p->entry_length <= 0) ||
			(p->value_offset >= p->entry_length) ||
			(p->mapping == NULL)) {
		log_warn (LOG_DEFAULT, "Could not read type mapping file '%s!%s'. Ignoring...", source_apk, source_entry);
		free (p);
		return;
	}

	p->source_apk   = monodroid_strdup_printf ("%s", source_apk);
	p->source_entry = monodroid_strdup_printf ("%s", source_entry);

	if (*info) {
		(*info)->next = p;
	} else {
		*info = p;
	}
}

struct md_mmap_info {
	void   *area;
	size_t  size;
};

static void*
md_mmap_open_file (void *opaque, const char *filename, int mode)
{
	if ((mode & ZLIB_FILEFUNC_MODE_READWRITEFILTER) == ZLIB_FILEFUNC_MODE_READ)
		return xcalloc (1, sizeof (int));
	return NULL;
}

static uLong
md_mmap_read_file (void *opaque, void *stream, void *buf, uLong size)
{
	int *offset = stream;
	struct md_mmap_info *info = opaque;

	memcpy (buf, ((const char*) info->area) + *offset, size);
	*offset += size;

	return size;
}

static long
md_mmap_tell_file (void *opaque, void *stream)
{
	int *offset = stream;
	return *offset;
}

static long
md_mmap_seek_file (void *opaque, void *stream, uLong offset, int origin)
{
	int *pos = stream;
	struct md_mmap_info *info = opaque;

	switch (origin) {
	case ZLIB_FILEFUNC_SEEK_END:
		*pos = info->size;
		/* goto case ZLIB_FILEFUNC_SEEK_CUR */
	case ZLIB_FILEFUNC_SEEK_CUR:
		*pos += (int) offset;
		break;
	case ZLIB_FILEFUNC_SEEK_SET:
		*pos = (int) offset;
		break;
	default:
		return -1;
	}
	return 0;
}

static int
md_mmap_close_file (void *opaque, void *stream)
{
	free (stream);
	return 0;
}

static int
md_mmap_error_file (void *opaque, void *stream)
{
	return 0;
}

static mono_bool
register_debug_symbols_for_assembly (struct DylibMono *mono, const char *entry_name, MonoBundledAssembly *assembly, const mono_byte *debug_contents, int debug_size)
{
	const char *entry_basename  = strrchr (entry_name, '/') + 1;
	// System.dll, System.dll.mdb case
	if (strncmp (assembly->name, entry_basename, strlen (assembly->name)) != 0) {
		// That failed; try for System.dll, System.pdb case
		const char *eb_ext  = strrchr (entry_basename, '.');
		if (eb_ext == NULL)
			return 0;
		int basename_len    = eb_ext - entry_basename;
		assert (basename_len > 0 && "basename must have a length!");
		if (strncmp (assembly->name, entry_basename, basename_len) != 0)
			return 0;
	}

	mono->mono_register_symfile_for_assembly (assembly->name, debug_contents, debug_size);

	return 1;
}

MONO_API int
monodroid_embedded_assemblies_set_assemblies_prefix (const char *prefix)
{
	free (assemblies_prefix);
	assemblies_prefix = strdup (prefix);
	return 0;
}

static const char *
get_assemblies_prefix (void)
{
	if (!assemblies_prefix) {
		assemblies_prefix = strdup ("assemblies/");
	}
	return assemblies_prefix;
}

static int
gather_bundled_assemblies_from_apk (
		struct DylibMono      *mono,
		const char            *apk,
		MonoBundledAssembly ***bundle,
		int                   *bundle_count)
{
	int fd;
	struct stat buf;
	struct md_mmap_info mmap_info;
	unzFile file;

	zlib_filefunc_def funcs = {
		md_mmap_open_file,  // zopen_file,
		md_mmap_read_file,  // zread_file,
		NULL,               // zwrite_file,
		md_mmap_tell_file,  // ztell_file,
		md_mmap_seek_file,  // zseek_file,
		md_mmap_close_file, // zclose_file
		md_mmap_error_file, // zerror_file
		NULL                // opaque
	};

	if ((fd = open (apk, O_RDONLY)) < 0) {
		log_error (LOG_DEFAULT, "ERROR: Unable to load application package %s.", apk);
		// TODO: throw
		return -1;
	}
	if (fstat (fd, &buf) < 0) {
		close (fd);
		// TODO: throw
		return -1;
	}

	mmap_info.area = mmap (NULL, buf.st_size, PROT_READ, MAP_PRIVATE, fd, 0);
	mmap_info.size = buf.st_size;

	log_info (LOG_ASSEMBLY, "                       start: %08p  end: %08p  len: % 12u        apk: %s", 
			mmap_info.area, mmap_info.area + mmap_info.size, (unsigned int) mmap_info.size, apk);

	close (fd);

	funcs.opaque = &mmap_info;

	if ((file = unzOpen2 (NULL, &funcs)) != NULL) {
		do {
			unz_file_info info;
			uLong offset;
			unsigned int *psize;
			char cur_entry_name [256];
			MonoBundledAssembly *cur;
			int entry_is_overridden = FALSE;

			cur_entry_name [0] = 0;
			if (unzGetCurrentFileInfo (file, &info, cur_entry_name, sizeof (cur_entry_name)-1, NULL, 0, NULL, 0) != UNZ_OK ||
					info.compression_method != 0 ||
					unzOpenCurrentFile3 (file, NULL, NULL, 1, NULL) != UNZ_OK ||
					unzGetRawFileOffset (file, &offset) != UNZ_OK) {
				continue;
			}

			if (strcmp ("typemap.jm", cur_entry_name) == 0) {
				add_type_mapping (&java_to_managed_maps, apk, cur_entry_name, ((const char*) mmap_info.area) + offset);
				continue;
			}
			if (strcmp ("typemap.mj", cur_entry_name) == 0) {
				add_type_mapping (&managed_to_java_maps, apk, cur_entry_name, ((const char*) mmap_info.area) + offset);
				continue;
			}

			const char *prefix = get_assemblies_prefix();
			if (strncmp (prefix, cur_entry_name, strlen (prefix)) != 0)
				continue;

			// assemblies must be 4-byte aligned, or Bad Things happen
			if ((offset & 0x3) != 0) {
				log_fatal (LOG_ASSEMBLY, "Assembly '%s' is located at a bad address %p\n", cur_entry_name,
						((const unsigned char*) mmap_info.area) + offset);
				log_fatal (LOG_ASSEMBLY, "You MUST run `zipalign` on %s\n", strrchr (apk, '/') + 1);
				exit (FATAL_EXIT_MISSING_ZIPALIGN);
			}

			if (should_register)
				entry_is_overridden = !should_register (strrchr (cur_entry_name, '/') + 1, should_register_data);

			if ((ends_with (cur_entry_name, ".mdb") || ends_with (cur_entry_name, ".pdb")) &&
					register_debug_symbols &&
					!entry_is_overridden &&
					*bundle != NULL &&
					register_debug_symbols_for_assembly (mono, cur_entry_name, (*bundle) [*bundle_count-1],
						((const mono_byte*) mmap_info.area) + offset,
						info.uncompressed_size))
				continue;

			if (ends_with (cur_entry_name, ".config") &&
					*bundle != NULL) {
				char *assembly_name = monodroid_strdup_printf ("%s", basename (cur_entry_name));
				// Remove '.config' suffix
				*strrchr (assembly_name, '.') = '\0';

				mono->mono_register_config_for_assembly (assembly_name, ((const char*) mmap_info.area) + offset);

				continue;
			}

			if (!(ends_with (cur_entry_name, ".dll") || ends_with (cur_entry_name, ".exe")))
				continue;

			if (entry_is_overridden)
				continue;

			*bundle = xrealloc (*bundle, sizeof(void*)*(*bundle_count + 1));
			cur = (*bundle) [*bundle_count] = xcalloc (1, sizeof (MonoBundledAssembly));
			++*bundle_count;

			cur->name = monodroid_strdup_printf ("%s", strstr (cur_entry_name, prefix) + strlen (prefix));
			cur->data = ((const unsigned char*) mmap_info.area) + offset;

			// MonoBundledAssembly::size is const?!
			psize = (unsigned int*) &cur->size;
			*psize = info.uncompressed_size;

			if ((log_categories & LOG_ASSEMBLY) != 0) {
				const char *p = (const char*) cur->data;

				char header[9];
				int i;
				for (i = 0; i < sizeof(header)-1; ++i)
					header[i] = isprint (p [i]) ? p [i] : '.';
				header [sizeof(header)-1] = '\0';

				log_info (LOG_ASSEMBLY, "file-offset: % 8x  start: %08p  end: %08p  len: % 12i  zip-entry:  %s name: %s [%s]",
						(int) offset, cur->data, cur->data + *psize, (int) info.uncompressed_size, cur_entry_name, cur->name, header);
			}

			unzCloseCurrentFile (file);

		} while (unzGoToNextFile (file) == UNZ_OK);
		unzClose (file);
	}

	return 0;
}

MONO_API int
monodroid_embedded_assemblies_register_from (
		struct DylibMono *imports,
		const char       *apk_file)
{
	int prev  = bundled_assemblies_count;

	gather_bundled_assemblies_from_apk (
			imports,
			apk_file,
			&bundled_assemblies,
			&bundled_assemblies_count);

	log_info (LOG_ASSEMBLY, "Package '%s' contains %i assemblies", apk_file, bundled_assemblies_count - prev);

	if (bundled_assemblies) {
		bundled_assemblies  = xrealloc (bundled_assemblies, sizeof(void*)*(bundled_assemblies_count + 1));
		bundled_assemblies [bundled_assemblies_count] = NULL;
	}

	return bundled_assemblies_count;
}
