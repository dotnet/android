#pragma once

#include <cerrno>
#include <cstdint>
#include <cstdlib>
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
		explicit MainThreadDsoLoader () noexcept = default;

		MainThreadDsoLoader (const MainThreadDsoLoader&) = delete;
		MainThreadDsoLoader (MainThreadDsoLoader&&) = delete;

		// Not `virtual` on purpose. The class is never derived from nor destroyed through a base class
		// pointer and a virtual destructor would make the compiler emit the deleting destructor, which
		// pulls in `operator delete` and, with it, a dependency on `libc++`.
		~MainThreadDsoLoader () noexcept
		{
			if (state == nullptr) {
				return;
			}

			// A timed-out callback may still be queued or running. It owns a separate reference to
			// the state and releases it after unregistering itself and finishing all state accesses.
			__atomic_store_n (&state->cancelled, true, __ATOMIC_RELEASE);
			release_state (state);
		}

		MainThreadDsoLoader& operator=(const MainThreadDsoLoader&) = delete;
		MainThreadDsoLoader& operator=(MainThreadDsoLoader&&) = delete;

		bool load (std::string_view const& full_name, std::string_view const& undecorated_name) noexcept
		{
			if (state != nullptr) [[unlikely]] {
				Helpers::abort_application ("Main thread DSO loader object reused! DO NOT DO THAT!"sv);
			}
			log_debugf (LOG_ASSEMBLY, "Running DSO loader on thread %d, dispatching to main thread", static_cast<int>(gettid ()));

			state = create_state (undecorated_name);
			constexpr std::array<uint8_t, 1> payload { 0xFF };
			ssize_t nbytes;
			do {
				nbytes = write (state->pipe_fds[1], payload.data (), payload.size ());
			} while (nbytes == -1 && errno == EINTR);

			if (nbytes != static_cast<ssize_t>(payload.size ())) {
				log_warnf (
					LOG_ASSEMBLY,
					"Write failure when posting a DSO load event to main thread. %s",
					nbytes == -1 ? strerror (errno) : "incomplete write"
				);
				__atomic_store_n (&state->cancelled, true, __ATOMIC_RELEASE);
				close (state->pipe_fds[1]);
				state->pipe_fds[1] = -1;
				return false;
			}

			// Wait for the callback to complete. 3s should be more than enough time for the library to load.
			constexpr time_t LoadTimeoutSeconds = 3;

			if (!try_acquire_for (LoadTimeoutSeconds)) {
				log_warnf (LOG_ASSEMBLY, "Timeout while waiting for shared library '%.*s' to load.", static_cast<int>(full_name.length ()), full_name.data ());
				__atomic_store_n (&state->cancelled, true, __ATOMIC_RELEASE);
				return false;
			}

			return state->load_success;
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
		struct LoadState
		{
			int      pipe_fds[2];
			sem_t    load_complete_sem;
			char    *undecorated_library_name;
			size_t   undecorated_library_name_length;
			uint32_t references;
			bool     load_success;
			bool     cancelled;
		};

		static auto create_state (std::string_view const& undecorated_name) noexcept -> LoadState*
		{
			auto *new_state = static_cast<LoadState*> (std::calloc (1uz, sizeof (LoadState)));
			if (new_state == nullptr) [[unlikely]] {
				Helpers::abort_application ("Unable to allocate main thread DSO loader state"sv);
			}

			new_state->pipe_fds[0] = -1;
			new_state->pipe_fds[1] = -1;
			new_state->references = 2u; // The stack loader and the looper callback each own one.

			size_t name_capacity = Helpers::add_with_overflow_check<size_t> (undecorated_name.length (), 1uz);
			new_state->undecorated_library_name = static_cast<char*> (std::malloc (name_capacity));
			if (new_state->undecorated_library_name == nullptr) [[unlikely]] {
				Helpers::abort_application ("Unable to allocate main thread DSO library name"sv);
			}
			if (!undecorated_name.empty ()) {
				std::memcpy (new_state->undecorated_library_name, undecorated_name.data (), undecorated_name.length ());
			}
			new_state->undecorated_library_name[undecorated_name.length ()] = '\0';
			new_state->undecorated_library_name_length = undecorated_name.length ();

			// Not shared between processes, initially unsignalled. Can only fail if the initial value
			// exceeds `SEM_VALUE_MAX`, which 0 clearly does not.
			if (sem_init (&new_state->load_complete_sem, 0, 0) != 0) {
				Helpers::abort_applicationf (
					LOG_ASSEMBLY,
					std::source_location::current (),
					"Failed to initialize the DSO load semaphore. %s",
					strerror (errno)
				);
			}

			if (pipe (new_state->pipe_fds) != 0) {
				Helpers::abort_applicationf (
					LOG_ASSEMBLY,
					std::source_location::current (),
					"Failed to create a pipe for main thread DSO loader. %s",
					strerror (errno)
				);
			}

			int ret = ALooper_addFd (
				main_thread_looper,
				new_state->pipe_fds[0],
				ALOOPER_POLL_CALLBACK,
				ALOOPER_EVENT_INPUT,
				load_cb,
				new_state
			);

			if (ret == -1) {
				Helpers::abort_application ("Failed to init main looper with pipe file descriptors in the main thread DSO loader"sv);
			}

			return new_state;
		}

		static void release_state (LoadState *load_state) noexcept
		{
			if (__atomic_sub_fetch (&load_state->references, 1u, __ATOMIC_ACQ_REL) != 0u) {
				return;
			}

			if (load_state->pipe_fds[0] != -1) {
				close (load_state->pipe_fds[0]);
			}
			if (load_state->pipe_fds[1] != -1) {
				close (load_state->pipe_fds[1]);
			}
			if (sem_destroy (&load_state->load_complete_sem) != 0) {
				log_warnf (LOG_ASSEMBLY, "Failed to destroy the DSO load semaphore. %s", strerror (errno));
			}

			std::free (load_state->undecorated_library_name);
			std::free (load_state);
		}

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
				ret = sem_timedwait (&state->load_complete_sem, &deadline);
			} while (ret == -1 && errno == EINTR);

			if (ret != 0 && errno != ETIMEDOUT) [[unlikely]] {
				log_warnf (LOG_ASSEMBLY, "Failed to wait for the DSO load to complete. %s", strerror (errno));
			}

			return ret == 0;
		}

		static auto load_cb ([[maybe_unused]] int fd, [[maybe_unused]] int events, void *data) noexcept -> int
		{
			auto load_state = reinterpret_cast<LoadState*> (data);
			if (load_state == nullptr) [[unlikely]] {
				Helpers::abort_application ("MainThreadDsoLoader state not passed to the looper callback."sv);
			}

			auto over_and_out = [fd, load_state]() -> int {
				// We're one-shot, 0 means just that
				ALooper_removeFd (main_thread_looper, fd);
				sem_post (&load_state->load_complete_sem);
				release_state (load_state);
				return 0;
			};

			if (__atomic_load_n (&load_state->cancelled, __ATOMIC_ACQUIRE)) {
				return over_and_out ();
			}

			if (load_state->undecorated_library_name_length == 0uz) {
				log_warnf (LOG_ASSEMBLY, "Library name not specified in main thread looper callback.");
				return over_and_out ();
			}

			log_debugf (
				LOG_ASSEMBLY,
				"Looper CB called on thread %d. Will attempt to load DSO '%.*s'",
				static_cast<int>(gettid ()),
				static_cast<int>(load_state->undecorated_library_name_length),
				load_state->undecorated_library_name
			);

			load_state->load_success = SystemLoadLibraryWrapper::load (
				main_thread_jni_env /* RuntimeEnvironment::get_jnienv () */,
				std::string_view { load_state->undecorated_library_name, load_state->undecorated_library_name_length }
			);
			return over_and_out ();
		}

	private:
		LoadState *state = nullptr;

		static inline ALooper *main_thread_looper = nullptr;
		static inline JNIEnv *main_thread_jni_env = nullptr;
	};
}
