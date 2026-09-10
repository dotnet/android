#pragma once

#include <cerrno>
#include <cstring>
#include <ctime>
#include <semaphore.h>
#include <unistd.h>

#include <array>
#include <string_view>

#include <android/looper.h>

#include <runtime-base/logger.hh>
#include <runtime-base/runtime-environment.hh>
#include <runtime-base/system-loadlibrary-wrapper.hh>
#include <shared/helpers.hh>

namespace xamarin::android {
	// This class is **strictly** one-shot-per-instance! That is, the `load` method mustn't be called on the
	// same object more than once. This is by design, to make the code simpler.
	class MainThreadDsoLoader
	{
	public:
		explicit MainThreadDsoLoader () noexcept
		{
			// Not shared between processes, initially unsignalled. Can only fail if the initial value
			// exceeds `SEM_VALUE_MAX`, which 0 clearly does not.
			if (sem_init (&load_complete_sem, 0, 0) != 0) {
				Helpers::abort_applicationf (
					LOG_ASSEMBLY,
					std::source_location::current (),
					"Failed to initialize the DSO load semaphore. %s",
					strerror (errno)
				);
			}

			if (pipe (pipe_fds) != 0) {
				Helpers::abort_applicationf (
					LOG_ASSEMBLY,
					std::source_location::current (),
					"Failed to create a pipe for main thread DSO loader. %s",
					strerror (errno)
				);
			}

			int ret = ALooper_addFd (
				main_thread_looper,
				pipe_fds[0],
				ALOOPER_POLL_CALLBACK,
				ALOOPER_EVENT_INPUT,
				load_cb,
				this
			);

			if (ret == -1) {
				Helpers::abort_application ("Failed to init main looper with pipe file descriptors in the main thread DSO loader"sv);
			}
		}

		MainThreadDsoLoader (const MainThreadDsoLoader&) = delete;
		MainThreadDsoLoader (MainThreadDsoLoader&&) = delete;

		// Not `virtual` on purpose. The class is never derived from nor destroyed through a base class
		// pointer and a virtual destructor would make the compiler emit the deleting destructor, which
		// pulls in `operator delete` and, with it, a dependency on `libc++`.
		~MainThreadDsoLoader () noexcept
		{
			if (pipe_fds[0] != -1) {
				ALooper_removeFd (main_thread_looper, pipe_fds[0]);
				close (pipe_fds[0]);
			}

			if (pipe_fds[1] != -1) {
				close (pipe_fds[1]);
			}

			sem_destroy (&load_complete_sem);

			// No need to release the looper, it needs to stay acquired.
		}

		MainThreadDsoLoader& operator=(const MainThreadDsoLoader&) = delete;
		MainThreadDsoLoader& operator=(MainThreadDsoLoader&&) = delete;

		bool load (std::string_view const& full_name, std::string_view const& undecorated_name) noexcept
		{
			if (!undecorated_library_name.empty ()) [[unlikely]] {
				Helpers::abort_application ("Main thread DSO loader object reused! DO NOT DO THAT!"sv);
			}
			log_debugf (LOG_ASSEMBLY, "Running DSO loader on thread %d, dispatching to main thread", static_cast<int>(gettid ()));

			undecorated_library_name = undecorated_name;
			load_success = false;
			constexpr std::array<uint8_t, 1> payload { 0xFF };
			ssize_t nbytes;
			do {
				nbytes = write (pipe_fds[1], payload.data (), payload.size ());
			} while (nbytes == -1 && errno == EINTR);

			if (nbytes != static_cast<ssize_t>(payload.size ())) {
				log_warnf (
					LOG_ASSEMBLY,
					"Write failure when posting a DSO load event to main thread. %s",
					nbytes == -1 ? strerror (errno) : "incomplete write"
				);
				return false;
			}

			// Wait for the callback to complete. 3s should be more than enough time for the library to load.
			constexpr time_t LoadTimeoutSeconds = 3;

			if (!try_acquire_for (LoadTimeoutSeconds)) {
				log_warnf (LOG_ASSEMBLY, "Timeout while waiting for shared library '%.*s' to load.", static_cast<int>(full_name.length ()), full_name.data ());
				return false;
			}

			return load_success;
		}

		static void init (JNIEnv *main_jni_env, ALooper *main_looper)
		{
			if (main_thread_looper != nullptr) {
				return;
			}

			main_thread_looper = main_looper;
			main_thread_jni_env = main_jni_env;
			// This will keep the looper around for the lifetime of the application.
			ALooper_acquire (main_looper);
		}

	private:

		// Waits up to `timeout_seconds` for the main thread callback to signal that it is done.
		// Returns `false` if it didn't within that time.
		[[nodiscard]] auto try_acquire_for (time_t timeout_seconds) noexcept -> bool
		{
			// `sem_timedwait` takes an absolute deadline and, until API 28, only supports
			// `CLOCK_REALTIME`. A wall clock adjustment inside the timeout window could cut the wait
			// short or stretch it, which is harmless for a sanity timeout like this one.
			timespec deadline {};
			clock_gettime (CLOCK_REALTIME, &deadline);
			deadline.tv_sec += timeout_seconds;

			// The deadline is absolute, so retrying after a signal cannot extend the total wait.
			int ret;
			do {
				ret = sem_timedwait (&load_complete_sem, &deadline);
			} while (ret == -1 && errno == EINTR);

			if (ret != 0 && errno != ETIMEDOUT) [[unlikely]] {
				log_warnf (LOG_ASSEMBLY, "Failed to wait for the DSO load to complete. %s", strerror (errno));
			}

			return ret == 0;
		}

		static auto load_cb ([[maybe_unused]] int fd, [[maybe_unused]] int events, void *data) noexcept -> int
		{
			auto self = reinterpret_cast<MainThreadDsoLoader*> (data);
			if (self == nullptr) [[unlikely]] {
				Helpers::abort_application ("MainThreadDsoLoader instance not passed to the looper callback."sv);
			}

			auto over_and_out = [&self]() -> int {
				// We're one-shot, 0 means just that
				sem_post (&self->load_complete_sem);
				return 0;
			};

			if (self->undecorated_library_name.empty ()) {
				log_warnf (LOG_ASSEMBLY, "Library name not specified in main thread looper callback.");
				return over_and_out ();
			}

			log_debugf (
				LOG_ASSEMBLY,
				"Looper CB called on thread %d. Will attempt to load DSO '%.*s'",
				static_cast<int>(gettid ()),
				static_cast<int>(self->undecorated_library_name.length ()),
				self->undecorated_library_name.data ()
			);

			self->load_success = SystemLoadLibraryWrapper::load (main_thread_jni_env /* RuntimeEnvironment::get_jnienv () */, self->undecorated_library_name);
			return over_and_out ();
		}

	private:
		int pipe_fds[2] = {-1, -1};
		sem_t load_complete_sem {};
		std::string_view undecorated_library_name {};
		bool load_success = false;

		static inline ALooper *main_thread_looper = nullptr;
		static inline JNIEnv *main_thread_jni_env = nullptr;
	};
}
