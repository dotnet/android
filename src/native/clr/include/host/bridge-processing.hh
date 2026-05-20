#pragma once

#include <runtime-base/logger.hh>

#include "bridge-processing-shared.hh"

class BridgeProcessing final : public BridgeProcessingShared
{
public:
	explicit BridgeProcessing (MarkCrossReferencesArgs *args) noexcept
		: BridgeProcessingShared (args)
	{}
};
