#include <array>

#include <host/typemap.hh>
#include <runtime-base/timing-internal.hh>
#include <runtime-base/search.hh>
#include <shared/xxhash.hh>
#include <xamarin-app.hh>

using namespace xamarin::android;

namespace {
	class MonoGuidString
	{
		static inline constexpr size_t MVID_SIZE = 16;
		static inline constexpr size_t NUM_HYPHENS = 4;
		static inline constexpr size_t BUF_SIZE = (MVID_SIZE * 2) + NUM_HYPHENS + 1;
		static inline std::array<char, 16> hex_map {
			'0', '1', '2', '3', '4', '5', '6', '7',
			'8', '9', 'a', 'b', 'c', 'd', 'e', 'f',
		};

	public:
		explicit MonoGuidString (const uint8_t *mvid) noexcept
		{
			if (mvid == nullptr) {
				_ascii_form[0] = '\0';
				return;
			}

			// In the caller we trust, we have no way to validate the size here
			auto to_hex = [this, &mvid] (size_t &dest_idx, size_t src_idx) {
				_ascii_form[dest_idx++] = hex_map[(mvid[src_idx] & 0xf0) >> 4];
				_ascii_form[dest_idx++] = hex_map[mvid[src_idx] & 0x0f];
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
[[gnu::always_inline]]
auto TypeMapper::typemap_managed_to_java_debug (const char *typeName, const uint8_t *mvid) noexcept -> const char*
{
	Helpers::abort_application ("TypeMap support for Debug builds not implemented yet"sv);
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
auto TypeMapper::typemap_managed_to_java_release (const char *typeName, const uint8_t *mvid) noexcept -> const char*
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
				optional_string (managed_assembly_names[match->assembly_name_index])
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
			optional_string (managed_assembly_names[match->assembly_name_index]),
			entry->java_map_index
		);
		return nullptr;
	}

	TypeMapJava const& java_entry = java_to_managed_map[entry->java_map_index];
	if (java_entry.java_name_index >= java_type_count) [[unlikely]] {
		log_warn (
			LOG_ASSEMBLY,
			"typemap: managed type '{}' (hash {:x}) in module [{}] ({}) points to invalid Java type at index {} (invalid type name index {})",
			optional_string (typeName),
			name_hash,
			MonoGuidString (mvid).c_str (),
			optional_string (managed_assembly_names[match->assembly_name_index]),
			entry->java_map_index,
			java_entry.java_name_index
		);

		return nullptr;
	}

	const char *ret = java_type_names[java_entry.java_name_index];
	if (ret == nullptr) [[unlikely]] {
		log_warn (LOG_ASSEMBLY, "typemap: empty Java type name returned for entry at index {}", entry->java_map_index);
	}

	log_debug (
		LOG_ASSEMBLY,
		"typemap: managed type '{}' (hash {:x}) in module [{}] ({}) corresponds to Java type '{}'",
		optional_string (typeName),
		name_hash,
		MonoGuidString (mvid).c_str (),
		optional_string (managed_assembly_names[match->assembly_name_index]),
		ret
	);

	return ret;
}
#endif // def RELEASE

[[gnu::flatten]]
auto TypeMapper::typemap_managed_to_java (const char *typeName, const uint8_t *mvid) noexcept -> const char*
{
	size_t total_time_index;
	if (FastTiming::enabled ()) [[unlikely]] {
		//timing = new Timing ();
		total_time_index = internal_timing.start_event (TimingEventKind::ManagedToJava);
	}

	if (typeName == nullptr) [[unlikely]] {
		log_warn (LOG_ASSEMBLY, "typemap: type name not specified in typemap_managed_to_java");
		return nullptr;
	}

	const char *ret = nullptr;
#if defined(RELEASE)
	ret = typemap_managed_to_java_release (typeName, mvid);
#else
	ret = typemap_managed_to_java_debug (typeName, mvid);
#endif

	if (FastTiming::enabled ()) [[unlikely]] {
		internal_timing.end_event (total_time_index);
	}

	return ret;
}

#if defined(DEBUG)
[[gnu::flatten]]
auto TypeMapper::typemap_java_to_managed (const char *typeName) noexcept -> const char*
{
	Helpers::abort_application ("typemap_java_to_managed not implemented for debug builds yet");
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
auto TypeMapper::typemap_java_to_managed (const char *typeName) noexcept -> const char*
{
	log_warn (LOG_ASSEMBLY, "{} WIP"sv, __PRETTY_FUNCTION__);
	log_warn (LOG_ASSEMBLY, "  asking for '{}'"sv, optional_string (typeName));

	if (typeName == nullptr) [[unlikely]] {
		return nullptr;
	}

	hash_t name_hash = xxhash::hash (typeName, strlen (typeName));
	TypeMapJava const* java_entry = find_java_to_managed_entry (name_hash);
	if (java_entry == nullptr) {
		log_info (
			LOG_ASSEMBLY,
			"typemap: unable to find mapping to a managed type from Java type '{}' (hash {:x})",
			optional_string (typeName),
			name_hash
		);

		return nullptr;
	}

	log_debug (
		LOG_ASSEMBLY,
		"Java type '{}' corresponds to managed type '{}' ({:p}",
		optional_string (typeName),
		optional_string (managed_type_names[java_entry->managed_type_name_index]),
		reinterpret_cast<const void*>(managed_type_names[java_entry->managed_type_name_index])
	);
	return managed_type_names[java_entry->managed_type_name_index];
}
#endif // ndef DEBUG
