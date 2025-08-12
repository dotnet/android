#pragma once

#include <cerrno>
#include <cstring>
#include <unistd.h>

#include <format>
#include <semaphore>
#include <string_view>

#include <android/looper.h>

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
						"Failed to create a pipe for main thread DSO loader. {}",
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

		void load (std::string_view const& full_name, std::string const& undecorated_name) noexcept
		{
			// TODO: init load here by writing to pipe_fds[1]

			// Wait for the callback to complete
			load_complete_sem.acquire ();
		}

		static void init (ALooper *main_looper)
		{
			main_thread_looper = main_looper;

			// This will keep the looper around for the lifetime of the application.
			ALooper_acquire (main_looper);
		}

	private:
		static auto load_cb (int fd, int events, void *data) noexcept -> int
		{
			auto self = reinterpret_cast<MainThreadDsoLoader*> (data);

			self->load_complete_sem.release ();
			return 0; // We're one-shot, 0 means just that
		}

	private:
		int pipe_fds[2] = {-1, -1};
		std::binary_semaphore load_complete_sem {0};

		static inline ALooper *main_thread_looper = nullptr;
	};
}
