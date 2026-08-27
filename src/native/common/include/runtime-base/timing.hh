#pragma once

#include <pthread.h>
#include <sys/time.h>

#include <chrono>
#include <cstdlib>
#include <string_view>

#include <android/log.h>

#include "timing-internal.hh"

namespace xamarin::android
{
	struct managed_timing_sequence
	{
		time_point  start;
		time_point  end;
		bool             in_use;

		// Valid only while the sequence sits on `Timing::free_sequences`.
		managed_timing_sequence *next_free;
	};

	// This class is intended to be used by the managed code. It can be used by the native code as
	// well, but the overhead it has (out of necessity) might not be desirable in native code.
	class Timing
	{
	public:
		Timing (Timing const&) = delete;
		Timing (Timing&&) = delete;
		Timing& operator= (Timing const&) = delete;
		Timing& operator= (Timing&&) = delete;

		static void info (managed_timing_sequence const *seq, const char *message)
		{
			do_log (LogLevel::Info, seq, message);
		}

		static void warn (managed_timing_sequence const *seq, const char *message)
		{
			do_log (LogLevel::Warn, seq, message);
		}

		auto get_available_sequence () noexcept -> managed_timing_sequence*
		{
			pthread_mutex_lock (&sequence_lock);

			managed_timing_sequence *ret = free_sequences;
			if (ret != nullptr) {
				free_sequences = ret->next_free;
			} else {
				// Sequences are handed out to managed code, which holds on to them until it
				// stops the measurement, so they must never move. Each one is therefore its
				// own allocation, recycled through `free_sequences` rather than freed, for
				// the lifetime of the process.
				ret = static_cast<managed_timing_sequence*> (std::malloc (sizeof (managed_timing_sequence)));
				if (ret == nullptr) [[unlikely]] {
					pthread_mutex_unlock (&sequence_lock);
					return nullptr;
				}
			}

			ret->start = time_point::min ();
			ret->end = time_point::min ();
			ret->in_use = true;
			ret->next_free = nullptr;

			pthread_mutex_unlock (&sequence_lock);
			return ret;
		}

		void release_sequence (managed_timing_sequence *sequence)
		{
			if (sequence == nullptr) {
				return;
			}

			pthread_mutex_lock (&sequence_lock);

			// Ignore a sequence that isn't checked out, otherwise a double release would put
			// it on the free list twice and it would then be handed to two callers at once.
			if (sequence->in_use) {
				sequence->start = time_point::min ();
				sequence->end = time_point::min ();
				sequence->in_use = false;
				sequence->next_free = free_sequences;
				free_sequences = sequence;
			}

			pthread_mutex_unlock (&sequence_lock);
		}

	private:
		[[gnu::always_inline]]
		static void do_log (LogLevel level, managed_timing_sequence const *seq, const char *message)
		{
			if (seq == nullptr) {
				return;
			}

			using namespace std::literals;
			auto interval = seq->end - seq->start; // nanoseconds
			log_writef (
				LOG_TIMING,
				level,
				"%s; elapsed: %llu:%llu::%llu",
				optional_string (message, ""),
				static_cast<unsigned long long>(std::chrono::duration_cast<std::chrono::seconds>(interval).count ()),
				static_cast<unsigned long long>(std::chrono::duration_cast<std::chrono::milliseconds>(interval).count ()),
				static_cast<unsigned long long>((interval % 1ms).count ())
			);
		}

	private:
		managed_timing_sequence  *free_sequences = nullptr;
		pthread_mutex_t           sequence_lock = PTHREAD_MUTEX_INITIALIZER;
	};
}
