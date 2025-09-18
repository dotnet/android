#pragma once

#include <cstdint>

// Must be declared before including host-environment.hh
struct AppEnvironmentVariable {
	uint32_t name_index;
	uint32_t value_index;
};

#include <host/host-environment.hh>

extern "C" {
	extern const uint32_t __naot_android_app_system_property_count;
	extern const AppEnvironmentVariable __naot_android_app_system_properties[];
	extern const char __naot_android_app_system_property_contents[];
}
