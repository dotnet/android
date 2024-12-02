#pragma once

#include <sys/system_properties.h>

#include <string_view>

#include "shared/cpp-util.hh"

namespace xamarin::android {
	class Constants
	{
#if INTPTR_MAX == INT64_MAX
		static inline constexpr std::string_view BITNESS { "64bit" };
#else
		static inline constexpr std::string_view BITNESS { "32bit" };
#endif

	public:
		static constexpr std::string_view MANGLED_ASSEMBLY_NAME_EXT { ".so" };

	private:
		static constexpr std::string_view RUNTIME_CONFIG_BLOB_BASE_NAME       { "libarc.bin" };
		static constexpr size_t runtime_config_blob_name_size                 = calc_size (RUNTIME_CONFIG_BLOB_BASE_NAME, MANGLED_ASSEMBLY_NAME_EXT);
		static constexpr auto RUNTIME_CONFIG_BLOB_NAME_ARRAY                  = concat_string_views<runtime_config_blob_name_size> (RUNTIME_CONFIG_BLOB_BASE_NAME, MANGLED_ASSEMBLY_NAME_EXT);

	public:
		// .data() must be used otherwise string_view length will include the trailing \0 in the array
		static constexpr std::string_view RUNTIME_CONFIG_BLOB_NAME            { RUNTIME_CONFIG_BLOB_NAME_ARRAY.data () };
		static constexpr std::string_view OVERRIDE_DIRECTORY_NAME             { ".__override__" };

		/* Android property containing connection information, set by XS */
		static inline constexpr std::string_view DEBUG_MONO_CONNECT_PROPERTY      { "debug.mono.connect" };
		static inline constexpr std::string_view DEBUG_MONO_DEBUG_PROPERTY        { "debug.mono.debug" };
		static inline constexpr std::string_view DEBUG_MONO_ENV_PROPERTY          { "debug.mono.env" };
		static inline constexpr std::string_view DEBUG_MONO_EXTRA_PROPERTY        { "debug.mono.extra" };
		static inline constexpr std::string_view DEBUG_MONO_GC_PROPERTY           { "debug.mono.gc" };
		static inline constexpr std::string_view DEBUG_MONO_GDB_PROPERTY          { "debug.mono.gdb" };
		static inline constexpr std::string_view DEBUG_MONO_LOG_PROPERTY          { "debug.mono.log" };
		static inline constexpr std::string_view DEBUG_MONO_MAX_GREFC             { "debug.mono.max_grefc" };
		static inline constexpr std::string_view DEBUG_MONO_PROFILE_PROPERTY      { "debug.mono.profile" };
		static inline constexpr std::string_view DEBUG_MONO_RUNTIME_ARGS_PROPERTY { "debug.mono.runtime_args" };
		static inline constexpr std::string_view DEBUG_MONO_SOFT_BREAKPOINTS      { "debug.mono.soft_breakpoints" };
		static inline constexpr std::string_view DEBUG_MONO_TRACE_PROPERTY        { "debug.mono.trace" };
		static inline constexpr std::string_view DEBUG_MONO_WREF_PROPERTY         { "debug.mono.wref" };

		static constexpr std::string_view LOG_CATEGORY_NAME_NONE                  { "*none*" };
		static constexpr std::string_view LOG_CATEGORY_NAME_MONODROID             { "monodroid" };
		static constexpr std::string_view LOG_CATEGORY_NAME_MONODROID_ASSEMBLY    { "monodroid-assembly" };
		static constexpr std::string_view LOG_CATEGORY_NAME_MONODROID_DEBUG       { "monodroid-debug" };
		static constexpr std::string_view LOG_CATEGORY_NAME_MONODROID_GC          { "monodroid-gc" };
		static constexpr std::string_view LOG_CATEGORY_NAME_MONODROID_GREF        { "monodroid-gref" };
		static constexpr std::string_view LOG_CATEGORY_NAME_MONODROID_LREF        { "monodroid-lref" };
		static constexpr std::string_view LOG_CATEGORY_NAME_MONODROID_TIMING      { "monodroid-timing" };
		static constexpr std::string_view LOG_CATEGORY_NAME_MONODROID_BUNDLE      { "monodroid-bundle" };
		static constexpr std::string_view LOG_CATEGORY_NAME_MONODROID_NETWORK     { "monodroid-network" };
		static constexpr std::string_view LOG_CATEGORY_NAME_MONODROID_NETLINK     { "monodroid-netlink" };
		static constexpr std::string_view LOG_CATEGORY_NAME_ERROR                 { "*error*" };

#if defined(__arm__)
		static constexpr std::string_view android_abi        { "armeabi_v7a" };
		static constexpr std::string_view android_lib_abi    { "armeabi-v7a" };
		static constexpr std::string_view runtime_identifier { "android-arm" };
#elif defined(__aarch64__)
		static constexpr std::string_view android_abi        { "arm64_v8a" };
		static constexpr std::string_view android_lib_abi    { "arm64-v8a" };
		static constexpr std::string_view runtime_identifier { "android-arm64" };
#elif defined(__x86_64__)
		static constexpr std::string_view android_abi        { "x86_64" };
		static constexpr std::string_view android_lib_abi    { "x86_64" };
		static constexpr std::string_view runtime_identifier { "android-x64" };
#elif defined(__i386__)
		static constexpr std::string_view android_abi        { "x86" };
		static constexpr std::string_view android_lib_abi    { "x86" };
		static constexpr std::string_view runtime_identifier { "android-x86" };
#endif

		static constexpr std::string_view split_config_prefix { "/split_config." };
		static constexpr std::string_view split_config_extension { ".apk" };

		static constexpr size_t split_config_abi_apk_name_size = calc_size (split_config_prefix, android_abi, split_config_extension);
		static constexpr auto split_config_abi_apk_name = concat_string_views<split_config_abi_apk_name_size> (split_config_prefix, android_abi, split_config_extension);

		//
		// Indexes must match these of trhe `appDirs` array in src/java-runtime/mono/android/MonoPackageManager.java
		//
		static constexpr size_t APP_DIRS_FILES_DIR_INDEX = 0uz;
		static constexpr size_t APP_DIRS_CACHE_DIR_INDEX = 1uz;
		static constexpr size_t APP_DIRS_DATA_DIR_INDEX = 2uz;

		static inline constexpr size_t PROPERTY_VALUE_BUFFER_LEN = PROP_VALUE_MAX + 1uz;

		// 64-bit unsigned or 64-bit signed with sign
		static constexpr size_t MAX_INTEGER_DIGIT_COUNT_BASE10 = 21uz;
		static constexpr size_t INTEGER_BASE10_BUFFER_SIZE = MAX_INTEGER_DIGIT_COUNT_BASE10 + 1uz;
	};
}
