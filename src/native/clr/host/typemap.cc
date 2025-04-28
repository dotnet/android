#include <array>

#include <host/typemap.hh>
#include <runtime-base/timing-internal.hh>
#include <runtime-base/search.hh>
#include <runtime-base/util.hh>
#include <shared/xxhash.hh>
#include <xamarin-app.hh>

using namespace xamarin::android;

namespace {
	class MonoGuidString
	{
		static inline constexpr size_t MVID_SIZE = 16;
		static inline constexpr size_t NUM_HYPHENS = 4;
		static inline constexpr size_t BUF_SIZE = (MVID_SIZE * 2) + NUM_HYPHENS + 1;

	public:
		explicit MonoGuidString (const uint8_t *mvid) noexcept
		{
			if (mvid == nullptr) {
				_ascii_form[0] = '\0';
				return;
			}

			// In the caller we trust, we have no way to validate the size here
			auto to_hex = [this, &mvid] (size_t &dest_idx, size_t src_idx) {
				Util::to_hex (mvid[src_idx], _ascii_form[dest_idx], _ascii_form[dest_idx + 1]);
				dest_idx += 2;
			};

			auto hyphen = [this] (size_t &dest_idx) {
				_ascii_form[dest_idx++] = '-';
			};

			size_t dest_idx = 0;
			to_hex (dest_idx, 3); to_hex (dest_idx, 2); to_hex (dest_idx, 1); to_hex (dest_idx, 0);
			hyphen (dest_idx);

			to_hex (dest_idx, 5); to_hex (dest_idx, 4);
			hyphen (dest_idx);

			to_hex (dest_idx, 7); to_hex (dest_idx, 6);
			hyphen (dest_idx);

			to_hex (dest_idx, 8); to_hex (dest_idx, 9);
			hyphen (dest_idx);

			to_hex (dest_idx, 10); to_hex (dest_idx, 11); to_hex (dest_idx, 12);
			to_hex (dest_idx, 13); to_hex (dest_idx, 14); to_hex (dest_idx, 15);

			_ascii_form[_ascii_form.size () - 1] = '\0';
		}

		auto c_str () const noexcept -> const char*
		{
			return _ascii_form.data ();
		}

	private:
		std::array<char, BUF_SIZE> _ascii_form;
	};
}

#if defined(DEBUG)
[[gnu::always_inline, gnu::flatten]]
auto TypeMapper::find_index_by_name (const char *typeName, const TypeMapEntry *map, const char (&name_map)[], std::string_view const& from_name, std::string_view const& to_name) noexcept -> ssize_t
{
	log_debug (LOG_ASSEMBLY, "typemap: map {} -> {} uses strings", from_name, to_name);

	auto equal = [](TypeMapEntry const& entry, const char *key, const char (&name_map)[]) -> bool {
		if (entry.from == std::numeric_limits<uint32_t>::max ()) [[unlikely]] {
			return 1;
		}

		const char *type_name = &name_map[entry.from];
		return strcmp (type_name, key) == 0;
	};

	auto less_than = [](TypeMapEntry const& entry, const char *key, const char (&name_map)[]) -> bool {
		if (entry.from == std::numeric_limits<uint32_t>::max ()) [[unlikely]] {
			return 1;
		}

		const char *type_name = &name_map[entry.from];
		return strcmp (type_name, key) < 0;
	};

	return Search::binary_search<TypeMapEntry, const char*, const char[], equal, less_than> (name_map, typeName, map, type_map.entry_count);
}

[[gnu::always_inline, gnu::flatten]]
auto TypeMapper::find_index_by_hash (const char *typeName, const TypeMapEntry *map, const char (&name_map)[], std::string_view const& from_name, std::string_view const& to_name) noexcept -> ssize_t
{
	if (!typemap_use_hashes) [[unlikely]] {
		return find_index_by_name (typeName, map, name_map, from_name, to_name);
	}

	log_debug (LOG_ASSEMBLY, "typemap: map {} -> {} uses hashes", from_name, to_name);

	auto equal = [](TypeMapEntry const& entry, hash_t key) -> bool {
		if (entry.from == std::numeric_limits<uint32_t>::max ()) [[unlikely]] {
			return 1;
		}

		return entry.from_hash == key;
	};

	auto less_than = [](TypeMapEntry const& entry, hash_t key) -> bool {
		if (entry.from == std::numeric_limits<uint32_t>::max ()) [[unlikely]] {
			return 1;
		}

		return entry.from_hash < key;
	};
	hash_t type_name_hash = xxhash::hash (typeName, strlen (typeName));
	return Search::binary_search<TypeMapEntry, hash_t, equal, less_than> (type_name_hash, map, type_map.entry_count);
}

[[gnu::always_inline, gnu::flatten]]
auto TypeMapper::index_to_name (ssize_t idx, const char* typeName, const TypeMapEntry *map, const char (&name_map)[], std::string_view const& from_name, std::string_view const& to_name) -> const char*
{
	if (idx < 0) [[unlikely]] {
		log_debug (LOG_ASSEMBLY, "typemap: unable to map from {} type '{}' to {} type", from_name, typeName, to_name);
		return nullptr;
	}

	TypeMapEntry const& entry = map[idx];
	const char *mapped_name = &name_map[entry.to];

	log_debug (
		LOG_ASSEMBLY,
		"typemap: {} type '{}' maps to {} type '{}'",
		from_name,
		optional_string (typeName),
		to_name,
		optional_string (mapped_name)
	);
	return mapped_name;
}

[[gnu::always_inline, gnu::flatten]]
auto TypeMapper::managed_to_java_debug (const char *typeName, const uint8_t *mvid) noexcept -> const char*
{
	dynamic_local_path_string full_type_name;
	full_type_name.append (typeName);

	hash_t mvid_hash = xxhash::hash (mvid, 16z); // we must hope managed land called us with valid data

	auto equal = [](TypeMapAssembly const& entry, hash_t key) -> bool { return entry.mvid_hash == key; };
	auto less_than = [](TypeMapAssembly const& entry, hash_t key) -> bool { return entry.mvid_hash < key; };
	ssize_t idx = Search::binary_search<TypeMapAssembly, hash_t, equal, less_than> (mvid_hash, type_map_unique_assemblies, type_map.unique_assemblies_count);

	if (idx >= 0) [[likely]] {
		TypeMapAssembly const& assm = type_map_unique_assemblies[idx];
		full_type_name.append (", "sv);

		if (assm.name_offset < type_map.assembly_names_blob_size) [[likely]] {
			full_type_name.append (&type_map_assembly_names[assm.name_offset], assm.name_length);
			log_debug (LOG_ASSEMBLY, "typemap: fixed-up type name: '{}'", full_type_name.get ());
		} else {
			log_warn (LOG_ASSEMBLY, "typemap: fnvalid assembly name offset {}", assm.name_offset);
		}
	} else {
		log_warn (LOG_ASSEMBLY, "typemap: unable to look up assembly name for type '{}', trying without it.", typeName);
	}

	// If hashes are used for matching, the type names array is not used. If, however, string-based matching is in
	// effect, the managed type name is looked up and then...
	idx = find_index_by_hash (full_type_name.get (), type_map.managed_to_java, type_map_managed_type_names, MANAGED, JAVA);

	// ...either method gives us index into the Java type names array
	return index_to_name (idx, full_type_name.get (), type_map.managed_to_java, type_map_java_type_names, MANAGED, JAVA);
}
#endif // def DEBUG

#if defined(RELEASE)
[[gnu::always_inline]]
auto TypeMapper::compare_mvid (const uint8_t *mvid, TypeMapModule const& module) noexcept -> int
{
	return memcmp (module.module_uuid, mvid, sizeof(module.module_uuid));
}

[[gnu::always_inline]]
auto TypeMapper::find_module_entry (const uint8_t *mvid, const TypeMapModule *entries, size_t entry_count) noexcept -> const TypeMapModule*
{
	if (entries == nullptr || mvid == nullptr) [[unlikely]] {
		return nullptr;
	}

	auto equal = [](TypeMapModule const& entry, const uint8_t *key) -> bool { return compare_mvid (key, entry) == 0; };
	auto less_than = [](TypeMapModule const& entry, const uint8_t *key) -> bool { return compare_mvid (key, entry) < 0; };
	ssize_t idx = Search::binary_search<TypeMapModule, const uint8_t*, equal, less_than> (mvid, entries, entry_count);
	if (idx >= 0) [[likely]] {
		return &entries[idx];
	}

	return nullptr;
}

[[gnu::always_inline]]
auto TypeMapper::find_managed_to_java_map_entry (hash_t name_hash, const TypeMapModuleEntry *map, size_t entry_count) noexcept -> const TypeMapModuleEntry*
{
	if (map == nullptr) {
		return nullptr;
	};

	auto equal = [](TypeMapModuleEntry const& entry, hash_t key) -> bool { return entry.managed_type_name_hash == key; };
	auto less_than = [](TypeMapModuleEntry const& entry, hash_t key) -> bool { return entry.managed_type_name_hash < key; };
	ssize_t idx = Search::binary_search<TypeMapModuleEntry, equal, less_than> (name_hash, map, entry_count);
	if (idx >= 0) [[likely]] {
		return &map[idx];
	}

	return nullptr;
}

[[gnu::always_inline]]
auto TypeMapper::managed_to_java_release (const char *typeName, const uint8_t *mvid) noexcept -> const char*
{
	const TypeMapModule *match = find_module_entry (mvid, managed_to_java_map, managed_to_java_map_module_count);
	if (match == nullptr) {
		if (mvid == nullptr) {
			log_warn (LOG_ASSEMBLY, "typemap: no mvid specified in call to typemap_managed_to_java"sv);
		} else {
			log_info (LOG_ASSEMBLY, "typemap: module matching MVID [{}] not found."sv, MonoGuidString (mvid).c_str ());
		}
		return nullptr;
	}

	log_debug (LOG_ASSEMBLY, "typemap: found module matching MVID [{}]"sv, MonoGuidString (mvid).c_str ());
	hash_t name_hash = xxhash::hash (typeName, strlen (typeName));

	const TypeMapModuleEntry *entry = find_managed_to_java_map_entry (name_hash, match->map, match->entry_count);
	if (entry == nullptr) [[unlikely]] {
		if (match->map == nullptr) [[unlikely]] {
			log_warn (LOG_ASSEMBLY, "typemap: module with MVID [{}] has no associated type map.", MonoGuidString (mvid).c_str ());
			return nullptr;
		}

		if (match->duplicate_count > 0 && match->duplicate_map != nullptr) {
			log_debug (
				LOG_ASSEMBLY,
				"typemap: searching module [{}] duplicate map for type '{}' (hash {:x})",
				MonoGuidString (mvid).c_str (),
				optional_string (typeName),
				name_hash
			);
			entry = find_managed_to_java_map_entry (name_hash, match->duplicate_map, match->duplicate_count);
		}

		if (entry == nullptr) {
			log_warn (
				LOG_ASSEMBLY,
				"typemap: managed type '{}' (hash {:x}) not found in module [{}] ({}).",
				optional_string (typeName),
				name_hash,
				MonoGuidString (mvid).c_str (),
				std::string_view (&managed_assembly_names[match->assembly_name_index], match->assembly_name_length)
			);
			return nullptr;
		}
	}

	if (entry->java_map_index >= java_type_count) [[unlikely]] {
		log_warn (
			LOG_ASSEMBLY,
			"typemap: managed type '{}' (hash {:x}) in module [{}] ({}) has invalid Java type index {}",
			optional_string (typeName),
			name_hash,
			MonoGuidString (mvid).c_str (),
			std::string_view (&managed_assembly_names[match->assembly_name_index], match->assembly_name_length),
			entry->java_map_index
		);
		return nullptr;
	}

	TypeMapJava const& java_entry = java_to_managed_map[entry->java_map_index];
	if (java_entry.java_name_index >= java_type_names_size) [[unlikely]] {
		log_warn (
			LOG_ASSEMBLY,
			"typemap: managed type '{}' (hash {:x}) in module [{}] ({}) points to invalid Java type at index {} (invalid type name index {})",
			optional_string (typeName),
			name_hash,
			MonoGuidString (mvid).c_str (),
			std::string_view (&managed_assembly_names[match->assembly_name_index], match->assembly_name_length),
			entry->java_map_index,
			java_entry.java_name_index
		);

		return nullptr;
	}

	const char *ret = &java_type_names[java_entry.java_name_index];
	if (ret == nullptr) [[unlikely]] {
		log_warn (LOG_ASSEMBLY, "typemap: empty Java type name returned for entry at index {}", entry->java_map_index);
	}

	log_debug (
		LOG_ASSEMBLY,
		"typemap: managed type '{}' (hash {:x}) in module [{}] ({}) corresponds to Java type '{}'",
		optional_string (typeName),
		name_hash,
		MonoGuidString (mvid).c_str (),
		std::string_view (&managed_assembly_names[match->assembly_name_index], match->assembly_name_length),
		ret
	);

	return ret;
}
#endif // def RELEASE

[[gnu::flatten]]
auto TypeMapper::managed_to_java (const char *typeName, const uint8_t *mvid) noexcept -> const char*
{
	log_debug (LOG_ASSEMBLY, "managed_to_java: looking up type '{}'", optional_string (typeName));
	if (FastTiming::enabled ()) [[unlikely]] {
		internal_timing.start_event (TimingEventKind::ManagedToJava);
	}

	if (typeName == nullptr) [[unlikely]] {
		log_warn (LOG_ASSEMBLY, "typemap: type name not specified in typemap_managed_to_java");
		return nullptr;
	}

	auto do_map = [&typeName, &mvid]() -> const char* {
#if defined(RELEASE)
		return typemap_managed_to_java_release (typeName, mvid);
#else
		return managed_to_java_debug (typeName, mvid);
#endif
	};
	const char *ret = do_map ();

	if (FastTiming::enabled ()) [[unlikely]] {
		internal_timing.end_event ();
	}

	return ret;
}

#if defined(DEBUG)
[[gnu::flatten]]
auto TypeMapper::java_to_managed_debug (const char *java_type_name, char const** assembly_name, uint32_t *managed_type_token_id) noexcept -> bool
{
	if (assembly_name == nullptr || managed_type_token_id == nullptr) [[unlikely]] {
		log_warn (LOG_ASSEMBLY, "Managed land called java-to-managed mapping function with invalid pointers");
		return false;
	}

	// We need to find entry matching the Java type name, which will then...
	ssize_t idx = find_index_by_name (java_type_name, type_map.java_to_managed, type_map_java_type_names, JAVA, MANAGED);

	// ..provide us with the managed type name index
	const char *name = index_to_name (idx, java_type_name, type_map.java_to_managed, type_map_managed_type_names, JAVA, MANAGED);
	if (name == nullptr) {
		*assembly_name = nullptr;
		*managed_type_token_id = 0;
		return false;
	}

	TypeMapManagedTypeInfo const& type_info = type_map_managed_type_info[idx];
	*assembly_name = &type_map_assembly_names[type_info.assembly_name_index];
	*managed_type_token_id = type_info.managed_type_token_id;

	log_debug (
		LOG_ASSEMBLY,
		"Mapped Java type '{}' to managed type '{}' in assembly '{}' and with token '{:x}'",
		optional_string (java_type_name),
		name,
		*assembly_name,
		*managed_type_token_id
	);

	return true;
}
#else // def DEBUG

[[gnu::always_inline]]
auto TypeMapper::find_java_to_managed_entry (hash_t name_hash) noexcept -> const TypeMapJava*
{
	ssize_t idx = Search::binary_search (name_hash, java_to_managed_hashes, java_type_count);
	if (idx < 0) [[unlikely]] {
		return nullptr;
	}

	return &java_to_managed_map[idx];
}

[[gnu::flatten]]
auto TypeMapper::java_to_managed_release (const char *java_type_name, char const** assembly_name, uint32_t *managed_type_token_id) noexcept -> bool
{
	if (java_type_name == nullptr || assembly_name == nullptr || managed_type_token_id == nullptr) [[unlikely]] {
		if (java_type_name == nullptr) {
			log_warn (
				LOG_ASSEMBLY,
				"typemap: required parameter `{}` not passed to {}",
				"java_type_name"sv,
				__PRETTY_FUNCTION__
			);
		}

		if (assembly_name == nullptr) {
			log_warn (
				LOG_ASSEMBLY,
				"typemap: required parameter `{}` not passed to {}",
				"assembly_name"sv,
				__PRETTY_FUNCTION__
			);
		}

		if (managed_type_token_id == nullptr) {
			log_warn (
				LOG_ASSEMBLY,
				"typemap: required parameter `{}` not passed to {}",
				"managed_type_token_id"sv,
				__PRETTY_FUNCTION__
			);
		}

		return false;
	}

	hash_t name_hash = xxhash::hash (java_type_name, strlen (java_type_name));
	TypeMapJava const* java_entry = find_java_to_managed_entry (name_hash);
	if (java_entry == nullptr) {
		log_info (
			LOG_ASSEMBLY,
			"typemap: unable to find mapping to a managed type from Java type '{}' (hash {:x})",
			optional_string (java_type_name),
			name_hash
		);

		return false;
	}

	TypeMapModule const &module = managed_to_java_map[java_entry->module_index];
	*assembly_name = &managed_assembly_names[module.assembly_name_index];
	*managed_type_token_id = java_entry->managed_type_token_id;

	log_debug (
		LOG_ASSEMBLY,
		"Java type '{}' corresponds to managed type '{}' (token 0x{:x} in assembly '{}')",
		optional_string (java_type_name),
		std::string_view (&managed_type_names[java_entry->managed_type_name_index], java_entry->managed_type_name_length),
		*managed_type_token_id,
		std::string_view (&managed_assembly_names[module.assembly_name_index], module.assembly_name_length)
	);

	return true;
}
#endif // ndef DEBUG

[[gnu::flatten]]
auto TypeMapper::java_to_managed (const char *java_type_name, char const** assembly_name, uint32_t *managed_type_token_id) noexcept -> bool
{
	log_debug (LOG_ASSEMBLY, "java_to_managed: looking up type '{}'", optional_string (java_type_name));
	if (FastTiming::enabled ()) [[unlikely]] {
		internal_timing.start_event (TimingEventKind::JavaToManaged);
	}

	if (java_type_name == nullptr) [[unlikely]] {
		log_warn (LOG_ASSEMBLY, "typemap: type name not specified in typemap_java_to_managed");
		return false;
	}

	bool ret;
#if defined(RELEASE)
	ret = java_to_managed_release (java_type_name, assembly_name, managed_type_token_id);
#else
	ret = java_to_managed_debug (java_type_name, assembly_name, managed_type_token_id);
#endif

	if (FastTiming::enabled ()) [[unlikely]] {
		internal_timing.end_event ();
	}

	return ret;
}
#endif // ndef DEBUG

[[gnu::flatten]]
auto TypeMapper::typemap_java_to_managed (const char *java_type_name, char const** assembly_name, uint32_t *managed_type_token_id) noexcept -> bool
{
	log_debug (LOG_ASSEMBLY, "typemap_java_to_managed: looking up type '{}'", optional_string (java_type_name));
	if (FastTiming::enabled ()) [[unlikely]] {
		internal_timing.start_event (TimingEventKind::JavaToManaged);
	}

	if (java_type_name == nullptr) [[unlikely]] {
		log_warn (LOG_ASSEMBLY, "typemap: type name not specified in typemap_java_to_managed");
		return false;
	}

	bool ret;
#if defined(RELEASE)
	ret = typemap_java_to_managed_release (java_type_name, assembly_name, managed_type_token_id);
#else
	ret = typemap_java_to_managed_debug (java_type_name, assembly_name, managed_type_token_id);
#endif

	if (FastTiming::enabled ()) [[unlikely]] {
		internal_timing.end_event ();
	}

	return ret;
}
