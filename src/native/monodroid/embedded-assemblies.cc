#include <host-config.h>

#include <cctype>
#include <cerrno>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <functional>

#include <fcntl.h>
#include <libgen.h>
#include <sys/mman.h>
#include <sys/stat.h>
#include <unistd.h>
#include <dirent.h>
#include <sys/types.h>

#if defined (HAVE_LZ4)
#include <lz4.h>
#endif

#include <mono/metadata/assembly.h>
#include <mono/metadata/class.h>
#include <mono/metadata/image.h>
#include <mono/metadata/mono-config.h>
#include <mono/metadata/mono-debug.h>
#include <mono/metadata/reflection.h>

#include "util.hh"
#include "embedded-assemblies.hh"
#include "globals.hh"
#include "mono-image-loader.hh"
#include "xamarin-app.hh"
#include "cpp-util.hh"
#include "monodroid-glue-internal.hh"
#include "monodroid-state.hh"
#include "startup-aware-lock.hh"
#include "timing-internal.hh"
#include "search.hh"

using namespace xamarin::android;
using namespace xamarin::android::internal;

// A utility class which allows us to manage memory allocated by `mono_guid_to_string` in an elegant way. We can create
// temporary instances of this class in calls to e.g. `log_debug` which are executed ONLY when debug logging is enabled
class MonoGuidString final
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
	assemblies_prefix_override = prefix != nullptr ? Util::strdup_new (prefix) : nullptr;
}

force_inline void
EmbeddedAssemblies::set_assembly_data_and_size (uint8_t* source_assembly_data, uint32_t source_assembly_data_size, uint8_t*& dest_assembly_data, uint32_t& dest_assembly_data_size) noexcept
{
	dest_assembly_data = source_assembly_data;
	dest_assembly_data_size = source_assembly_data_size;
}

force_inline void
EmbeddedAssemblies::get_assembly_data (uint8_t *data, uint32_t data_size, [[maybe_unused]] const char *name, uint8_t*& assembly_data, uint32_t& assembly_data_size) noexcept
{
#if defined (HAVE_LZ4) && defined (RELEASE)
	auto header = reinterpret_cast<const CompressedAssemblyHeader*>(data);
	if (header->magic == COMPRESSED_DATA_MAGIC) {
		if (compressed_assemblies.descriptors == nullptr) [[unlikely]] {
			log_fatal (LOG_ASSEMBLY, "Compressed assembly found but no descriptor defined");
			Helpers::abort_application ();
		}
		if (header->descriptor_index >= compressed_assemblies.count) [[unlikely]] {
			log_fatal (LOG_ASSEMBLY, "Invalid compressed assembly descriptor index %u", header->descriptor_index);
			Helpers::abort_application ();
		}

		CompressedAssemblyDescriptor &cad = compressed_assemblies.descriptors[header->descriptor_index];
		assembly_data_size = data_size - sizeof(CompressedAssemblyHeader);
		if (!cad.loaded) {
			StartupAwareLock decompress_lock (assembly_decompress_mutex);

			if (cad.loaded) {
				set_assembly_data_and_size (reinterpret_cast<uint8_t*>(cad.data), cad.uncompressed_file_size, assembly_data, assembly_data_size);
				return;
			}

			if (cad.data == nullptr) [[unlikely]] {
				log_fatal (LOG_ASSEMBLY, "Invalid compressed assembly descriptor at %u: no data", header->descriptor_index);
				Helpers::abort_application ();
			}

			if (header->uncompressed_length != cad.uncompressed_file_size) {
				if (header->uncompressed_length > cad.uncompressed_file_size) {
					log_fatal (LOG_ASSEMBLY, "Compressed assembly '%s' is larger than when the application was built (expected at most %u, got %u). Assemblies don't grow just like that!", name, cad.uncompressed_file_size, header->uncompressed_length);
					Helpers::abort_application ();
				} else {
					log_debug (LOG_ASSEMBLY, "Compressed assembly '%s' is smaller than when the application was built. Adjusting accordingly.", name);
				}
				cad.uncompressed_file_size = header->uncompressed_length;
			}

			const char *data_start = reinterpret_cast<const char*>(data + sizeof(CompressedAssemblyHeader));
			int ret = LZ4_decompress_safe (data_start, reinterpret_cast<char*>(cad.data), static_cast<int>(assembly_data_size), static_cast<int>(cad.uncompressed_file_size));

			if (ret < 0) {
				log_fatal (LOG_ASSEMBLY, "Decompression of assembly %s failed with code %d", name, ret);
				Helpers::abort_application ();
			}

			if (static_cast<uint64_t>(ret) != cad.uncompressed_file_size) {
				log_debug (LOG_ASSEMBLY, "Decompression of assembly %s yielded a different size (expected %lu, got %u)", name, cad.uncompressed_file_size, static_cast<uint32_t>(ret));
				Helpers::abort_application ();
			}
			cad.loaded = true;
		}

		set_assembly_data_and_size (reinterpret_cast<uint8_t*>(cad.data), cad.uncompressed_file_size, assembly_data, assembly_data_size);
	} else
#endif // def HAVE_LZ4 && def RELEASE
	{
		set_assembly_data_and_size (data, data_size, assembly_data, assembly_data_size);
	}
}

force_inline void
EmbeddedAssemblies::get_assembly_data (XamarinAndroidBundledAssembly const& e, uint8_t*& assembly_data, uint32_t& assembly_data_size) noexcept
{
	get_assembly_data (e.data, e.data_size, e.name, assembly_data, assembly_data_size);
}

force_inline void
EmbeddedAssemblies::get_assembly_data (AssemblyStoreSingleAssemblyRuntimeData const& e, uint8_t*& assembly_data, uint32_t& assembly_data_size) noexcept
{
	get_assembly_data (e.image_data, e.descriptor->data_size, "<assembly_store>", assembly_data, assembly_data_size);
}

template<bool LogMapping>
force_inline void
EmbeddedAssemblies::map_runtime_file (XamarinAndroidBundledAssembly& file) noexcept
{
	int fd;
	bool close_fd;
	if (!AndroidSystem::is_embedded_dso_mode_enabled ()) {
		log_debug (LOG_ASSEMBLY, "Mapping a runtime file from a filesystem");
		close_fd = true;

		// file.file_fd refers to the directory where our files live
		auto temp_fd = Util::open_file_ro_at (file.file_fd, file.file_name);
		if (!temp_fd) {
			return;
		}
		fd = temp_fd.value ();
	} else {
		fd = file.file_fd;
		close_fd = false;
	}

	md_mmap_info map_info = md_mmap_apk_file (fd, file.data_offset, file.data_size, file.name);
	if (close_fd) {
		close (fd);
	}

	if (MonodroidState::is_startup_in_progress ()) {
		file.data = static_cast<uint8_t*>(map_info.area);
	} else {
		uint8_t *expected_null = nullptr;
		bool already_mapped = !__atomic_compare_exchange (
			/* ptr */              &file.data,
			/* expected */         &expected_null,
			/* desired */           reinterpret_cast<uint8_t**>(&map_info.area),
			/* weak */              false,
			/* success_memorder */  __ATOMIC_ACQUIRE,
			/* failure_memorder */  __ATOMIC_RELAXED
		);

		if (already_mapped) {
			log_debug (LOG_ASSEMBLY, "Assembly %s already mmapped by another thread, unmapping our copy", file.name);
			munmap (map_info.area, file.data_size);
			map_info.area = nullptr;
		}
	}

	if constexpr (LogMapping) {
		if (Util::should_log (LOG_ASSEMBLY) && map_info.area != nullptr) [[unlikely]] {
			const char *p = (const char*) file.data;

			std::array<char, 9> header;
			for (size_t j = 0; j < header.size () - 1; ++j)
				header[j] = isprint (p [j]) ? p [j] : '.';
			header [header.size () - 1] = '\0';

			log_info_nocheck (LOG_ASSEMBLY, "file-offset: % 8x  start: %08p  end: %08p  len: % 12i  zip-entry:  %s name: %s [%s]",
			                  (int) file.data_offset, file.data, file.data + file.data_size, (int) file.data_size, file.name, file.name, header.data ());
		}
	}
}

force_inline void
EmbeddedAssemblies::map_assembly (XamarinAndroidBundledAssembly& file) noexcept
{
	map_runtime_file<true> (file);
}

force_inline void
EmbeddedAssemblies::map_debug_data (XamarinAndroidBundledAssembly& file) noexcept
{
	map_runtime_file<false> (file);
}

template<LoaderData TLoaderData>
force_inline MonoAssembly*
EmbeddedAssemblies::load_bundled_assembly (
	XamarinAndroidBundledAssembly& assembly,
	dynamic_local_string<SENSIBLE_PATH_MAX> const& name,
	dynamic_local_string<SENSIBLE_PATH_MAX> const& abi_name,
	TLoaderData loader_data,
	bool ref_only) noexcept
{
	if (assembly.name == nullptr || assembly.name[0] == '\0') {
		return nullptr;
	}

	if (strcmp (assembly.name, name.get ()) != 0) {
		if (strcmp (assembly.name, abi_name.get ()) != 0) {
			return nullptr;
		} else {
			log_debug (LOG_ASSEMBLY, "open_from_bundles: found architecture-specific: '%s'", abi_name.get ());
		}
	}

	if (assembly.data == nullptr) {
		map_assembly (assembly);
	}

	uint8_t *assembly_data;
	uint32_t assembly_data_size;

	get_assembly_data (assembly, assembly_data, assembly_data_size);
	MonoImage *image = MonoImageLoader::load (name, loader_data, assembly_data, assembly_data_size);
	if (image == nullptr) {
		return nullptr;
	}

	if (have_and_want_debug_symbols) {
		uint32_t base_name_length = assembly.name_length - 3; // we need the trailing dot
		for (XamarinAndroidBundledAssembly& debug_file : *bundled_debug_data) {
			if (debug_file.name_length != assembly.name_length) {
				continue;
			}

			if (strncmp (debug_file.name, assembly.name, base_name_length) != 0) {
				continue;
			}

			if (debug_file.data == nullptr) {
				map_debug_data (debug_file);
			}

			if (debug_file.data != nullptr) {
				if (debug_file.data_size > std::numeric_limits<int>::max ()) {
					log_warn (LOG_ASSEMBLY, "Debug info file '%s' is too big for Mono to consume", debug_file.name);
				} else {
					mono_debug_open_image_from_memory (image, reinterpret_cast<const mono_byte*>(debug_file.data), static_cast<int>(debug_file.data_size));
				}
			}
			break;
		}
	}

	MonoImageOpenStatus status;
	MonoAssembly *a = mono_assembly_load_from_full (image, name.get (), &status, ref_only);
	if (a == nullptr || status != MonoImageOpenStatus::MONO_IMAGE_OK) {
		log_warn (LOG_ASSEMBLY, "Failed to load managed assembly '%s'. %s", name.get (), mono_image_strerror (status));
		return nullptr;
	}

	return a;
}

template<LoaderData TLoaderData>
force_inline MonoAssembly*
EmbeddedAssemblies::individual_assemblies_open_from_bundles (dynamic_local_string<SENSIBLE_PATH_MAX>& name, TLoaderData loader_data, bool ref_only) noexcept
{
	if (!Util::ends_with (name, SharedConstants::DLL_EXTENSION)) {
		name.append (SharedConstants::DLL_EXTENSION);
	}

	log_debug (LOG_ASSEMBLY, "individual_assemblies_open_from_bundles: looking for bundled name: '%s'", name.get ());

	dynamic_local_string<SENSIBLE_PATH_MAX> abi_name;
	abi_name
		.assign (SharedConstants::android_lib_abi)
		.append (zip_path_separator)
		.append (name);

	MonoAssembly *a = nullptr;

	for (size_t i = 0; i < application_config.number_of_assemblies_in_apk; i++) {
		a = load_bundled_assembly (bundled_assemblies [i], name, abi_name, loader_data, ref_only);
		if (a != nullptr) {
			return a;
		}
	}

	if (extra_bundled_assemblies != nullptr) {
		for (XamarinAndroidBundledAssembly& assembly : *extra_bundled_assemblies) {
			a = load_bundled_assembly (assembly, name, abi_name, loader_data, ref_only);
			if (a != nullptr) {
				return a;
			}
		}
	}

	return nullptr;
}

force_inline const AssemblyStoreIndexEntry*
EmbeddedAssemblies::find_assembly_store_entry (hash_t hash, const AssemblyStoreIndexEntry *entries, size_t entry_count) noexcept
{
	auto equal = [](AssemblyStoreIndexEntry const& entry, hash_t key) -> bool { return entry.name_hash == key; };
	auto less_than = [](AssemblyStoreIndexEntry const& entry, hash_t key) -> bool { return entry.name_hash < key; };
	ssize_t idx = Search::binary_search<AssemblyStoreIndexEntry, equal, less_than> (hash, entries, entry_count);
	if (idx >= 0) {
		return &entries[idx];
	}

	return nullptr;
}

template<LoaderData TLoaderData>
force_inline MonoAssembly*
EmbeddedAssemblies::assembly_store_open_from_bundles (dynamic_local_string<SENSIBLE_PATH_MAX>& name, TLoaderData loader_data, bool ref_only) noexcept
{
	hash_t name_hash = xxhash::hash (name.get (), name.length ());
	log_debug (LOG_ASSEMBLY, "assembly_store_open_from_bundles: looking for bundled name: '%s' (hash 0x%zx)", name.get (), name_hash);

	const AssemblyStoreIndexEntry *hash_entry = find_assembly_store_entry (name_hash, assembly_store_hashes, assembly_store.index_entry_count);
	if (hash_entry == nullptr) {
		log_warn (LOG_ASSEMBLY, "Assembly '%s' (hash 0x%zx) not found", name.get (), name_hash);
		return nullptr;
	}

	if (hash_entry->descriptor_index >= assembly_store.assembly_count) {
		log_fatal (LOG_ASSEMBLY, "Invalid assembly descriptor index %u, exceeds the maximum value of %u", hash_entry->descriptor_index, assembly_store.assembly_count - 1);
		Helpers::abort_application ();
	}

	AssemblyStoreEntryDescriptor &store_entry = assembly_store.assemblies[hash_entry->descriptor_index];
	AssemblyStoreSingleAssemblyRuntimeData &assembly_runtime_info = assembly_store_bundled_assemblies[store_entry.mapping_index];

	if (assembly_runtime_info.image_data == nullptr) {
		// The assignments here don't need to be atomic, the value will always be the same, so even if two threads
		// arrive here at the same time, nothing bad will happen.
		assembly_runtime_info.image_data = assembly_store.data_start + store_entry.data_offset;
		assembly_runtime_info.descriptor = &store_entry;
		if (store_entry.debug_data_offset != 0) {
			assembly_runtime_info.debug_info_data = assembly_store.data_start + store_entry.debug_data_offset;
		}

		log_debug (
			LOG_ASSEMBLY,
			"Mapped: image_data == %p; debug_info_data == %p; config_data == %p; descriptor == %p; data size == %u; debug data size == %u; config data size == %u; name == '%s'",
			assembly_runtime_info.image_data,
			assembly_runtime_info.debug_info_data,
			assembly_runtime_info.config_data,
			assembly_runtime_info.descriptor,
			assembly_runtime_info.descriptor->data_size,
			assembly_runtime_info.descriptor->debug_data_size,
			assembly_runtime_info.descriptor->config_data_size,
			name.get ()
		);
	}

	uint8_t *assembly_data;
	uint32_t assembly_data_size;

	get_assembly_data (assembly_runtime_info, assembly_data, assembly_data_size);
	MonoImage *image = MonoImageLoader::load (name, loader_data, name_hash, assembly_data, assembly_data_size);
	if (image == nullptr) {
		log_warn (LOG_ASSEMBLY, "Failed to load MonoImage of '%s'", name.get ());
		return nullptr;
	}

	if (have_and_want_debug_symbols && assembly_runtime_info.debug_info_data != nullptr) {
		mono_debug_open_image_from_memory (image, reinterpret_cast<const mono_byte*> (assembly_runtime_info.debug_info_data), static_cast<int>(assembly_runtime_info.descriptor->debug_data_size));
	}

	MonoImageOpenStatus status;
	MonoAssembly *a = mono_assembly_load_from_full (image, name.get (), &status, ref_only);
	if (a == nullptr || status != MonoImageOpenStatus::MONO_IMAGE_OK) {
		log_warn (LOG_ASSEMBLY, "Failed to load managed assembly '%s'. %s", name.get (), mono_image_strerror (status));
		return nullptr;
	}

	return a;
}

// TODO: need to forbid loading assemblies into non-default ALC if they contain marshal method callbacks.
//       The best way is probably to store the information in the assembly `MonoImage*` cache. We should
//       abort() if the assembly contains marshal callbacks.
template<LoaderData TLoaderData>
force_inline MonoAssembly*
EmbeddedAssemblies::open_from_bundles (MonoAssemblyName* aname, TLoaderData loader_data, [[maybe_unused]] MonoError *error, bool ref_only) noexcept
{
	const char *culture = mono_assembly_name_get_culture (aname);
	const char *asmname = mono_assembly_name_get_name (aname);

	dynamic_local_string<SENSIBLE_PATH_MAX> name;
	if (culture != nullptr && *culture != '\0') {
		name.append_c (culture);
		name.append (zip_path_separator);
	}
	name.append_c (asmname);

	MonoAssembly *a;
	if (application_config.have_assembly_store) {
		a = assembly_store_open_from_bundles (name, loader_data, ref_only);
	} else {
		a = individual_assemblies_open_from_bundles (name, loader_data, ref_only);
	}

	if (a == nullptr) {
		log_warn (LOG_ASSEMBLY, "open_from_bundles: failed to load bundled assembly %s", name.get ());
#if defined(DEBUG)
		log_warn (LOG_ASSEMBLY, "open_from_bundles: the assembly might have been uploaded to the device with FastDev instead");
#endif
	}

	return a;
}

MonoAssembly*
EmbeddedAssemblies::open_from_bundles (MonoAssemblyLoadContextGCHandle alc_gchandle, MonoAssemblyName *aname, [[maybe_unused]] char **assemblies_path, [[maybe_unused]] void *user_data, MonoError *error)
{
	constexpr bool ref_only = false;
	return embeddedAssemblies.open_from_bundles (aname, alc_gchandle, error, ref_only);
}

MonoAssembly*
EmbeddedAssemblies::open_from_bundles_full (MonoAssemblyName *aname, [[maybe_unused]] char **assemblies_path, [[maybe_unused]] void *user_data)
{
	constexpr bool ref_only = false;

	return embeddedAssemblies.open_from_bundles (aname, ref_only /* loader_data */, nullptr /* error */, ref_only);
}

void
EmbeddedAssemblies::install_preload_hooks_for_appdomains ()
{
	mono_install_assembly_preload_hook (open_from_bundles_full, nullptr);
}

void
EmbeddedAssemblies::install_preload_hooks_for_alc ()
{
	mono_install_assembly_preload_hook_v3 (
		open_from_bundles,
		nullptr /* user_data */,
		0 /* append */
	);
}

template<typename Key, typename Entry, int (*compare)(const Key*, const Entry*), bool use_precalculated_size>
force_inline const Entry*
EmbeddedAssemblies::binary_search (const Key *key, const Entry *base, size_t nmemb, [[maybe_unused]] size_t precalculated_size) noexcept
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
		Helpers::abort_application ();
	}

	[[maybe_unused]]
	size_t size;

	if constexpr (use_precalculated_size) {
		size = precalculated_size;
		log_info (LOG_ASSEMBLY, "Pre-calculated entry size = %u", size);
	}

	while (nmemb > 0) {
		const Entry *ret;
		if constexpr (use_precalculated_size) {
			ret = reinterpret_cast<const Entry*>(reinterpret_cast<const uint8_t*>(base) + (precalculated_size * (nmemb / 2)));
		} else {
			ret = base + (nmemb / 2);
		}

		int result = compare (key, ret);
		if (result < 0) {
			nmemb /= 2;
		} else if (result > 0) {
			if constexpr (use_precalculated_size) {
				base = reinterpret_cast<const Entry*>(reinterpret_cast<const uint8_t*>(ret) + precalculated_size);
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

#if defined (RELEASE)
force_inline const TypeMapModuleEntry*
EmbeddedAssemblies::binary_search (uint32_t key, const TypeMapModuleEntry *arr, uint32_t n) noexcept
{
	ssize_t left = -1;
	ssize_t right = static_cast<ssize_t>(n);
	ssize_t middle;

	while (right - left > 1) {
		middle = (left + right) >> 1;
		if (arr[middle].type_token_id < key) {
			left = middle;
		} else {
			right = middle;
		}
	}

	return arr[right].type_token_id == key ? &arr[right] : nullptr;
}
#endif // def RELEASE

#if defined (DEBUG)
force_inline int
EmbeddedAssemblies::compare_type_name (const char *type_name, const TypeMapEntry *entry) noexcept
{
	if (entry == nullptr)
		return 1;

	return strcmp (type_name, entry->from);
}

force_inline MonoReflectionType*
EmbeddedAssemblies::typemap_java_to_managed ([[maybe_unused]] hash_t hash, const MonoString *java_type) noexcept
{
	c_unique_ptr<char> java_type_name {mono_string_to_utf8 (const_cast<MonoString*>(java_type))};
	const TypeMapEntry *entry = nullptr;

	if (application_config.instant_run_enabled) {
		TypeMap *module;
		for (size_t i = 0; i < type_map_count; i++) {
			module = &type_maps[i];
			entry = binary_search<const char, TypeMapEntry, compare_type_name, false> (java_type_name.get (), module->java_to_managed, module->entry_count);
			if (entry != nullptr)
				break;
		}
	} else {
		entry = binary_search<const char, TypeMapEntry, compare_type_name, false> (java_type_name.get (), type_map.java_to_managed, type_map.entry_count);
	}

	if (entry == nullptr) [[unlikely]] {
		log_info (LOG_ASSEMBLY, "typemap: unable to find mapping to a managed type from Java type '%s'", java_type_name.get ());
		return nullptr;
	}

	const char *managed_type_name = entry->to;
	if (managed_type_name == nullptr) {
		log_debug (LOG_ASSEMBLY, "typemap: Java type '%s' maps either to an open generic type or an interface type.", java_type_name.get ());
		return nullptr;
	}
 	log_debug (LOG_DEFAULT, "typemap: Java type '%s' corresponds to managed type '%s'", java_type_name.get (), managed_type_name);

	MonoType *type = mono_reflection_type_from_name (const_cast<char*>(managed_type_name), nullptr);
	if (type == nullptr) [[unlikely]] {
		log_info (LOG_ASSEMBLY, "typemap: managed type '%s' (mapped from Java type '%s') could not be loaded", managed_type_name, java_type_name.get ());
		return nullptr;
	}

	MonoReflectionType *ret = mono_type_get_object (Util::get_current_domain (), type);
	if (ret == nullptr) [[unlikely]] {
		log_warn (LOG_ASSEMBLY, "typemap: unable to instantiate managed type '%s'", managed_type_name);
		return nullptr;
	}

	return ret;
}
#else // def DEBUG
force_inline MonoReflectionType*
EmbeddedAssemblies::typemap_java_to_managed (hash_t hash, const MonoString *java_type_name) noexcept
{
	// In microbrenchmarks, `binary_search_branchless` is faster than `binary_search` but in "real" application tests,
	// the simple version appears to yield faster startup... Leaving both for now, for further investigation and
	// potential optimizations
	ssize_t idx = Search::binary_search (hash, map_java_hashes, java_type_count);
	//ptrdiff_t idx = binary_search_branchless (hash, map_java_hashes, java_type_count);

	TypeMapJava const* java_entry = idx >= 0 ? &map_java[idx] : nullptr;
	TypeMapModule *module = java_entry != nullptr && java_entry->module_index < map_module_count ? &map_modules[java_entry->module_index] : nullptr;
	if (module == nullptr) {
		if (java_entry == nullptr) {
			log_info (LOG_ASSEMBLY, "typemap: unable to find mapping to a managed type from Java type '%s' (hash 0x%zx)", to_utf8 (java_type_name).get (), hash);
		} else {
			log_warn (LOG_ASSEMBLY, "typemap: mapping from Java type '%s' to managed type has invalid module index %u", to_utf8 (java_type_name).get (), java_entry->module_index);
		}
		return nullptr;
	}

	const TypeMapModuleEntry *entry = binary_search (java_entry->type_token_id, module->map, module->entry_count);
	if (entry == nullptr) {
		log_info (LOG_ASSEMBLY, "typemap: unable to find mapping from Java type '%s' to managed type with token ID %u in module [%s]", to_utf8 (java_type_name).get (), java_entry->type_token_id, MonoGuidString (module->module_uuid).get ());
		return nullptr;
	}

	if (module->image == nullptr) {
		module->image = mono_image_loaded (module->assembly_name);

		if (module->image == nullptr) {
			log_debug (LOG_ASSEMBLY, "typemap: assembly '%s' hasn't been loaded yet, attempting a full load", module->assembly_name);

			// Fake a request from MonoVM to load the assembly.
			MonoAssemblyName *assembly_name = mono_assembly_name_new (module->assembly_name);
			MonoAssembly *assm;

			if (assembly_name == nullptr) {
				log_error (LOG_ASSEMBLY, "typemap: failed to create Mono assembly name for '%s'", module->assembly_name);
				assm = nullptr;
			} else {
				MonoAssemblyLoadContextGCHandle alc_gchandle = mono_alc_get_default_gchandle ();
				MonoError mono_error;
				assm = embeddedAssemblies.open_from_bundles (assembly_name, alc_gchandle, &mono_error, false /* ref_only */);
			}

			if (assm == nullptr) {
				log_warn (LOG_ASSEMBLY, "typemap: failed to load managed assembly '%s'", module->assembly_name);
			} else {
				module->image = mono_assembly_get_image (assm);
			}
		}

		if (module->image == nullptr) {
			log_error (LOG_ASSEMBLY, "typemap: unable to load assembly '%s' when looking up managed type corresponding to Java type '%s'", module->assembly_name, to_utf8 (java_type_name).get ());
			return nullptr;
		}
	}

	log_debug (LOG_ASSEMBLY, "typemap: java type '%s' corresponds to managed token id %u (0x%x)", to_utf8 (java_type_name).get (), java_entry->type_token_id, java_entry->type_token_id);
	MonoClass *klass = mono_class_get (module->image, java_entry->type_token_id);
	if (klass == nullptr) [[unlikely]] {
		log_error (LOG_ASSEMBLY, "typemap: unable to find managed type with token ID %u in assembly '%s', corresponding to Java type '%s'", java_entry->type_token_id, module->assembly_name, to_utf8 (java_type_name).get ());
		return nullptr;
	}

	// MonoVM in dotnet runtime doesn't use the `domain` parameter passed to `mono_type_get_object` (since AppDomains
	// are gone in NET 6+), in fact, the function `mono_type_get_object` calls (`mono_type_get_object_checked`) itself
	// calls `mono_get_root_domain`. Thus, we can save on a one function call here by passing `nullptr`
	constexpr MonoDomain *domain = nullptr;

	MonoReflectionType *ret = mono_type_get_object (domain, mono_class_get_type (klass));
	if (ret == nullptr) {
		log_warn (LOG_ASSEMBLY, "typemap: unable to instantiate managed type with token ID %u in assembly '%s', corresponding to Java type '%s'", java_entry->type_token_id, module->assembly_name, to_utf8 (java_type_name).get ());
		return nullptr;
	}

	return ret;
}
#endif // ndef DEBUG

MonoReflectionType*
EmbeddedAssemblies::typemap_java_to_managed (MonoString *java_type) noexcept
{
	size_t total_time_index;
	if (FastTiming::enabled ()) [[unlikely]] {
		timing = new Timing ();
		total_time_index = internal_timing->start_event (TimingEventKind::JavaToManaged);
	}

	if (java_type == nullptr) [[unlikely]]{
		log_warn (LOG_ASSEMBLY, "typemap: null 'java_type' passed to 'typemap_java_to_managed'");
		return nullptr;
	}

	// We need to generate hash for all the bytes, and since MonoString is Unicode, we double the length to get the
	// number of bytes.
	int name_len = mono_string_length (java_type) << 1;
	if (name_len <= 0) [[unlikely]] {
		log_warn (LOG_ASSEMBLY, "typemap: empty 'java_type' passed to 'typemap_java_to_managed'");
		return nullptr;
	}

	const mono_unichar2 *type_chars = mono_string_chars (java_type);
	hash_t hash = xxhash::hash (reinterpret_cast<const char*>(type_chars), static_cast<size_t>(name_len));
	MonoReflectionType *ret = typemap_java_to_managed (hash, java_type);

	if (FastTiming::enabled ()) [[unlikely]] {
		internal_timing->end_event (total_time_index);
	}

	return ret;
}

#if defined (DEBUG)
force_inline const TypeMapEntry*
EmbeddedAssemblies::typemap_managed_to_java (const char *managed_type_name) noexcept
{
	const TypeMapEntry *entry = nullptr;

	if (application_config.instant_run_enabled) {
		TypeMap *module;
		for (size_t i = 0; i < type_map_count; i++) {
			module = &type_maps[i];
			entry = binary_search<const char, TypeMapEntry, compare_type_name, false> (managed_type_name, module->managed_to_java, module->entry_count);
			if (entry != nullptr)
				break;
		}
	} else {
		entry = binary_search<const char, TypeMapEntry, compare_type_name, false> (managed_type_name, type_map.managed_to_java, type_map.entry_count);
	}

	return entry;
}

force_inline const char*
EmbeddedAssemblies::typemap_managed_to_java ([[maybe_unused]] MonoType *type, MonoClass *klass, [[maybe_unused]] const uint8_t *mvid) noexcept
{
	c_unique_ptr<char> type_name {mono_type_get_name_full (type, MONO_TYPE_NAME_FORMAT_FULL_NAME)};
	MonoImage *image = mono_class_get_image (klass);
	const char *image_name = mono_image_get_name (image);
	size_t type_name_len = strlen (type_name.get ());
	size_t image_name_len = strlen (image_name);

	dynamic_local_string<SENSIBLE_PATH_MAX> full_name;
	full_name
		.append (type_name.get (), type_name_len)
		.append (", ")
		.append (image_name, image_name_len);

	const TypeMapEntry *entry = typemap_managed_to_java (full_name.get ());
	if (entry == nullptr) [[unlikely]] {
		log_info (LOG_ASSEMBLY, "typemap: unable to find mapping to a Java type from managed type '%s'", full_name.get ());
		return nullptr;
	}

	return entry->to;
}
#else // def DEBUG
force_inline int
EmbeddedAssemblies::compare_mvid (const uint8_t *mvid, const TypeMapModule *module) noexcept
{
	return memcmp (mvid, module->module_uuid, sizeof(module->module_uuid));
}

force_inline const char*
EmbeddedAssemblies::typemap_managed_to_java ([[maybe_unused]] MonoType *type, MonoClass *klass, const uint8_t *mvid) noexcept
{
	const TypeMapModule *match = mvid != nullptr ? binary_search<uint8_t, TypeMapModule, compare_mvid> (mvid, map_modules, map_module_count) : nullptr;
	if (match == nullptr) {
		if (mvid == nullptr) {
			log_warn (LOG_ASSEMBLY, "typemap: no mvid specified in call to typemap_managed_to_java");
		} else {
			log_info (LOG_ASSEMBLY, "typemap: module matching MVID [%s] not found.", MonoGuidString (mvid).get ());
		}
		return nullptr;
	}

	uint32_t token = mono_class_get_type_token (klass);
	log_debug (LOG_ASSEMBLY, "typemap: MVID [%s] maps to assembly %s, looking for token %d (0x%x), table index %d", MonoGuidString (mvid).get (), match->assembly_name, token, token, token & 0x00FFFFFF);
	// Each map entry is a pair of 32-bit integers: [TypeTokenID][JavaMapArrayIndex]
	const TypeMapModuleEntry *entry = match->map != nullptr ? binary_search (token, match->map, match->entry_count) : nullptr;
	if (entry == nullptr) {
		if (match->map == nullptr) {
			log_warn (LOG_ASSEMBLY, "typemap: module with mvid [%s] has no associated type map.", MonoGuidString (mvid).get ());
			return nullptr;
		}

		if (match->duplicate_count > 0 && match->duplicate_map != nullptr) {
			log_debug (LOG_ASSEMBLY, "typemap: searching module [%s] duplicate map for token %u (0x%x)", MonoGuidString (mvid).get (), token, token);
			entry = binary_search (token, match->duplicate_map, match->duplicate_count);
		}

		if (entry == nullptr) {
			log_info (LOG_ASSEMBLY, "typemap: type with token %d (0x%x) in module {%s} (%s) not found.", token, token, MonoGuidString (mvid).get (), match->assembly_name);
			return nullptr;
		}
	}

	if (entry->java_map_index >= java_type_count) [[unlikely]] {
		log_warn (LOG_ASSEMBLY, "typemap: type with token %d (0x%x) in module {%s} (%s) has invalid Java type index %u", token, token, MonoGuidString (mvid).get (), match->assembly_name, entry->java_map_index);
		return nullptr;
	}

	TypeMapJava const& java_entry = map_java[entry->java_map_index];
	if (java_entry.java_name_index >= java_type_count) [[unlikely]] {
		log_warn (LOG_ASSEMBLY, "typemap: type with token %d (0x%x) in module {%s} (%s) points to invalid Java type at index %u (invalid type name index %u)", token, token, MonoGuidString (mvid).get (), match->assembly_name, entry->java_map_index, java_entry.java_name_index);
		return nullptr;
	}
	const char *ret = java_type_names[java_entry.java_name_index];

	if (ret == nullptr) [[unlikely]] {
		log_warn (LOG_ASSEMBLY, "typemap: empty Java type name returned for entry at index %u", entry->java_map_index);
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
#endif // ndef DEBUG

const char*
EmbeddedAssemblies::typemap_managed_to_java (MonoReflectionType *reflection_type, const uint8_t *mvid) noexcept
{
	size_t total_time_index;
	if (FastTiming::enabled ()) [[unlikely]] {
		timing = new Timing ();
		total_time_index = internal_timing->start_event (TimingEventKind::ManagedToJava);
	}

	MonoType *type = mono_reflection_type_get_type (reflection_type);
	if (type == nullptr) {
		log_warn (LOG_ASSEMBLY, "Failed to map reflection type to MonoType");
		return nullptr;
	}

	const char *ret = typemap_managed_to_java (type, mono_class_from_mono_type (type), mvid);

	if (FastTiming::enabled ()) [[unlikely]] {
		internal_timing->end_event (total_time_index);
	}

	return ret;
}

EmbeddedAssemblies::md_mmap_info
EmbeddedAssemblies::md_mmap_apk_file (int fd, uint32_t offset, size_t size, const char* filename)
{
	md_mmap_info file_info;
	md_mmap_info mmap_info;

	size_t pageSize        = static_cast<size_t>(Util::monodroid_getpagesize ());
	size_t offsetFromPage  = offset % pageSize;
	size_t offsetPage      = offset - offsetFromPage;
	size_t offsetSize      = size + offsetFromPage;

	mmap_info.area        = mmap (nullptr, offsetSize, PROT_READ, MAP_PRIVATE, fd, static_cast<off_t>(offsetPage));

	if (mmap_info.area == MAP_FAILED) {
		log_fatal (LOG_DEFAULT, "Could not `mmap` apk fd %d entry `%s`: %s", fd, filename, strerror (errno));
		Helpers::abort_application ();
	}

	mmap_info.size  = offsetSize;
	file_info.area  = (void*)((const char*)mmap_info.area + offsetFromPage);
	file_info.size  = size;

	log_info (LOG_ASSEMBLY, "                       mmap_start: %08p  mmap_end: %08p  mmap_len: % 12u  file_start: %08p  file_end: %08p  file_len: % 12u      apk descriptor: %d  file: %s",
	          mmap_info.area, reinterpret_cast<int*> (mmap_info.area) + mmap_info.size, mmap_info.size,
	          file_info.area, reinterpret_cast<int*> (file_info.area) + file_info.size, file_info.size, fd, filename);

	return file_info;
}

void
EmbeddedAssemblies::gather_bundled_assemblies_from_apk (const char* apk, monodroid_should_register should_register)
{
	int fd;

	if ((fd = open (apk, O_RDONLY)) < 0) {
		log_error (LOG_DEFAULT, "ERROR: Unable to load application package %s.", apk);
		Helpers::abort_application ();
	}
	log_info (LOG_ASSEMBLY, "APK %s FD: %d", apk, fd);

	zip_load_entries (fd, apk, should_register);
}

#if defined (DEBUG)
ssize_t EmbeddedAssemblies::do_read (int fd, void *buf, size_t count)
{
	ssize_t ret;
	do {
		ret = ::read (
			fd,
			buf,
			count
		);
	} while (ret < 0 && errno == EINTR);

	return ret;
}

template<typename H>
bool
EmbeddedAssemblies::typemap_read_header ([[maybe_unused]] int dir_fd, const char *file_type, const char *dir_path, const char *file_path, uint32_t expected_magic, H &header, size_t &file_size, int &fd)
{
	struct stat sbuf;
	int res = fstatat (dir_fd, file_path, &sbuf, 0);
	if (res < 0) {
		log_error (LOG_ASSEMBLY, "typemap: failed to stat %s file '%s/%s': %s", file_type, dir_path, file_path, strerror (errno));
		return false;
	}

	file_size = static_cast<size_t>(sbuf.st_size);
	if (file_size < sizeof (header)) {
		log_error (LOG_ASSEMBLY, "typemap: %s file '%s/%s' is too small (must be at least %u bytes)", file_type, dir_path, file_path, sizeof (header));
		return false;
	}

	fd = openat (dir_fd, file_path, O_RDONLY);
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

std::unique_ptr<uint8_t[]>
EmbeddedAssemblies::typemap_load_index (TypeMapIndexHeader &header, size_t file_size, int index_fd)
{
	size_t entry_size = header.module_file_name_width;
	size_t data_size = entry_size * type_map_count;
	if (sizeof(header) + data_size > file_size) {
		log_error (LOG_ASSEMBLY, "typemap: index file is too small, expected %u, found %u bytes", data_size + sizeof(header), file_size);
		return nullptr;
	}

	auto data = std::make_unique<uint8_t[]> (data_size);
	ssize_t nread = do_read (index_fd, data.get (), data_size);
	if (nread != static_cast<ssize_t>(data_size)) {
		log_error (LOG_ASSEMBLY, "typemap: failed to read %u bytes from index file. %s", data_size, strerror (errno));
		return nullptr;
	}

	uint8_t *p = data.get ();
	for (size_t i = 0; i < type_map_count; i++) {
		type_maps[i].assembly_name = reinterpret_cast<char*>(p);
		p += entry_size;
	}

	return data;
}

std::unique_ptr<uint8_t[]>
EmbeddedAssemblies::typemap_load_index (int dir_fd, const char *dir_path, const char *index_path)
{
	log_debug (LOG_ASSEMBLY, "typemap: loading TypeMap index file '%s/%s'", dir_path, index_path);

	TypeMapIndexHeader header;
	size_t file_size;
	int fd = -1;
	std::unique_ptr<uint8_t[]> data;

	if (typemap_read_header (dir_fd, "TypeMap index", dir_path, index_path, MODULE_INDEX_MAGIC, header, file_size, fd)) {
		type_map_count = header.entry_count;
		type_maps = new TypeMap[type_map_count]();
		data = typemap_load_index (header, file_size, fd);
	}

	if (fd >= 0)
		close (fd);

	return data;
}

bool
EmbeddedAssemblies::typemap_load_file (BinaryTypeMapHeader &header, const char *dir_path, const char *file_path, int file_fd, TypeMap &module)
{
	size_t alloc_size = Helpers::add_with_overflow_check<size_t> (header.assembly_name_length, 1);
	module.assembly_name = new char[alloc_size];

	ssize_t nread = do_read (file_fd, module.assembly_name, header.assembly_name_length);
	if (nread != static_cast<ssize_t>(header.assembly_name_length)) {
		log_error (LOG_ASSEMBLY, "tyemap: failed to read map assembly name from '%s/%s': %s", dir_path, file_path, strerror (errno));
		return false;
	}

	module.assembly_name [header.assembly_name_length] = 0;
	module.entry_count = header.entry_count;

	log_debug (
		LOG_ASSEMBLY,
		"typemap: '%s/%s':: entry count == %u; Java name field width == %u; Managed name width == %u; assembly name length == %u; assembly name == %s",
		dir_path, file_path, header.entry_count, header.java_name_width, header.managed_name_width, header.assembly_name_length, module.assembly_name
	);

	// [name][index]
	size_t java_entry_size = header.java_name_width + sizeof(uint32_t);
	size_t managed_entry_size = header.managed_name_width + sizeof(uint32_t);
	size_t data_size = Helpers::add_with_overflow_check<size_t> (
		header.entry_count * java_entry_size,
		header.entry_count * managed_entry_size
	);

	module.data = new uint8_t [data_size];
	nread = do_read (file_fd, module.data, data_size);
	if (nread != static_cast<ssize_t>(data_size)) {
		log_error (LOG_ASSEMBLY, "tyemap: failed to read map data from '%s/%s': %s", dir_path, file_path, strerror (errno));
		return false;
	}

	module.java_to_managed = new TypeMapEntry [module.entry_count];
	module.managed_to_java = new TypeMapEntry [module.entry_count];

	uint8_t *java_start = module.data;
	uint8_t *managed_start = module.data + (module.entry_count * java_entry_size);
	uint8_t *java_pos = java_start;
	uint8_t *managed_pos = managed_start;
	TypeMapEntry *cur;

	constexpr uint32_t INVALID_TYPE_INDEX = std::numeric_limits<uint32_t>::max ();
	for (size_t i = 0; i < module.entry_count; i++) {
		cur = const_cast<TypeMapEntry*> (&module.java_to_managed[i]);
		cur->from = reinterpret_cast<char*>(java_pos);

		uint32_t idx;
		// This might seem slow but it is in fact compiled into a single instruction and is safe when loading the 32-bit
		// integer from unaligned memory
		memcpy (&idx, java_pos + header.java_name_width, sizeof (idx));
		if (idx < INVALID_TYPE_INDEX) {
			cur->to = reinterpret_cast<char*>(managed_start + (managed_entry_size * idx));
		} else {
			// Ignore the type mapping
			cur->to = nullptr;
		}
		java_pos += java_entry_size;

		cur = const_cast<TypeMapEntry*>(&module.managed_to_java[i]);
		cur->from = reinterpret_cast<char*>(managed_pos);

		memcpy (&idx, managed_pos + header.managed_name_width, sizeof (idx));
		cur->to = reinterpret_cast<char*>(java_start + (java_entry_size * idx));
		managed_pos += managed_entry_size;
	}

	return true;
}

bool
EmbeddedAssemblies::typemap_load_file (int dir_fd, const char *dir_path, const char *file_path, TypeMap &module)
{
	log_debug (LOG_ASSEMBLY, "typemap: loading TypeMap file '%s/%s'", dir_path, file_path);

	bool ret = true;
	BinaryTypeMapHeader header;
	size_t file_size;
	int fd = -1;

	module.java_to_managed = nullptr;
	module.managed_to_java = nullptr;

	if (!typemap_read_header (dir_fd, "TypeMap", dir_path, file_path, MODULE_MAGIC_NAMES, header, file_size, fd)) {
		ret = false;
		goto cleanup;
	}

	ret = typemap_load_file (header, dir_path, file_path, fd, module);

  cleanup:
	if (fd >= 0)
		close (fd);

	if (!ret) {
		delete[] module.java_to_managed;
		module.java_to_managed = nullptr;
		delete[] module.managed_to_java;
		module.managed_to_java = nullptr;
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

	std::unique_ptr<char> dir_path {Util::path_combine (path, "typemaps")};
	DIR *dir;
	if ((dir = ::opendir (dir_path.get ())) == nullptr) {
		log_warn (LOG_ASSEMBLY, "typemap: could not open directory: `%s`", dir_path.get ());
		return;
	}

	int dir_fd = dirfd (dir);

	constexpr char index_name[] = "typemap.index";

	// The pointer must be stored here because, after index is loaded, module.assembly_name points into the index data
	// and must be valid until after the actual module file is loaded.
	std::unique_ptr<uint8_t[]> index_data = typemap_load_index (dir_fd, dir_path.get (), index_name);
	if (!index_data) {
		log_fatal (LOG_ASSEMBLY, "typemap: unable to load TypeMap data index from '%s/%s'", dir_path.get (), index_name);
		Helpers::abort_application ();
	}

	for (size_t i = 0; i < type_map_count; i++) {
		TypeMap *module = &type_maps[i];
		if (!typemap_load_file (dir_fd, dir_path.get (), module->assembly_name, *module)) {
			continue;
		}
	}

	::closedir (dir);
}
#endif // def DEBUG

size_t
EmbeddedAssemblies::register_from_apk (const char *apk_file, monodroid_should_register should_register) noexcept
{
	size_t prev  = number_of_found_assemblies;

	gather_bundled_assemblies_from_apk (apk_file, should_register);

	log_info (LOG_ASSEMBLY, "Package '%s' contains %i assemblies", apk_file, number_of_found_assemblies - prev);

	return number_of_found_assemblies;
}

template<bool MangledNamesMode>
force_inline bool
EmbeddedAssemblies::maybe_register_assembly_from_filesystem (
	[[maybe_unused]] monodroid_should_register should_register,
	size_t &assembly_count,
	const dirent* dir_entry,
	ZipEntryLoadState& state) noexcept
{
	dynamic_local_string<SENSIBLE_PATH_MAX> entry_name;
	auto copy_dentry_and_update_state = [] (dynamic_local_string<SENSIBLE_PATH_MAX> &name, ZipEntryLoadState& state, const dirent* dir_entry)
	{
		name.assign_c (dir_entry->d_name);

		// We don't need to duplicate the name here, it will be done farther on
		state.file_name = dir_entry->d_name;
	};

	// We check whether dir_entry->d_name is an array with a fixed size and whether it's
	// big enough so that we can index the array below without having to worry about buffer
	// overflows.  These are compile-time checks and the status of the field won't change at
	// runtime unless Android breaks compatibility (unlikely).
	//
	// Currently (Jan 2024), dir_try->d_name is declared as `char[256]` by Bionic
	static_assert (std::is_bounded_array_v<decltype(dir_entry->d_name)>);
	static_assert (sizeof(dir_entry->d_name) > SharedConstants::MANGLED_ASSEMBLY_REGULAR_ASSEMBLY_MARKER.size());
	static_assert (sizeof(dir_entry->d_name) > SharedConstants::MANGLED_ASSEMBLY_SATELLITE_ASSEMBLY_MARKER.size());

	if constexpr (MangledNamesMode) {
		// We're only interested in "mangled" file names, namely those starting with either the `lib_` or `lib-` prefixes
		if (dir_entry->d_name[SharedConstants::REGULAR_ASSEMBLY_MARKER_INDEX] == SharedConstants::REGULAR_ASSEMBLY_MARKER_CHAR) {
			assembly_count++;
			copy_dentry_and_update_state (entry_name, state, dir_entry);
			unmangle_name<UnmangleRegularAssembly> (entry_name);
		} else if (dir_entry->d_name[SharedConstants::SATELLITE_ASSEMBLY_MARKER_INDEX] == SharedConstants::SATELLITE_ASSEMBLY_MARKER_CHAR) {
			assembly_count++;
			copy_dentry_and_update_state (entry_name, state, dir_entry);
			unmangle_name<UnmangleSatelliteAssembly> (entry_name);
		} else {
			return false;
		}
	} else {
		if (Util::ends_with (dir_entry->d_name, SharedConstants::DLL_EXTENSION) ||
			Util::ends_with (dir_entry->d_name, SharedConstants::PDB_EXTENSION)) {
			assembly_count++;
			copy_dentry_and_update_state (entry_name, state, dir_entry);
		} else {
			return false;
		}

	}
	state.data_offset = 0;

	auto file_size = Util::get_file_size_at (state.file_fd, state.file_name);
	if (!file_size) {
		return false; // don't terminate, keep going
	}

	state.file_size = static_cast<decltype(state.file_size)>(file_size.value ());
	store_individual_assembly_data (entry_name, state, should_register);

	return false;
}

force_inline bool
EmbeddedAssemblies::maybe_register_blob_from_filesystem (
	[[maybe_unused]] monodroid_should_register should_register,
	size_t &assembly_count,
	const dirent* dir_entry,
	ZipEntryLoadState& state) noexcept
{
	if (dir_entry->d_name[0] != assembly_store_file_name[0]) {
		return false; // keep going
	}

	if (strncmp (dir_entry->d_name, assembly_store_file_name.data (), assembly_store_file_name.size ()) != 0) {
		return false; // keep going
	}

	dynamic_local_string<SENSIBLE_PATH_MAX> blob_name;
	blob_name.assign_c (dir_entry->d_name);

	state.data_offset = 0;
	state.file_name = dir_entry->d_name;

	auto file_size = Util::get_file_size_at (state.file_fd, state.file_name);
	if (!file_size) {
		return false; // don't terminate, keep going
	}
	state.file_size = static_cast<decltype(state.file_size)>(file_size.value ());

	map_assembly_store (blob_name, state);
	assembly_count = assembly_store.assembly_count;

	return true;
}

force_inline size_t
EmbeddedAssemblies::register_from_filesystem (const char *lib_dir_path,bool look_for_mangled_names, monodroid_should_register should_register) noexcept
{
	log_debug (LOG_ASSEMBLY, "Looking for assemblies in '%s'", lib_dir_path);
	DIR *lib_dir = opendir (lib_dir_path); // TODO: put it in a scope guard at some point
	if (lib_dir == nullptr) {
		log_warn (LOG_ASSEMBLY, "Unable to open app library directory '%s': %s", lib_dir_path, std::strerror (errno));
		return 0;
	}

	ZipEntryLoadState state{};
	configure_state_for_individual_assembly_load (state);

	int dir_fd = dirfd (lib_dir);
	if (dir_fd < 0) [[unlikely]] {
		log_warn (LOG_ASSEMBLY, "Unable to obtain file descriptor for directory '%s': %s", lib_dir_path, std::strerror (errno));
		closedir (lib_dir);
		return 0;
	}

	state.file_fd = dup (dir_fd);
	if (state.file_fd < 0) [[unlikely]] {
		log_warn (LOG_ASSEMBLY, "Unable to duplicate file descriptor %d for directory '%s': %s", dir_fd, lib_dir_path, std::strerror (errno));
		closedir (lib_dir);
		return 0;
	}

	auto register_fn =
		application_config.have_assembly_store ? std::mem_fn (&EmbeddedAssemblies::maybe_register_blob_from_filesystem) :
		(look_for_mangled_names ?
		 std::mem_fn (&EmbeddedAssemblies::maybe_register_assembly_from_filesystem<true>) :
		 std::mem_fn (&EmbeddedAssemblies::maybe_register_assembly_from_filesystem<false>
		)
	);

	size_t assembly_count = 0;
	do {
		errno = 0;
		dirent *cur = readdir (lib_dir);
		if (cur == nullptr) {
			if (errno != 0) {
				log_warn (LOG_ASSEMBLY, "Failed to open a directory entry from '%s': %s", lib_dir_path, std::strerror (errno));
				continue; // keep going, no harm
			}
			break; // No more entries, we're done
		}

		// We can ignore the obvious entries here...
		if (cur->d_name[0] == '.') {
			continue;
		}

#if defined (DEBUG)
		if (!should_register (cur->d_name)) {
			assembly_count++;
			continue;
		}
#endif // def DEBUG

		// ...and we can handle the runtime config entry
		if (!runtime_config_blob_found && std::strncmp (cur->d_name, SharedConstants::RUNTIME_CONFIG_BLOB_NAME.data (), SharedConstants::RUNTIME_CONFIG_BLOB_NAME.size ()) == 0) {
			log_debug (LOG_ASSEMBLY, "Mapping runtime config blob from '%s'", cur->d_name);
			auto file_size = Util::get_file_size_at (state.file_fd, cur->d_name);
			if (!file_size) {
				continue;
			}

			auto fd = Util::open_file_ro_at (state.file_fd, cur->d_name);
			if (!fd) {
				continue;
			}

			runtime_config_blob_mmap = md_mmap_apk_file (fd.value (), 0, file_size.value (), cur->d_name);
			runtime_config_blob_found = true;
			continue;
		}

		// We get `true` if it's time to terminate
		if (register_fn (this, should_register, assembly_count, cur, state)) {
			break;
		}
	} while (true);
	closedir (lib_dir);

	return assembly_count;
}

size_t
EmbeddedAssemblies::register_from_filesystem (monodroid_should_register should_register) noexcept
{
	log_debug (LOG_ASSEMBLY, "Registering assemblies from the filesystem");
	constexpr bool LookForMangledNames = true;
	size_t assembly_count = register_from_filesystem (
		AndroidSystem::app_lib_directories[0],
		LookForMangledNames,
		should_register
	);

#if defined(DEBUG)
	constexpr bool DoNotLookForMangledNames = false;

	assembly_count += register_from_filesystem (
		AndroidSystem::get_primary_override_dir (),
		DoNotLookForMangledNames,
		should_register
	);
#endif

	log_debug (LOG_ASSEMBLY, "Found %zu assemblies on the filesystem", assembly_count);
	return assembly_count;
}
