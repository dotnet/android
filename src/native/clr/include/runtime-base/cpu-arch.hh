#pragma once

#include <cstdint>

static inline constexpr uint16_t CPU_KIND_UNKNOWN = 0;
static inline constexpr uint16_t CPU_KIND_ARM     = 1;
static inline constexpr uint16_t CPU_KIND_ARM64   = 2;
static inline constexpr uint16_t CPU_KIND_MIPS    = 3;
static inline constexpr uint16_t CPU_KIND_X86     = 4;
static inline constexpr uint16_t CPU_KIND_X86_64  = 5;
static inline constexpr uint16_t CPU_KIND_RISCV   = 6;

void _monodroid_detect_cpu_and_architecture (uint16_t &built_for_cpu, uint16_t &running_on_cpu, bool &is64bit);
