#pragma once

// Keep in sync with src/Mono.Android/Android.Runtime/LogCategories.cs.
enum LogCategories {
	LOG_NONE      = 0,
	LOG_DEFAULT   = 1 << 0,
	LOG_ASSEMBLY  = 1 << 1,
	// Bit 2 is reserved for the removed debugger logging category.
	LOG_GC        = 1 << 3,
	// Bits 4 and 5 are reserved for the removed Java.Interop reference logging categories.
	LOG_TIMING    = 1 << 6,
	// Bit 7 is reserved for the removed bundle logging category.
	LOG_NET       = 1 << 8,
	// Bit 9 is reserved for the removed netlink logging category.
};

extern unsigned int log_categories;
