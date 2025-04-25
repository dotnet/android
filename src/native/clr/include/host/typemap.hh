#pragma once

#include <cstdint>
#include <string_view>

#include "../runtime-base/logger.hh"
#include <shared/xxhash.hh>
#include "../xamarin-app.hh"

namespace xamarin::android {
	class TypeMapper
	{
		static constexpr std::string_view MANAGED { "Managed" };
		static constexpr std::string_view JAVA { "Java" };

	public:
		static auto typemap_managed_to_java (const char *typeName, const uint8_t *mvid) noexcept -> const char*;
		static auto typemap_java_to_managed (const char *java_type_name, char const** assembly_name, uint32_t *managed_type_token_id) noexcept -> bool;

	private:
#if defined(RELEASE)
		static auto compare_mvid (const uint8_t *mvid, TypeMapModule const& module) noexcept -> int;
		static auto find_module_entry (const uint8_t *mvid, const TypeMapModule *entries, size_t entry_count) noexcept -> const TypeMapModule*;
		static auto find_managed_to_java_map_entry (hash_t name_hash, const TypeMapModuleEntry *map, size_t entry_count) noexcept -> const TypeMapModuleEntry*;
		static auto typemap_managed_to_java_release (const char *typeName, const uint8_t *mvid) noexcept -> const char*;
		static auto typemap_java_to_managed_release (const char *java_type_name, char const** assembly_name, uint32_t *managed_type_token_id) noexcept -> bool;

		static auto find_java_to_managed_entry (hash_t name_hash) noexcept -> const TypeMapJava*;
#else
		static auto typemap_type_to_type_debug (const char *typeName, const TypeMapEntry *map, const char (&name_map)[], std::string_view const& from_name, std::string_view const& to_name) noexcept -> const char*;
		static auto typemap_managed_to_java_debug (const char *typeName, const uint8_t *mvid) noexcept -> const char*;
		static auto typemap_java_to_managed_debug (const char *java_type_name, char const** assembly_name, uint32_t *managed_type_token_id) noexcept -> bool;
#endif
	};
}
