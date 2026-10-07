#pragma once

#include <cstdint>
#include <dlfcn.h>
#include <link.h>
#include <managed-interface.hh>
#include <shared/helpers.hh>

namespace xamarin::android {
	inline void *remapping_module;

	struct RemappingRange {
		uintptr_t base;
		uintptr_t start;
		uintptr_t segment_end;
		bool valid;
	};

	inline auto check_remapping_range (dl_phdr_info *info, size_t, void *opaque) noexcept -> int
	{
		auto &range = *static_cast<RemappingRange*> (opaque);
		if (static_cast<uintptr_t> (info->dlpi_addr) != range.base)
			return 0;
		for (size_t i = 0; i < info->dlpi_phnum; ++i) {
			const auto &segment = info->dlpi_phdr [i];
			if (segment.p_type != PT_LOAD || (segment.p_flags & PF_R) == 0 || (segment.p_flags & (PF_W | PF_X)) != 0)
				continue;
			if (segment.p_vaddr > UINTPTR_MAX - range.base)
				continue;
			uintptr_t start = range.base + static_cast<uintptr_t> (segment.p_vaddr);
			if (segment.p_memsz > UINTPTR_MAX - start)
				continue;
			uintptr_t end = start + static_cast<uintptr_t> (segment.p_memsz);
			if (range.start >= start && range.start < end && end - range.start >= 64) {
				range.segment_end = end;
				range.valid = true;
				break;
			}
		}
		return 1;
	}

	inline void load_jni_remapping_asset (JnienvInitializeArgs &args) noexcept
	{
		abort_unless (remapping_module == nullptr, "The JNI remapping module was already loaded");
		remapping_module = ::dlopen ("libandroid_runtime_blobs.so", RTLD_NOW | RTLD_LOCAL);
		if (remapping_module == nullptr) {
			const char *error = ::dlerror ();
			Helpers::abort_applicationf (LOG_DEFAULT, std::source_location::current (),
				"Failed to dlopen JNI remapping data library: %s", error == nullptr ? "unknown linker error" : error);
		}
		::dlerror ();
		const void *data = ::dlsym (remapping_module, "xajr_payload");
		const char *data_error = ::dlerror ();
		abort_unless (data != nullptr && data_error == nullptr, "JNI remapping data symbol is missing");
		uintptr_t start_address = reinterpret_cast<uintptr_t> (data);
		Dl_info module_info {};
		abort_unless (::dladdr (data, &module_info) != 0 && module_info.dli_fbase != nullptr,
			"JNI remapping symbol has no module mapping");
		RemappingRange range {reinterpret_cast<uintptr_t> (module_info.dli_fbase), start_address, 0, false};
		::dl_iterate_phdr (check_remapping_range, &range);
		abort_unless (range.valid, "JNI remapping header is outside a read-only data load segment");

		// Read the declared size only after independently bounding the fixed header.
		const auto *bytes = static_cast<const uint8_t*> (data);
		uint32_t length = static_cast<uint32_t> (bytes [12]) | (static_cast<uint32_t> (bytes [13]) << 8) |
			(static_cast<uint32_t> (bytes [14]) << 16) | (static_cast<uint32_t> (bytes [15]) << 24);
		abort_unless (length >= 64 && length <= INT32_MAX && length <= range.segment_end - start_address,
			"JNI remapping payload has an invalid size or exceeds its read-only load segment");

		// Java.Interop can retain these pointers for the process lifetime: never dlclose.
		args.jniRemappingData = static_cast<const uint8_t*> (data);
		args.jniRemappingDataLength = length;
	}
}
