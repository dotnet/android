#pragma once

#include <cstdint>

#include "../runtime-base/logger.hh"
#include <shared/xxhash.hh>
#include "../xamarin-app.hh"

namespace xamarin::android {
	class TypeMapper
	{
	public:
		static auto typemap_managed_to_java (const char *typeName, const uint8_t *mvid) noexcept -> const char*;
		static auto typemap_java_to_managed (const char *java_type_name, char const** assembly_name, uint32_t *managed_type_doken_id) noexcept -> bool;

	private:
#if defined(RELEASE)
		static auto compare_mvid (const uint8_t *mvid, TypeMapModule const& module) noexcept -> int;
		static auto find_module_entry (const uint8_t *mvid, const TypeMapModule *entries, size_t entry_count) noexcept -> const TypeMapModule*;
		static auto find_managed_to_java_map_entry (hash_t name_hash, const TypeMapModuleEntry *map, size_t entry_count) noexcept -> const TypeMapModuleEntry*;
		static auto typemap_managed_to_java_release (const char *typeName, const uint8_t *mvid) noexcept -> const char*;

		static auto find_java_to_managed_entry (hash_t name_hash) noexcept -> const TypeMapJava*;
#else
		static auto typemap_managed_to_java_debug (const char *typeName, const uint8_t *mvid) noexcept -> const char*;
#endif
	};
}
