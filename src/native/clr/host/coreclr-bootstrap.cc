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
	constexpr uint32_t ConfigMagic = 0x47464358; // XCFG
	constexpr uint32_t HeaderSize = 84;

	[[noreturn]] void invalid (const char *reason) noexcept
	{
		Helpers::abort_applicationf (LOG_DEFAULT, std::source_location::current (), "Invalid CoreCLR bootstrap blob: %s", reason);
	}
}

auto CoreClrBootstrap::read (uint32_t offset) noexcept -> uint32_t
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
		return reinterpret_cast<const char*> (body + strings_start);
	}
	if (offset < strings_start || offset >= strings_end) {
		invalid ("string offset outside the string section");
	}
	const char *value = reinterpret_cast<const char*> (body + offset);
	if (std::memchr (value, 0, strings_end - offset) == nullptr || (required && *value == 0)) {
		invalid ("missing or unterminated string");
	}
	return value;
}

auto CoreClrBootstrap::pair (bool system, uint32_t index) noexcept -> AppEnvironmentVariable
{
	uint32_t count = system ? config.system_property_count : config.environment_variable_count;
	if (index >= count) {
		invalid ("property index out of range");
	}
	uint32_t offset = (system ? sys_offset : env_offset) + index * 8;
	return { read (offset), read (offset + 4) };
}

auto CoreClrBootstrap::preload_index (uint32_t index) noexcept -> uint32_t
{
	if (index >= preload_count) {
		invalid ("preload index out of range");
	}
	uint32_t entry = read (preload_offset + index * 4);
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
	if (read (0) != ConfigMagic) {
		invalid ("invalid body magic");
	}
	uint16_t version, body_flags;
	std::memcpy (&version, body + 4, sizeof (version));
	std::memcpy (&body_flags, body + 6, sizeof (body_flags));
	if (version != 1 || (body_flags & ~uint16_t { 3 }) != 0 || read (8) != body_size) {
		invalid ("invalid body header");
	}
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
	uint32_t strings_length = read (64);

	uint64_t next = HeaderSize;
	auto check_section = [&next] (uint32_t offset, uint32_t count, uint32_t stride) {
		if (offset != next) {
			invalid ("noncontiguous table offset");
		}
		next += static_cast<uint64_t> (count) * stride;
		if (next > body_size) {
			invalid ("table extends beyond the body");
		}
	};
	check_section (env_offset, env_count, 8);
	check_section (sys_offset, sys_count, 8);
	check_section (prop_offset, prop_count, 8);
	check_section (dso_offset, dso_count, 12);
	check_section (preload_offset, preload_count, 4);
	if (strings_start != next || strings_length == 0 || strings_length > body_size - strings_start ||
		strings_length != body_size - strings_start || body [strings_start] != 0 ||
		prop_count < 3 || prop_count > std::numeric_limits<int>::max () ||
		preload_stride == 0 || preload_count % preload_stride != 0) {
		invalid ("invalid string section, runtime properties, or preload layout");
	}
	strings_end = strings_start + strings_length;
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
	uint32_t previous_hash = 0;
	for (uint32_t i = 0; i < dso_count; i++) {
		uint32_t offset = dso_offset + i * 12;
		uint32_t hash = read (offset);
		if ((i != 0 && hash < previous_hash) || body [offset + 4] > 1 || body [offset + 5] > 1 ||
			body [offset + 6] != 0 || body [offset + 7] != 0) {
			invalid ("invalid or unsorted DSO cache entry");
		}
		uint32_t name = read (offset + 8);
		if (name <= strings_start || name >= strings_end) {
			invalid ("DSO name offset outside the string section");
		}
		new (&dso_cache [i]) DSOCacheEntry { hash, body [offset + 4] != 0, body [offset + 5] != 0, name, nullptr };
		previous_hash = hash;
	}
	for (uint32_t i = 0; i < preload_count; i++) {
		preload_index (i);
	}
	property_names = static_cast<const char**> (std::calloc (prop_count, sizeof (const char*)));
	property_values = static_cast<char**> (std::calloc (prop_count, sizeof (char*)));
	if (property_names == nullptr || property_values == nullptr) {
		invalid ("out of memory allocating runtime properties");
	}
	constexpr const char *required_names[] = { "HOST_RUNTIME_CONTRACT", "RUNTIME_IDENTIFIER", "APP_CONTEXT_BASE_DIRECTORY" };
	for (uint32_t i = 0; i < prop_count; i++) {
		uint32_t offset = prop_offset + i * 8;
		property_names [i] = string (read (offset));
		uint32_t value = read (offset + 4);
		if (i < 3) {
			if (std::strcmp (property_names [i], required_names [i]) != 0 || value != 0) {
				invalid ("invalid runtime property sentinel");
			}
		} else {
			property_values [i] = const_cast<char*> (string (value, false));
		}
	}
}
