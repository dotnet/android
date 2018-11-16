#ifndef __MONODROID_H
#define __MONODROID_H

/* VS 2010 and later have stdint.h */
#if defined(_MSC_VER)

	#define MONO_API_EXPORT __declspec(dllexport)
	#define MONO_API_IMPORT __declspec(dllimport)

#else   /* defined(_MSC_VER */

	#define MONO_API_EXPORT __attribute__ ((visibility ("default")))
	#define MONO_API_IMPORT

#endif  /* !defined(_MSC_VER) */

#if defined(MONO_DLL_EXPORT)
	#define MONO_API_DEF MONO_API_EXPORT
#elif defined(MONO_DLL_IMPORT)
	#define MONO_API_DEF MONO_API_IMPORT
#else   /* !defined(MONO_DLL_IMPORT) && !defined(MONO_API_IMPORT) */
	#define MONO_API_DEF
#endif  /* MONO_DLL_EXPORT... */

#ifdef __cplusplus
#define MONO_API extern "C" MONO_API_DEF
#else

/* Use our own definition, to stay consistent */
#if defined (MONO_API)
#undef MONO_API
#endif
#define MONO_API MONO_API_DEF

#endif /* __cplusplus */

#endif  /* defined __MONODROID_H */
