#pragma once

#include <runtime-base/logger.hh>

#include "bridge-processing-shared.hh"

class BridgeProcessing final : public BridgeProcessingShared
{
public:
	explicit BridgeProcessing (MarkCrossReferencesArgs *args) noexcept
		: BridgeProcessingShared (args)
	{}

private:
	auto maybe_call_gc_user_peerable_add_managed_reference ([[maybe_unused]] JNIEnv *env, [[maybe_unused]] jobject from, [[maybe_unused]] jobject to) noexcept -> bool override final
	{
		log_warn (LOG_ASSEMBLY, "{}", __PRETTY_FUNCTION__);
		return false; // no-op for CoreCLR, we didn't process the call
	}

	auto maybe_call_gc_user_peerable_clear_managed_references ([[maybe_unused]] JNIEnv *env, [[maybe_unused]] jobject handle) noexcept -> bool override final
	{
		log_warn (LOG_ASSEMBLY, "{}", __PRETTY_FUNCTION__);
		return false; // no-op for CoreCLR, we didn't process the call
	}
};
