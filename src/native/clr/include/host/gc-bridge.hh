#pragma once

#include <cstdint>
#include <pthread.h>
#include <semaphore.h>

#include <shared/cpp-util.hh>

struct JniObjectReferenceControlBlock
{
	void *handle;
	int handle_type;
	int refs_added;
};

struct HandleContext
{
	int32_t identity_hash_code;
	JniObjectReferenceControlBlock *control_block;
};

struct StronglyConnectedComponent
{
	size_t Count;
	HandleContext **Contexts;
};

struct ComponentCrossReference
{
	size_t SourceGroupIndex;
	size_t DestinationGroupIndex;
};

struct MarkCrossReferencesArgs
{
	size_t ComponentCount;
	StronglyConnectedComponent *Components;
	size_t CrossReferenceCount;
	ComponentCrossReference *CrossReferences;
};

using BridgeProcessingFtn = void (*)(MarkCrossReferencesArgs*);

namespace xamarin::android {
	class GCBridge
	{
	public:
		static BridgeProcessingFtn initialize_callback (BridgeProcessingFtn managed_bridge_processing_callback) noexcept
		{
			abort_if_invalid_pointer_argument (managed_bridge_processing_callback, "managed_bridge_processing_callback");
			abort_unless (!initialized, "GC bridge callback is already initialized");

			bridge_processing_callback = managed_bridge_processing_callback;
			initialize_shared_args_semaphore ();
			initialized = true;
			start_bridge_processing_thread ();

			return mark_cross_references;
		}

	private:
		static inline sem_t shared_args_semaphore {};
		// JavaMarshal serializes bridge rounds: it does not publish another argument block until
		// FinishCrossReferenceProcessing has completed. This is therefore a single-slot handoff;
		// the semaphore signals availability but does not queue distinct argument blocks.
		static inline MarkCrossReferencesArgs *shared_args = nullptr;
		static inline BridgeProcessingFtn bridge_processing_callback = nullptr;
		static inline bool initialized {};

		static void initialize_shared_args_semaphore () noexcept;
		static void start_bridge_processing_thread () noexcept;
		static void publish_shared_args (MarkCrossReferencesArgs *args) noexcept;
		static auto wait_for_shared_args () noexcept -> MarkCrossReferencesArgs*;
		static void bridge_processing () noexcept;
		static auto bridge_processing_thread_entry (void *arg) noexcept -> void*;
		static void mark_cross_references (MarkCrossReferencesArgs *args) noexcept;
	};
}
