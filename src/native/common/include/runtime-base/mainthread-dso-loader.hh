#pragma once

#include <cerrno>
#include <cstring>
#include <unistd.h>

#include <array>
#include <chrono>
#include <format>
#include <semaphore>
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
			if (pipe (pipe_fds) != 0) {
				Helpers::abort_application (
					LOG_ASSEMBLY,
					std::format (
						"Failed to create a pipe for main thread DSO loader. {}"sv,
						strerror (errno)
					)
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

		virtual ~MainThreadDsoLoader () noexcept
		{
			if (pipe_fds[0] != -1) {
				ALooper_removeFd (main_thread_looper, pipe_fds[0]);
				close (pipe_fds[0]);
			}

			if (pipe_fds[1] != -1) {
				close (pipe_fds[1]);
			}

			// No need to release the looper, it needs to stay acquired.
		}

		MainThreadDsoLoader& operator=(const MainThreadDsoLoader&) = delete;
		MainThreadDsoLoader& operator=(MainThreadDsoLoader&&) = delete;

		bool load (std::string_view const& full_name, std::string_view const& undecorated_name) noexcept
		{
			if (!undecorated_library_name.empty ()) [[unlikely]] {
				Helpers::abort_application ("Main thread DSO loader object reused! DO NOT DO THAT!"sv);
			}
			log_debug (LOG_ASSEMBLY, "Running DSO loader on thread {}, dispatching to main thread"sv, gettid ());

			undecorated_library_name = undecorated_name;
			load_success = false;
			constexpr std::array<uint8_t, 1> payload { 0xFF };
			ssize_t nbytes = write (pipe_fds[1], payload.data (), payload.size ());
			if (nbytes == -1) {
				log_warn (
					LOG_ASSEMBLY,
					"Write failure when posting a DSO load event to main thread. {}"sv,
					strerror (errno)
				);
				return false;
			}

			// Wait for the callback to complete
			using namespace std::literals;

			// We'll wait for up to 3s, it should be more than enough time for the library to load
			bool success = load_complete_sem.try_acquire_for (3s);
			if (!success) {
				log_warn (LOG_ASSEMBLY, "Timeout while waiting for shared library '{}' to load."sv, full_name);
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

		static auto load_cb ([[maybe_unused]] int fd, [[maybe_unused]] int events, void *data) noexcept -> int
		{
			auto self = reinterpret_cast<MainThreadDsoLoader*> (data);
			if (self == nullptr) [[unlikely]] {
				Helpers::abort_application ("MainThreadDsoLoader instance not passed to the looper callback."sv);
			}

			auto over_and_out = [&self]() -> int {
				// We're one-shot, 0 means just that
				self->load_complete_sem.release ();
				return 0;
			};

			if (self->undecorated_library_name.empty ()) {
				log_warn (LOG_ASSEMBLY, "Library name not specified in main thread looper callback."sv);
				return over_and_out ();
			}

			log_debug (
				LOG_ASSEMBLY,
				"Looper CB called on thread {}. Will attempt to load DSO '{}'"sv,
				gettid (),
				self->undecorated_library_name
			);

			self->load_success = SystemLoadLibraryWrapper::load (main_thread_jni_env /* RuntimeEnvironment::get_jnienv () */, self->undecorated_library_name);
			return over_and_out ();
		}

	private:
		int pipe_fds[2] = {-1, -1};
		std::binary_semaphore load_complete_sem {0};
		std::string_view undecorated_library_name {};
		bool load_success = false;

		static inline ALooper *main_thread_looper = nullptr;
		static inline JNIEnv *main_thread_jni_env = nullptr;
	};
}
