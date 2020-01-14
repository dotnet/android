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
#include <unistd.h>

#include <mono/metadata/assembly.h>
#include <mono/metadata/image.h>
#include <mono/metadata/mono-config.h>

#include "java-interop-util.h"

#include "monodroid.h"
#include "util.hh"
#include "embedded-assemblies.hh"
#include "globals.hh"
#include "monodroid-glue.hh"
#include "xamarin-app.hh"
#include "cpp-util.hh"

using namespace xamarin::android;
using namespace xamarin::android::internal;

// A utility class which allows us to manage memory allocated by `mono_guid_to_string` in an elegant way. We can create
// temporary instances of this class in calls to e.g. `log_debug` which are executed ONLY when debug logging is enabled
class MonoGuidString
{
public:
	explicit MonoGuidString (const uint8_t *id) noexcept
	{
		guid = mono_guid_to_string (id);
	}

	~MonoGuidString ()
	{
		::free (guid);
	}

	const char* get () const noexcept
	{
		return guid;
	}

private:
	char *guid = nullptr;
};

void EmbeddedAssemblies::set_assemblies_prefix (const char *prefix)
{
	if (assemblies_prefix_override != nullptr)
		delete[] assemblies_prefix_override;
	assemblies_prefix_override = prefix != nullptr ? utils.strdup_new (prefix) : nullptr;
}

MonoAssembly*
EmbeddedAssemblies::open_from_bundles (MonoAssemblyName* aname, bool ref_only)
{
	const char *culture = mono_assembly_name_get_culture (aname);
	const char *asmname = mono_assembly_name_get_name (aname);

	size_t name_len = culture == nullptr ? 0 : strlen (culture) + 1;
	name_len += sizeof (".dll");
	name_len += strlen (asmname);

	size_t alloc_size = ADD_WITH_OVERFLOW_CHECK (size_t, name_len, 1);
	char *name = new char [alloc_size];
	name [0] = '\0';

	if (culture != nullptr && *culture != '\0') {
		strcat (name, culture);
		strcat (name, "/");
	}
	strcat (name, asmname);
	char *ename = name + strlen (name);

	MonoAssembly *a = nullptr;
	MonoBundledAssembly **p;

	*ename = '\0';
	if (!utils.ends_with (name, ".dll")) {
		strcat (name, ".dll");
	}

	log_info (LOG_ASSEMBLY, "open_from_bundles: looking for bundled name: '%s'", name);

	for (p = bundled_assemblies; p != nullptr && *p; ++p) {
		MonoImage *image = nullptr;
		MonoImageOpenStatus status;
		const MonoBundledAssembly *e = *p;

		if (strcmp (e->name, name) == 0 &&
				(image  = mono_image_open_from_data_with_name ((char*) e->data, e->size, 0, nullptr, ref_only, name)) != nullptr &&
				(a      = mono_assembly_load_from_full (image, name, &status, ref_only)) != nullptr) {
			mono_config_for_assembly (image);
			break;
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
	mono_install_assembly_preload_hook (open_from_bundles_full, nullptr);
	mono_install_assembly_refonly_preload_hook (open_from_bundles_refonly, nullptr);
}

template<typename Key, typename Entry, int (*compare)(const Key*, const Entry*), bool use_extra_size>
const Entry*
EmbeddedAssemblies::binary_search (const Key *key, const Entry *base, size_t nmemb, [[maybe_unused]] size_t extra_size)
{
	static_assert (compare != nullptr, "compare is a required template parameter");

	// This comes from the user code, so let's be civil
	if (key == nullptr) {
		log_warn (LOG_ASSEMBLY, "Key passed to binary_search must not be nullptr");
		return nullptr;
	}

	// This is a coding error on our part, crash!
	if (base == nullptr) {
		log_fatal (LOG_ASSEMBLY, "Map address not passed to binary_search");
		exit (FATAL_EXIT_MISSING_ASSEMBLY);
	}

	constexpr size_t size = sizeof(Entry);
	while (nmemb > 0) {
		const Entry *ret;
		if constexpr (use_extra_size) {
			ret = reinterpret_cast<const Entry*>(reinterpret_cast<const uint8_t*>(base) + ((size + extra_size) * (nmemb / 2)));
		} else {
			ret = base + (nmemb / 2);
		}

		int result = compare (key, ret);
		if (result < 0) {
			nmemb /= 2;
		} else if (result > 0) {
			if constexpr (use_extra_size) {
				base = reinterpret_cast<const Entry*>(reinterpret_cast<const uint8_t*>(ret) + size + extra_size);
			} else {
				base = ret + 1;
			}
			nmemb -= nmemb / 2 + 1;
		} else {
			return ret;
		}
	}

	return nullptr;
}

MonoReflectionType*
EmbeddedAssemblies::typemap_java_to_managed (MonoString *java_type)
{
	timing_period total_time;
	if (XA_UNLIKELY (utils.should_log (LOG_TIMING))) {
		timing = new Timing ();
		total_time.mark_start ();
	}

	if (XA_UNLIKELY (java_type == nullptr)) {
		log_warn (LOG_ASSEMBLY, "typemap: null 'java_type' passed to 'typemap_java_to_managed'");
		return nullptr;
	}

	simple_pointer_guard<char[], false> java_type_name (mono_string_to_utf8 (java_type));
	if (XA_UNLIKELY (!java_type_name || *java_type_name == '\0')) {
		log_warn (LOG_ASSEMBLY, "typemap: empty Java type name passed to 'typemap_java_to_managed'");
		return nullptr;
	}

	int32_t type_token_id = -1;
	TypeMapModule *module;
#if defined (DEBUG) || !defined (ANDROID)
	if (application_config.instant_run_enabled) {
		size_t idx = 0;
		for (; idx < module_count; idx++) {
			const uint8_t *java_entry = binary_search<const char, uint8_t, compare_java_name, true> (java_type_name.get (), modules[idx].java_map, modules[idx].entry_count, modules[idx].java_name_width + 3);
			if (java_entry == nullptr)
				continue;
			type_token_id = *reinterpret_cast<const int32_t*>(java_entry + modules[idx].java_name_width);
			break;
		}

		if (idx >= module_count) {
			log_error (LOG_ASSEMBLY, "typemap: unable to find module with Java type '%s' mapping", java_type_name.get ());
			return nullptr;
		}

		module = &modules[idx];
	} else {
#endif
		const TypeMapJava *java_entry = binary_search<const char, TypeMapJava, compare_java_name, true> (java_type_name.get (), map_java, java_type_count, java_name_width);
		if (java_entry == nullptr) {
			log_warn (LOG_ASSEMBLY, "typemap: unable to find mapping to a managed type from Java type '%s'", java_type_name.get ());
			return nullptr;
		}

		if (java_entry->module_index >= map_module_count) {
			log_warn (LOG_ASSEMBLY, "typemap: mapping from Java type '%s' to managed type has invalid module index", java_type_name.get ());
			return nullptr;
		}

		module = const_cast<TypeMapModule*>(&map_modules[java_entry->module_index]);
		const TypeMapModuleEntry *entry = binary_search <int32_t, TypeMapModuleEntry, compare_type_token> (&java_entry->type_token_id, module->map, module->entry_count);
		if (entry == nullptr) {
			log_warn (LOG_ASSEMBLY, "typemap: unable to find mapping from Java type '%s' to managed type with token ID %u in module [%s]", java_type_name.get (), java_entry->type_token_id, MonoGuidString (module->module_uuid).get ());
			return nullptr;
		}
		type_token_id = java_entry->type_token_id;
#if defined (DEBUG) || !defined (ANDROID)
	}
#endif

	if (module->image == nullptr) {
		module->image = mono_image_loaded (module->assembly_name);
		if (module->image == nullptr) {
			// TODO: load
			log_error (LOG_ASSEMBLY, "typemap: assembly '%s' not loaded yet!", module->assembly_name);
		}

		if (module->image == nullptr) {
			log_error (LOG_ASSEMBLY, "typemap: unable to load assembly '%s' when looking up managed type corresponding to Java type '%s'", module->assembly_name, java_type_name.get ());
			return nullptr;
		}
	}

	log_debug (LOG_ASSEMBLY, "typemap: java type '%s' corresponds to managed token id %u (0x%x)", java_type_name.get (), type_token_id, type_token_id);
	MonoClass *klass = mono_class_get (module->image, static_cast<uint32_t>(type_token_id));
	if (klass == nullptr) {
		log_error (LOG_ASSEMBLY, "typemap: unable to find managed type with token ID %u in assembly '%s', corresponding to Java type '%s'", type_token_id, module->assembly_name, java_type_name.get ());
		return nullptr;
	}

	MonoReflectionType *ret = mono_type_get_object (mono_domain_get (), mono_class_get_type (klass));
	if (XA_UNLIKELY (utils.should_log (LOG_TIMING))) {
		total_time.mark_end ();

		Timing::info (total_time, "Typemap.java_to_managed: end, total time");
	}

	return ret;
}

int
EmbeddedAssemblies::compare_java_name (const char *java_name, const TypeMapJava *entry)
{
	if (entry == nullptr || entry->java_name[0] == '\0') {
		return -1;
	}

	return strcmp (java_name, reinterpret_cast<const char*>(entry->java_name));
}

#if defined (DEBUG) || !defined (ANDROID)
int
EmbeddedAssemblies::compare_java_name (const char *java_name, const uint8_t *entry)
{
	if (entry == nullptr)
		return 1;

	return strcmp (java_name, reinterpret_cast<const char*>(entry));
}
#endif // DEBUG || !ANDROID

const char*
EmbeddedAssemblies::typemap_managed_to_java (const uint8_t *mvid, const int32_t token)
{
	timing_period total_time;
	if (XA_UNLIKELY (utils.should_log (LOG_TIMING))) {
		timing = new Timing ();
		total_time.mark_start ();
	}

	if (mvid == nullptr) {
		log_warn (LOG_ASSEMBLY, "typemap: no mvid specified in call to typemap_managed_to_java");
		return nullptr;
	}

	const TypeMapModule *map;
	size_t map_entry_count;
#if defined (DEBUG) || !defined (ANDROID)
	if (application_config.instant_run_enabled) {
		map = modules;
		map_entry_count = module_count;
	} else {
#endif
		map = map_modules;
		map_entry_count = map_module_count;
#if defined (DEBUG) || !defined (ANDROID)
	}
#endif
	const TypeMapModule *match = binary_search<uint8_t, TypeMapModule, compare_mvid> (mvid, map, map_entry_count);
	if (match == nullptr) {
		log_warn (LOG_ASSEMBLY, "typemap: module matching MVID [%s] not found.", MonoGuidString (mvid).get ());
		return nullptr;
	}

	if (match->map == nullptr) {
		log_warn (LOG_ASSEMBLY, "typemap: module with MVID [%s] has no associated type map.", MonoGuidString (mvid).get ());
		return nullptr;
	}

	log_debug (LOG_ASSEMBLY, "typemap: MVID [%s] maps to assembly %s, looking for token %d (0x%x), table index %d", MonoGuidString (mvid).get (), match->assembly_name, token, token, token & 0x00FFFFFF);
	// Each map entry is a pair of 32-bit integers: [TypeTokenID][JavaMapArrayIndex]
	const TypeMapModuleEntry *entry = binary_search <int32_t, TypeMapModuleEntry, compare_type_token> (&token, match->map, match->entry_count);
	if (entry == nullptr) {
		if (match->duplicate_count > 0 && match->duplicate_map != nullptr) {
			log_debug (LOG_ASSEMBLY, "typemap: searching module [%s] duplicate map for token %u (0x%x)", MonoGuidString (mvid).get (), token, token);
			entry = binary_search <int32_t, TypeMapModuleEntry, compare_type_token> (&token, match->duplicate_map, match->duplicate_count);
		}

		if (entry == nullptr) {
			log_warn (LOG_ASSEMBLY, "typemap: type with token %d (0x%x) in module {%s} (%s) not found.", token, token, MonoGuidString (mvid).get (), match->assembly_name);
			return nullptr;
		}
	}

	uint32_t java_entry_count;
#if defined (DEBUG) || !defined (ANDROID)
	if (application_config.instant_run_enabled) {
		java_entry_count = match->entry_count;
	} else {
#endif
		java_entry_count = java_type_count;
#if defined (DEBUG) || !defined (ANDROID)
	}
#endif
	if (entry->java_map_index >= java_entry_count) {
		log_warn (LOG_ASSEMBLY, "typemap: type with token %d (0x%x) in module {%s} (%s) has invalid Java type index %u", token, token, MonoGuidString (mvid).get (), match->assembly_name, entry->java_map_index);
		return nullptr;
	}

	const char *ret;
#if defined (DEBUG) || !defined (ANDROID)
	if (application_config.instant_run_enabled) {
		ret = reinterpret_cast<char*>(match->java_map + ((match->java_name_width + 4) * entry->java_map_index));
	} else {
#endif
		const TypeMapJava *java_entry = reinterpret_cast<const TypeMapJava*> (reinterpret_cast<const uint8_t*>(map_java) + ((sizeof(TypeMapJava) + java_name_width) * entry->java_map_index));
		ret = reinterpret_cast<const char*>(reinterpret_cast<const uint8_t*>(java_entry) + 8);
#if defined (DEBUG) || !defined (ANDROID)
	}
#endif

	if (XA_UNLIKELY (ret == nullptr)) {
		log_warn (LOG_ASSEMBLY, "typemap: empty Java type name returned for entry at index %u", entry->java_map_index);
	}

	if (XA_UNLIKELY (utils.should_log (LOG_TIMING))) {
		total_time.mark_end ();

		Timing::info (total_time, "Typemap.managed_to_java: end, total time");
	}

	log_debug (
		LOG_ASSEMBLY,
		"typemap: type with token %d (0x%x) in module {%s} (%s) corresponds to Java type '%s'",
		token,
		token,
		MonoGuidString (mvid).get (),
		match->assembly_name,
		ret
	);

	return ret;
}

int
EmbeddedAssemblies::compare_type_token (const int32_t *token, const TypeMapModuleEntry *entry)
{
	if (entry == nullptr) {
		log_fatal (LOG_ASSEMBLY, "typemap: compare_type_token: entry is nullptr");
		exit (FATAL_EXIT_MISSING_ASSEMBLY);
	}

	return *token - entry->type_token_id;
}

int
EmbeddedAssemblies::compare_mvid (const uint8_t *mvid, const TypeMapModule *module)
{
	return memcmp (mvid, module->module_uuid, sizeof(module->module_uuid));
}

EmbeddedAssemblies::md_mmap_info
EmbeddedAssemblies::md_mmap_apk_file (int fd, uint32_t offset, uint32_t size, const char* filename, const char* apk)
{
	md_mmap_info file_info;
	md_mmap_info mmap_info;

	size_t pageSize       = static_cast<size_t>(monodroid_getpagesize());
	uint32_t offsetFromPage  = static_cast<uint32_t>(offset % pageSize);
	uint32_t offsetPage      = offset - offsetFromPage;
	uint32_t offsetSize      = size + offsetFromPage;

	mmap_info.area        = mmap (nullptr, offsetSize, PROT_READ, MAP_PRIVATE, fd, static_cast<off_t>(offsetPage));

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
		off_t basename_len    = static_cast<off_t>(eb_ext - entry_basename);
		assert (basename_len > 0 && "basename must have a length!");
		if (strncmp (assembly->name, entry_basename, static_cast<size_t>(basename_len)) != 0)
			return false;
	}

	mono_register_symfile_for_assembly (assembly->name, debug_contents, debug_size);

	return true;
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
	close(fd);
}

#if defined (DEBUG) || !defined (ANDROID)
ssize_t EmbeddedAssemblies::do_read (int fd, void *buf, size_t count)
{
	ssize_t ret;
	do {
		ret = ::read (fd, buf, count);
	} while (ret < 0 && errno == EINTR);

	return ret;
}

template<typename H>
bool
EmbeddedAssemblies::typemap_read_header ([[maybe_unused]] int dir_fd, const char *file_type, const char *dir_path, const char *file_path, uint32_t expected_magic, H &header, size_t &file_size, int &fd)
{
	struct stat sbuf;
	int res;

#if __ANDROID_API__ < 21
	simple_pointer_guard<char[]> full_file_path = utils.path_combine (dir_path, file_path);
	res = stat (full_file_path, &sbuf);
#else
	res = fstatat (dir_fd, file_path, &sbuf, 0);
#endif
	if (res < 0) {
		log_error (LOG_ASSEMBLY, "typemap: failed to stat %s file '%s/%s': %s", file_type, dir_path, file_path, strerror (errno));
		return false;
	}

	file_size = static_cast<size_t>(sbuf.st_size);
	if (file_size < sizeof (header)) {
		log_error (LOG_ASSEMBLY, "typemap: %s file '%s/%s' is too small (must be at least %u bytes)", file_type, dir_path, file_path, sizeof (header));
		return false;
	}

#if __ANDROID_API__ < 21
	fd = open (full_file_path, O_RDONLY);
#else
	fd = openat (dir_fd, file_path, O_RDONLY);
#endif
	if (fd < 0) {
		log_error (LOG_ASSEMBLY, "typemap: failed to open %s file %s/%s for reading: %s", file_type, dir_path, file_path, strerror (errno));
		return false;
	}

	ssize_t nread = do_read (fd, &header, sizeof (header));
	if (nread <= 0) {
		if (nread < 0) {
			log_error (LOG_ASSEMBLY, "typemap: failed to read %s file header from '%s/%s': %s", file_type, dir_path, file_path, strerror (errno));
		} else {
			log_error (LOG_ASSEMBLY, "typemap: end of file while reading %s file header from '%s/%s'", file_type, dir_path, file_path);
		}

		return false;
	}

	if (header.magic != expected_magic) {
		log_error (LOG_ASSEMBLY, "typemap: invalid magic value in the %s file header from '%s/%s': expected 0x%X, got 0x%X", file_type, dir_path, file_path, expected_magic, header.magic);
		return false;
	}

	if (header.version != MODULE_FORMAT_VERSION) {
		log_error (LOG_ASSEMBLY, "typemap: incompatible %s format version. This build supports only version %u, file '%s/%s' uses version %u", file_type, MODULE_FORMAT_VERSION, dir_path, file_path, header.version);
		return false;
	}

	return true;
}

uint8_t*
EmbeddedAssemblies::typemap_load_index (TypeMapIndexHeader &header, size_t file_size, int index_fd)
{
	constexpr size_t UUID_SIZE = 16;

	size_t entry_size = header.module_file_name_width + UUID_SIZE;
	size_t data_size = entry_size * module_count;
	if (sizeof(header) + data_size > file_size) {
		log_error (LOG_ASSEMBLY, "typemap: index file is too small, expected %u, found %u bytes", data_size + sizeof(header), file_size);
		return nullptr;
	}

	auto data = new uint8_t [data_size];
	ssize_t nread = do_read (index_fd, data, data_size);
	if (nread != static_cast<ssize_t>(data_size)) {
		log_error (LOG_ASSEMBLY, "typemap: failed to read %u bytes from index file. %s", data_size, strerror (errno));
		return nullptr;
	}

	uint8_t *p = data;
	for (size_t i = 0; i < module_count; i++) {
		memcpy (modules[i].module_uuid, p, UUID_SIZE);
		modules[i].assembly_name = reinterpret_cast<char*>(p + UUID_SIZE);
		p += entry_size;
	}

	return data;
}

uint8_t*
EmbeddedAssemblies::typemap_load_index (int dir_fd, const char *dir_path, const char *index_path)
{
	log_debug (LOG_ASSEMBLY, "typemap: loading TypeMap index file '%s/%s'", dir_path, index_path);

	TypeMapIndexHeader header;
	size_t file_size;
	int fd = -1;
	uint8_t *data = nullptr;

	if (!typemap_read_header (dir_fd, "TypeMap index", dir_path, index_path, MODULE_INDEX_MAGIC, header, file_size, fd)) {
		goto cleanup;
	}

	module_count = header.entry_count;
	modules = new TypeMapModule[module_count]();
	data = typemap_load_index (header, file_size, fd);

  cleanup:
	if (fd >= 0)
		close (fd);

	return data;
}

bool
EmbeddedAssemblies::typemap_load_file (BinaryTypeMapHeader &header, const char *dir_path, const char *file_path, int file_fd, TypeMapModule &module)
{
	size_t alloc_size = ADD_WITH_OVERFLOW_CHECK (size_t, header.assembly_name_length, 1);
	module.assembly_name = new char[alloc_size];

	ssize_t nread = do_read (file_fd, module.assembly_name, header.assembly_name_length);
	module.assembly_name [header.assembly_name_length] = 0;
	module.entry_count = header.entry_count;

	log_debug (
		LOG_ASSEMBLY,
		"typemap: '%s/%s':: entry count == %u; duplicate entry count == %u; Java type name field width == %u; MVID == %s; assembly name length == %u; assembly name == %s",
		dir_path, file_path, header.entry_count, header.duplicate_count, header.java_name_width,
		MonoGuidString (header.module_uuid).get (), header.assembly_name_length, module.assembly_name
	);

	alloc_size = MULTIPLY_WITH_OVERFLOW_CHECK (size_t, header.java_name_width + 4, header.entry_count);
	module.java_name_width = header.java_name_width;
	module.java_map = new uint8_t[alloc_size];
	nread = do_read (file_fd, module.java_map, alloc_size);
	if (nread != static_cast<ssize_t>(alloc_size)) {
		log_error (LOG_ASSEMBLY, "typemap: failed to read %u bytes (java-to-managed) from module file %s/%s. %s", alloc_size, dir_path, file_path, strerror (errno));
		return false;
	}

	module.map = new TypeMapModuleEntry[header.entry_count];
	alloc_size = MULTIPLY_WITH_OVERFLOW_CHECK (size_t, sizeof(TypeMapModuleEntry), header.entry_count);
	nread = do_read (file_fd, module.map, alloc_size);
	if (nread != static_cast<ssize_t>(alloc_size)) {
		log_error (LOG_ASSEMBLY, "typemap: failed to read %u bytes (managed-to-java) from module file %s/%s. %s", alloc_size, dir_path, file_path, strerror (errno));
		return false;
	}

	// alloc_size = module.java_name_width + 1;
	// auto chars = new char[alloc_size]();
	// uint8_t *p = module.java_map;
	// log_debug (LOG_ASSEMBLY, "Java entries in %s/%s", dir_path, file_path);
	// for (size_t i = 0; i < module.entry_count; i++) {
	// 	memcpy (chars, p, module.java_name_width);
	// 	uint32_t token = *reinterpret_cast<const uint32_t*>(p + module.java_name_width);
	// 	log_debug (LOG_ASSEMBLY, "  %04u: %s; %u (0x%x)", i, chars, token, token);
	// 	p += module.java_name_width + 4;
	// }
	// delete[] chars;

	// log_debug (LOG_ASSEMBLY, "Managed entries in %s/%s", dir_path, file_path);
	// for (size_t i = 0; i < module.entry_count; i++) {
	// 	log_debug (LOG_ASSEMBLY, "  %04u: token %u (0x%x); index %u", i, module.map[i].type_token_id, module.map[i].type_token_id, module.map[i].java_map_index);
	// }

	if (header.duplicate_count == 0)
		return true;

	module.duplicate_map = new TypeMapModuleEntry[header.duplicate_count];
	alloc_size = MULTIPLY_WITH_OVERFLOW_CHECK (size_t, sizeof(TypeMapModuleEntry), header.duplicate_count);
	nread = do_read (file_fd, module.duplicate_map, alloc_size);
	if (nread != static_cast<ssize_t>(alloc_size)) {
		log_error (LOG_ASSEMBLY, "typemap: failed to read %u bytes (managed-to-java duplicates) from module file %s/%s. %s", alloc_size, dir_path, file_path, strerror (errno));
		return false;
	}

	return true;
}

bool
EmbeddedAssemblies::typemap_load_file (int dir_fd, const char *dir_path, const char *file_path, TypeMapModule &module)
{
	log_debug (LOG_ASSEMBLY, "typemap: loading TypeMap file '%s/%s'", dir_path, file_path);

	bool ret = true;
	BinaryTypeMapHeader header;
	size_t file_size;
	int fd = -1;

	module.java_map = nullptr;
	module.map = nullptr;
	module.duplicate_map = nullptr;

	if (!typemap_read_header (dir_fd, "TypeMap", dir_path, file_path, MODULE_MAGIC, header, file_size, fd)) {
		ret = false;
		goto cleanup;
	}

	ret = typemap_load_file (header, dir_path, file_path, fd, module);

  cleanup:
	if (fd >= 0)
		close (fd);

	if (!ret) {
		delete[] module.java_map;
		module.java_map = nullptr;
		delete[] module.map;
		module.map = nullptr;
		delete[] module.duplicate_map;
		module.duplicate_map = nullptr;
	}

	return ret;
}

void
EmbeddedAssemblies::try_load_typemaps_from_directory (const char *path)
{
	if (!application_config.instant_run_enabled) {
		log_info (LOG_ASSEMBLY, "typemap: instant run disabled, not loading type maps from storage");
		return;
	}

	simple_pointer_guard<char[]> dir_path (utils.path_combine (path, "typemaps"));
	monodroid_dir_t *dir;
	if ((dir = utils.monodroid_opendir (dir_path)) == nullptr) {
		log_warn (LOG_DEFAULT, "typemap: could not open directory: `%s`", dir_path.get ());
		return;
	}

	int dir_fd;
#if __ANDROID_API__ < 21
	dir_fd = -1;
#else
	dir_fd = dirfd (dir);
#endif

	constexpr char index_name[] = "typemap.index";

	// The pointer must be stored here because, after index is loaded, module.assembly_name points into the index data
	// and must be valid until after the actual module file is loaded.
	simple_pointer_guard<uint8_t[], true> index_data = typemap_load_index (dir_fd, dir_path, index_name);
	if (!index_data) {
		log_fatal (LOG_ASSEMBLY, "typemap: unable to load TypeMap data index from '%s/%s'", dir_path.get (), index_name);
		exit (FATAL_EXIT_NO_ASSEMBLIES); // TODO: use a new error code here
	}

	for (size_t i = 0; i < module_count; i++) {
		TypeMapModule &module = modules[i];
		if (!typemap_load_file (dir_fd, dir_path, module.assembly_name, module)) {
			continue;
		}
	}

	utils.monodroid_closedir (dir);
}
#endif

size_t
EmbeddedAssemblies::register_from (const char *apk_file, monodroid_should_register should_register)
{
	size_t prev  = bundled_assemblies_count;

	gather_bundled_assemblies_from_apk (apk_file, should_register);

	log_info (LOG_ASSEMBLY, "Package '%s' contains %i assemblies", apk_file, bundled_assemblies_count - prev);

	if (bundled_assemblies) {
		size_t alloc_size = MULTIPLY_WITH_OVERFLOW_CHECK (size_t, sizeof(void*), bundled_assemblies_count + 1);
		bundled_assemblies  = reinterpret_cast <MonoBundledAssembly**> (utils.xrealloc (bundled_assemblies, alloc_size));
		bundled_assemblies [bundled_assemblies_count] = nullptr;
	}

	return bundled_assemblies_count;
}
