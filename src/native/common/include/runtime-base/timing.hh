#pragma once

#include <pthread.h>
#include <sys/time.h>

#include <cstdlib>
#include <string_view>

#include <android/log.h>

#include <shared/helpers.hh>

#include "timing-internal.hh"

namespace xamarin::android
{
	struct managed_timing_sequence
	{
		time_point  start;
		time_point  end;
		bool             in_use;
	};

	// This class is intended to be used by the managed code. It can be used by the native code as
	// well, but the overhead it has (out of necessity) might not be desirable in native code.
	class Timing
	{
	public:
		Timing () noexcept = default;
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

			managed_timing_sequence *ret = find_unused_sequence ();
			if (ret == nullptr) {
				ret = allocate_chunk ();
			}

			ret->start = 0;
			ret->end = 0;
			ret->in_use = true;

			pthread_mutex_unlock (&sequence_lock);
			return ret;
		}

		void release_sequence (managed_timing_sequence *sequence)
		{
			if (sequence == nullptr) {
				return;
			}

			pthread_mutex_lock (&sequence_lock);
			sequence->in_use = false;
			pthread_mutex_unlock (&sequence_lock);
		}

	private:
		// Sequences are handed out to managed code, which holds on to them until it stops the
		// measurement, so they must never move. They are allocated in chunks that are chained
		// together and never freed, and are recycled through `in_use`, so that every address
		// handed out stays valid for the lifetime of the process.
		static inline constexpr size_t SEQUENCE_CHUNK_SIZE = 16uz;

		struct sequence_chunk
		{
			sequence_chunk           *next;
			managed_timing_sequence   sequences[SEQUENCE_CHUNK_SIZE];
		};

		// Must be called with `sequence_lock` held.
		auto find_unused_sequence () noexcept -> managed_timing_sequence*
		{
			for (sequence_chunk *chunk = sequence_chunks; chunk != nullptr; chunk = chunk->next) {
				for (size_t i = 0uz; i < SEQUENCE_CHUNK_SIZE; i++) {
					if (!chunk->sequences[i].in_use) {
						return &chunk->sequences[i];
					}
				}
			}

			return nullptr;
		}

		// Must be called with `sequence_lock` held. `calloc` clears `in_use` for every entry in
		// the new chunk, so all of them start out available.
		auto allocate_chunk () noexcept -> managed_timing_sequence*
		{
			auto *chunk = static_cast<sequence_chunk*> (std::calloc (1uz, sizeof (sequence_chunk)));
			if (chunk == nullptr) [[unlikely]] {
				Helpers::abort_application (LOG_TIMING, "Unable to allocate memory for timing sequences");
			}

			chunk->next = sequence_chunks;
			sequence_chunks = chunk;

			return &chunk->sequences[0uz];
		}

		[[gnu::always_inline]]
		static void do_log (LogLevel level, managed_timing_sequence const *seq, const char *message)
		{
			if (seq == nullptr) {
				return;
			}

			time_interval interval { seq->end - seq->start };
			log_writef (
				LOG_TIMING,
				level,
				"%s; elapsed: %llu:%llu::%llu",
				optional_string (message, ""),
				interval.seconds,
				interval.milliseconds,
				interval.nanoseconds
			);
		}

	private:
		sequence_chunk   *sequence_chunks = nullptr;
		pthread_mutex_t   sequence_lock = PTHREAD_MUTEX_INITIALIZER;
	};
}
