#pragma once

#include <cstddef>
#include <cstdint>
#include <span>
#include <type_traits>

#include <runtime-base/binary-blob-loader.hh>

namespace xamarin::android {
	auto load_nativeaot_remapping () noexcept -> BinaryBlobPayload;

	class AppSystemProperties
	{
	public:
		struct Entry {
			const char *name;
			const char *value;
		};

		struct OffsetPair {
			uint32_t name;
			uint32_t value;
		};
		static_assert (std::is_standard_layout_v<OffsetPair> && std::is_trivial_v<OffsetPair>);
		static_assert (sizeof (OffsetPair) == 8 && alignof (OffsetPair) == 4);
		static_assert (offsetof (OffsetPair, name) == 0 && offsetof (OffsetPair, value) == 4);

		class Entries
		{
		public:
			struct Iterator {
				const uint8_t *data;
				const OffsetPair *pair;

				auto operator* () const noexcept -> Entry
				{
					return { reinterpret_cast<const char*>(data + pair->name), reinterpret_cast<const char*>(data + pair->value) };
				}
				auto operator++ () noexcept -> Iterator&
				{
					++pair;
					return *this;
				}
				auto operator!= (const Iterator &other) const noexcept -> bool { return pair != other.pair; }
			};

			const uint8_t *data;
			std::span<const OffsetPair> pairs;

			auto begin () const noexcept -> Iterator { return { data, pairs.data () }; }
			auto end () const noexcept -> Iterator { return { data, pairs.data () + pairs.size () }; }
		};

		static void initialize (BinaryBlobPayload payload) noexcept;
		static auto lookup (const char *name, size_t &value_length) noexcept -> const char*;
		static auto entries () noexcept -> Entries { return { body, property_pairs }; }
		static auto environment () noexcept -> Entries { return { body, environment_pairs }; }

	private:
		// DT_INIT precedes C++ dynamic initialization. All retained state is constant-initialized.
		static inline constinit const uint8_t *body = nullptr;
		static inline constinit std::span<const OffsetPair> environment_pairs = {};
		static inline constinit std::span<const OffsetPair> property_pairs = {};
	};
}
