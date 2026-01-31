#include <host/gc-bridge.hh>
#include <host/os-bridge.hh>
#include <host/host.hh>
#include <runtime-base/util.hh>
#include <shared/helpers.hh>

using namespace xamarin::android;

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

void GCBridge::initialize_on_runtime_init ([[maybe_unused]] JNIEnv *env, [[maybe_unused]] jclass runtimeClass) noexcept
{
	// Bridge processing is now done in managed code (C#)
	// No native initialization is needed
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

	shared_args.store (args);
	shared_args_semaphore.release ();
}

[[gnu::always_inline]]
void GCBridge::log_mark_cross_references_args_if_enabled (MarkCrossReferencesArgs *args) noexcept
{
	if (!Logger::gc_spew_enabled ()) [[likely]] {
		return;
	}

	log_info (LOG_GC, "cross references callback invoked with {} sccs and {} xrefs.", args->ComponentCount, args->CrossReferenceCount);

	JNIEnv *env = OSBridge::ensure_jnienv ();
	
	for (size_t i = 0; i < args->ComponentCount; ++i) {
		const StronglyConnectedComponent &scc = args->Components [i];
		log_info (LOG_GC, "group {} with {} objects", i, scc.Count);
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
		log_info_nocheck_fmt (LOG_GC, "xref [{}] {} -> {}", i, source_index, dest_index);
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
		log_info (LOG_GC, "gref {:#x} [{}]", reinterpret_cast<intptr_t> (handle), class_name);
		free (class_name);
	} else {
		log_info (LOG_GC, "gref {:#x} [unknown class]", reinterpret_cast<intptr_t> (handle));
	}
}
