#include <cerrno>
#include <cinttypes>
#include <cstddef>
#include <cstdint>
#include <ctime>
#include <dlfcn.h>
#include <pthread.h>
#include <semaphore.h>
#include <unistd.h>

#include <host/gc-bridge.hh>
#include <host/bridge-processing.hh>
#include <host/os-bridge.hh>
#include <host/host.hh>
#include <runtime-base/util.hh>
#include <shared/helpers.hh>
#include <xamarin-app.hh>

using namespace xamarin::android;

namespace {
	// The CoreCLR GC bridge runs the explicit ART GC (Runtime.gc()) synchronously on a dedicated
	// pthread that is idle >99% of the time. When it wakes, Android's schedutil DVFS governor sees a
	// cold thread and keeps its big core near the bottom of the frequency table for the whole short
	// GC burst, making the GC ~2x slower than under MonoVM (which drives the same GC from the
	// always-hot render thread). See dotnet/android#12263 / dotnet/runtime#131370.
	//
	// The Android-recommended fix is ADPF (the Android Dynamic Performance Framework): create an
	// APerformanceHint session bound to the bridge thread with a target work duration, report the
	// actual duration each round, and notify the framework of the imminent spike so it pre-boosts the
	// carrier core. The framework then clocks the core up (via uclamp or schedtune, whichever the
	// kernel/power-HAL supports) whenever the thread runs. This works without root, without RT
	// priority, and independent of the kernel's CONFIG_UCLAMP_TASK setting, unlike a direct
	// sched_setattr() util-clamp hint. Everything here fails soft: a scheduling hint must never take
	// down the process.
	//
	// The APerformanceHint_* entry points live in libandroid, which neither host links directly, and
	// they are API-gated (getManager/createSession/reportActualWorkDuration are API 33+;
	// notifyWorkloadSpike is API 36+). We resolve them at runtime with dlopen/dlsym rather than
	// hard-linking libandroid and weak-linking the symbols: that keeps the dependency optional, avoids
	// forcing libandroid into every app's DT_NEEDED (which under NativeAOT would also mean
	// redistributing an NDK stub), behaves identically on the CoreCLR and NativeAOT hosts, and makes a
	// missing library or symbol a natural no-op. A non-null resolved pointer *is* the availability
	// test -- more precise than an OS-version proxy.
	//
	// Android docs:
	//   ADPF overview:          https://developer.android.com/games/optimize/adpf
	//   APerformanceHint (NDK): https://developer.android.com/ndk/reference/group/a-performance-hint

	// Opaque ADPF handles. We deliberately do not include <android/performance_hint.h>: its
	// __INTRODUCED_IN declarations would emit references the linker would have to satisfy, defeating
	// the point of resolving everything through dlsym.
	struct APerformanceHintManager;
	struct APerformanceHintSession;

	using APerformanceHint_getManager_fn = APerformanceHintManager* (*) ();
	using APerformanceHint_createSession_fn =
		APerformanceHintSession* (*) (APerformanceHintManager*, const int32_t*, size_t, int64_t);
	using APerformanceHint_reportActualWorkDuration_fn = int (*) (APerformanceHintSession*, int64_t);
	using APerformanceHint_notifyWorkloadSpike_fn = int (*) (APerformanceHintSession*, bool, bool, const char*);

	// Target work duration reported to ADPF. The bridge GC should finish well inside a 60 Hz frame;
	// a tight target relative to the real (multi-ms) duration keeps the framework boosting the core.
	constexpr int64_t GCBridgeHintTargetDurationNs = 4'000'000; // 4 ms

	// Resolved once, on the bridge thread, in create_gc_bridge_hint_session(); read on the per-GC path.
	APerformanceHintSession *gc_bridge_hint_session = nullptr;
	APerformanceHint_reportActualWorkDuration_fn gc_bridge_report_actual_work_duration = nullptr;
	APerformanceHint_notifyWorkloadSpike_fn gc_bridge_notify_workload_spike = nullptr;

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
		// libandroid is a core system library present on every device; when it is already mapped into
		// the process (the common case) this dlopen just bumps its refcount. RTLD_LOCAL: we only dlsym
		// our own handle. Kept open for the process lifetime -- there is no teardown path (see the
		// session retention note below).
		void *libandroid = ::dlopen ("libandroid.so", RTLD_NOW | RTLD_LOCAL);
		if (libandroid == nullptr) {
			log_info (LOG_DEFAULT, "GC bridge boost: libandroid.so not available; skipping ADPF hint");
			return;
		}

		// A non-null getManager is the availability test: libandroid only exports the APerformanceHint_*
		// API on Android 13+ (API 33), so resolving these is equivalent to an SDK_INT >= 33 check, and
		// works uniformly on the CoreCLR and NativeAOT hosts without either linking libandroid.
		auto get_manager = reinterpret_cast<APerformanceHint_getManager_fn> (::dlsym (libandroid, "APerformanceHint_getManager"));
		auto create_session = reinterpret_cast<APerformanceHint_createSession_fn> (::dlsym (libandroid, "APerformanceHint_createSession"));
		auto report_actual = reinterpret_cast<APerformanceHint_reportActualWorkDuration_fn> (::dlsym (libandroid, "APerformanceHint_reportActualWorkDuration"));
		if (get_manager == nullptr || create_session == nullptr || report_actual == nullptr) {
			log_info (LOG_DEFAULT, "GC bridge boost: ADPF hint sessions require Android 13 (API 33); skipping");
			return;
		}

		APerformanceHintManager *manager = get_manager ();
		if (manager == nullptr) {
			log_info (LOG_DEFAULT, "GC bridge boost: no ADPF hint manager on this device; skipping");
			return;
		}

		int32_t tids[1] = { static_cast<int32_t> (gettid ()) };
		APerformanceHintSession *session = create_session (manager, tids, 1, GCBridgeHintTargetDurationNs);
		if (session == nullptr) {
			log_info (LOG_DEFAULT, "GC bridge boost: device declined ADPF hint session; skipping");
			return;
		}

		// Intentionally never APerformanceHint_closeSession()-d: the session lives for the lifetime of
		// the GC bridge thread, which itself lives for the whole process. There is no teardown path to
		// close it from, so this is a deliberate process-lifetime retention, not a leak.
		gc_bridge_hint_session = session;
		gc_bridge_report_actual_work_duration = report_actual;

		// notifyWorkloadSpike was added in Android 16 (API 36); it stays null on older platforms, which
		// disables only the pre-boost -- the after-the-fact reportActualWorkDuration path still runs.
		gc_bridge_notify_workload_spike =
			reinterpret_cast<APerformanceHint_notifyWorkloadSpike_fn> (::dlsym (libandroid, "APerformanceHint_notifyWorkloadSpike"));

		log_infof (LOG_DEFAULT, "GC bridge boost: ADPF hint session created (target=%" PRId64 " ns)", GCBridgeHintTargetDurationNs);
		log_infof (LOG_DEFAULT, "GC bridge boost: workload-spike pre-boost %s",
			gc_bridge_notify_workload_spike != nullptr ? "available" : "unavailable");
	}

	// Report the just-finished bridge GC duration so ADPF keeps boosting this thread's core. Only ever
	// called when a session exists, which guarantees the reporter pointer was resolved alongside it.
	void report_gc_bridge_work (int64_t actual_duration_ns) noexcept
	{
		gc_bridge_report_actual_work_duration (gc_bridge_hint_session, actual_duration_ns);
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
			// the GC starts, instead of the governor lagging ~200ms behind the ~10ms burst. Null (a
			// no-op) on platforms below API 36; resolved in create_gc_bridge_hint_session().
			if (gc_bridge_notify_workload_spike != nullptr) {
				gc_bridge_notify_workload_spike (gc_bridge_hint_session, /* cpu */ true, /* gpu */ false, "gc-bridge");
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
	// unconditionally; the ADPF entry points are resolved via dlopen/dlsym, so they now work on this
	// host too, and it still fails soft on devices without ADPF support.
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
