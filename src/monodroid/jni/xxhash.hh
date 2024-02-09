#if !defined (__XXHASH_HH)
#define __XXHASH_HH

//
// Based on original code at https://github.com/ekpyron/xxhashct
//
// Original code license:
//

/**
* MIT License
*
* Copyright (c) 2021 Zachary Arnaise
*
* Permission is hereby granted, free of charge, to any person obtaining a copy
* of this software and associated documentation files (the "Software"), to deal
* in the Software without restriction, including without limitation the rights
* to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
* copies of the Software, and to permit persons to whom the Software is
* furnished to do so, subject to the following conditions:
*
* The above copyright notice and this permission notice shall be included in
* all copies or substantial portions of the Software.
*
* THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
* IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
* FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
* AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
* LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
* OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
* THE SOFTWARE.
*/

#include <cstdint>
#include <string_view>

#include "platform-compat.hh"

namespace xamarin::android
{
	class xxhash32 final
	{
		static constexpr uint32_t PRIME1 = 0x9E3779B1U;
		static constexpr uint32_t PRIME2 = 0x85EBCA77U;
		static constexpr uint32_t PRIME3 = 0xC2B2AE3DU;
		static constexpr uint32_t PRIME4 = 0x27D4EB2FU;
		static constexpr uint32_t PRIME5 = 0x165667B1U;

	public:
		// We don't use any special seed in XA, the template parameter is just to keep the algorithm more easily
		// understood and to run compile-time algorithm correctness tests
		template<uint32_t Seed = 0>
		force_inline static constexpr uint32_t hash (const char *input, size_t len) noexcept
		{
			return finalize (
				(len >= 16 ? h16bytes<Seed> (input, len) : Seed + PRIME5) + static_cast<uint32_t>(len),
				(input) + (len & ~0xFU),
				len & 0xF
			);
		}

		template<size_t Size, uint32_t Seed = 0>
		force_inline static constexpr uint32_t hash (const char (&input)[Size]) noexcept
		{
			return hash<Seed> (input, Size - 1);
		}

		template<uint32_t Seed = 0>
		force_inline static constexpr uint32_t hash (std::string_view const& input) noexcept
		{
			return hash<Seed> (input.data (), input.length ());
		}

	private:
		// 32-bit rotate left.
		template<int Bits>
		force_inline static constexpr uint32_t rotl (uint32_t x) noexcept
		{
			return ((x << Bits) | (x >> (32 - Bits)));
		}

		// Normal stripe processing routine.
		force_inline static constexpr uint32_t round (uint32_t acc, const uint32_t input) noexcept
		{
			return rotl<13> (acc + (input * PRIME2)) * PRIME1;
		}

		template<int RShift, uint32_t Prime>
		force_inline static constexpr uint32_t avalanche_step (const uint32_t h) noexcept
		{
			return (h ^ (h >> RShift)) * Prime;
		}

		// Mixes all bits to finalize the hash.
		force_inline static constexpr uint32_t avalanche (const uint32_t h) noexcept
		{
			return
				avalanche_step<16, 1> (
					avalanche_step<13, PRIME3> (
						avalanche_step <15, PRIME2> (h)
					)
				);
		}

		// little-endian version: all our target platforms are little-endian
		force_inline static constexpr uint32_t endian32 (const char *v) noexcept
		{
			return
				static_cast<uint32_t>(static_cast<uint8_t>(v[0])) |
				(static_cast<uint32_t>(static_cast<uint8_t>(v[1])) << 8) |
				(static_cast<uint32_t>(static_cast<uint8_t>(v[2])) << 16) |
				(static_cast<uint32_t>(static_cast<uint8_t>(v[3])) << 24);
		}

		force_inline static constexpr uint32_t fetch32 (const char *p, const uint32_t v) noexcept
		{
			return round (v, endian32 (p));
		}

		// Processes the last 0-15 bytes of p.
		force_inline static constexpr uint32_t finalize (const uint32_t h, const char *p, size_t len) noexcept
		{
			return
				(len >= 4) ? finalize (rotl<17> (h + (endian32 (p) * PRIME3)) * PRIME4, p + 4, len - 4) :
				(len > 0)  ? finalize (rotl<11> (h + (static_cast<uint8_t>(*p) * PRIME5)) * PRIME1, p + 1, len - 1) :
				avalanche (h);
		}

		force_inline static constexpr uint32_t h16bytes (const char *p, size_t len, const uint32_t v1, const uint32_t v2, const uint32_t v3, const uint32_t v4) noexcept
		{
			return
				(len >= 16) ? h16bytes (p + 16, len - 16, fetch32 (p, v1), fetch32 (p+4, v2), fetch32 (p+8, v3), fetch32 (p+12, v4)) :
				rotl<1> (v1) + rotl<7> (v2) + rotl<12> (v3) + rotl<18> (v4);
		}

		// We don't use any special seed in XA, the template parameter is just to keep the algorithm more easily
		// understood
		template<uint32_t Seed = 0>
		force_inline static constexpr uint32_t h16bytes (const char *p, size_t len)
		{
			return h16bytes(p, len, Seed + PRIME1 + PRIME2, Seed + PRIME2, Seed, Seed - PRIME1);
		}
	};

	class xxhash64 final
	{
		static constexpr uint64_t PRIME1 = 11400714785074694791ULL;
		static constexpr uint64_t PRIME2 = 14029467366897019727ULL;
		static constexpr uint64_t PRIME3 =  1609587929392839161ULL;
		static constexpr uint64_t PRIME4 =  9650029242287828579ULL;
		static constexpr uint64_t PRIME5 =  2870177450012600261ULL;

	public:
		// We don't use any special seed in XA, the template parameter is just to keep the algorithm more easily
		// understood and to run compile-time algorithm correctness tests
		template<uint64_t Seed = 0>
		force_inline static constexpr uint64_t hash (const char *p, size_t len) noexcept
		{
			return finalize ((len >= 32 ? h32bytes<Seed> (p, len) : Seed + PRIME5) + len, p + (len & ~0x1FU), len & 0x1F);
		}

		template<size_t Size, uint64_t Seed = 0>
		force_inline static constexpr uint64_t hash (const char (&input)[Size]) noexcept
		{
			return hash<Seed> (input, Size - 1);
		}

		template<uint64_t Seed = 0>
		force_inline static constexpr uint64_t hash (std::string_view const& input) noexcept
		{
			return hash<Seed> (input.data (), input.length ());
		}

	private:
		template<int Bits>
		force_inline static constexpr uint64_t rotl (uint64_t x) noexcept
		{
			return ((x << Bits) | (x >> (64 - Bits)));
		}

		template<int RShift>
		force_inline static constexpr uint64_t mix1 (const uint64_t h, const uint64_t prime) noexcept
		{
			return (h ^ (h >> RShift)) * prime;
		}

		force_inline static constexpr uint64_t mix2 (const uint64_t p, const uint64_t v = 0) noexcept
		{
			return rotl<31> (v + p * PRIME2) * PRIME1;
		}

		force_inline static constexpr uint64_t mix3 (const uint64_t h, const uint64_t v) noexcept
		{
			return (h ^ mix2 (v)) * PRIME1 + PRIME4;
		}

		// little-endian versions: all our target platforms are little-endian
		force_inline static constexpr uint32_t endian32 (const char *v) noexcept
		{
			return
				static_cast<uint32_t>(static_cast<uint8_t>(v[0])) |
				(static_cast<uint32_t>(static_cast<uint8_t>(v[1])) << 8) |
				(static_cast<uint32_t>(static_cast<uint8_t>(v[2])) << 16) |
				(static_cast<uint32_t>(static_cast<uint8_t>(v[3])) << 24);
		}

		force_inline static constexpr uint64_t endian64 (const char *v)
		{
			return
				static_cast<uint64_t>(static_cast<uint8_t>(v[0])) |
				(static_cast<uint64_t>(static_cast<uint8_t>(v[1])) << 8) |
				(static_cast<uint64_t>(static_cast<uint8_t>(v[2])) << 16) |
				(static_cast<uint64_t>(static_cast<uint8_t>(v[3])) << 24) |
				(static_cast<uint64_t>(static_cast<uint8_t>(v[4])) << 32) |
				(static_cast<uint64_t>(static_cast<uint8_t>(v[5])) << 40) |
				(static_cast<uint64_t>(static_cast<uint8_t>(v[6])) << 48) |
				(static_cast<uint64_t>(static_cast<uint8_t>(v[7])) << 56);
		}

		force_inline static constexpr uint64_t fetch64 (const char *p, const uint64_t v = 0) noexcept
		{
			return mix2 (endian64 (p), v);
		}

		force_inline static constexpr uint64_t fetch32 (const char *p) noexcept
		{
			return static_cast<uint64_t>(endian32 (p)) * PRIME1;
		}

		force_inline static constexpr uint64_t fetch8 (const char *p) noexcept
		{
			return static_cast<uint8_t> (*p) * PRIME5;
		}

		force_inline static constexpr uint64_t finalize (const uint64_t h, const char *p, size_t len) noexcept
		{
			return
				(len >= 8) ? (finalize (rotl<27> (h ^ fetch64 (p)) * PRIME1 + PRIME4, p + 8, len - 8)) :
				((len >= 4) ? (finalize (rotl<23> (h ^ fetch32 (p)) * PRIME2 + PRIME3, p + 4, len - 4)) :
				 ((len > 0) ? (finalize (rotl<11> (h ^ fetch8 (p)) * PRIME1, p + 1, len - 1)) :
				  (mix1<32> (mix1<29> (mix1<33> (h, PRIME2), PRIME3), 1))));
		}

		force_inline static constexpr uint64_t h32bytes (const char *p, size_t len, const uint64_t v1,const uint64_t v2, const uint64_t v3, const uint64_t v4) noexcept
		{
			return (len >= 32) ? h32bytes (p + 32, len - 32, fetch64 (p, v1), fetch64 (p + 8, v2), fetch64 (p + 16, v3), fetch64 (p + 24, v4)) :
				mix3 (mix3 (mix3 (mix3 (rotl<1> (v1) + rotl<7> (v2) + rotl<12> (v3) + rotl<18> (v4), v1), v2), v3), v4);
		}

		// We don't use any special seed in XA, the template parameter is just to keep the algorithm more easily
		// understood
		template<uint64_t Seed = 0>
		force_inline static constexpr uint64_t h32bytes (const char *p, size_t len) noexcept
		{
			return h32bytes (p, len, Seed + PRIME1 + PRIME2, Seed + PRIME2, Seed, Seed - PRIME1);
		}
	};

#if INTPTR_MAX == INT64_MAX
	using hash_t = uint64_t;
	using xxhash = xxhash64;
#else
	using hash_t = uint32_t;
	using xxhash = xxhash32;
#endif
}
#endif
