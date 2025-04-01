#pragma once

#include <atomic>
#include <cerrno>
#include <chrono>
#include <ctime>
#include <limits>
#include <memory>
#include <mutex>
#include <source_location>
#include <stack>
#include <string>
#include <string_view>
#include <thread>
#include <vector>

#include <runtime-base/logger.hh>
#include <runtime-base/startup-aware-lock.hh>
#include <runtime-base/util.hh>
#include <shared/log_types.hh>

namespace xamarin::android::exp {
	namespace chrono = std::chrono;

	using time_point = chrono::time_point<chrono::steady_clock, chrono::nanoseconds>;

	// Events should never change their assigned values and no values should be reused.
	// Values are used by the test runner to determine what measurement was taken.
	//
	enum class TimingEventKind : uint16_t
	{
		AssemblyDecompression     = 0,
		AssemblyLoad              = 1,
		AssemblyPreload           = 2,
		DebugStart                = 3,
		Init                      = 4,
		JavaToManaged             = 5,
		ManagedToJava             = 6,
		ManagedRuntimeInit        = 7,
		NativeToManagedTransition = 8,
		RuntimeConfigBlob         = 9,
		RuntimeRegister           = 10,
		TotalRuntimeInit          = 11,
		GetTimeOverhead           = 12,
		StartEndOverhead          = 13,

		Unspecified               = std::numeric_limits<uint16_t>::max (),
	};

	struct TimingEvent
	{
		bool       before_managed;
		time_point start;
		time_point end;
		TimingEventKind  kind;
		std::unique_ptr<std::string> more_info;
	};

	class FastTiming;
	extern FastTiming internal_timing;

	class FastTiming final
	{
		// Number of TimingEvent entries in the event vector allocated at the
		// time of class instantiation.  It's an arbitrary value, but it should
		// be large enough to not require any dynamic reallocation of memory at
		// the run time.
		static inline constexpr size_t INITIAL_EVENT_VECTOR_SIZE = 4096uz;

	protected:
		void configure_for_use () noexcept
		{
			events.reserve (INITIAL_EVENT_VECTOR_SIZE);
		}

	public:
		constexpr FastTiming () noexcept
		{}

		[[gnu::always_inline]]
		static auto enabled () noexcept -> bool
		{
			return is_enabled;
		}

		[[gnu::always_inline]]
		static auto is_bare_mode () noexcept -> bool
		{
			return
				(Logger::log_timing_categories() & LogTimingCategories::Bare) == LogTimingCategories::Bare ||
				(Logger::log_timing_categories() & LogTimingCategories::FastBare) == LogTimingCategories::FastBare;
		}

		[[gnu::always_inline]]
		static void initialize (bool log_immediately) noexcept
		{
			if (!Util::should_log (LOG_TIMING)) [[likely]] {
				return;
			}

			init_time.kind = TimingEventKind::Init;
			init_time.before_managed = true;
			init_time.start = get_time ();
			really_initialize (log_immediately);

			// It should really be done in a loop, but we're interested in **some** figure here,
			// doesn't have to be very accurate.
			start_end_event_time.kind = TimingEventKind::StartEndOverhead;
			start_end_event_time.before_managed = true;
			start_end_event_time.start = get_time ();
			internal_timing.start_event ();
			internal_timing.end_event (false /* uses_more_info */, true /* skip_log */);
			start_end_event_time.end = get_time ();

			// Same here, a rough figure is enough
			get_time_overhead.kind = TimingEventKind::GetTimeOverhead;
			get_time_overhead.before_managed = true;
			get_time_overhead.start = get_time ();
			time_point _ = get_time ();
			get_time_overhead.end = get_time ();

			init_time.end = get_time ();
			if (!immediate_logging) {
				return;
			}

			log (start_end_event_time, false /* skip_log_if_more_info_missing */);
			log (get_time_overhead, false /* skip_log_if_more_info_missing */);
			log (init_time, false /* skip_log_if_more_info_missing */);
		}

		//
		// Message format is as follows: <OPTIONAL_INDENT>[STAGE/EVENT] <MESSAGE>; elapsed s:ms::ns
		//
		//  STAGE is one of:
		//    0 - native init (before managed code runs)
		//    1 - managed code enabled
		//    2 - events summary (see the `dump()` function)
		//
		//  EVENT is one of:
		//    for stages 0 and 1, it's the value of the TimingEventKind member
		//    for stage 2 see the `dump()` function
		//
		// The [STAGE/EVENT] format is meant to help the test runner application, so that it can parse logcat without
		// having to be kept in sync with the actual wording used for the event message.
		//
		template<size_t BufferSize> [[gnu::always_inline]]
		static void format_and_log (TimingEvent const& event, time_point::duration const& interval, dynamic_local_string<BufferSize, char>& message, bool indent = false) noexcept
		{
			using namespace std::literals;

			constexpr auto INDENT = "  "sv;
			constexpr auto NATIVE_INIT_TAG = "[0/"sv;
			constexpr auto MANAGED_TAG = "[1/"sv;

			message.clear ();
			if (indent) {
				message.append (INDENT);
			}

			if (event.before_managed) {
				message.append (NATIVE_INIT_TAG);
			} else {
				message.append (MANAGED_TAG);
			}

			message.append (static_cast<uint32_t>(event.kind));
			message.append ("] "sv);

			append_event_kind_description (event.kind, message);
			if (event.more_info && !event.more_info->empty ()) {
				message.append (event.more_info->c_str (), event.more_info->length ());
			}

			constexpr auto COLON = ":"sv;
			constexpr auto TWO_COLONS = "::"sv;

			message.append ("; elapsed exp: "sv);
			message.append (static_cast<uint64_t>(interval / 1s));
			message.append (COLON);
			message.append (static_cast<uint64_t>(interval / 1ms));
			message.append (TWO_COLONS);
			message.append (static_cast<uint64_t>(interval / 1ns));

			log_write (LOG_TIMING, LogLevel::Info, message.get ());
		}

		template<size_t BufferSize> [[gnu::always_inline]]
		static void format_and_log (TimingEvent const& event, dynamic_local_string<BufferSize, char>& message, uint64_t& total_ns, bool indent = false) noexcept
		{
			using namespace std::literals;

			auto interval = event.end - event.start;
			total_ns = static_cast<uint64_t>(interval / 1ns);
			format_and_log (event, interval, message, indent);
		}

		[[gnu::always_inline]]
		static void format_and_log (TimingEvent const& event) noexcept
		{
			// `message` isn't used here, it is passed to `format_and_log` so that the `dump()` function can
			// be slightly more efficient when dumping the event buffer.
			dynamic_local_string<Constants::MAX_LOGCAT_MESSAGE_LENGTH, char> message;
			format_and_log (event, event.end - event.start, message);
		}

		[[gnu::always_inline]]
		static void log (TimingEvent const& event, bool skip_log_if_more_info_missing) noexcept
		{
			if (!immediate_logging) {
				return;
			}

			if (skip_log_if_more_info_missing && (!event.more_info || event.more_info->empty ())) {
				return;
			}

			format_and_log (event);
		}

		// std::vector<T> isn't used in a conventional manner here. We treat it as if it was a standard array and we
		// don't take advantage of any emplacement functionality, merely using vector's ability to resize itself when
		// needed.  The reason for this is speed - we can atomically increase index into the array and relatively
		// quickly check whether it's within the boundaries.  We can then safely use thus indexed element without
		// worrying about concurrency.  Emplacing a new element in the vector would require holding the mutex, something
		// that's fairly costly and has unpredictable effect on time spent acquiring and holding the lock (the OS can
		// preempt us at this point)
		[[gnu::always_inline]]
		void start_event (TimingEventKind kind = TimingEventKind::Unspecified) noexcept
		{
			size_t index = next_event_index.fetch_add (1);

			if (index >= events.capacity ()) [[unlikely]] {
				StartupAwareLock lock (event_vector_realloc_mutex);
				if (index >= events.size ()) { // don't increase unnecessarily, if another thread has already done that
					// Double the vector size. We should, in theory, check for integer overflow here, but it's more
					// likely we'll run out of memory way, way, way before that happens
					size_t old_size = events.capacity ();
					events.reserve (old_size << 1);
					log_warn (LOG_TIMING, "Reallocated timing event buffer from {} to {}", old_size, events.size ());
				}
			}

			open_sequences.push (index);
			TimingEvent &ev = events[index];
			ev.start = get_time ();
			ev.kind = kind;
			ev.before_managed = MonodroidState::is_startup_in_progress ();
			ev.more_info = nullptr;
		}

		// The return value is necessary only if one needs to add some extra information to the event, otherwise
		// it can be ignored.
		[[gnu::always_inline]]
		auto end_event (bool uses_more_info = false, bool skip_log = false) noexcept -> size_t
		{
			if (open_sequences.empty ()) [[unlikely]] {
				log_warn (LOG_TIMING, "FastTiming::end_event called without prior FastTiming::start_event called");
				return 0;
			}

			size_t index = open_sequences.top ();
			open_sequences.pop ();

			if (!is_valid_event_index (index)) [[unlikely]] {
				return 0;
			}

			events[index].end = get_time ();
			if (!skip_log) [[likely]] {
				log (events[index], uses_more_info /* skip_log_if_more_info_missing */);
			}
			return index;
		}

	private:
		template<size_t BufferSize> [[gnu::always_inline]]
		static void append_event_kind_description (TimingEventKind kind, dynamic_local_string<BufferSize, char>& message) noexcept
		{
			switch (kind) {
				case TimingEventKind::AssemblyDecompression: {
					constexpr auto desc = "LZ4 decompression time for "sv;
					message.append (desc);
					return;
				}

				case TimingEventKind::AssemblyLoad: {
					constexpr auto desc = "Assembly load"sv;
					message.append (desc);
					return;
				}

				case TimingEventKind::AssemblyPreload: {
					constexpr auto desc = "Finished preloading, number of loaded assemblies: "sv;
					message.append (desc);
					return;
				}

				case TimingEventKind::DebugStart: {
					constexpr auto desc = "Debug::start_debugging_and_profiling: end"sv;
					message.append (desc);
					return;
				}

				case TimingEventKind::Init: {
					constexpr auto desc = "XATiming: init time"sv;
					message.append (desc);
					return;
				}

				case TimingEventKind::JavaToManaged: {
					constexpr auto desc = "Typemap.java_to_managed: end, total time"sv;
					message.append (desc);
					return;
				}

				case TimingEventKind::ManagedToJava: {
					constexpr auto desc = "Typemap.managed_to_java: end, total time"sv;
					message.append (desc);
					return;
				}

				case TimingEventKind::ManagedRuntimeInit: {
					constexpr auto desc = "Runtime.init: Mono runtime init"sv;
					message.append (desc);
					return;
				}

				case TimingEventKind::NativeToManagedTransition: {
					constexpr auto desc = "Runtime.init: end native-to-managed transition"sv;
					message.append (desc);
					return;
				}

				case TimingEventKind::RuntimeConfigBlob: {
					constexpr auto desc = "Register runtimeconfig binary blob"sv;
					message.append (desc);
					return;
				}

				case TimingEventKind::RuntimeRegister: {
					constexpr auto desc = "Runtime.register: end time. Registered type: "sv;
					message.append (desc);
					return;
				}

				case TimingEventKind::TotalRuntimeInit: {
					constexpr auto desc = "Runtime.init: end, total time"sv;
					message.append (desc);
					return;
				}

				case TimingEventKind::GetTimeOverhead: {
					constexpr auto desc = "clock_gettime overhead"sv;
					message.append (desc);
					return;
				}

				case TimingEventKind::StartEndOverhead: {
					constexpr auto desc = "start+end event overhead"sv;
					message.append (desc);
					return;
				}

				default: {
					constexpr auto desc = "Unknown timing event"sv;
					message.append (desc);
					return;
				}
			}
		}

	private:
		static void really_initialize (bool log_immediately) noexcept;
		static void* timing_signal_thread (void *arg) noexcept;

		// We cheat a bit here, by avoiding a call to libc++ code that performs the same action.
		// We can do it because we know our target platform.
		[[gnu::always_inline]]
		static auto get_time () noexcept -> time_point
		{
			struct timespec t;
			if (clock_gettime (CLOCK_MONOTONIC_RAW, &t) != 0) [[unlikely]] {
				log_warn (LOG_TIMING, "clock_gettime failed for CLOCK_MONOTONIC_RAW: {}", optional_string (strerror (errno)));
				return {}; // Results will be nonsensical, but no point in aborting the app
			}
			return time_point (chrono::seconds (t.tv_sec) + chrono::nanoseconds (t.tv_nsec));
		}

		[[gnu::always_inline, nodiscard]]
		auto is_valid_event_index (size_t index, std::source_location sloc = std::source_location::current ()) const noexcept -> bool
		{
			if (index >= events.capacity ()) [[unlikely]] {
				log_warn (LOG_TIMING, "Invalid event index passed to method '{}'", sloc.function_name ());
				return false;
			}

			return true;
		}

	private:
		std::atomic_size_t next_event_index = 0uz;
		std::mutex event_vector_realloc_mutex;
		std::vector<TimingEvent> events;

		static inline thread_local std::stack<size_t> open_sequences;
		static inline bool is_enabled = false;
		static inline bool immediate_logging = false;
		static inline TimingEvent init_time{};
		static inline TimingEvent start_end_event_time{};
		static inline TimingEvent get_time_overhead{};
	};
}
