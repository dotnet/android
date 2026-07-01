#ifndef INC_JAVA_INTEROP_H
#define INC_JAVA_INTEROP_H

#include <stdint.h>

#if defined(_MSC_VER)

	#define JAVA_INTEROP_API_EXPORT __declspec(dllexport)
	#define JAVA_INTEROP_API_IMPORT __declspec(dllimport)

#else   /* defined(_MSC_VER */

	#ifdef __GNUC__
		#define JAVA_INTEROP_API_EXPORT __attribute__ ((visibility ("default")))
	#else
		#define JAVA_INTEROP_API_EXPORT
	#endif
	#define JAVA_INTEROP_API_IMPORT

#endif  /* !defined(_MSC_VER) */

#if defined(MONO_DLL_EXPORT) || defined(JAVA_INTEROP_DLL_EXPORT)
	#define JAVA_INTEROP_API JAVA_INTEROP_API_EXPORT
#elif defined(MONO_DLL_IMPORT) || defined(JAVA_INTEROP_DLL_IMPORT)
	#define JAVA_INTEROP_API JAVA_INTEROP_API_IMPORT
#else   /* !defined(MONO_DLL_IMPORT) && !defined(MONO_DLL_EXPORT) */
	#define JAVA_INTEROP_API
#endif  /* MONO_DLL_EXPORT... */

#ifdef __cplusplus
	#define JAVA_INTEROP_BEGIN_DECLS    extern "C" {
	#define JAVA_INTEROP_END_DECLS      }
#else   /* ndef __cplusplus */
	#define JAVA_INTEROP_BEGIN_DECLS
	#define JAVA_INTEROP_END_DECLS
#endif  /* ndef __cplusplus */

JAVA_INTEROP_BEGIN_DECLS

JAVA_INTEROP_API    char   *java_interop_strdup (const char* value);
JAVA_INTEROP_API    void    java_interop_free   (void *p);

JAVA_INTEROP_END_DECLS

#endif  /* ndef INC_JAVA_INTEROP_H */
