#pragma once

#include <sys/types.h>

#include <concepts>
#include <string_view>

#include <constants.hh>
#include <shared/cpp-util.hh>
#include <runtime-base/strings.hh>

namespace xamarin::android {
	namespace detail {
		template<typename T>
		concept ByteArrayContainer = requires (T a) {
				a.size ();
				a.data ();
				requires std::same_as<typename T::value_type, uint8_t>;
		};
	}

	class Zip
	{
	public:
		// Returns `true` if the entry was something we need.
		using ScanCallbackFn = bool(const char *apk_path, int apk_fd, dynamic_local_string<SENSIBLE_PATH_MAX> const& entry_name, uint32_t offset, uint32_t size);

		struct zip_scan_state
		{
			int					  file_fd;
			const char *		  file_name;
			const char * const	  prefix;
			uint32_t			  prefix_len;
			size_t				  buf_offset;
			uint16_t			  compression_method;
			uint32_t			  local_header_offset;
			uint32_t			  data_offset;
			uint32_t			  file_size;
			bool				  bundled_assemblies_slow_path;
			uint32_t			  max_assembly_name_size;
			uint32_t			  max_assembly_file_name_size;
		};

	private:
		static inline constexpr off_t ZIP_EOCD_LEN		  = 22;
		static inline constexpr off_t ZIP_CENTRAL_LEN	  = 46;
		static inline constexpr off_t ZIP_LOCAL_LEN		  = 30;

		static inline constexpr std::string_view ZIP_CENTRAL_MAGIC { "PK\1\2" };
		static inline constexpr std::string_view ZIP_LOCAL_MAGIC   { "PK\3\4" };
		static inline constexpr std::string_view ZIP_EOCD_MAGIC	   { "PK\5\6" };

		static constexpr std::string_view zip_path_separator	   { "/" };
		static constexpr std::string_view apk_lib_dir_name		   { "lib" };

		static constexpr size_t lib_prefix_size = calc_size(apk_lib_dir_name, zip_path_separator, Constants::android_lib_abi, zip_path_separator);
		static constexpr auto lib_prefix_array = concat_string_views<lib_prefix_size> (apk_lib_dir_name, zip_path_separator, Constants::android_lib_abi, zip_path_separator);

		// .data() must be used otherwise string_view length will include the trailing \0 in the array
		static constexpr std::string_view lib_prefix { lib_prefix_array.data () };

	public:
		// Scans the ZIP archive for any entries matching the `lib/{ARCH}/` prefix and calls `entry_cb`
		// for each of them. If the callback returns `false` for all of the entries (meaning none of them
		// was interesting/useful), then the APK file descriptor is closed. Otherwise, the descriptor is
		// kept open since we will need it later on.
		static void scan_archive (const char *apk_path, ScanCallbackFn entry_cb) noexcept;

	private:
		static std::tuple<const char*, uint32_t> get_assemblies_prefix_and_length () noexcept;

		// Returns `true` if the APK fd needs to remain open.
		static bool zip_scan_entries (int apk_fd, const char *apk_path, ScanCallbackFn entry_cb) noexcept;
		static bool zip_read_cd_info (int apk_fd, uint32_t& cd_offset, uint32_t& cd_size, uint16_t& cd_entries) noexcept;
		static bool zip_read_entry_info (std::vector<uint8_t> const& buf, dynamic_local_string<SENSIBLE_PATH_MAX>& file_name, zip_scan_state &state) noexcept;
		static bool zip_load_entry_common (size_t entry_index, std::vector<uint8_t> const& buf, dynamic_local_string<SENSIBLE_PATH_MAX> &entry_name, zip_scan_state &state) noexcept;
		static bool zip_adjust_data_offset (int fd, zip_scan_state &state) noexcept;

		template<size_t BufSize>
		static bool zip_extract_cd_info (std::array<uint8_t, BufSize> const& buf, uint32_t& cd_offset, uint32_t& cd_size, uint16_t& cd_entries) noexcept;

		template<class T>
		static bool zip_ensure_valid_params (T const& buf, size_t index, size_t to_read) noexcept;

		template<detail::ByteArrayContainer T>
		static bool zip_read_field (T const& src, size_t source_index, uint16_t& dst) noexcept;

		template<detail::ByteArrayContainer T>
		static bool zip_read_field (T const& src, size_t source_index, uint32_t& dst) noexcept;

		template<detail::ByteArrayContainer T>
		static bool zip_read_field (T const& src, size_t source_index, std::array<uint8_t, 4>& dst_sig) noexcept;

		template<detail::ByteArrayContainer T>
		static bool zip_read_field (T const& buf, size_t index, size_t count, dynamic_local_string<SENSIBLE_PATH_MAX>& characters) noexcept;
	};
}
