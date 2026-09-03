#pragma once

#include <atomic>
#include <cerrno>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <ctime>
#include <limits>
#include <string_view>
#include <thread>

#if defined(XA_HOST_MONOVM)
#include <runtime-base/shared-constants.hh>

using Constants = xamarin::android::internal::SharedConstants;
using namespace xamarin::android::internal;
#else
#include <constants.hh>
#endif

#include <runtime-base/logger.hh>
#include <runtime-base/monodroid-state.hh>
#include <runtime-base/util.hh>
#include <shared/cpp-util.hh>
#include <shared/helpers.hh>
#include <shared/log_types.hh>

namespace xamarin::android {
	inline constexpr uint64_t NANOSECONDS_PER_MILLISECOND = 1000000ull;
	inline constexpr uint64_t NANOSECONDS_PER_SECOND = 1000000000ull;

	// A monotonic point in time, or an interval between two such points, in nanoseconds.
	using time_point = uint64_t;

	// Splits an interval into the components used by the timing output format. Note that `seconds`
	// and `milliseconds` are both totals for the *entire* interval rather than a breakdown of it:
	// an interval of 1.5s has `seconds == 1` and `milliseconds == 1500`, and both are printed. This
	// is what `duration_cast<seconds>` and `duration_cast<milliseconds>` used to return and it must
	// not be "corrected" to milliseconds-within-the-second, because the output format is consumed by
	// performance measuring utilities. Only `nanoseconds` is a remainder, within the last millisecond.
	struct time_interval
	{
		unsigned long long seconds;
		unsigned long long milliseconds;
		unsigned long long nanoseconds;

		explicit constexpr time_interval (time_point interval) noexcept
			: seconds { interval / NANOSECONDS_PER_SECOND },
			  milliseconds { interval / NANOSECONDS_PER_MILLISECOND },
			  nanoseconds { interval % NANOSECONDS_PER_MILLISECOND }
		{}
	};

	static_assert (
		time_interval { 1500000123ull }.seconds == 1ull &&
		time_interval { 1500000123ull }.milliseconds == 1500ull &&
		time_interval { 1500000123ull }.nanoseconds == 123ull,
		"The timing output for 1500000123ns must remain 1:1500::123"
	);

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
		FunctionCall              = 14,

		Unspecified               = std::numeric_limits<uint16_t>::max (),
	};

	struct TimingEvent
	{
		time_point                   start;
		time_point                   end;
		char                        *more_info = nullptr;
		TimingEvent                 *previous_open_event = nullptr;
		TimingEventKind              kind;
		bool                         before_managed;
		bool                         complete = false;
	};

	class FastTiming;
	extern FastTiming internal_timing;

	class FastTiming final
	{
		// Number of TimingEvent entries in each allocation.  It's an arbitrary
		// value large enough to avoid allocating additional chunks during
		// normal application startup.
		static constexpr size_t EVENT_CHUNK_SIZE = 4096uz;

		struct TimingEventChunk
		{
			TimingEvent events [EVENT_CHUNK_SIZE];
			TimingEventChunk *next = nullptr;
		};

		// defaults
		static constexpr bool default_fast_timing_enabled = false;
		static constexpr bool default_log_to_file = false;
		static constexpr size_t default_duration_milliseconds = 1500;
		static constexpr std::string_view default_timing_file_name { "timing.txt" };

		// Parameters for the runtime timing property
		static constexpr std::string_view OPT_DURATION      { "duration=" };
		static constexpr std::string_view OPT_FILE_NAME     { "filename=" };
		static constexpr std::string_view OPT_TO_FILE       { "to-file" };

		// Enough to hold any value the `debug.mono.timing` property can carry, `PROP_VALUE_MAX` is 92.
		static constexpr size_t MAX_TIMING_FILE_NAME_SIZE = 128uz;

	protected:
		void configure_for_use () noexcept
		{
			first_event_chunk = allocate_event_chunk ();
		}

	public:
		constexpr FastTiming () noexcept
		{}

		~FastTiming ()
		{
			TimingEventChunk *chunk = first_event_chunk;
			while (chunk != nullptr) {
				TimingEventChunk *next = chunk->next;
				for (TimingEvent &event : chunk->events) {
					std::free (event.more_info);
				}
				std::free (chunk);
				chunk = next;
			}
		}

		[[gnu::always_inline]]
		static auto enabled () noexcept -> bool
		{
			return __atomic_load_n (&is_enabled, __ATOMIC_ACQUIRE);
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
			__atomic_store_n (&is_enabled, true, __ATOMIC_RELEASE);
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
		[[gnu::always_inline]]
		static auto event_duration_ns (TimingEvent const& event) noexcept -> uint64_t
		{
			return event.end - event.start;
		}

		// Returns the message length excluding NUL, or the negative required capacity including NUL.
		static auto format_message (TimingEvent const& event, char *buffer, size_t buffer_size, bool indent) noexcept -> ssize_t
		{
			time_interval interval { event.end - event.start };
			int length = snprintf (
				buffer,
				buffer_size,
				"%s%s%u] %s%s; elapsed: %llu:%llu::%llu",
				indent ? "  " : "",
				event.before_managed ? "[0/" : "[1/",
				static_cast<unsigned int>(event.kind),
				event_kind_description (event.kind),
				event.more_info == nullptr ? "" : event.more_info,
				interval.seconds,
				interval.milliseconds,
				interval.nanoseconds
			);
			if (length < 0) {
				if (buffer != nullptr && buffer_size > 0uz) {
					buffer [0] = '\0';
				}
				return 0;
			}

			size_t required_capacity = static_cast<size_t>(length) + 1uz;
			if (buffer == nullptr || buffer_size < required_capacity) {
				return -static_cast<ssize_t>(required_capacity);
			}

			return static_cast<ssize_t>(length);
		}

		// Formats the event message into `stack_buffer`, falling back to a heap buffer when the message
		// doesn't fit.  The returned pointer must be passed to `std::free` if it differs from `stack_buffer`.
		static auto build_message (TimingEvent const& event, char *stack_buffer, size_t stack_buffer_size, size_t *message_length, bool indent) noexcept -> char*
		{
			ssize_t result = format_message (event, stack_buffer, stack_buffer_size, indent);
			if (result < 0) {
				size_t required_capacity = static_cast<size_t>(-result);
				char *heap_buffer = static_cast<char*> (std::malloc (required_capacity));
				abort_unless (heap_buffer != nullptr, "Failed to allocate the timing event message");
				result = format_message (event, heap_buffer, required_capacity, indent);
				abort_unless (result >= 0, "Failed to format the timing event message using the required capacity");
				if (message_length != nullptr) {
					*message_length = static_cast<size_t>(result);
				}
				return heap_buffer;
			}

			if (message_length != nullptr) {
				*message_length = static_cast<size_t>(result);
			}
			return stack_buffer;
		}

		[[gnu::always_inline]]
		static void format_and_log (TimingEvent const& event, bool indent = false) noexcept
		{
			char stack_buffer [Constants::MAX_LOGCAT_MESSAGE_LENGTH];
			char *message = build_message (event, stack_buffer, sizeof (stack_buffer), nullptr, indent);
			log_write (LOG_TIMING, LogLevel::Info, message);
			if (message != stack_buffer) {
				std::free (message);
			}
		}

		[[gnu::always_inline]]
		static void log (TimingEvent const& event, bool skip_log_if_more_info_missing) noexcept
		{
			if (!immediate_logging) {
				return;
			}

			if (skip_log_if_more_info_missing && (event.more_info == nullptr || event.more_info[0] == '\0')) {
				return;
			}

			format_and_log (event);
		}

		[[gnu::always_inline]]
		void start_event (TimingEventKind kind = TimingEventKind::Unspecified) noexcept
		{
			size_t index = next_event_index.fetch_add (1);
			TimingEvent &ev = get_event (index);
			ev.start = get_time ();
			ev.kind = kind;
			ev.before_managed = MonodroidState::is_startup_in_progress ();
			push_sequence_event (&ev);
		}

		// If `uses_more_info` is `true`, the caller **MUST** call `add_more_info`, since the
		// timing sequence number will **NOT** be popped off the stack by this call!
		[[gnu::always_inline]]
		void end_event (bool uses_more_info = false, bool skip_log = false) noexcept
		{
			TimingEvent *event = uses_more_info ? get_sequence_event () : pop_sequence_event ();
			if (event == nullptr) [[unlikely]] {
				log_warnf (LOG_TIMING, "FastTiming::end_event called without prior FastTiming::start_event called");
				return;
			}

			event->end = get_time ();
			if (!uses_more_info) [[likely]] {
				__atomic_store_n (&event->complete, true, __ATOMIC_RELEASE);
			}
			if (!skip_log) [[likely]] {
				log (*event, uses_more_info /* skip_log_if_more_info_missing */);
			}
		}

		[[gnu::always_inline]]
		void add_more_info (const char *str, size_t length) noexcept
		{
			store_more_info (duplicate_more_info (std::string_view { str, length }, {}));
		}

		// Builds the message from two parts, so that its exact length is known up front and the
		// caller doesn't need a temporary buffer that the message might not fit into.
		[[gnu::always_inline]]
		void add_more_info (std::string_view const& first, std::string_view const& second) noexcept
		{
			store_more_info (duplicate_more_info (first, second));
		}

		[[gnu::always_inline]]
		void add_more_info (const char* str) noexcept
		{
			add_more_info (str, strlen (str));
		}

		[[gnu::always_inline]]
		void add_more_info (std::string_view const& str) noexcept
		{
			add_more_info (str.data (), str.length ());
		}

		void dump () noexcept;

		// The `time_call` function declarations look definitely funky, but it all boils down to
		// detecting whether the `F` object is a functor returning `void` or not and, depending on that,
		// enabling one overload or another (SFINAE: https://en.wikipedia.org/wiki/Substitution_failure_is_not_an_error).
		// The "true" portion of `std::enable_if_t` sets the return type of the wrapper to match `F`
		template<typename F, typename... Args> [[gnu::always_inline]]
		std::enable_if_t<!std::is_void_v<decltype(std::declval<F>()(std::declval<Args>()...))>, decltype(std::declval<F>()(std::declval<Args>()...))>
		static time_call (std::string_view const& name, F&& fn, Args... args) noexcept
		{
			if (!enabled ()) [[likely]] {
				return fn (std::forward<Args>(args)...);
			}

			internal_timing.start_event (TimingEventKind::FunctionCall);
			auto ret = fn (std::forward<Args>(args)...);
			internal_timing.end_event (true /* uses_more_info */);
			internal_timing.add_more_info (name);
			return ret;
		}

		template<typename F, typename... Args> [[gnu::always_inline]]
		std::enable_if_t<std::is_void_v<decltype(std::declval<F>()(std::declval<Args>()...))>, void>
		static time_call (std::string_view const& name, F&& fn, Args... args) noexcept
		{
			if (!enabled ()) [[likely]] {
				fn (std::forward<Args>(args)...);
				return;
			}

			internal_timing.start_event (TimingEventKind::FunctionCall);
			fn (std::forward<Args>(args)...);
			internal_timing.end_event (true /* uses_more_info */);
			internal_timing.add_more_info (name);
		}

		// We cheat a bit here, by avoiding a call to libc++ code that performs the same action.
		// We can do it because we know our target platform.
		[[gnu::always_inline]]
		static auto get_time () noexcept -> time_point
		{
			struct timespec t;
			if (clock_gettime (CLOCK_MONOTONIC, &t) != 0) [[unlikely]] {
				log_warnf (LOG_TIMING, "clock_gettime failed for CLOCK_MONOTONIC: %s", optional_string (strerror (errno)));
				return {}; // Results will be nonsensical, but no point in aborting the app
			}
			return (static_cast<time_point>(t.tv_sec) * NANOSECONDS_PER_SECOND) + static_cast<time_point>(t.tv_nsec);
		}

	private:
		// Writes a single output line.  `output` is the file passed to `dump`, or `nullptr` when
		// the caller doesn't write to a file.
		using LineWriter = void (*) (FILE *output, std::string_view const& line);

		bool no_events_logged (size_t entries) noexcept;
		void dump_to_logcat (size_t entries) noexcept;
		void dump_to_file (size_t entries) noexcept;
		void dump (size_t entries, bool indent, LineWriter line_writer, FILE *output) noexcept;

		// Returns a NUL-terminated copy of `first` and `second` concatenated, or `nullptr` if it
		// cannot be allocated. Timing is a diagnostic facility, so a failure here only costs us the
		// extra information attached to a single event and must not bring the application down.
		[[gnu::always_inline]]
		static auto duplicate_more_info (std::string_view const& first, std::string_view const& second) noexcept -> char*
		{
			size_t length = Helpers::add_with_overflow_check<size_t> (first.length (), second.length ());
			auto *more_info = static_cast<char*> (std::malloc (Helpers::add_with_overflow_check<size_t> (length, 1uz)));
			if (more_info == nullptr) [[unlikely]] {
				return nullptr;
			}

			// `memcpy` must not be called with a `nullptr` source, not even for a zero length, and an
			// empty `std::string_view` is allowed to have a `nullptr` data pointer.
			if (!first.empty ()) {
				std::memcpy (more_info, first.data (), first.length ());
			}

			if (!second.empty ()) {
				std::memcpy (more_info + first.length (), second.data (), second.length ());
			}

			more_info[length] = '\0';

			return more_info;
		}

		// Takes ownership of `more_info`.
		[[gnu::always_inline]]
		void store_more_info (char *more_info) noexcept
		{
			TimingEvent *event = pop_sequence_event ();
			if (event == nullptr) [[unlikely]] {
				std::free (more_info);
				log_warnf (LOG_TIMING, "FastTiming::add_more_info called without prior FastTiming::start_event called");
				return;
			}

			event->more_info = more_info;
			__atomic_store_n (&event->complete, true, __ATOMIC_RELEASE);
			log (*event, false /* skip_log_if_more_info_missing */);
		}

		[[gnu::always_inline]]
		static void push_sequence_event (TimingEvent *event) noexcept
		{
			event->previous_open_event = open_sequence;
			open_sequence = event;
		}

		[[gnu::always_inline]]
		static auto get_sequence_event () noexcept -> TimingEvent*
		{
			return open_sequence;
		}

		[[gnu::always_inline]]
		static auto pop_sequence_event () noexcept -> TimingEvent*
		{
			TimingEvent *event = open_sequence;
			if (event == nullptr) [[unlikely]] {
				return nullptr;
			}

			open_sequence = event->previous_open_event;
			event->previous_open_event = nullptr;

			return event;
		}

		[[gnu::always_inline]]
		static auto event_kind_description (TimingEventKind kind) noexcept -> const char*
		{
			switch (kind) {
				case TimingEventKind::AssemblyDecompression:
					return "Zstd decompression time for ";

				case TimingEventKind::AssemblyLoad:
					return "Assembly load for ";

				case TimingEventKind::AssemblyPreload:
					return "Finished preloading, number of loaded assemblies: ";

				case TimingEventKind::DebugStart:
					return "Debug::start_debugging_and_profiling: end";

				case TimingEventKind::Init:
					return "XATiming: init time";

				case TimingEventKind::JavaToManaged:
					return "Typemap.java_to_managed: end, total time";

				case TimingEventKind::ManagedToJava:
					return "Typemap.managed_to_java: end, total time";

				case TimingEventKind::ManagedRuntimeInit:
					return "Runtime.init: Managed runtime init";

				case TimingEventKind::NativeToManagedTransition:
					return "Runtime.init: end native-to-managed transition";

				case TimingEventKind::RuntimeConfigBlob:
					return "Register runtimeconfig binary blob";

				case TimingEventKind::RuntimeRegister:
					return "Runtime.register: end time. Registered type: ";

				case TimingEventKind::TotalRuntimeInit:
					return "Runtime.init: end, total time";

				case TimingEventKind::GetTimeOverhead:
					return "clock_gettime overhead";

				case TimingEventKind::StartEndOverhead:
					return "start+end event overhead";

				case TimingEventKind::FunctionCall:
					return "function call: ";

				case TimingEventKind::Unspecified:
					return "unspecified event type: ";
			}

			log_warnf (
				LOG_TIMING,
				"Unknown event kind '%u' logged",
				static_cast<unsigned int>(kind)
			);
			return "unknown event kind";
		}

	private:
		// Event chunks are chained together and the events in them are handed out as references that
		// stay valid until the process exits, so a chunk must never move. Allocating them with
		// `calloc` avoids `operator new` and, with it, a dependency on `libc++`; zero-filling matches
		// the default member initializers of `TimingEvent`.
		static auto allocate_event_chunk () noexcept -> TimingEventChunk*
		{
			auto *chunk = static_cast<TimingEventChunk*> (std::calloc (1uz, sizeof (TimingEventChunk)));
			if (chunk == nullptr) [[unlikely]] {
				Helpers::abort_application (LOG_TIMING, "Unable to allocate memory for timing events");
			}

			return chunk;
		}

		void parse_options (const char *options) noexcept;
		static void really_initialize (bool log_immediately) noexcept;

		[[gnu::always_inline]]
		auto get_event (size_t index) noexcept -> TimingEvent&
		{
			size_t chunk_index = index / EVENT_CHUNK_SIZE;
			size_t current_chunk_index = cached_event_chunk_index;
			TimingEventChunk *chunk = cached_event_chunk;
			if (chunk == nullptr || chunk_index < current_chunk_index) {
				current_chunk_index = 0uz;
				chunk = first_event_chunk;
			}

			for (size_t i = current_chunk_index; i < chunk_index; ++i) {
				TimingEventChunk *next = __atomic_load_n (&chunk->next, __ATOMIC_ACQUIRE);
				if (next == nullptr) [[unlikely]] {
					TimingEventChunk *new_chunk = allocate_event_chunk ();
					if (__atomic_compare_exchange_n (
							&chunk->next,
							&next,
							new_chunk,
							false /* weak */,
							__ATOMIC_RELEASE,
							__ATOMIC_ACQUIRE
						)) {
						next = new_chunk;
						log_warnf (
							LOG_TIMING,
							"Allocated timing event buffer from %zu to %zu",
							(i + 1uz) * EVENT_CHUNK_SIZE,
							(i + 2uz) * EVENT_CHUNK_SIZE
						);
					} else {
						std::free (new_chunk);
					}
				}
				chunk = next;
			}

			cached_event_chunk = chunk;
			cached_event_chunk_index = chunk_index;
			return chunk->events[index % EVENT_CHUNK_SIZE];
		}

	private:
		std::atomic_size_t next_event_index = 0uz;
		TimingEventChunk *first_event_chunk = nullptr;
		// The name is read from the `debug.mono.timing` system property, whose whole value is limited
		// to `PROP_VALUE_MAX` (92) bytes, so a fixed buffer is always large enough. Keeping it inline
		// also keeps `FastTiming` constant-initialized, so the global instance needs no guard variable.
		char output_file_name[MAX_TIMING_FILE_NAME_SIZE] = {};

		static inline thread_local TimingEvent *open_sequence = nullptr;
		static inline thread_local TimingEventChunk *cached_event_chunk = nullptr;
		static inline thread_local size_t cached_event_chunk_index = 0uz;
		static inline bool is_enabled = false;
		static inline bool immediate_logging = false;
		static inline bool log_to_file = default_log_to_file;
		static inline size_t duration_ms = default_duration_milliseconds;
		static inline TimingEvent init_time{};
		static inline TimingEvent start_end_event_time{};
		static inline TimingEvent get_time_overhead{};
	};
}
