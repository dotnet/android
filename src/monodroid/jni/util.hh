// This is a -*- C++ -*- header
#ifndef __MONODROID_UTIL_H__
#define __MONODROID_UTIL_H__

#ifndef TRUE
#ifdef __cplusplus
static inline constexpr int TRUE = 1;
#else
#define TRUE 1
#endif // __cplusplus
#endif

#ifndef FALSE
#ifdef __cplusplus
static inline constexpr int FALSE = 0;
#else
#define FALSE 0
#endif // __cplusplus
#endif

#include <array>
#include <cstdarg>
#include <cstdlib>
#include <cstring>
#include <ctime>
#ifdef HAVE_BSD_STRING_H
#include <bsd/string.h>
#endif
#include <unistd.h>
#include <sys/stat.h>
#include <dirent.h>
#include <sys/time.h>

#include <jni.h>

#include <mono/metadata/assembly.h>
#include <mono/metadata/appdomain.h>
#include <mono/metadata/threads.h>
#include <mono/metadata/mono-private-unstable.h>

#include "monodroid.h"
#include "jni-wrappers.hh"
#ifdef __cplusplus
#include "basic-utilities.hh"
#endif

#include "java-interop-util.h"
#include "logger.hh"

#ifdef __cplusplus
namespace xamarin::android
{
	class Util : public BasicUtilities
	{
		static constexpr std::array<char, 16> hex_chars = { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'a', 'b', 'c', 'd', 'e', 'f' };
		static constexpr uint32_t ms_in_nsec = 1000000ULL;

	public:
		Util ();

		int              monodroid_getpagesize () const noexcept
		{
			return page_size;
		}

		void             monodroid_store_package_name (const char *name);
		MonoAssembly    *monodroid_load_assembly (MonoDomain *domain, const char *basename);
		MonoAssembly    *monodroid_load_assembly (MonoAssemblyLoadContextGCHandle alc_handle, const char *basename);
		MonoClass       *monodroid_get_class_from_name (MonoDomain *domain, const char* assembly, const char *_namespace, const char *type);
		int              send_uninterrupted (int fd, void *buf, size_t len);
		ssize_t          recv_uninterrupted (int fd, void *buf, size_t len);
		jclass           get_class_from_runtime_field (JNIEnv *env, jclass runtime, const char *name, bool make_gref = false);

		static bool should_log (LogCategories category) noexcept
		{
			return (log_categories & category) != 0;
		}

		MonoDomain *get_current_domain (bool attach_thread_if_needed = true) const noexcept
		{
			MonoDomain *ret = mono_domain_get ();
			if (ret != nullptr) {
				return ret;
			}

			// It's likely that we got a nullptr because the current thread isn't attached (see
			// https://github.com/xamarin/xamarin-android/issues/6211), so we need to attach the thread to the root
			// domain
			ret = mono_get_root_domain ();
			if (attach_thread_if_needed) {
				mono_thread_attach (ret);
			}

			return ret;
		}

	private:
		template<typename IdxType>
		void package_hash_to_hex (IdxType idx);

		template<typename IdxType = size_t, typename ...Indices>
		void package_hash_to_hex (uint32_t hash, IdxType idx, Indices... indices);

	private:
		char package_property_suffix[9];
		int page_size;
	};
}
#endif // __cplusplus
#endif /* __MONODROID_UTIL_H__ */
