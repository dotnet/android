#ifndef __JAVA_INTEROP_LOGGER_H__
#define __JAVA_INTEROP_LOGGER_H__

// Keep in sync with java-interop-logger.c's LogCategories enum
typedef enum _LogCategories {
	LOG_NONE      = 0,
	LOG_DEFAULT   = 1 << 0,
	LOG_ASSEMBLY  = 1 << 1,
	LOG_DEBUGGER  = 1 << 2,
	LOG_GC        = 1 << 3,
	LOG_GREF      = 1 << 4,
	LOG_LREF      = 1 << 5,
	LOG_TIMING    = 1 << 6,
	LOG_BUNDLE    = 1 << 7,
	LOG_NET       = 1 << 8,
	LOG_NETLINK   = 1 << 9,
} LogCategories;

extern unsigned int log_categories;

void log_error (LogCategories category, const char *format, ...);

void log_fatal (LogCategories category, const char *format, ...);

void log_info_nocheck (LogCategories category, const char *format, ...);

void log_warn (LogCategories category, const char *format, ...);

void log_debug_nocheck (LogCategories category, const char *format, ...);

#define DO_LOG(_level, _category_, ...)                                           \
	do {                                                                      \
		if ((log_categories & ((_category_))) != 0) {                     \
			::log_ ## _level ## _nocheck ((_category_), __VA_ARGS__); \
		}                                                                 \
	} while (0)

#define log_debug(_category_, ...) DO_LOG (debug, (_category_), __VA_ARGS__)
#define log_info(_category_, ...) DO_LOG (info, (_category_), __VA_ARGS__)

#endif /* __JAVA_INTEROP_LOGGER_H__ */
