#include <runtime-base/coreclr-bootstrap.hh>

#include <cstddef>
#include <cstdlib>
#include <cstring>
#include <limits>
#include <new>
#include <source_location>

#include <runtime-base/binary-blob-loader.hh>
#include <shared/helpers.hh>
#include <shared/log_functions.hh>

using namespace xamarin::android;

namespace
{
	struct BootstrapHeader {
		uint32_t magic;
		uint16_t version;
		uint16_t flags;
		uint32_t size;
		uint32_t env_count;
		uint32_t sys_count;
		uint32_t prop_count;
		uint32_t dso_count;
		uint32_t shared_count;
		uint32_t preload_count;
		uint32_t preload_stride;
		uint32_t env_offset;
		uint32_t sys_offset;
		uint32_t prop_offset;
		uint32_t dso_offset;
		uint32_t preload_offset;
		uint32_t strings_offset;
		uint32_t strings_length;
		uint32_t package_naming_policy;
		uint32_t assembly_count;
		uint32_t bundled_name_width;
		uint32_t package_offset;
	};
	static_assert (sizeof (BootstrapHeader) == 84);
	static_assert (offsetof (BootstrapHeader, flags) == 6);
	static_assert (offsetof (BootstrapHeader, package_offset) == 80);

	[[noreturn]] void invalid (const char *reason) noexcept
	{
		Helpers::abort_applicationf (LOG_DEFAULT, std::source_location::current (), "Invalid CoreCLR bootstrap blob: %s", reason);
	}
}

auto CoreClrBootstrap::read (uint64_t offset) noexcept -> uint32_t
{
	if (offset > body_size || body_size - offset < sizeof (uint32_t)) {
		invalid ("read exceeds the body");
	}
	uint32_t value;
	std::memcpy (&value, body + offset, sizeof (value));
	return value;
}

auto CoreClrBootstrap::string (uint32_t offset, bool required) noexcept -> const char*
{
	if (offset == 0 && !required) {
		// The producer reserves the first byte of the string pool for empty values.
		offset = strings_start;
	}
	if (offset < strings_start || offset >= body_size) {
		invalid ("string offset outside the string section");
	}
	return reinterpret_cast<const char*> (body + offset);
}

auto CoreClrBootstrap::pair (bool system, uint32_t index) noexcept -> AppEnvironmentVariable
{
	uint32_t count = system ? config.system_property_count : config.environment_variable_count;
	if (index >= count) {
		invalid ("property index out of range");
	}
	uint64_t offset = static_cast<uint64_t> (system ? sys_offset : env_offset) + static_cast<uint64_t> (index) * 8;
	return { read (offset), read (offset + 4) };
}

auto CoreClrBootstrap::preload_index (uint32_t index) noexcept -> uint32_t
{
	if (index >= preload_count) {
		invalid ("preload index out of range");
	}
	uint32_t entry = read (static_cast<uint64_t> (preload_offset) + static_cast<uint64_t> (index) * 4);
	if (entry >= config.number_of_dso_cache_entries) {
		invalid ("preload refers to a missing DSO");
	}
	return entry;
}

auto CoreClrBootstrap::lookup_system_property (const char *name, size_t &value_len) noexcept -> const char*
{
	value_len = 0;
	for (uint32_t i = 0; i < config.system_property_count; i++) {
		AppEnvironmentVariable entry = pair (true, i);
		if (std::strcmp (name, string (entry.name_index)) == 0) {
			const char *value = string (entry.value_index, false);
			value_len = std::strlen (value);
			return value;
		}
	}
	return nullptr;
}

void CoreClrBootstrap::initialize () noexcept
{
	if (body != nullptr) {
		return;
	}
	BinaryBlobPayload bootstrap = BinaryBlobLoader::load ("coreclr_bootstrap");
	body = bootstrap.data;
	body_size = bootstrap.size;
	if (body_size < sizeof (BootstrapHeader)) {
		invalid ("body is shorter than the header");
	}
	BootstrapHeader header;
	std::memcpy (&header, body, sizeof (header));
	preload_count = header.preload_count;
	preload_stride = header.preload_stride;
	env_offset = header.env_offset;
	sys_offset = header.sys_offset;
	preload_offset = header.preload_offset;
	strings_start = header.strings_offset;
	if (header.prop_count < 3 || header.prop_count > std::numeric_limits<int>::max ()) {
		invalid ("runtime property count out of range");
	}
	config = {
		.ignore_split_configs = (header.flags & 1) != 0,
		.number_of_runtime_properties = header.prop_count,
		.package_naming_policy = header.package_naming_policy,
		.environment_variable_count = header.env_count,
		.system_property_count = header.sys_count,
		.number_of_assemblies_in_apk = header.assembly_count,
		.bundled_assembly_name_width = header.bundled_name_width,
		.number_of_dso_cache_entries = header.dso_count,
		.number_of_shared_libraries = header.shared_count,
		.android_package_name = string (header.package_offset),
		.have_assembly_store = (header.flags & 2) != 0,
	};

	dso_cache = static_cast<DSOCacheEntry*> (std::malloc (static_cast<size_t> (header.dso_count) * sizeof (DSOCacheEntry)));
	if (header.dso_count != 0 && dso_cache == nullptr) {
		invalid ("out of memory allocating DSO cache");
	}
	for (uint32_t i = 0; i < header.dso_count; i++) {
		uint64_t offset = static_cast<uint64_t> (header.dso_offset) + static_cast<uint64_t> (i) * 12;
		uint32_t hash = read (offset);
		uint32_t flags = read (offset + 4);
		uint32_t name = read (offset + 8);
		new (&dso_cache [i]) DSOCacheEntry { hash, (flags & 0xff) != 0, (flags & 0xff00) != 0, name, nullptr };
	}
	property_names = static_cast<const char**> (std::calloc (header.prop_count, sizeof (const char*)));
	property_values = static_cast<char**> (std::calloc (header.prop_count, sizeof (char*)));
	if (property_names == nullptr || property_values == nullptr) {
		invalid ("out of memory allocating runtime properties");
	}
	for (uint32_t i = 0; i < header.prop_count; i++) {
		uint64_t offset = static_cast<uint64_t> (header.prop_offset) + static_cast<uint64_t> (i) * 8;
		property_names [i] = string (read (offset));
		uint32_t value = read (offset + 4);
		if (i >= 3) {
			property_values [i] = const_cast<char*> (string (value, false));
		}
	}
}
