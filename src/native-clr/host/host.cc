#include <host/host.hh>
#include <host/host-jni.hh>
#include <runtime-base/android-system.hh>
#include <runtime-base/logger.hh>
#include <runtime-base/timing-internal.hh>
#include <shared/log_types.hh>

using namespace xamarin::android;

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
}

auto Host::Java_JNI_OnLoad (JavaVM *vm, [[maybe_unused]] void *reserved) noexcept -> jint
{
	log_write (LOG_DEFAULT, LogLevel::Info, "Host init");

	AndroidSystem::init_max_gref_count ();
	return JNI_VERSION_1_6;
}
