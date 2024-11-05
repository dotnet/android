#include <cstdlib>
#include <inttypes.h>

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

	fprintf (
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

		fprintf (
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
	fprintf (jit_log, "</methods>\n");
	fflush (jit_log);
	fclose (jit_log);
	jit_log = nullptr;
}
