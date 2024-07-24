#if !defined(MONODROID_PROFILING_HH)
#define MONODROID_PROFILING_HH

#include <mono/metadata/profiler.h>

namespace xamarin::android {
	// Keep the values ordered in the order of increasing verbosity
	enum class ProfilingMode
	{
		Bare,
		Extended,
		Verbose,
		Extreme,
	};

	class MonodroidProfiling
	{
	public:
		static void prof_assembly_loading (MonoProfiler *prof, MonoAssembly *assembly) noexcept;
		static void prof_assembly_loaded (MonoProfiler *prof, MonoAssembly *assembly) noexcept;
		static void prof_image_loading (MonoProfiler *prof, MonoImage *assembly) noexcept;
		static void prof_image_loaded (MonoProfiler *prof, MonoImage *assembly) noexcept;
		static void prof_class_loading (MonoProfiler *prof, MonoClass *klass) noexcept;
		static void prof_class_loaded (MonoProfiler *prof, MonoClass *klass) noexcept;
		static void prof_vtable_loading (MonoProfiler *prof, MonoVTable *vtable) noexcept;
		static void prof_vtable_loaded (MonoProfiler *prof, MonoVTable *vtable) noexcept;
		static void prof_method_enter (MonoProfiler *prof, MonoMethod *method, MonoProfilerCallContext *context) noexcept;
		static void prof_method_leave (MonoProfiler *prof, MonoMethod *method, MonoProfilerCallContext *context) noexcept;
		static void prof_method_begin_invoke (MonoProfiler *prof, MonoMethod *method) noexcept;
		static void prof_method_end_invoke (MonoProfiler *prof, MonoMethod *method) noexcept;
		static void prof_monitor_contention (MonoProfiler *prof, MonoObject *object) noexcept;
		static void prof_monitor_acquired (MonoProfiler *prof, MonoObject *object) noexcept;
	};
}
#endif // MONODROID_PROFILING_HH
