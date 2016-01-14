#ifndef INC_JAVA_INTEROP_H
#define INC_JAVA_INTEROP_H

#include <stdint.h>

#if defined(_MSC_VER)

	#define MONO_API_EXPORT __declspec(dllexport)
	#define MONO_API_IMPORT __declspec(dllimport)

#else   /* defined(_MSC_VER */

	#ifdef __GNUC__
		#define MONO_API_EXPORT __attribute__ ((visibility ("default")))
	#else
		#define MONO_API_EXPORT
	#endif
	#define MONO_API_IMPORT

#endif  /* !defined(_MSC_VER) */

#if defined(MONO_DLL_EXPORT)
	#define MONO_API MONO_API_EXPORT
#elif defined(MONO_DLL_IMPORT)
	#define MONO_API MONO_API_IMPORT
#else   /* !defined(MONO_DLL_IMPORT) && !defined(MONO_API_IMPORT) */
	#define MONO_API
#endif  /* MONO_DLL_EXPORT... */

#ifdef __cplusplus
	#define JAVA_INTEROP_BEGIN_DECLS    extern "C" {
	#define JAVA_INTEROP_END_DECLS      }
#else   /* ndef __cplusplus */
	#define JAVA_INTEROP_BEGIN_DECLS
	#define JAVA_INTEROP_END_DECLS
#endif  /* ndef __cplusplus */

JAVA_INTEROP_BEGIN_DECLS

MONO_API    char   *java_interop_strdup (const char* value);
MONO_API    void    java_interop_free   (void *p);

JAVA_INTEROP_END_DECLS

#endif  /* ndef INC_JAVA_INTEROP_H */
