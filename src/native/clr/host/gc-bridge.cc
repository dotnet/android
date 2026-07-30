// The ADPF APerformanceHint_* API is API level 33+, but this runtime targets a lower minSdk. Weak-link
// the newer symbols so the translation unit builds, and guard every call with a runtime
// android_get_device_api_level() check (the native equivalent of Build.VERSION.SDK_INT >= 33). This
// define must precede any NDK header so __INTRODUCED_IN() emits weak references here.
#define __ANDROID_UNAVAILABLE_SYMBOLS_ARE_WEAK__

#include <cerrno>
#include <cinttypes>
#include <cstdint>
#include <ctime>
#include <pthread.h>
#include <semaphore.h>
#include <unistd.h>
#include <android/api-level.h>
#include <android/performance_hint.h>

#include <host/gc-bridge.hh>
#include <host/bridge-processing.hh>
#include <host/os-bridge.hh>
#include <host/host.hh>
#include <runtime-base/util.hh>
#include <shared/helpers.hh>
#include <xamarin-app.hh>

using namespace xamarin::android;

// APerformanceHint_notifyWorkloadSpike() ships in Android 16 (API 36), newer than the NDK headers
// this builds against, so declare it here as a weak reference. libandroid resolves it at load time
// on API 36+; on older platforms it stays null, and it is likewise null under the NativeAOT host
// (which does not link libandroid). Being weak, it does not trip the linker's --no-undefined, exactly
// like the ADPF symbols weak-linked via __ANDROID_UNAVAILABLE_SYMBOLS_ARE_WEAK__. Its ABI is frozen
// (see the NDK header's stable-API notice), so this local declaration is safe.
extern "C" int APerformanceHint_notifyWorkloadSpike (
	APerformanceHintSession *session, bool cpu, bool gpu, const char *debugName) __attribute__((weak));

namespace {
	// The CoreCLR GC bridge runs the explicit ART GC (Runtime.gc()) synchronously on a dedicated
	// pthread that is idle >99% of the time. When it wakes, Android's schedutil DVFS governor sees a
	// cold thread and keeps its big core near the bottom of the frequency table for the whole short
	// GC burst, making the GC ~2x slower than under MonoVM (which drives the same GC from the
	// always-hot render thread). See dotnet/android#12263 / dotnet/runtime#131370.
	//
	// The Android-recommended way to raise the frequency for a latency-critical thread is ADPF (the
	// Android Dynamic Performance Framework): create an APerformanceHint session for the thread with
	// a target work duration and report the actual duration each round. The framework then boosts the
	// carrier core (via uclamp or schedtune, whichever the kernel/power-HAL supports) whenever the
	// thread runs. This works without root, without RT priority, and independent of the kernel's
	// CONFIG_UCLAMP_TASK setting, unlike a direct sched_setattr() util-clamp hint. Everything here
	// fails soft: a scheduling hint must never take down the process.
	//
	// Android docs:
	//   ADPF overview:          https://developer.android.com/games/optimize/adpf
	//   APerformanceHint (NDK): https://developer.android.com/ndk/reference/group/a-performance-hint
	//   android_get_device_api_level: https://developer.android.com/ndk/reference/group/apilevels

	// Target work duration reported to ADPF. The bridge GC should finish well inside a 60 Hz frame;
	// a tight target relative to the real (multi-ms) duration keeps the framework boosting the core.
	constexpr int64_t GCBridgeHintTargetDurationNs = 4'000'000; // 4 ms

	APerformanceHintSession *gc_bridge_hint_session = nullptr;

	// The bridge GC is a rare, one-off CPU spike on an otherwise-idle thread, so reporting its
	// duration *after* the fact (via reportActualWorkDuration below) is too late to speed up that same
	// collection: the DVFS governor takes ~200ms to ramp, far longer than the ~10ms GC. ADPF's
	// APerformanceHint_notifyWorkloadSpike() exists for exactly this case -- it tells the framework a
	// sudden spike is imminent so it pre-boosts the core *before* the work starts. We call it just
	// before each Runtime.gc(). Rate-limited per app by the framework, but the bridge fires at most
	// once every several seconds, well within budget.
	//   notifyWorkloadSpike (NDK): https://developer.android.com/ndk/reference/group/a-performance-hint

	// notifyWorkloadSpike was added in Android 16 (API 36), newer than the NDK this builds against, so
	// it is not declared in <android/performance_hint.h> and there is no __ANDROID_API_*__ macro for it
	// to gate on. Instead we weak-link it (below) and null-check at the call site: libandroid only
	// exports the symbol on API 36+, so a non-null pointer *is* the availability test -- more precise
	// than an OS-version proxy.

	auto monotonic_now_ns () noexcept -> int64_t
	{
		struct timespec ts;
		clock_gettime (CLOCK_MONOTONIC, &ts);
		return (static_cast<int64_t> (ts.tv_sec) * 1'000'000'000) + static_cast<int64_t> (ts.tv_nsec);
	}

	// Create an ADPF performance-hint session bound to the calling (GC bridge) thread so the platform
	// clocks its core up during the explicit GC. Must be called once, from the bridge thread itself.
	void create_gc_bridge_hint_session () noexcept
	{
		// APerformanceHint_* was introduced in Android 13 (API 33 / Tiramisu).
		if (android_get_device_api_level () < __ANDROID_API_T__) {
			log_info (LOG_DEFAULT, "GC bridge boost: ADPF hint sessions require Android 13 (API 33); skipping");
			return;
		}

		// The ADPF symbols are weak-linked (see the file header). The NativeAOT host does not link
		// libandroid, so on that runtime the weak symbol can stay unresolved (null) even on API 33+.
		// Check the function pointer itself before calling it, so an unresolved symbol fails soft
		// instead of crashing the process on a null call.
		if (APerformanceHint_getManager == nullptr) {
			log_info (LOG_DEFAULT, "GC bridge boost: ADPF APerformanceHint API not available on this build; skipping");
			return;
		}

		APerformanceHintManager *manager = APerformanceHint_getManager ();
		if (manager == nullptr) {
			log_info (LOG_DEFAULT, "GC bridge boost: no ADPF hint manager on this device; skipping");
			return;
		}

		int32_t tids[1] = { static_cast<int32_t> (gettid ()) };
		APerformanceHintSession *session = APerformanceHint_createSession (manager, tids, 1, GCBridgeHintTargetDurationNs);
		if (session == nullptr) {
			log_info (LOG_DEFAULT, "GC bridge boost: device declined ADPF hint session; skipping");
			return;
		}

		// Intentionally never APerformanceHint_closeSession()-d: the session lives for the lifetime of
		// the GC bridge thread, which itself lives for the whole process. There is no teardown path to
		// close it from, so this is a deliberate process-lifetime retention, not a leak.
		gc_bridge_hint_session = session;
		log_infof (LOG_DEFAULT, "GC bridge boost: ADPF hint session created (target=%" PRId64 " ns)", GCBridgeHintTargetDurationNs);

		// Log once whether the Android 16 (API 36) spike pre-boost resolved on this platform; the
		// per-GC path null-checks the weak symbol directly.
		log_infof (LOG_DEFAULT, "GC bridge boost: workload-spike pre-boost %s",
			APerformanceHint_notifyWorkloadSpike != nullptr ? "available" : "unavailable");
	}

	// Report the just-finished bridge GC duration so ADPF keeps boosting this thread's core. Only ever
	// called when a session exists, which implies the device is API 33+.
	void report_gc_bridge_work (int64_t actual_duration_ns) noexcept
	{
		APerformanceHint_reportActualWorkDuration (gc_bridge_hint_session, actual_duration_ns);
	}
}

void GCBridge::initialize_shared_args_semaphore () noexcept
{
	int ret = sem_init (&shared_args_semaphore, 0, 0);
	abort_unless (ret == 0, "Failed to initialize GC bridge semaphore");
}

void GCBridge::start_bridge_processing_thread () noexcept
{
	pthread_t thread {};
	int ret = pthread_create (&thread, nullptr, bridge_processing_thread_entry, nullptr);
	abort_unless (ret == 0, "Failed to create GC bridge processing thread");

	ret = pthread_detach (thread);
	abort_unless (ret == 0, "Failed to detach GC bridge processing thread");
}

void GCBridge::publish_shared_args (MarkCrossReferencesArgs *args) noexcept
{
	__atomic_store_n (&shared_args, args, __ATOMIC_RELEASE);

	int ret = sem_post (&shared_args_semaphore);
	abort_unless (ret == 0, "Failed to release GC bridge semaphore");
}

auto GCBridge::wait_for_shared_args () noexcept -> MarkCrossReferencesArgs*
{
	int ret;
	do {
		ret = sem_wait (&shared_args_semaphore);
	} while (ret == -1 && errno == EINTR);
	abort_unless (ret == 0, "Failed to acquire GC bridge semaphore");

	return __atomic_load_n (&shared_args, __ATOMIC_ACQUIRE);
}

void GCBridge::initialize_on_onload (JNIEnv *env) noexcept
{
	abort_if_invalid_pointer_argument (env, "env");

	jclass Runtime_class = env->FindClass ("java/lang/Runtime");
	abort_unless (Runtime_class != nullptr, "Failed to look up java/lang/Runtime class.");

	jmethodID Runtime_getRuntime = env->GetStaticMethodID (Runtime_class, "getRuntime", "()Ljava/lang/Runtime;");
	abort_unless (Runtime_getRuntime != nullptr, "Failed to look up the Runtime.getRuntime() method.");

	Runtime_gc = env->GetMethodID (Runtime_class, "gc", "()V");
	abort_unless (Runtime_gc != nullptr, "Failed to look up the Runtime.gc() method.");

	Runtime_instance = OSBridge::lref_to_gref (env, env->CallStaticObjectMethod (Runtime_class, Runtime_getRuntime));
	abort_unless (Runtime_instance != nullptr, "Failed to obtain Runtime instance.");

	env->DeleteLocalRef (Runtime_class);
}

void GCBridge::initialize_on_runtime_init (JNIEnv *env, jclass runtimeClass) noexcept
{
	abort_if_invalid_pointer_argument (env, "env");
	abort_if_invalid_pointer_argument (runtimeClass, "runtimeClass");

	BridgeProcessing::initialize_on_runtime_init (env, runtimeClass);
}

void GCBridge::trigger_java_gc (JNIEnv *env) noexcept
{
	abort_if_invalid_pointer_argument (env, "env");

	env->CallVoidMethod (Runtime_instance, Runtime_gc);
	if (!env->ExceptionCheck ()) [[likely]] {
		return;
	}

	env->ExceptionDescribe ();
	env->ExceptionClear ();
	log_error (LOG_DEFAULT, "Java GC failed");
}

void GCBridge::mark_cross_references (MarkCrossReferencesArgs *args) noexcept
{
	abort_if_invalid_pointer_argument (args, "args");
	abort_unless (args->Components != nullptr || args->ComponentCount == 0, "Components must not be null if ComponentCount is greater than 0");
	abort_unless (args->CrossReferences != nullptr || args->CrossReferenceCount == 0, "CrossReferences must not be null if CrossReferenceCount is greater than 0");
	log_mark_cross_references_args_if_enabled (args);

	publish_shared_args (args);
}

void GCBridge::bridge_processing () noexcept
{
	abort_unless (bridge_processing_started_callback != nullptr, "GC bridge processing started callback is not set");
	abort_unless (bridge_processing_finished_callback != nullptr, "GC bridge processing finished callback is not set");

	while (true) {
		// wait until mark cross references args are set by the GC callback
		MarkCrossReferencesArgs *args = wait_for_shared_args ();

		bridge_processing_started_callback (args);

		BridgeProcessing bridge_processing {args};
		if (gc_bridge_hint_session != nullptr) [[likely]] {
			// Tell ADPF a CPU workload spike is imminent so it pre-ramps this thread's core *before*
			// the GC starts, instead of the governor lagging ~200ms behind the ~10ms burst. The weak
			// symbol is null (a no-op) on platforms without the API; see its declaration above.
			if (APerformanceHint_notifyWorkloadSpike != nullptr) {
				APerformanceHint_notifyWorkloadSpike (gc_bridge_hint_session, /* cpu */ true, /* gpu */ false, "gc-bridge");
			}

			// Time the explicit bridge GC and report it to ADPF so the platform keeps this thread's
			// carrier core clocked up for the rare, latency-sensitive collection.
			int64_t start_ns = monotonic_now_ns ();
			bridge_processing.process ();
			report_gc_bridge_work (monotonic_now_ns () - start_ns);
		} else {
			bridge_processing.process ();
		}

		bridge_processing_finished_callback (args);
	}
}

auto GCBridge::bridge_processing_thread_entry ([[maybe_unused]] void *arg) noexcept -> void*
{
	// Ask the platform to boost this thread's CPU frequency for the explicit ART GC, so schedutil
	// does not leave the mostly-idle bridge thread's core near the bottom of the frequency table.
#if defined (XA_HOST_NATIVEAOT)
	// The NativeAOT host does not link the generated application_config symbol (it has its own host and
	// never pulls in the CoreCLR config), so the per-app toggle is unavailable here. Enable the boost
	// unconditionally; it still fails soft on devices without ADPF support.
	create_gc_bridge_hint_session ();
#else
	if (application_config.gc_bridge_thread_boost_enabled) {
		create_gc_bridge_hint_session ();
	}
#endif

	bridge_processing ();
	return nullptr;
}

[[gnu::always_inline]]
void GCBridge::log_mark_cross_references_args_if_enabled (MarkCrossReferencesArgs *args) noexcept
{
	if (!Logger::gc_spew_enabled ()) [[likely]] {
		return;
	}

	log_infof (LOG_GC, "cross references callback invoked with %zu sccs and %zu xrefs.", args->ComponentCount, args->CrossReferenceCount);

	JNIEnv *env = OSBridge::ensure_jnienv ();
	
	for (size_t i = 0; i < args->ComponentCount; ++i) {
		const StronglyConnectedComponent &scc = args->Components [i];
		log_infof (LOG_GC, "group %zu with %zu objects", i, scc.Count);
		for (size_t j = 0; j < scc.Count; ++j) {
			log_handle_context (env, scc.Contexts [j]);
		}
	}

	if (!Util::should_log (LOG_GC)) {
		return;
	}

	for (size_t i = 0; i < args->CrossReferenceCount; ++i) {
		size_t source_index = args->CrossReferences [i].SourceGroupIndex;
		size_t dest_index = args->CrossReferences [i].DestinationGroupIndex;
		log_writef (LOG_GC, LogLevel::Info, "xref [%zu] %zu -> %zu", i, source_index, dest_index);
	}
}

[[gnu::always_inline]]
void GCBridge::log_handle_context (JNIEnv *env, HandleContext *ctx) noexcept
{
	abort_unless (ctx != nullptr, "Context must not be null");
	abort_unless (ctx->control_block != nullptr, "Control block must not be null");

	jobject handle = ctx->control_block->handle;
	jclass java_class = env->GetObjectClass (handle);
	if (java_class != nullptr) {
		char *class_name = Host::get_java_class_name_for_TypeManager (java_class);
		log_infof (LOG_GC, "gref 0x%" PRIxPTR " [%s]", reinterpret_cast<uintptr_t> (handle), optional_string (class_name));
		free (class_name);
		env->DeleteLocalRef (java_class);
	} else {
		log_infof (LOG_GC, "gref 0x%" PRIxPTR " [unknown class]", reinterpret_cast<uintptr_t> (handle));
	}
}
