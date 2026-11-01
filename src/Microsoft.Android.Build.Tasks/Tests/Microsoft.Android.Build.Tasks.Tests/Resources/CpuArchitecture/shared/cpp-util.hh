#pragma once

#include <cstdlib>
#include <shared/helpers.hh>

// Only the pointer guard is needed by the CPU detector; avoid Android-only headers on the host.
#define abort_if_invalid_pointer_argument(pointer, name) do { if ((pointer) == nullptr) std::abort (); } while (false)
