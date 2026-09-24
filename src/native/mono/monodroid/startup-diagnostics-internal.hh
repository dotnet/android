#pragma once

#include <cerrno>
#include <cstdint>
#include <limits>
#include <optional>
#include <string_view>

namespace xamarin::android::startup {
	inline constexpr std::string_view capture_key = "DOTNET_ANDROID_STARTUP_CAPTURE_ID";

	class PreserveErrno final
	{
		int saved = errno;
	public:
		~PreserveErrno () noexcept { errno = saved; }
	};

	inline bool valid_capture_id (std::string_view value) noexcept
	{
		if (value.size () != 32)
			return false;
		for (char c : value) {
			if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')))
				return false;
		}
		return true;
	}

	inline bool valid_build_id (std::string_view value) noexcept
	{
		if (value.empty () || value.size () > 64)
			return false;
		for (size_t i = 0; i < value.size (); ++i) {
			char c = value [i];
			if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
				continue;
			if (i == 0 || (c != '.' && c != '_' && c != '-'))
				return false;
		}
		return true;
	}

	inline std::optional<std::string_view> find_capture_id (const char* const* values, size_t count) noexcept
	{
		if (values == nullptr || count % 2 != 0)
			return {};
		std::optional<std::string_view> token;
		for (size_t i = 0; i < count; i += 2) {
			if (values [i] == nullptr || capture_key != values [i])
				continue;
			if (token.has_value () || values [i + 1] == nullptr)
				return {};
			// Inspect only the permitted token width; never copy an environment value into output.
			const char* value = values [i + 1];
			size_t length = 0;
			while (length <= 32 && value [length] != '\0')
				++length;
			if (!valid_capture_id ({value, length}))
				return {};
			token = std::string_view {value, length};
		}
		return token;
	}

	inline std::optional<uint64_t> parse_unsigned (std::string_view value) noexcept
	{
		if (value.empty ())
			return {};
		uint64_t result = 0;
		for (char c : value) {
			if (c < '0' || c > '9')
				return {};
			uint64_t digit = static_cast<uint64_t> (c - '0');
			if (result > (std::numeric_limits<uint64_t>::max () - digit) / 10)
				return {};
			result = result * 10 + digit;
		}
		return result;
	}

	// /proc stat's comm may contain spaces and ')'; field 22 follows its LAST closing parenthesis.
	inline std::optional<uint64_t> parse_start_ticks (std::string_view stat, uint64_t pid) noexcept
	{
		size_t space = stat.find (' ');
		size_t end_comm = stat.rfind (')');
		auto parsed_pid = parse_unsigned (stat.substr (0, space));
		if (!parsed_pid || *parsed_pid != pid || pid == 0 || space == std::string_view::npos ||
		    space + 1 >= stat.size () || stat [space + 1] != '(' || end_comm == std::string_view::npos ||
		    end_comm <= space + 1 || end_comm + 2 >= stat.size () || stat [end_comm + 1] != ' ')
			return {};
		stat.remove_prefix (end_comm + 2);
		for (int field = 3; field <= 22; ++field) {
			size_t end = stat.find (' ');
			if (field == 22) {
				auto ticks = parse_unsigned (stat.substr (0, end));
				return ticks && *ticks > 0 ? ticks : std::nullopt;
			}
			if (end == std::string_view::npos || end == 0)
				return {};
			stat.remove_prefix (end + 1);
		}
		return {};
	}

	inline bool valid_boot_id (std::string_view value) noexcept
	{
		if (value.size () != 36)
			return false;
		for (size_t i = 0; i < value.size (); ++i) {
			char c = value [i];
			if (i == 8 || i == 13 || i == 18 || i == 23) {
				if (c != '-')
					return false;
			} else if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) {
				return false;
			}
		}
		return true;
	}

	inline std::optional<uint64_t> nanoseconds (int64_t seconds, int64_t nanos) noexcept
	{
		if (seconds < 0 || nanos < 0 || nanos >= 1000000000)
			return {};
		auto sec = static_cast<uint64_t> (seconds);
		auto ns = static_cast<uint64_t> (nanos);
		if (sec > (std::numeric_limits<uint64_t>::max () - ns) / 1000000000)
			return {};
		return sec * 1000000000 + ns;
	}
}
