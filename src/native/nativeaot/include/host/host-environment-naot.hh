#pragma once

#include <cstdint>

// Must be declared before including host-environment.hh
struct AppEnvironmentVariable {
	uint32_t name_index;
	uint32_t value_index;
};

#include <host/host-environment.hh>
