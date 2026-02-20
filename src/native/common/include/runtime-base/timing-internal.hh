#pragma once

#include <atomic>
#include <cerrno>
#include <chrono>
#include <ctime>
#include <expected>
#include <functional>
#include <limits>
#include <memory>
#include <mutex>
#include <source_location>
#include <stack>
#include <string>
#include <string_view>
#include <thread>
#include <vector>

#if defined(XA_HOST_MONOVM)
#include <runtime-base/shared-constants.hh>

using Constants = xamarin::android::internal::SharedConstants;
using namespace xamarin::android::internal;
#else
#include <constants.hh>
#endif

#include <runtime-base/logger.hh>
#include <runtime-base/startup-aware-lock.hh>
#include <runtime-base/util.hh>
#include <shared/log_types.hh>

namespace xamarin::android {
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
		FunctionCall              = 14,

		Unspecified               = std::numeric_limits<uint16_t>::max (),
	};

	struct TimingEvent
	{
		bool                         before_managed;
		time_point                   start;
		time_point                   end;
		TimingEventKind              kind;
		std::string                 *more_info = nullptr;
	};

	class FastTiming;
	extern FastTiming internal_timing;

	class FastTiming final
	{
#if defined(XA_HOST_MONOVM)
		using mutex = xamarin::android::mutex;
#else
		using mutex = std::mutex;
#endif
		enum class SequenceError
		{
			EmptyStack,
			InvalidIndex,
		};

		// Number of TimingEvent entries in the event vector allocated at the
		// time of class instantiation.  It's an arbitrary value, but it should
		// be large enough to not require any dynamic reallocation of memory at
		// the run time.
		static constexpr size_t INITIAL_EVENT_VECTOR_SIZE = 4096uz;

		// defaults
		static constexpr bool default_fast_timing_enabled = false;
		static constexpr bool default_log_to_file = false;
		static constexpr size_t default_duration_milliseconds = 1500;
		static constexpr std::string_view default_timing_file_name { "timing.txt" };

		// Parameters for the `debug.mono.timing` property
		static constexpr std::string_view OPT_DURATION      { "duration=" };
		static constexpr std::string_view OPT_FILE_NAME     { "filename=" };
		static constexpr std::string_view OPT_TO_FILE       { "to-file" };

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
		static auto format_message (TimingEvent const& event, dynamic_local_string<BufferSize, char>& message, bool indent = false) noexcept -> uint64_t
		{
			using namespace std::literals;

			constexpr auto INDENT          = "  "sv;
			constexpr auto NATIVE_INIT_TAG = "[0/"sv;
			constexpr auto MANAGED_TAG     = "[1/"sv;

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
			if (event.more_info != nullptr && !event.more_info->empty ()) {
				message.append (event.more_info->c_str (), event.more_info->length ());
			}

			auto interval = event.end - event.start; // nanoseconds
			message.append ("; elapsed: "sv);
			message.append (static_cast<uint64_t>((chrono::duration_cast<chrono::seconds>(interval).count ())));
			message.append (":"sv);
			message.append (static_cast<uint64_t>((chrono::duration_cast<chrono::milliseconds>(interval)).count ()));
			message.append ("::"sv);
			message.append (static_cast<uint64_t>((interval % 1ms).count ()));

			return static_cast<uint64_t>(interval.count ());
		}

		[[gnu::always_inline]]
		static void format_and_log (TimingEvent const& event, bool indent = false) noexcept
		{
			// `message` isn't used here, it is passed to `format_and_log` so that the `dump()` function can
			// be slightly more efficient when dumping the event buffer.
			dynamic_local_string<Constants::MAX_LOGCAT_MESSAGE_LENGTH, char> message;
			format_message (event, message, indent);
			log_write (LOG_TIMING, LogLevel::Info, message.get ());
		}

		[[gnu::always_inline]]
		static void log (TimingEvent const& event, bool skip_log_if_more_info_missing) noexcept
		{
			if (!immediate_logging) {
				return;
			}

			if (skip_log_if_more_info_missing && (event.more_info == nullptr || event.more_info->empty ())) {
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
					log_warn (
						LOG_TIMING,
#if defined (XA_HOST_NATIVEAOT)
						"Reallocated timing event buffer from %z to %z",
#else
						"Reallocated timing event buffer from {} to {}"sv,
#endif
						old_size,
						events.size ()
					);
				}
			}

			open_sequences.push (index);
			TimingEvent &ev = events[index];
			ev.start = get_time ();
			ev.kind = kind;
			ev.before_managed = MonodroidState::is_startup_in_progress ();
			ev.more_info = nullptr;
		}

		// If `uses_more_info` is `true`, the caller **MUST** call `add_more_info`, since the
		// timing sequence number will **NOT** be popped off the stack by this call!
		[[gnu::always_inline]]
		void end_event (bool uses_more_info = false, bool skip_log = false) noexcept
		{
			std::expected<size_t, SequenceError> index;
			if (!uses_more_info) [[likely]] {
				index = pop_valid_sequence_index ();
			} else {
				index = get_valid_sequence_index ();
			}

			if (!index.has_value ()) [[unlikely]] {
				log_warn (LOG_TIMING, "FastTiming::end_event called without prior FastTiming::start_event called"sv);
				return;
			}

			events[*index].end = get_time ();
			if (!skip_log) [[likely]] {
				log (events[*index], uses_more_info /* skip_log_if_more_info_missing */);
			}
		}

		template<size_t MaxStackSize, typename TStorage, typename TChar = char>
		[[gnu::always_inline]]
		void add_more_info (string_base<MaxStackSize, TStorage, TChar> const& str) noexcept
		{
			auto index = pop_valid_sequence_index ();
			if (!index.has_value ()) [[unlikely]] {
				log_warn (LOG_TIMING, "FastTiming::add_more_info called without prior FastTiming::start_event called"sv);
				return;
			}

			events[*index].more_info = new std::string (str.get (), str.length ());
			log (events[*index], false /* skip_log_if_more_info_missing */);
		}

		[[gnu::always_inline]]
		void add_more_info (const char* str) noexcept
		{
			auto index = pop_valid_sequence_index ();
			if (!index.has_value ()) [[unlikely]] {
				log_warn (LOG_TIMING, "FastTiming::add_more_info called without prior FastTiming::start_event called"sv);
				return;
			}

			events[*index].more_info = new std::string (str);
			log (events[*index], false /* skip_log_if_more_info_missing */);
		}

		[[gnu::always_inline]]
		void add_more_info (std::string_view const& str) noexcept
		{
			auto index = pop_valid_sequence_index ();
			if (!index.has_value ()) [[unlikely]] {
				log_warn (LOG_TIMING, "FastTiming::add_more_info called without prior FastTiming::start_event called"sv);
				return;
			}

			events[*index].more_info = new std::string (str);
			log (events[*index], false /* skip_log_if_more_info_missing */);
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
			if (!is_enabled) [[likely]] {
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
			if (!is_enabled) [[likely]] {
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
			if (clock_gettime (CLOCK_MONOTONIC_RAW, &t) != 0) [[unlikely]] {
				log_warn (
					LOG_TIMING,
#if defined(XA_HOST_NATIVEAOT)
					"clock_gettime failed for CLOCK_MONOTONIC_RAW: %s",
#else
					"clock_gettime failed for CLOCK_MONOTONIC_RAW: {}"sv,
#endif
					optional_string (strerror (errno))
				);
				return {}; // Results will be nonsensical, but no point in aborting the app
			}
			return time_point (chrono::seconds (t.tv_sec) + chrono::nanoseconds (t.tv_nsec));
		}

	private:
		bool no_events_logged (size_t entries) noexcept;
		void dump_to_logcat (size_t entries) noexcept;
		void dump_to_file (size_t entries) noexcept;
		void dump (size_t entries, bool indent, std::function<void(std::string_view const&)> line_writer) noexcept;

		[[gnu::always_inline]]
		auto get_valid_sequence_index () noexcept -> std::expected<size_t, SequenceError>
		{
			if (open_sequences.empty ()) [[unlikely]] {
				return std::unexpected (SequenceError::EmptyStack);
			}

			size_t index = open_sequences.top ();
			if (!is_valid_event_index (index)) [[unlikely]] {
				return std::unexpected (SequenceError::InvalidIndex);
			}

			return index;
		}

		[[gnu::always_inline]]
		auto pop_valid_sequence_index () noexcept -> std::expected<size_t, SequenceError>
		{
			auto ret = get_valid_sequence_index ();
			if (ret.has_value ()) [[likely]] {
				open_sequences.pop ();
				return ret;
			}

			if (ret.error () != SequenceError::EmptyStack) {
				open_sequences.pop ();
			}

			return ret;
		}

		template<size_t BufferSize> [[gnu::always_inline]]
		static void append_event_kind_description (TimingEventKind kind, dynamic_local_string<BufferSize, char>& message) noexcept
		{
			auto append_desc = [&message] (std::string_view const& desc) {
				message.append (desc);
			};

			switch (kind) {
				case TimingEventKind::AssemblyDecompression:
					append_desc ("LZ4 decompression time for "sv);
					return;

				case TimingEventKind::AssemblyLoad:
					append_desc ("Assembly load for "sv);
					return;

				case TimingEventKind::AssemblyPreload:
					append_desc ("Finished preloading, number of loaded assemblies: "sv);
					return;

				case TimingEventKind::DebugStart:
					append_desc ("Debug::start_debugging_and_profiling: end"sv);
					return;

				case TimingEventKind::Init:
					append_desc ("XATiming: init time"sv);
					return;

				case TimingEventKind::JavaToManaged:
					append_desc ("Typemap.java_to_managed: end, total time"sv);
					return;

				case TimingEventKind::ManagedToJava:
					append_desc ("Typemap.managed_to_java: end, total time"sv);
					return;

				case TimingEventKind::ManagedRuntimeInit:
					append_desc ("Runtime.init: Managed runtime init"sv);
					return;

				case TimingEventKind::NativeToManagedTransition:
					append_desc ("Runtime.init: end native-to-managed transition"sv);
					return;

				case TimingEventKind::RuntimeConfigBlob:
					append_desc ("Register runtimeconfig binary blob"sv);
					return;

				case TimingEventKind::RuntimeRegister:
					append_desc ("Runtime.register: end time. Registered type: "sv);
					return;

				case TimingEventKind::TotalRuntimeInit:
					append_desc ("Runtime.init: end, total time"sv);
					return;

				case TimingEventKind::GetTimeOverhead:
					append_desc ("clock_gettime overhead"sv);
					return;

				case TimingEventKind::StartEndOverhead:
					append_desc ("start+end event overhead"sv);
					return;

				case TimingEventKind::FunctionCall:
					append_desc ("function call: "sv);
					return;

				case TimingEventKind::Unspecified:
					append_desc ("unspecified event type: "sv);
					return;
			}

			log_warn (
				LOG_TIMING,
#if defined(XA_HOST_NATIVEAOT)
				"Unknown event kind '%u' logged",
#else
				"Unknown event kind '{}' logged"sv,
#endif
				static_cast<std::underlying_type_t<decltype(kind)>>(kind)
			);
			append_desc ("unknown event kind"sv);
		}

	private:
		void parse_options (dynamic_local_property_string const& value) noexcept;
		static void really_initialize (bool log_immediately) noexcept;

		[[gnu::always_inline, nodiscard]]
		auto is_valid_event_index (size_t index, std::source_location sloc = std::source_location::current ()) const noexcept -> bool
		{
			if (index >= events.capacity ()) [[unlikely]] {
				log_warn (
					LOG_TIMING,
#if defined(XA_HOST_NATIVEAOT)
					"Invalid event index passed to method '%s'",
#else
					"Invalid event index passed to method '{}'"sv,
#endif
					sloc.function_name ()
				);
				return false;
			}

			return true;
		}

	private:
		std::atomic_size_t next_event_index = 0uz;
		mutex event_vector_realloc_mutex;
		std::vector<TimingEvent> events;
		std::unique_ptr<std::string> output_file_name{};

		static inline thread_local std::stack<size_t> open_sequences;
		static inline bool is_enabled = false;
		static inline bool immediate_logging = false;
		static inline bool log_to_file = default_log_to_file;
		static inline size_t duration_ms = default_duration_milliseconds;
		static inline TimingEvent init_time{};
		static inline TimingEvent start_end_event_time{};
		static inline TimingEvent get_time_overhead{};
	};
}
