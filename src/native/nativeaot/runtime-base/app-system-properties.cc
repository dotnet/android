#include <bit>
#include <cstdint>
#include <cstring>

#include <runtime-base/app-system-properties.hh>
#include <shared/helpers.hh>

using namespace xamarin::android;

void AppSystemProperties::initialize (BinaryBlobPayload payload) noexcept
{
	if (body != nullptr) {
		return;
	}
	struct Header {
		uint32_t magic;
		uint16_t version;
		uint16_t flags;
		uint32_t size;
		uint32_t environment_count;
		uint32_t property_count;
		uint32_t properties_offset;
		uint32_t strings_offset;
		uint32_t strings_size;
	};
	static_assert (std::endian::native == std::endian::little);
	static_assert (sizeof (Header) == 32);
	if (payload.data == nullptr || payload.size < sizeof (Header)) {
		Helpers::abort_application ("Missing NativeAOT bootstrap data");
	}
	if (reinterpret_cast<uintptr_t>(payload.data) % alignof (OffsetPair) != 0) {
		Helpers::abort_application ("Misaligned NativeAOT bootstrap data");
	}
	Header header;
	std::memcpy (&header, payload.data, sizeof (header));
	uint64_t properties_offset = sizeof (Header) + static_cast<uint64_t>(header.environment_count) * sizeof (OffsetPair);
	uint64_t strings_offset = properties_offset + static_cast<uint64_t>(header.property_count) * sizeof (OffsetPair);
	if (header.magic != 0x4746434e || header.version != 1 || header.flags != 0 ||
		header.size != payload.size || header.properties_offset != properties_offset ||
		header.strings_offset != strings_offset || header.strings_size == 0 ||
		strings_offset + header.strings_size != payload.size || payload.data[strings_offset] != 0) {
		Helpers::abort_application ("Invalid NativeAOT bootstrap header");
	}
	auto validate_string = [&] (uint32_t offset, bool name) noexcept {
		if (offset < strings_offset || offset >= payload.size ||
			(offset != strings_offset && payload.data[offset - 1] != 0) ||
			(name && payload.data[offset] == 0) ||
			std::memchr (payload.data + offset, 0, payload.size - offset) == nullptr) {
			Helpers::abort_application ("Invalid NativeAOT bootstrap string");
		}
	};
	// The linked NCFG table uses the POD layout above; native builds disable strict aliasing.
	auto all_pairs = std::span<const OffsetPair> (
		reinterpret_cast<const OffsetPair*>(payload.data + sizeof (Header)),
		static_cast<size_t>(header.environment_count) + header.property_count);
	for (const auto &pair : all_pairs) {
		validate_string (pair.name, true);
		validate_string (pair.value, false);
	}
	body = payload.data;
	environment_pairs = all_pairs.first (header.environment_count);
	property_pairs = all_pairs.subspan (header.environment_count);
}

auto AppSystemProperties::lookup (const char *name, size_t &value_length) noexcept -> const char*
{
	if (body == nullptr || name == nullptr) {
		Helpers::abort_application ("NativeAOT system properties must be initialized before lookup");
	}
	value_length = 0;
	for (Entry const& property : entries ()) {
		if (std::strcmp (name, property.name) == 0) {
			value_length = std::strlen (property.value);
			return property.value;
		}
	}
	return nullptr;
}
