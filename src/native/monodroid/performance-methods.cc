#include <cstdlib>
#include <cstdio>
#include <cstring>

#include <inttypes.h>
#include <fcntl.h>

#include "android-system.hh"
#include "cppcompat.hh"
#include "logger.hh"
#include "monodroid-glue-internal.hh"
#include "performance-methods.hh"

using namespace xamarin::android::internal;

namespace {
	const char *jit_state_description (uint32_t status) noexcept
	{
		if ((status & MethodEventRecord::JitStateStarted) != MethodEventRecord::JitStateStarted) {
			return "never JIT-ed";
		}

		if ((status & MethodEventRecord::JitStateCompleted) != MethodEventRecord::JitStateCompleted) {
			return "started but not completed";
		}

		if ((status & MethodEventRecord::JitStateSuccess) == MethodEventRecord::JitStateSuccess) {
			return "success";
		}

		return "failure";
	}
}

void
MonodroidRuntime::dump_method_events ()
{
	if (!method_event_map) {
		return;
	}

	log_debug (LOG_ASSEMBLY, "Dumping method events");
	lock_guard<mutex> write_mutex { *method_event_map_write_lock.get () };

	mono_profiler_set_jit_begin_callback (profiler_handle, nullptr);
	mono_profiler_set_jit_done_callback (profiler_handle, nullptr);
	mono_profiler_set_jit_failed_callback (profiler_handle, nullptr);
	mono_profiler_set_method_begin_invoke_callback (profiler_handle, nullptr);
	mono_profiler_set_method_end_invoke_callback (profiler_handle, nullptr);

	switch (AndroidSystem::get_mono_aot_mode ()) {
		case MonoAotMode::MONO_AOT_MODE_INTERP:
		case MonoAotMode::MONO_AOT_MODE_INTERP_ONLY:
		case MonoAotMode::MONO_AOT_MODE_INTERP_LLVMONLY:
		case MonoAotMode::MONO_AOT_MODE_LLVMONLY_INTERP:
			mono_profiler_set_call_instrumentation_filter_callback (profiler_handle, nullptr);
			mono_profiler_set_method_enter_callback (profiler_handle, nullptr);
			mono_profiler_set_method_leave_callback (profiler_handle, nullptr);
			break;

		default:
			// Other AOT modes are ignored
			break;
	}

	std::unique_ptr<char> jit_log_path {Util::path_combine (AndroidSystem::override_dirs [0], "methods.xml")};
	Util::create_directory (AndroidSystem::override_dirs [0], 0755);
	int jit_log = open (jit_log_path.get (), O_CREAT | O_WRONLY | O_TRUNC | O_SYNC, 0644);
	if (jit_log < 0) {
		log_error (LOG_DEFAULT, "Failed to open '%s' for writing: %s", jit_log_path.get (), strerror (errno));
		return;
	}
	Util::set_world_accessable (jit_log_path.get ());

	dprintf (
		jit_log,
		R"(<?xml version="1.0" encoding="utf-8"?>)
<methods count="%zu">
)",
		method_event_map->size ()
	);

	for (auto item : *method_event_map.get ()) {
		MethodEventRecord &record = item.second;
		bool was_jited = (record.state & MethodEventRecord::JitStateStarted) == MethodEventRecord::JitStateStarted;
		timing_diff diff { record.jit_elapsed };

		dprintf (
			jit_log,
			R"(  <method name="%s" invocation_count="%)" PRIu64 R"(" jit_time="%li:%u::%u" jit_status="%s" />
)",
			record.method_name,
			record.invocation_count,
			was_jited ? static_cast<long>(diff.sec) : 0,
			was_jited ? diff.ms : 0,
			was_jited ? diff.ns : 0,
			jit_state_description (record.state)
		);
		::free (static_cast<void*>(const_cast<char *>(record.method_name)));
		record.method_name = nullptr;
	}

	method_event_map->clear ();
	dprintf (jit_log, "</methods>\n");
	close (jit_log);
}
