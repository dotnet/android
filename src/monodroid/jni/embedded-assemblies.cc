#include <host-config.h>

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

#include <mono/metadata/assembly.h>
#include <mono/metadata/image.h>
#include <mono/metadata/mono-config.h>

#include "java-interop-util.h"

#include "monodroid.h"
#include "util.hh"
#include "embedded-assemblies.hh"
#include "globals.hh"
#include "monodroid-glue.h"
#include "xamarin-app.h"

namespace xamarin { namespace android { namespace internal {
#if defined (DEBUG) || !defined (ANDROID)
	struct TypeMappingInfo {
		char                     *source_apk;
		char                     *source_entry;
		int                       num_entries;
		int                       entry_length;
		int                       value_offset;
		const   char             *mapping;
		TypeMappingInfo          *next;
	};
#endif // DEBUG || !ANDROID
}}}

using namespace xamarin::android;
using namespace xamarin::android::internal;

// Mono almost always (with the exception of the very first assembly load for the corlib which
// passes the assembly name as `mscorlib.dll` for historical reasons) passes to the assembly preload
// hooks just the base name of the assembly, without any extension. Since the majority of assemblies
// we see in the APKs have the `.dll` extension, putting it first ensures that the loop in
// `open_from_bundles` will match the assembly in APK in the first iteration, thus saving some
// minute amout of time.
const char *EmbeddedAssemblies::suffixes[] = {
	".dll",
	".exe",
	"",
};


void EmbeddedAssemblies::set_assemblies_prefix (const char *prefix)
{
	if (assemblies_prefix_override != nullptr)
		delete[] assemblies_prefix_override;
	assemblies_prefix_override = prefix != nullptr ? utils.strdup_new (prefix) : nullptr;
}

void
EmbeddedAssemblies::resize_bundled_assemblies (size_t new_size)
{
	if (new_size <= bundled_assemblies_count) {
		return; // No need to raise a fuss, just ignore
	}

	auto new_array = new XamarinBundledAssembly[new_size] ();

	// We can safely do this because XamarinBundledAssembly is POD (Plain Old Data) and we don't
	// need to worry about copy constructors and destructors
	if (bundled_assemblies_count > 0) {
		memcpy (new_array, bundled_assemblies, bundled_assemblies_count * sizeof(XamarinBundledAssembly));
	}

	delete[] bundled_assemblies;
	bundled_assemblies = new_array;
	bundled_assemblies_size = new_size;
}

void
EmbeddedAssemblies::bundled_assemblies_cleanup ()
{
	if (bundled_assemblies_size - bundled_assemblies_count <= BUNDLED_ASSEMBLIES_EXCESS_ITEMS_LIMIT) {
		return;
	}
	resize_bundled_assemblies (bundled_assemblies_count);
}

inline void
EmbeddedAssemblies::load_assembly_debug_info_from_bundles (const char *aname, const char *assembly_file_name)
{
	if (!register_debug_symbols || !bundled_assemblies_have_debug_info || aname == nullptr) {
		return;
	}

	size_t aname_len = strlen (aname);
	for (size_t i = 0; i < bundled_assemblies_count; i++) {
		XamarinBundledAssembly &entry = bundled_assemblies[i];
		if (entry.type != FileType::DebugInfo)
			continue;

		// We know the entry has one of the debug info extensions, so we can take this shortcut to
		// avoid having to allocate memory and use string comparison in order to find a match for
		// the `aname` assembly
		if (strncmp (entry.name, aname, aname_len) != 0) {
			continue;
		}

		size_t entry_name_len = strlen (entry.name);
		if (entry_name_len < aname_len) {
			continue;
		}

		size_t ext_len = entry_name_len - aname_len;
		if (ext_len != 4 && ext_len != 8) { // 'Assembly.pdb' || 'Assembly.{exe,dll}.mdb'
			continue;
		}

		log_info (LOG_ASSEMBLY, "Registering symbol file %s for %s", entry.name, assembly_file_name);
		mono_register_symfile_for_assembly (assembly_file_name, get_mmap_file_data<const mono_byte*> (entry), static_cast<int> (entry.data_size));
		break;
	}
}

inline void
EmbeddedAssemblies::load_assembly_config_from_bundles (const char *aname)
{
	static constexpr size_t config_ext_len = sizeof(config_ext) - 1;

	if (!bundled_assemblies_have_configs || aname == nullptr) {
	        return;
	}

	size_t aname_len = strlen (aname);
	for (size_t i = 0; i < bundled_assemblies_count; i++) {
		XamarinBundledAssembly &entry = bundled_assemblies[i];
		if (entry.type != FileType::Config)
			continue;

		// We know the entry has the `.{dll,exe}.config` extension, so we can take this shortcut to
		// avoid having to allocate memory and use string comparison in order to find a match for
		// the `aname` assembly
		if (strncmp (entry.name, aname, aname_len) != 0) {
			continue;
		}

		size_t entry_name_len = strlen (entry.name);
		if (entry_name_len < aname_len || entry_name_len - aname_len != config_ext_len) {
			continue;
		}

		log_info (LOG_ASSEMBLY, "Registering config file %s for %s", entry.name, aname);
		mono_register_config_for_assembly (aname, get_mmap_file_data<const char*> (entry));
		break;
	}
}

MonoAssembly*
EmbeddedAssemblies::open_from_bundles (MonoAssemblyName* aname, bool ref_only)
{
	if (bundled_assemblies_count == 0) {
		return nullptr;
	}

	const char *culture = mono_assembly_name_get_culture (aname);
	const char *asmname = mono_assembly_name_get_name (aname);

	bool free_asmname = false;
	size_t alloc_size;
	if (XA_UNLIKELY (!first_assembly_load_hook_called)) {
		static constexpr char mscorlib[] = "mscorlib.dll";
		static constexpr size_t basename_len = 8;

		// The very first thing Mono does is load the corlib using the `mscorlib.dll` name while all
		// the other assemblies (including new calls to map `mscorlib`) are loaded (at least in the
		// vast majority of cases) without the .dll or .exe extensions. Adjust `asmname` if this is
		// the very first load.
		if (strcmp (asmname, mscorlib) == 0) {
			// Mustn't modify `asmname` as the value returned by `mono_assembly_name_get_name` comes
			// directly from the opaque `MonoAssemblyName` structure and may be used elsewhere.
			alloc_size = basename_len + 1;
			auto new_name = new char[alloc_size]();
			memcpy (new_name, asmname, basename_len);
			asmname = new_name;
			free_asmname = true;

			log_info (LOG_ASSEMBLY, "open_from_bundles: first assembly hook invocation. Assembly name adjusted from '%s' to '%s'", mscorlib, asmname);
		}
		first_assembly_load_hook_called = true;
	}

	size_t name_len = culture == nullptr ? 0 : strlen (culture) + 1;
	name_len += sizeof (".exe");
	name_len += strlen (asmname);

	alloc_size = ADD_WITH_OVERFLOW_CHECK (size_t, name_len, 1);
	char *name = new char [alloc_size];
	name [0] = '\0';

	if (culture != nullptr && *culture != '\0') {
		strcat (name, culture);
		strcat (name, "/");
	}
	strcat (name, asmname);
	char *ename = name + strlen (name);

	MonoAssembly *a = nullptr;
	for (size_t si = 0; si < sizeof (suffixes)/sizeof (suffixes [0]) && a == nullptr; ++si) {
		*ename = '\0';
		strcat (name, suffixes [si]);

		log_info (LOG_ASSEMBLY, "open_from_bundles: looking for bundled name: '%s'", name);

		for (size_t i = 0; i < bundled_assemblies_count; i++) {
			XamarinBundledAssembly &assembly = bundled_assemblies[i];

			if (assembly.type != FileType::Assembly || strcmp (assembly.name, name) != 0) {
				continue;
			}

			if (!assembly.config_and_symbols_processed) {
				load_assembly_debug_info_from_bundles (asmname, assembly.name);
				load_assembly_config_from_bundles (assembly.name);
				assembly.config_and_symbols_processed = true;
			}

			MonoImage *image = mono_image_open_from_data_with_name (get_mmap_file_data<char*>(assembly), static_cast<uint32_t>(assembly.data_size), 0, nullptr, ref_only, name);
			if (image == nullptr) {
				break;
			}

			MonoImageOpenStatus status;
			a = mono_assembly_load_from_full (image, name, &status, ref_only);
			if (a == nullptr) {
				mono_image_close (image);
				break;
			}

			mono_config_for_assembly (image);
			break;
		}
	}
	delete[] name;
	if (free_asmname)
		delete[] asmname;

	if (XA_UNLIKELY (a != nullptr && utils.should_log (LOG_ASSEMBLY))) {
		log_info_nocheck (LOG_ASSEMBLY, "open_from_bundles: loaded assembly: %s (%p)\n", asmname, a);
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
	mono_install_assembly_preload_hook (open_from_bundles_full, nullptr);
	mono_install_assembly_refonly_preload_hook (open_from_bundles_refonly, nullptr);
}

int
EmbeddedAssemblies::TypeMappingInfo_compare_key (const void *a, const void *b)
{
	return strcmp (reinterpret_cast <const char*> (a), reinterpret_cast <const char*> (b));
}

inline const char*
EmbeddedAssemblies::find_entry_in_type_map (const char *name, uint8_t map[], TypeMapHeader& header)
{
	const char *e = reinterpret_cast<const char*> (bsearch (name, map, header.entry_count, header.entry_length, TypeMappingInfo_compare_key ));
	if (e == nullptr)
		return nullptr;
	return e + header.value_offset;
}

inline const char*
EmbeddedAssemblies::typemap_java_to_managed (const char *java)
{
#if defined (DEBUG) || !defined (ANDROID)
	for (TypeMappingInfo *info = java_to_managed_maps; info != nullptr; info = info->next) {
		/* log_warn (LOG_DEFAULT, "# jonp: checking file: %s!%s for type '%s'", info->source_apk, info->source_entry, java); */
		const char *e = reinterpret_cast<const char*> (bsearch (java, info->mapping, static_cast<size_t>(info->num_entries), static_cast<size_t>(info->entry_length), TypeMappingInfo_compare_key));
		if (e == nullptr)
			continue;
		return e + info->value_offset;
	}
#endif
	return find_entry_in_type_map (java, jm_typemap, jm_typemap_header);
}

inline const char*
EmbeddedAssemblies::typemap_managed_to_java (const char *managed)
{
#if defined (DEBUG) || !defined (ANDROID)
	for (TypeMappingInfo *info = managed_to_java_maps; info != nullptr; info = info->next) {
		/* log_warn (LOG_DEFAULT, "# jonp: checking file: %s!%s for type '%s'", info->source_apk, info->source_entry, managed); */
		const char *e = reinterpret_cast <const char*> (bsearch (managed, info->mapping, static_cast<size_t>(info->num_entries), static_cast<size_t>(info->entry_length), TypeMappingInfo_compare_key));
		if (e == nullptr)
			continue;
		return e + info->value_offset;
	}
#endif
	return find_entry_in_type_map (managed, mj_typemap, mj_typemap_header);
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

#if defined (DEBUG) || !defined (ANDROID)
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
		delete p;
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
		delete p;
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
#endif // DEBUG || !ANDROID

void
EmbeddedAssemblies::md_mmap_apk_file (XamarinBundledAssembly &xba)
{
	if (xba.mmap_file_data != nullptr)
		return;

	auto pageSize       = static_cast<size_t>(monodroid_getpagesize());
	auto offsetFromPage = static_cast<off_t>(static_cast<size_t>(xba.data_offset) % pageSize);
	off_t offsetPage    = xba.data_offset - offsetFromPage;
	size_t offsetSize   = xba.data_size + static_cast<size_t>(offsetFromPage);

	auto area = reinterpret_cast<uint8_t*>(mmap (nullptr, offsetSize, PROT_READ, MAP_PRIVATE, xba.apk_fd, static_cast<off_t>(offsetPage)));

	if (area == MAP_FAILED) {
		log_fatal (LOG_DEFAULT, "Could not `mmap` apk `%s` entry `%s`: %s", xba.apk_name, xba.name, strerror (errno));
		exit (FATAL_EXIT_CANNOT_FIND_APK);
	}

	xba.mmap_size  = offsetSize;
	xba.mmap_file_data  = area + offsetFromPage;

	log_info (LOG_ASSEMBLY, "[%s] mmap_start: %08p; mmap_end: %08p; mmap_len: % 12u; file_start: %08p; file_end: %08p; file_len: % 12u; apk: %s",
	          xba.name,
	          area,
	          area + xba.mmap_size,
	          static_cast<uint32_t>(xba.mmap_size),
	          xba.mmap_file_data,
	          xba.mmap_file_data + xba.data_size,
	          static_cast<uint32_t> (xba.data_size),
	          xba.apk_name);
}

void
EmbeddedAssemblies::gather_bundled_assemblies_from_apk (const char* apk, monodroid_should_register should_register)
{
	int fd;

	if ((fd = open (apk, O_RDONLY)) < 0) {
		log_error (LOG_DEFAULT, "ERROR: Unable to load application package %s.", apk);
		exit (FATAL_EXIT_NO_ASSEMBLIES);
	}

	zip_load_entries (fd, utils.strdup_new (apk), should_register);
}

#if defined (DEBUG) || !defined (ANDROID)
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

	monodroid_dirent_t *e;
	while ((e = androidSystem.readdir (dir)) != nullptr) {
#if WINDOWS
		char *file_name = utils.utf16_to_utf8 (e->d_name);
#else   /* def WINDOWS */
		char *file_name = e->d_name;
#endif  /* ndef WINDOWS */
		char *file_path = utils.path_combine (dir_path, file_name);
		if (utils.monodroid_dirent_hasextension (e, ".mj") || utils.monodroid_dirent_hasextension (e, ".jm")) {
			char *val = nullptr;
			size_t len = androidSystem.monodroid_read_file_into_memory (file_path, &val);
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
#endif

size_t
EmbeddedAssemblies::register_from (const char *apk_file, monodroid_should_register should_register)
{
	size_t prev  = bundled_assemblies_count;

	gather_bundled_assemblies_from_apk (apk_file, should_register);

	log_info (LOG_ASSEMBLY, "Package '%s' contains %i assemblies", apk_file, bundled_assemblies_count - prev);

	return bundled_assemblies_count;
}

MONO_API int monodroid_embedded_assemblies_set_assemblies_prefix (const char *prefix)
{
	embeddedAssemblies.set_assemblies_prefix (prefix);
	return 0;
}
