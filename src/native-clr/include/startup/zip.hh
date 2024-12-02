#pragma once

#include <sys/types.h>

#include <string_view>

#include <constants.hh>
#include <shared/cpp-util.hh>

namespace xamarin::android {
	class Zip
	{
	private:
		static inline constexpr off_t ZIP_EOCD_LEN        = 22;
		static inline constexpr off_t ZIP_CENTRAL_LEN     = 46;
		static inline constexpr off_t ZIP_LOCAL_LEN       = 30;

		static inline constexpr std::string_view ZIP_CENTRAL_MAGIC { "PK\1\2" };
		static inline constexpr std::string_view ZIP_LOCAL_MAGIC   { "PK\3\4" };
		static inline constexpr std::string_view ZIP_EOCD_MAGIC    { "PK\5\6" };

		static constexpr std::string_view zip_path_separator       { "/" };
		static constexpr std::string_view apk_lib_dir_name         { "lib" };

		static constexpr size_t assemblies_prefix_size = calc_size(apk_lib_dir_name, zip_path_separator, Constants::android_lib_abi, zip_path_separator);
		static constexpr auto assemblies_prefix_array = concat_string_views<assemblies_prefix_size> (apk_lib_dir_name, zip_path_separator, Constants::android_lib_abi, zip_path_separator);

		// .data() must be used otherwise string_view length will include the trailing \0 in the array
		static constexpr std::string_view assemblies_prefix { assemblies_prefix_array.data () };
	};
}
