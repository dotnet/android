#pragma once

#include <string_view>

#include <jni.h>
#include <corehost/host_runtime_contract.h>

#include "../runtime-base/jni-wrappers.hh"
#include "../runtime-base/timing.hh"
#include "../shared/log_types.hh"

namespace xamarin::android {
	// NOTE: Keep this in sync with managed side in src/Mono.Android/Android.Runtime/JNIEnvInit.cs
	struct JnienvInitializeArgs
	{
		JavaVM         *javaVm;
		JNIEnv         *env;
		jobject         grefLoader;
		jmethodID       Loader_loadClass;
		jclass          grefClass;
		unsigned int    logCategories;
		int             version;
		int             grefGcThreshold;
		jobject         grefIGCUserPeer;
		int             isRunningOnDesktop;
		uint8_t         brokenExceptionTransitions;
		int             packageNamingPolicy;
		uint8_t         boundExceptionType;
		int             jniAddNativeMethodRegistrationAttributePresent;
		bool            jniRemappingInUse;
		bool            marshalMethodsEnabled;
		jobject         grefGCUserPeerable;
	};

	class Host
	{
		using jnienv_initialize_fn = void (*) (JnienvInitializeArgs*);

	public:
		static auto Java_JNI_OnLoad (JavaVM *vm, void *reserved) noexcept -> jint;
		static void Java_mono_android_Runtime_initInternal (JNIEnv *env, jclass klass, jstring lang, jobjectArray runtimeApksJava,
			jstring runtimeNativeLibDir, jobjectArray appDirs, jint localDateTimeOffset, jobject loader,
			jobjectArray assembliesJava, jboolean isEmulator, jboolean haveSplitApks);

		static auto get_timing () -> Timing*
		{
			return _timing.get ();
		}

	private:
		static void create_xdg_directory (jstring_wrapper& home, size_t home_len, std::string_view const& relative_path, std::string_view const& environment_variable_name) noexcept;
		static void create_xdg_directories_and_environment (jstring_wrapper &homeDir) noexcept;
		static auto zip_scan_callback (std::string_view const& apk_path, int apk_fd, dynamic_local_string<SENSIBLE_PATH_MAX> const& entry_name, uint32_t offset, uint32_t size) -> bool;
		static void gather_assemblies_and_libraries (jstring_array_wrapper& runtimeApks, bool have_split_apks);

		static size_t clr_get_runtime_property (const char *key, char *value_buffer, size_t value_buffer_size, void *contract_context) noexcept;
		static bool clr_bundle_probe (const char *path, void **data_start, int64_t *size) noexcept;
		static const void* clr_pinvoke_override (const char *library_name, const char *entry_point_name) noexcept;
		static void clr_error_writer (const char *message) noexcept;

	private:
		static inline void *clr_host = nullptr;
		static inline unsigned int domain_id = 0;
		static inline std::unique_ptr<Timing> _timing{};
		static inline bool found_assembly_store = false;

		static inline JavaVM *jvm = nullptr;
		static inline jmethodID Class_getName = nullptr;

		static inline host_runtime_contract runtime_contract{
			.size = sizeof(host_runtime_contract),
			.context = nullptr,
			.get_runtime_property = clr_get_runtime_property,
			.android_bundle_probe = clr_bundle_probe,
			.bundle_probe = nullptr,
			.pinvoke_override = clr_pinvoke_override,
		};
	};
}
