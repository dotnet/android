#include <runtime-base/coreclr-bootstrap.hh>

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
	constexpr uint32_t HeaderSize = 84;

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

void CoreClrBootstrap::initialize () noexcept
{
	if (body != nullptr) {
		return;
	}
	BinaryBlobPayload bootstrap = BinaryBlobLoader::load ("coreclr_bootstrap");
	body = bootstrap.data;
	body_size = bootstrap.size;
	if (body_size < HeaderSize) {
		invalid ("body is shorter than the header");
	}
	uint16_t body_flags;
	std::memcpy (&body_flags, body + 6, sizeof (body_flags));
	uint32_t env_count = read (12);
	uint32_t sys_count = read (16);
	uint32_t prop_count = read (20);
	uint32_t dso_count = read (24);
	uint32_t shared_count = read (28);
	preload_count = read (32);
	preload_stride = read (36);
	env_offset = read (40);
	sys_offset = read (44);
	uint32_t prop_offset = read (48);
	uint32_t dso_offset = read (52);
	preload_offset = read (56);
	strings_start = read (60);
	if (prop_count < 3 || prop_count > std::numeric_limits<int>::max ()) {
		invalid ("runtime property count out of range");
	}
	config = {
		.ignore_split_configs = (body_flags & 1) != 0,
		.number_of_runtime_properties = prop_count,
		.package_naming_policy = read (68),
		.environment_variable_count = env_count,
		.system_property_count = sys_count,
		.number_of_assemblies_in_apk = read (72),
		.bundled_assembly_name_width = read (76),
		.number_of_dso_cache_entries = dso_count,
		.number_of_shared_libraries = shared_count,
		.android_package_name = string (read (80)),
		.have_assembly_store = (body_flags & 2) != 0,
	};

	dso_cache = static_cast<DSOCacheEntry*> (std::malloc (static_cast<size_t> (dso_count) * sizeof (DSOCacheEntry)));
	if (dso_count != 0 && dso_cache == nullptr) {
		invalid ("out of memory allocating DSO cache");
	}
	for (uint32_t i = 0; i < dso_count; i++) {
		uint64_t offset = static_cast<uint64_t> (dso_offset) + static_cast<uint64_t> (i) * 12;
		uint32_t hash = read (offset);
		uint32_t flags = read (offset + 4);
		uint32_t name = read (offset + 8);
		new (&dso_cache [i]) DSOCacheEntry { hash, (flags & 0xff) != 0, (flags & 0xff00) != 0, name, nullptr };
	}
	property_names = static_cast<const char**> (std::calloc (prop_count, sizeof (const char*)));
	property_values = static_cast<char**> (std::calloc (prop_count, sizeof (char*)));
	if (property_names == nullptr || property_values == nullptr) {
		invalid ("out of memory allocating runtime properties");
	}
	for (uint32_t i = 0; i < prop_count; i++) {
		uint64_t offset = static_cast<uint64_t> (prop_offset) + static_cast<uint64_t> (i) * 8;
		property_names [i] = string (read (offset));
		uint32_t value = read (offset + 4);
		if (i >= 3) {
			property_values [i] = const_cast<char*> (string (value, false));
		}
	}
}
