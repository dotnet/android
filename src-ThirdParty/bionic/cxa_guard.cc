//
// Ported to .NET for Android from: https://android.googlesource.com/platform/bionic/+/refs/tags/android-11.0.0_r31/libc/bionic/__cxa_guard.cpp
//

/*
 * Copyright (C) 2006 The Android Open Source Project
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

#include <endian.h>
#include <limits.h>
#include <stdatomic.h>
#include <stddef.h>

#include "bionic_futex.hh"

// This file contains C++ ABI support functions for one time
// constructors as defined in the "Run-time ABI for the ARM Architecture"
// section 4.4.2
//
// ARM C++ ABI and Itanium/x86 C++ ABI has different definition for
// one time construction:
//
//    ARM C++ ABI defines the LSB of guard variable should be tested
//    by compiler-generated code before calling __cxa_guard_acquire et al.
//
//    The Itanium/x86 C++ ABI defines the low-order _byte_ should be
//    tested instead.
//
//    Meanwhile, guard variable are 32bit aligned for ARM, and 64bit
//    aligned for x86.
//
// Reference documentation:
//
//    section 3.2.3 of ARM IHI 0041C (for ARM)
//    section 3.3.2 of the Itanium C++ ABI specification v1.83 (for x86).
//
// There is no C++ ABI available for other ARCH. But the gcc source
// shows all other ARCH follow the definition of Itanium/x86 C++ ABI.

#if defined(__arm__)
// The ARM C++ ABI mandates that guard variables are 32-bit aligned, 32-bit
// values. The LSB is tested by the compiler-generated code before calling
// __cxa_guard_acquire.
union _guard_t {
  atomic_int state;
  int32_t aligner;
};

#else
// The Itanium/x86 C++ ABI (used by all other architectures) mandates that
// guard variables are 64-bit aligned, 64-bit values. The LSB is tested by
// the compiler-generated code before calling __cxa_guard_acquire.
union _guard_t {
  atomic_int state;
  int64_t aligner;
};

#endif

// Set construction state values according to reference documentation.
// 0 is the initialization value.
// Arm requires ((*gv & 1) == 1) after __cxa_guard_release, ((*gv & 3) == 0) after __cxa_guard_abort.
// X86 requires first byte not modified by __cxa_guard_acquire, first byte is non-zero after
// __cxa_guard_release.

#define CONSTRUCTION_NOT_YET_STARTED                0
#define CONSTRUCTION_COMPLETE                       1
#define CONSTRUCTION_UNDERWAY_WITHOUT_WAITER    0x100
#define CONSTRUCTION_UNDERWAY_WITH_WAITER       0x200

extern "C" int __cxa_guard_acquire(_guard_t* gv) {
  int old_value = atomic_load_explicit(&gv->state, memory_order_acquire);
  // In the common CONSTRUCTION_COMPLETE case we have to ensure that all the stores performed by
  // the construction function are observable on this CPU after we exit. A similar constraint may
  // apply in the CONSTRUCTION_NOT_YET_STARTED case with a prior abort.

  while (true) {
    if (old_value == CONSTRUCTION_COMPLETE) {
      return 0;
    } else if (old_value == CONSTRUCTION_NOT_YET_STARTED) {
      if (!atomic_compare_exchange_weak_explicit(&gv->state, &old_value,
                                                  CONSTRUCTION_UNDERWAY_WITHOUT_WAITER,
                                                  memory_order_acquire /* or relaxed in C++17 */,
                                                  memory_order_acquire)) {
        continue;
      }
      return 1;
    } else if (old_value == CONSTRUCTION_UNDERWAY_WITHOUT_WAITER) {
      if (!atomic_compare_exchange_weak_explicit(&gv->state, &old_value,
                                                 CONSTRUCTION_UNDERWAY_WITH_WAITER,
                                                 memory_order_acquire /* or relaxed in C++17 */,
                                                 memory_order_acquire)) {
        continue;
      }
    }

    __futex_wait_ex(&gv->state, false, CONSTRUCTION_UNDERWAY_WITH_WAITER);
    old_value = atomic_load_explicit(&gv->state, memory_order_acquire);
  }
}

extern "C" void __cxa_guard_release(_guard_t* gv) {
  // Release fence is used to make all stores performed by the construction function
  // visible in other threads.
  int old_value = atomic_exchange_explicit(&gv->state, CONSTRUCTION_COMPLETE, memory_order_release);
  if (old_value == CONSTRUCTION_UNDERWAY_WITH_WAITER) {
    __futex_wake_ex(&gv->state, false, INT_MAX);
  }
}

extern "C" void __cxa_guard_abort(_guard_t* gv) {
  // Release fence is used to make all stores performed by the construction function
  // visible in other threads.
  int old_value = atomic_exchange_explicit(&gv->state, CONSTRUCTION_NOT_YET_STARTED, memory_order_release);
  if (old_value == CONSTRUCTION_UNDERWAY_WITH_WAITER) {
    __futex_wake_ex(&gv->state, false, INT_MAX);
  }
}
