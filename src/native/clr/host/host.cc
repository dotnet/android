#include <clr/hosts/coreclrhost.h>

#include <xamarin-app.hh>
#include <host/host.hh>
#include <host/host-jni.hh>
#include <runtime-base/android-system.hh>
#include <runtime-base/jni-wrappers.hh>
#include <runtime-base/logger.hh>
#include <runtime-base/timing-internal.hh>
#include <shared/log_types.hh>

using namespace xamarin::android;

size_t Host::clr_get_runtime_property (const char *key, char *value_buffer, size_t value_buffer_size, void *contract_context) noexcept
{
	log_info (LOG_DEFAULT, "clr_get_runtime_property (\"{}\"...)", key);
	return 0;
}

bool Host::clr_bundle_probe (const char *path, int64_t *offset, int64_t *size, int64_t *compressedSize) noexcept
{
	log_info (LOG_DEFAULT, "clr_bundle_probe (\"{}\"...)", path);
	Helpers::abort_application ("Gimme stack trace");
	return false;
}

const void* Host::clr_pinvoke_override (const char *library_name, const char *entry_point_name) noexcept
{
	log_info (LOG_DEFAULT, "clr_pinvoke_override (\"{}\", \"{}\")", library_name, entry_point_name);
	return nullptr;
}

void Host::create_xdg_directory (jstring_wrapper& home, size_t home_len, std::string_view const& relative_path, std::string_view const& environment_variable_name) noexcept
{
	static_local_string<SENSIBLE_PATH_MAX> dir (home_len + relative_path.length ());
	Util::path_combine (dir, home.get_string_view (), relative_path);

	log_debug (LOG_DEFAULT, "Creating XDG directory: {}", optional_string (dir.get ()));
	int rv = Util::create_directory (dir.get (), Constants::DEFAULT_DIRECTORY_MODE);
	if (rv < 0 && errno != EEXIST) {
		log_warn (LOG_DEFAULT, "Failed to create XDG directory {}. {}", optional_string (dir.get ()), strerror (errno));
	}

	if (!environment_variable_name.empty ()) {
		setenv (environment_variable_name.data (), dir.get (), 1);
	}
}

void Host::create_xdg_directories_and_environment (jstring_wrapper &homeDir) noexcept
{
	size_t home_len = strlen (homeDir.get_cstr ());

	constexpr auto XDG_DATA_HOME = "XDG_DATA_HOME"sv;
	constexpr auto HOME_PATH = ".local/share"sv;
	create_xdg_directory (homeDir, home_len, HOME_PATH, XDG_DATA_HOME);

	constexpr auto XDG_CONFIG_HOME = "XDG_CONFIG_HOME"sv;
	constexpr auto CONFIG_PATH = ".config"sv;
	create_xdg_directory (homeDir, home_len, CONFIG_PATH, XDG_CONFIG_HOME);
}

void Host::Java_mono_android_Runtime_initInternal (JNIEnv *env, jclass klass, jstring lang, jobjectArray runtimeApksJava,
	jstring runtimeNativeLibDir, jobjectArray appDirs, jint localDateTimeOffset, jobject loader,
	jobjectArray assembliesJava, jboolean isEmulator, jboolean haveSplitApks)
{
	Logger::init_logging_categories ();

	// If fast logging is disabled, log messages immediately
	FastTiming::initialize ((Logger::log_timing_categories() & LogTimingCategories::FastBare) != LogTimingCategories::FastBare);

	size_t total_time_index;
	if (FastTiming::enabled ()) [[unlikely]] {
		_timing = std::make_unique<Timing> ();
		total_time_index = internal_timing->start_event (TimingEventKind::TotalRuntimeInit);
	}

	jstring_array_wrapper applicationDirs (env, appDirs);

	jstring_wrapper jstr (env, lang);
	Util::set_environment_variable ("LANG", jstr);

	jstring_wrapper &home = applicationDirs[Constants::APP_DIRS_FILES_DIR_INDEX];
	Util::set_environment_variable_for_directory ("TMPDIR", applicationDirs[Constants::APP_DIRS_CACHE_DIR_INDEX]);
	Util::set_environment_variable_for_directory ("HOME", home);
	create_xdg_directories_and_environment (home);

	AndroidSystem::detect_embedded_dso_mode (applicationDirs);
	AndroidSystem::set_running_in_emulator (isEmulator);
	AndroidSystem::set_primary_override_dir (home);
	AndroidSystem::create_update_dir (AndroidSystem::get_primary_override_dir ());
	AndroidSystem::setup_environment ();

	log_write (LOG_DEFAULT, LogLevel::Info, "Calling CoreCLR initialization routine");
	android_coreclr_initialize (
		application_config.android_package_name,
		u"Xamarin.Android",
		&runtime_contract,
		&host_config_properties,
		&clr_host,
		&domain_id
	);
	log_write (LOG_DEFAULT, LogLevel::Info, "CoreCLR initialization routine returned");

	if (FastTiming::enabled ()) [[unlikely]] {
		internal_timing->end_event (total_time_index);
	}
}

auto Host::Java_JNI_OnLoad (JavaVM *vm, [[maybe_unused]] void *reserved) noexcept -> jint
{
	log_write (LOG_DEFAULT, LogLevel::Info, "Host init");

	AndroidSystem::init_max_gref_count ();
	return JNI_VERSION_1_6;
}
