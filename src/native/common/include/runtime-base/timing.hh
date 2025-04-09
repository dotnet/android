#pragma once

#include <sys/time.h>

#include <chrono>
#include <mutex>
#include <vector>
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
	};

	// This class is intended to be used by the managed code. It can be used by the native code as
	// well, but the overhead it has (out of necessity) might not be desirable in native code.
	class Timing
	{
		static constexpr size_t DEFAULT_POOL_SIZE = 16uz;

	public:
		explicit Timing (size_t initial_pool_size = DEFAULT_POOL_SIZE) noexcept
		{
			sequence_pool.resize (initial_pool_size);
		}

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
			std::lock_guard<std::mutex> lock (sequence_lock);

			managed_timing_sequence *ret;
			for (size_t i = 0uz; i < sequence_pool.size (); i++) {
				if (sequence_pool[i].in_use) {
					continue;
				}

				ret = &sequence_pool[i];
				ret->in_use = true;

				return ret;
			}
			ret = &sequence_pool.emplace_back ();
			ret->in_use = true;

			return ret;
		}

		void release_sequence (managed_timing_sequence *sequence)
		{
			if (sequence == nullptr) {
				return;
			}

			std::lock_guard<std::mutex> lock (sequence_lock);
			sequence->start = time_point::min ();
			sequence->end = time_point::min ();
			sequence->in_use = false;
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
			auto text = std::format (
				"{}; elapsed: {}:{}::{}"sv,
				message == nullptr ? ""sv : message,
				static_cast<uint64_t>((std::chrono::duration_cast<std::chrono::seconds>(interval).count ())),
				static_cast<uint64_t>((std::chrono::duration_cast<std::chrono::milliseconds>(interval)).count ()),
				static_cast<uint64_t>((interval % 1ms).count ())
			);

			log_write (LOG_TIMING, level, text.c_str ());
		}

	private:
		std::vector<managed_timing_sequence> sequence_pool;
		std::mutex                sequence_lock;
	};
}
