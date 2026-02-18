#include <host/gc-bridge.hh>

using namespace xamarin::android;

void GCBridge::mark_cross_references (void *args) noexcept
{
	// Signal the background thread to process
	// All validation and logging is done in managed code
	shared_args.store (args);
	shared_args_semaphore.release ();
}

void GCBridge::bridge_processing () noexcept
{
	while (true) {
		// Wait until mark_cross_references is called by the GC
		shared_args_semaphore.acquire ();
		void *args = shared_args.load ();

		// Call into managed code to do the actual processing
		bridge_processing_callback (args);
	}
}
