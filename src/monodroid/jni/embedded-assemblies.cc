#include <assert.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/stat.h>
#include <sys/mman.h>
#include <fcntl.h>
#include <ctype.h>
#include <libgen.h>
#include <errno.h>

extern "C" {
#include "java-interop-util.h"
}

#include "monodroid.h"
#include "dylib-mono.h"
#include "util.h"
#include "embedded-assemblies.h"
#include "globals.h"
#include "monodroid-glue.h"

namespace xamarin { namespace android { namespace internal {
	struct TypeMappingInfo {
		char                     *source_apk;
		char                     *source_entry;
		int                       num_entries;
		int                       entry_length;
		int                       value_offset;
		const   char             *mapping;
		TypeMappingInfo          *next;
	};

	struct md_mmap_info {
		void   *area;
		size_t  size;
	};
}}}

using namespace xamarin::android;
using namespace xamarin::android::internal;

const char *EmbeddedAssemblies::suffixes[] = {
	"",
	".dll",
	".exe",
};

constexpr char EmbeddedAssemblies::assemblies_prefix[];
constexpr char EmbeddedAssemblies::override_typemap_entry_name[];


void EmbeddedAssemblies::set_assemblies_prefix (const char *prefix)
{
	if (assemblies_prefix_override != nullptr)
		delete[] assemblies_prefix_override;
	assemblies_prefix_override = prefix != nullptr ? utils.strdup_new (prefix) : nullptr;
}

MonoAssembly*
EmbeddedAssemblies::open_from_bundles (MonoAssemblyName* aname, bool ref_only)
{
	const char *culture = monoFunctions.assembly_name_get_culture (aname);
	const char *asmname = monoFunctions.assembly_name_get_name (aname);

	int name_len = culture == nullptr ? 0 : strlen (culture) + 1;
	name_len += sizeof (".exe");
	name_len += strlen (asmname);
	char *name = new char [name_len + 1];
	name [0] = '\0';

	if (culture != nullptr && *culture != '\0') {
		strcat (name, culture);
		strcat (name, "/");
	}
	strcat (name, asmname);
	char *ename = name + strlen (name);

	MonoAssembly *a = nullptr;
	for (size_t si = 0; si < sizeof (suffixes)/sizeof (suffixes [0]) && a == nullptr; ++si) {
		MonoBundledAssembly **p;

		*ename = '\0';
		strcat (ename, suffixes [si]);

		log_info (LOG_ASSEMBLY, "open_from_bundles: looking for bundled name: '%s'", name);

		for (p = bundled_assemblies; p != nullptr && *p; ++p) {
			MonoImage *image = nullptr;
			MonoImageOpenStatus status;
			const MonoBundledAssembly *e = *p;

			if (strcmp (e->name, name) == 0 &&
					(image  = monoFunctions.image_open_from_data_with_name ((char*) e->data, e->size, 0, nullptr, ref_only, name)) != nullptr &&
					(a      = monoFunctions.assembly_load_from_full (image, name, &status, ref_only)) != nullptr) {
				monoFunctions.config_for_assembly (image);
				break;
			}
		}
	}
	delete[] name;

	if (a && utils.should_log (LOG_ASSEMBLY)) {
		log_info_nocheck (LOG_ASSEMBLY, "open_from_bundles: loaded assembly: %p\n", a);
	}
	return a;
}

MonoAssembly*
EmbeddedAssemblies::open_from_bundles_full (MonoAssemblyName *aname, UNUSED_ARG char **assemblies_path, UNUSED_ARG void *user_data)
{
	return embeddedAssemblies.open_from_bundles (aname, false);
}

MonoAssembly*
EmbeddedAssemblies::open_from_bundles_refonly (MonoAssemblyName *aname, UNUSED_ARG char **assemblies_path, UNUSED_ARG void *user_data)
{
	return embeddedAssemblies.open_from_bundles (aname, true);
}

void
EmbeddedAssemblies::install_preload_hooks ()
{
	monoFunctions.install_assembly_preload_hook (open_from_bundles_full, nullptr);
	monoFunctions.install_assembly_refonly_preload_hook (open_from_bundles_refonly, nullptr);
}

int
EmbeddedAssemblies::TypeMappingInfo_compare_key (const void *a, const void *b)
{
	return strcmp (reinterpret_cast <const char*> (a), reinterpret_cast <const char*> (b));
}

inline const char*
EmbeddedAssemblies::typemap_java_to_managed (const char *java)
{
	for (TypeMappingInfo *info = java_to_managed_maps; info != nullptr; info = info->next) {
		/* log_warn (LOG_DEFAULT, "# jonp: checking file: %s!%s for type '%s'", info->source_apk, info->source_entry, java); */
		const char *e = reinterpret_cast<const char*> (bsearch (java, info->mapping, info->num_entries, info->entry_length, TypeMappingInfo_compare_key));
		if (e == nullptr)
			continue;
		return e + info->value_offset;
	}
	return nullptr;
}

inline const char*
EmbeddedAssemblies::typemap_managed_to_java (const char *managed)
{
	for (TypeMappingInfo *info = managed_to_java_maps; info != nullptr; info = info->next) {
		/* log_warn (LOG_DEFAULT, "# jonp: checking file: %s!%s for type '%s'", info->source_apk, info->source_entry, managed); */
		const char *e = reinterpret_cast <const char*> (bsearch (managed, info->mapping, info->num_entries, info->entry_length, TypeMappingInfo_compare_key));
		if (e == nullptr)
			continue;
		return e + info->value_offset;
	}
	return nullptr;
}

MONO_API const char *
monodroid_typemap_java_to_managed (const char *java)
{
	return embeddedAssemblies.typemap_java_to_managed (java);
}

MONO_API const char *
monodroid_typemap_managed_to_java (const char *managed)
{
	return embeddedAssemblies.typemap_managed_to_java (managed);
}

void
EmbeddedAssemblies::extract_int (const char **header, const char *source_apk, const char *source_entry, const char *key_name, int *value)
{
	int    read              = 0;
	int    consumed          = 0;
	size_t key_name_len      = 0;
	char   scanf_format [20] = { 0, };

	if (header == nullptr || *header == nullptr)
		return;

	key_name_len    = strlen (key_name);
	if (key_name_len >= (sizeof (scanf_format) - sizeof ("=%d%n"))) {
		*header = nullptr;
		return;
	}

	snprintf (scanf_format, sizeof (scanf_format), "%s=%%d%%n", key_name);

	read = sscanf (*header, scanf_format, value, &consumed);
	if (read != 1) {
		log_warn (LOG_DEFAULT, "Could not read header '%s' value from '%s!%s': read %i elements, expected 1 element. Contents: '%s'",
				key_name, source_apk, source_entry, read, *header);
		*header = nullptr;
		return;
	}
	*header = *header + consumed + 1;
}

bool
EmbeddedAssemblies::add_type_mapping (TypeMappingInfo **info, const char *source_apk, const char *source_entry, const char *addr)
{
	TypeMappingInfo *p        = new TypeMappingInfo (); // calloc (1, sizeof (struct TypeMappingInfo));
	int              version  = 0;
	const char      *data     = addr;

	extract_int (&data, source_apk, source_entry, "version",   &version);
	if (version != 1) {
		log_warn (LOG_DEFAULT, "Unsupported version '%i' within type mapping file '%s!%s'. Ignoring...", version, source_apk, source_entry);
		return false;
	}

	extract_int (&data, source_apk, source_entry, "entry-count",  &p->num_entries);
	extract_int (&data, source_apk, source_entry, "entry-len",    &p->entry_length);
	extract_int (&data, source_apk, source_entry, "value-offset", &p->value_offset);
	p->mapping      = data;

	if ((p->mapping == 0) ||
			(p->num_entries <= 0) ||
			(p->entry_length <= 0) ||
			(p->value_offset >= p->entry_length) ||
			(p->mapping == nullptr)) {
		log_warn (LOG_DEFAULT, "Could not read type mapping file '%s!%s'. Ignoring...", source_apk, source_entry);
		free (p);
		return false;
	}

	p->source_apk   = strdup (source_apk);
	p->source_entry = strdup (source_entry);
	if (*info) {
		(*info)->next = p;
	} else {
		*info = p;
	}
	return true;
}

md_mmap_info
EmbeddedAssemblies::md_mmap_apk_file (int fd, uLong offset, uLong size, const char* filename, const char* apk)
{
	md_mmap_info file_info;
	md_mmap_info mmap_info;
	
	int pageSize          = monodroid_getpagesize();
	uLong offsetFromPage  = offset % pageSize;
	uLong offsetPage      = offset - offsetFromPage;
	uLong offsetSize      = size + offsetFromPage;
	
	mmap_info.area        = mmap (nullptr, offsetSize, PROT_READ, MAP_PRIVATE, fd, offsetPage);
	if (mmap_info.area == MAP_FAILED) {
		log_fatal (LOG_DEFAULT, "Could not `mmap` apk `%s` entry `%s`: %s", apk, filename, strerror (errno));
		exit (FATAL_EXIT_CANNOT_FIND_APK);
	}
	
	mmap_info.size  = offsetSize;
	file_info.area  = (void*)((const char*)mmap_info.area + offsetFromPage);
	file_info.size  = size;
	
	log_info (LOG_ASSEMBLY, "                       mmap_start: %08p  mmap_end: %08p  mmap_len: % 12u  file_start: %08p  file_end: %08p  file_len: % 12u      apk: %s  file: %s", 
			mmap_info.area, reinterpret_cast<int*> (mmap_info.area) + mmap_info.size, (unsigned int) mmap_info.size,
			file_info.area, reinterpret_cast<int*> (file_info.area) + file_info.size, (unsigned int) file_info.size, apk, filename);
	
	return file_info;
}

void*
EmbeddedAssemblies::md_mmap_open_file (UNUSED_ARG void *opaque, UNUSED_ARG const char *filename, int mode)
{
	if ((mode & ZLIB_FILEFUNC_MODE_READWRITEFILTER) == ZLIB_FILEFUNC_MODE_READ)
		return utils.xcalloc (1, sizeof (int));
	return nullptr;
}

uLong
EmbeddedAssemblies::md_mmap_read_file (void *opaque, UNUSED_ARG void *stream, void *buf, uLong size)
{
	int fd = *reinterpret_cast<int*>(opaque);
	return read (fd, buf, size);
}

long
EmbeddedAssemblies::md_mmap_tell_file (void *opaque, UNUSED_ARG void *stream)
{
	int fd = *reinterpret_cast<int*>(opaque);
	return lseek (fd, 0, SEEK_CUR);
}

long
EmbeddedAssemblies::md_mmap_seek_file (void *opaque, UNUSED_ARG void *stream, uLong offset, int origin)
{
	int fd = *reinterpret_cast<int*>(opaque);

	switch (origin) {
	case ZLIB_FILEFUNC_SEEK_END:
		lseek (fd, offset, SEEK_END);
		break;
	case ZLIB_FILEFUNC_SEEK_CUR:
		lseek (fd, offset, SEEK_CUR);
		break;
	case ZLIB_FILEFUNC_SEEK_SET:
		lseek (fd, offset, SEEK_SET);
		break;
	default:
		return -1;
	}
	return 0;
}

int
EmbeddedAssemblies::md_mmap_close_file (UNUSED_ARG void *opaque, void *stream)
{
	free (stream);
	return 0;
}

int
EmbeddedAssemblies::md_mmap_error_file (UNUSED_ARG void *opaque, UNUSED_ARG void *stream)
{
	return 0;
}

bool
EmbeddedAssemblies::register_debug_symbols_for_assembly (const char *entry_name, MonoBundledAssembly *assembly, const mono_byte *debug_contents, int debug_size)
{
	const char *entry_basename  = strrchr (entry_name, '/') + 1;
	// System.dll, System.dll.mdb case
	if (strncmp (assembly->name, entry_basename, strlen (assembly->name)) != 0) {
		// That failed; try for System.dll, System.pdb case
		const char *eb_ext  = strrchr (entry_basename, '.');
		if (eb_ext == nullptr)
			return false;
		int basename_len    = eb_ext - entry_basename;
		assert (basename_len > 0 && "basename must have a length!");
		if (strncmp (assembly->name, entry_basename, basename_len) != 0)
			return false;
	}

	monoFunctions.register_symfile_for_assembly (assembly->name, debug_contents, debug_size);

	return true;
}

bool
EmbeddedAssemblies::gather_bundled_assemblies_from_apk (const char* apk, monodroid_should_register should_register)
{
	int fd;
	unzFile file;

	zlib_filefunc_def funcs = {
		md_mmap_open_file,  // zopen_file,
		md_mmap_read_file,  // zread_file,
		nullptr,            // zwrite_file,
		md_mmap_tell_file,  // ztell_file,
		md_mmap_seek_file,  // zseek_file,
		md_mmap_close_file, // zclose_file
		md_mmap_error_file, // zerror_file
		nullptr             // opaque
	};

	if ((fd = open (apk, O_RDONLY)) < 0) {
		log_error (LOG_DEFAULT, "ERROR: Unable to load application package %s.", apk);
		return false;
	}

	funcs.opaque = &fd;

	if ((file = unzOpen2 (nullptr, &funcs)) != nullptr) {
		do {
			unz_file_info info;
			uLong offset;
			unsigned int *psize;
			char cur_entry_name [256];
			MonoBundledAssembly *cur;

			cur_entry_name [0] = 0;
			if (unzGetCurrentFileInfo (file, &info, cur_entry_name, sizeof (cur_entry_name)-1, nullptr, 0, nullptr, 0) != UNZ_OK ||
					info.compression_method != 0 ||
					unzOpenCurrentFile3 (file, nullptr, nullptr, 1, nullptr) != UNZ_OK ||
					unzGetRawFileOffset (file, &offset) != UNZ_OK) {
				continue;
			}

			if (utils.ends_with (cur_entry_name, ".jm")) {
				md_mmap_info map_info   = md_mmap_apk_file (fd, offset, info.uncompressed_size, cur_entry_name, apk);
				add_type_mapping (&java_to_managed_maps, apk, cur_entry_name, (const char*)map_info.area);
				continue;
			}
			if (utils.ends_with (cur_entry_name, ".mj")) {
				md_mmap_info map_info   = md_mmap_apk_file (fd, offset, info.uncompressed_size, cur_entry_name, apk);
				add_type_mapping (&managed_to_java_maps, apk, cur_entry_name, (const char*)map_info.area);
				continue;
			}

			const char *prefix = get_assemblies_prefix();
			if (strncmp (prefix, cur_entry_name, strlen (prefix)) != 0)
				continue;

			// assemblies must be 4-byte aligned, or Bad Things happen
			if ((offset & 0x3) != 0) {
				log_fatal (LOG_ASSEMBLY, "Assembly '%s' is located at bad offset %lu within the .apk\n", cur_entry_name,
						offset);
				log_fatal (LOG_ASSEMBLY, "You MUST run `zipalign` on %s\n", strrchr (apk, '/') + 1);
				exit (FATAL_EXIT_MISSING_ZIPALIGN);
			}

			bool entry_is_overridden = !should_register (strrchr (cur_entry_name, '/') + 1);

			if ((utils.ends_with (cur_entry_name, ".mdb") || utils.ends_with (cur_entry_name, ".pdb")) &&
					register_debug_symbols &&
					!entry_is_overridden &&
					bundled_assemblies != nullptr) {
				md_mmap_info map_info = md_mmap_apk_file(fd, offset, info.uncompressed_size, cur_entry_name, apk);
				if (register_debug_symbols_for_assembly (cur_entry_name, (bundled_assemblies) [bundled_assemblies_count - 1],
						(const mono_byte*)map_info.area,
						info.uncompressed_size))
					continue;
			}

			if (utils.ends_with (cur_entry_name, ".config") && bundled_assemblies != nullptr) {
				char *assembly_name = strdup (basename (cur_entry_name));
				// Remove '.config' suffix
				*strrchr (assembly_name, '.') = '\0';
				
				md_mmap_info map_info = md_mmap_apk_file(fd, offset, info.uncompressed_size, cur_entry_name, apk);
				monoFunctions.register_config_for_assembly (assembly_name, (const char*)map_info.area);

				continue;
			}

			if (!(utils.ends_with (cur_entry_name, ".dll") || utils.ends_with (cur_entry_name, ".exe")))
				continue;

			if (entry_is_overridden)
				continue;

			bundled_assemblies = reinterpret_cast<MonoBundledAssembly**> (utils.xrealloc (bundled_assemblies, sizeof(void*) * (bundled_assemblies_count + 1)));
			cur = bundled_assemblies [bundled_assemblies_count] = reinterpret_cast<MonoBundledAssembly*> (utils.xcalloc (1, sizeof (MonoBundledAssembly)));
			++bundled_assemblies_count;

			md_mmap_info map_info = md_mmap_apk_file (fd, offset, info.uncompressed_size, cur_entry_name, apk);
			cur->name = utils.monodroid_strdup_printf ("%s", strstr (cur_entry_name, prefix) + strlen (prefix));
			cur->data = (const unsigned char*)map_info.area;

			// MonoBundledAssembly::size is const?!
			psize = (unsigned int*) &cur->size;
			*psize = info.uncompressed_size;

			if (utils.should_log (LOG_ASSEMBLY)) {
				const char *p = (const char*) cur->data;

				char header[9];
				for (size_t i = 0; i < sizeof(header)-1; ++i)
					header[i] = isprint (p [i]) ? p [i] : '.';
				header [sizeof(header)-1] = '\0';

				log_info_nocheck (LOG_ASSEMBLY, "file-offset: % 8x  start: %08p  end: %08p  len: % 12i  zip-entry:  %s name: %s [%s]",
						(int) offset, cur->data, cur->data + *psize, (int) info.uncompressed_size, cur_entry_name, cur->name, header);
			}

			unzCloseCurrentFile (file);

		} while (unzGoToNextFile (file) == UNZ_OK);
		unzClose (file);
	}
	
	close(fd);

	return true;
}

void
EmbeddedAssemblies::try_load_typemaps_from_directory (const char *path)
{
	// read the entire typemap file into a string
	// process the string using the add_type_mapping
	char *dir_path = utils.path_combine (path, "typemaps");
	if (dir_path == nullptr || !utils.directory_exists (dir_path)) {
		log_warn (LOG_DEFAULT, "directory does not exist: `%s`", dir_path);
		free (dir_path);
		return;
	}

	monodroid_dir_t *dir;
	if ((dir = utils.monodroid_opendir (dir_path)) == nullptr) {
		log_warn (LOG_DEFAULT, "could not open directory: `%s`", dir_path);
		free (dir_path);
		return;
	}

	monodroid_dirent_t b, *e;
	while (androidSystem.readdir (dir, &b, &e) == 0 && e) {
#if WINDOWS
		char *file_name = utils.utf16_to_utf8 (e->d_name);
#else   /* def WINDOWS */
		char *file_name = e->d_name;
#endif  /* ndef WINDOWS */
		char *file_path = utils.path_combine (dir_path, file_name);
		if (utils.monodroid_dirent_hasextension (e, ".mj") || utils.monodroid_dirent_hasextension (e, ".jm")) {
			char *val = nullptr;
			int len = androidSystem.monodroid_read_file_into_memory (file_path, &val);
			if (len > 0 && val != nullptr) {
				if (utils.monodroid_dirent_hasextension (e, ".mj")) {
					if (!add_type_mapping (&managed_to_java_maps, file_path, override_typemap_entry_name, ((const char*)val)))
						delete[] val;
				} else if (utils.monodroid_dirent_hasextension (e, ".jm")) {
					if (!add_type_mapping (&java_to_managed_maps, file_path, override_typemap_entry_name, ((const char*)val)))
						delete[] val;
				}
			}
		}
	}
	utils.monodroid_closedir (dir);
	free (dir_path);
	return;
}

size_t
EmbeddedAssemblies::register_from (const char *apk_file, monodroid_should_register should_register)
{
	int prev  = bundled_assemblies_count;

	gather_bundled_assemblies_from_apk (apk_file, should_register);

	log_info (LOG_ASSEMBLY, "Package '%s' contains %i assemblies", apk_file, bundled_assemblies_count - prev);

	if (bundled_assemblies) {
		bundled_assemblies  = reinterpret_cast <MonoBundledAssembly**> (utils.xrealloc (bundled_assemblies, sizeof(void*)*(bundled_assemblies_count + 1)));
		bundled_assemblies [bundled_assemblies_count] = nullptr;
	}

	return bundled_assemblies_count;
}

MONO_API int monodroid_embedded_assemblies_set_assemblies_prefix (const char *prefix)
{
	embeddedAssemblies.set_assemblies_prefix (prefix);
	return 0;
}
