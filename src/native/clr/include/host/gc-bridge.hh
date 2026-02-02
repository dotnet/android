#pragma once

#include <atomic>
#include <semaphore>
#include <thread>

#include <shared/cpp-util.hh>

// Function pointer types for bridge processing
using BridgeProcessingFtn = void (*)(void*);
using BridgeProcessingCallback = void (*)(void*);

namespace xamarin::android {
	class GCBridge
	{
	public:
		// Initialize GC bridge for managed processing mode.
		// Takes a callback that will be invoked from a background thread when mark_cross_references is called.
		// Returns the mark_cross_references function pointer for JavaMarshal.Initialize.
		static BridgeProcessingFtn initialize_for_managed_processing (BridgeProcessingCallback callback) noexcept
		{
			abort_if_invalid_pointer_argument (callback, "callback");
			bridge_processing_callback = callback;

			// Start the background thread that will call into managed code
			bridge_processing_thread = std::thread { bridge_processing };
			bridge_processing_thread.detach ();

			return mark_cross_references;
		}

	private:
		static inline std::thread bridge_processing_thread {};
		static inline std::binary_semaphore shared_args_semaphore{0};
		static inline std::atomic<void*> shared_args;
		static inline BridgeProcessingCallback bridge_processing_callback = nullptr;

		static void bridge_processing () noexcept;
		static void mark_cross_references (void *args) noexcept;
	};
}
