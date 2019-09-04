#include <assert.h>
#include <stdlib.h>
#include <stdarg.h>
#include <stdio.h>
#include <errno.h>
#ifndef WINDOWS
#include <sys/socket.h>
#else
#include <winsock2.h>
#include <shlwapi.h>
#endif
#include <sys/stat.h>
#include <sys/types.h>
#include <string.h>
#ifdef HAVE_BSD_STRING_H
#include <bsd/string.h>
#endif

#ifdef WINDOWS
#include <direct.h>
#endif

#include <mono/metadata/appdomain.h>
#include <mono/metadata/assembly.h>

#include "java-interop-util.h"

#include "monodroid.h"
#include "util.hh"
#include "globals.hh"
#include "monodroid-glue.h"

using namespace xamarin::android;

#if defined (ANDROID) || defined (LINUX)
using timestruct = timespec;
#else
using timestruct = timeval;
#endif

static const char hex_chars [] = { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'a', 'b', 'c', 'd', 'e', 'f' };

void timing_point::mark ()
{
	int ret;
	uint64_t tail;
	timestruct tv_ctm;

#if defined (ANDROID) || defined (LINUX)
	ret = clock_gettime (CLOCK_MONOTONIC, &tv_ctm);
	tail = static_cast<uint64_t>(tv_ctm.tv_nsec);
#else
	ret = gettimeofday (&tv_ctm, static_cast<timestruct*> (nullptr));
	tail = tv_ctm.tv_usec * 1000LL;
#endif
	if (ret != 0) {
		sec = 0ULL;
		ns = 0ULL;
		return;
	}

	sec = tv_ctm.tv_sec;
	ns = tail;
}

timing_diff::timing_diff (const timing_period &period)
{
	uint64_t nsec;
	if (period.end.ns < period.start.ns) {
		sec = period.end.sec - period.start.sec - 1;
		if (sec < 0)
			sec = 0;
		nsec = 1000000000ULL + period.end.ns - period.start.ns;
	} else {
		sec = period.end.sec - period.start.sec;
		nsec = period.end.ns - period.start.ns;
	}

	ms = static_cast<uint32_t>(nsec / ms_in_nsec);
	if (ms >= 1000) {
		sec += ms / 1000;
		ms = ms % 1000;
	}

	ns = static_cast<uint32_t>(nsec % ms_in_nsec);
}

int
Util::send_uninterrupted (int fd, void *buf, size_t len)
{
	ssize_t res;
#ifdef WINDOWS
	const char *buffer = static_cast<const char*> (buf);
#else
	void *buffer = buf;
#endif
	do {
		res = send (
			fd,
			buffer,
#ifdef WINDOWS
			static_cast<int>(len),
#else
			len,
#endif
			0
		);
	} while (res == -1 && errno == EINTR);

	return static_cast<size_t>(res) == len;
}

ssize_t
Util::recv_uninterrupted (int fd, void *buf, size_t len)
{
	ssize_t res;
	size_t total = 0;
	int flags = 0;
#ifdef WINDOWS
	int nbytes;
#else
	size_t nbytes;
#endif
	do {
		nbytes = len - total;
		res = recv (fd, (char *) buf + total, nbytes, flags);

		if (res > 0)
			total += static_cast<size_t>(res);
	} while ((res > 0 && total < len) || (res == -1 && errno == EINTR));

	return static_cast<ssize_t>(total);
}

#if WINDOWS
//
// This version should be removed once MXE we have on mac can build the glorious version in the
// #else below.
//
// Currently mxe fails with:
//
//  Cannot export _ZN7xamarin7android4Util19package_hash_to_hexIiIEEEvjT_DpT0_: symbol wrong type (4 vs 3)
//   Cannot export _ZN7xamarin7android4Util19package_hash_to_hexIiIiEEEvjT_DpT0_: symbol wrong type (4 vs 3)
//   Cannot export _ZN7xamarin7android4Util19package_hash_to_hexIiIiiEEEvjT_DpT0_: symbol wrong type (4 vs 3)
//   Cannot export _ZN7xamarin7android4Util19package_hash_to_hexIiIiiiEEEvjT_DpT0_: symbol wrong type (4 vs 3)
//   Cannot export _ZN7xamarin7android4Util19package_hash_to_hexIiIiiiiEEEvjT_DpT0_: symbol wrong type (4 vs 3)
//   Cannot export _ZN7xamarin7android4Util19package_hash_to_hexIiIiiiiiEEEvjT_DpT0_: symbol wrong type (4 vs 3)
//   Cannot export _ZN7xamarin7android4Util19package_hash_to_hexIiIiiiiiiEEEvjT_DpT0_: symbol wrong type (4 vs 3)
//   Cannot export _ZN7xamarin7android4Util19package_hash_to_hexIiIiiiiiiiEEEvjT_DpT0_: symbol wrong type (4 vs 3)
// collect2 : error : ld returned 1 exit status
//   [/Users/builder/jenkins/workspace/xamarin-android-pr-builder-debug/xamarin-android/src/monodroid/monodroid.csproj]
//
void Util::package_hash_to_hex (uint32_t hash)
{
	for (uint32_t idx = 0; idx < 8; idx++) {
		package_property_suffix [idx] = hex_chars [(hash & (0xF0000000 >> idx * 4)) >> ((7 - idx) * 4)];
	}
	package_property_suffix[sizeof (package_property_suffix) / sizeof (char) - 1] = 0x00;
}
#else
template<typename IdxType>
inline void
Util::package_hash_to_hex (IdxType /* idx */)
{
	package_property_suffix[sizeof (package_property_suffix) / sizeof (char) - 1] = 0x00;
}

template<typename IdxType, typename ...Indices>
inline void
Util::package_hash_to_hex (uint32_t hash, IdxType idx, Indices... indices)
{
	package_property_suffix [idx] = hex_chars [(hash & (0xF0000000 >> idx * 4)) >> ((7 - idx) * 4)];
	package_hash_to_hex <IdxType> (hash, indices...);
}
#endif

void
Util::monodroid_store_package_name (const char *name)
{
	if (!name || *name == '\0')
		return;

	/* Android properties can be at most 32 bytes long (!) and so we mustn't append the package name
	 * as-is since it will most likely generate conflicts (packages tend to be named
	 * com.mycompany.app), so we simply generate a hash code and use that instead. We treat the name
	 * as a stream of bytes assumming it's an ASCII string using a simplified version of the hash
	 * algorithm used by BCL's String.GetHashCode ()
	 */
	const char *ch = name;
	uint32_t hash = 0;
	while (*ch)
		hash = (hash << 5) - (hash + static_cast<uint32_t>(*ch++));

#if WINDOWS
	package_hash_to_hex (hash);
#else
	// In C++14 or newer we could use std::index_sequence, but in C++11 it's a bit too much ado
	// for this simple case, so a manual sequence it is.
	//
	// And yes, I know it could be done in a simple loop or in even simpler 8 lines of code, but
	// that would be boring, wouldn't it? :)
	package_hash_to_hex (hash, 0u, 1u, 2u, 3u, 4u, 5u, 6u, 7u);
#endif
	log_info (LOG_DEFAULT, "Generated hash 0x%s for package name %s", package_property_suffix, name);
}

size_t
Util::monodroid_get_namespaced_system_property (const char *name, char **value)
{
	char *local_value = nullptr;
	ssize_t result = 0;

	if (value)
		*value = nullptr;

	if (package_property_suffix[0] != '\0') {
		log_info (LOG_DEFAULT, "Trying to get property %s.%s", name, package_property_suffix);
		char *propname;
#if WINDOWS
		propname = monodroid_strdup_printf ("%s.%s", name, package_property_suffix);
#else
		propname = string_concat (name, ".", package_property_suffix);
#endif
		result = androidSystem.monodroid_get_system_property (propname, &local_value);
#if WINDOWS
		free (propname);
#else
		delete[] propname;
#endif
	}

	if (result <= 0 || local_value == nullptr)
		result = androidSystem.monodroid_get_system_property (name, &local_value);

	if (result > 0) {
		if (local_value != nullptr && local_value[0] == '\0') {
			delete[] local_value;
			return 0;
		}

		log_info (LOG_DEFAULT, "Property '%s' has value '%s'.", name, local_value);

		if (value)
			*value = local_value;
		else
			delete[] local_value;
		return static_cast<size_t>(result);
	}

	return androidSystem.monodroid_get_system_property_from_overrides (name, value);
}

MonoAssembly *
Util::monodroid_load_assembly (MonoDomain *domain, const char *basename)
{
	MonoAssembly         *assm;
	MonoAssemblyName     *aname;
	MonoImageOpenStatus   status;

	aname = mono_assembly_name_new (basename);
	MonoDomain *current = mono_domain_get ();

	if (domain != current) {
		mono_domain_set (domain, FALSE);
		assm  = mono_assembly_load_full (aname, nullptr, &status, 0);
		mono_domain_set (current, FALSE);
	} else {
		assm  = mono_assembly_load_full (aname, nullptr, &status, 0);
	}

	mono_assembly_name_free (aname);

	if (!assm) {
		log_fatal (LOG_DEFAULT, "Unable to find assembly '%s'.", basename);
		exit (FATAL_EXIT_MISSING_ASSEMBLY);
	}
	return assm;
}

MonoObject *
Util::monodroid_runtime_invoke (MonoDomain *domain, MonoMethod *method, void *obj, void **params, MonoObject **exc)
{
	MonoDomain *current = mono_domain_get ();
	if (domain != current) {
		mono_domain_set (domain, FALSE);
		MonoObject *r = mono_runtime_invoke (method, obj, params, exc);
		mono_domain_set (current, FALSE);
		return r;
	} else {
		return mono_runtime_invoke (method, obj, params, exc);
	}
}

void
Util::monodroid_property_set (MonoDomain *domain, MonoProperty *property, void *obj, void **params, MonoObject **exc)
{
	MonoDomain *current = mono_domain_get ();
	if (domain != current) {
		mono_domain_set (domain, FALSE);
		mono_property_set_value (property, obj, params, exc);
		mono_domain_set (current, FALSE);
	} else {
		mono_property_set_value (property, obj, params, exc);
	}
}

MonoDomain*
Util::monodroid_create_appdomain (MonoDomain *parent_domain, const char *friendly_name, int shadow_copy, const char *shadow_directories)
{
	MonoClass *appdomain_setup_klass = monodroid_get_class_from_name (parent_domain, "mscorlib", "System", "AppDomainSetup");
	MonoClass *appdomain_klass = monodroid_get_class_from_name (parent_domain, "mscorlib", "System", "AppDomain");
	MonoMethod *create_domain = mono_class_get_method_from_name (appdomain_klass, "CreateDomain", 3);
	MonoProperty *shadow_copy_prop = mono_class_get_property_from_name (appdomain_setup_klass, "ShadowCopyFiles");
	MonoProperty *shadow_copy_dirs_prop = mono_class_get_property_from_name (appdomain_setup_klass, "ShadowCopyDirectories");

	MonoObject *setup = mono_object_new (parent_domain, appdomain_setup_klass);
	MonoString *mono_friendly_name = mono_string_new (parent_domain, friendly_name);
	MonoString *mono_shadow_copy = mono_string_new (parent_domain, shadow_copy ? "true" : "false");
	MonoString *mono_shadow_copy_dirs = shadow_directories == nullptr ? nullptr : mono_string_new (parent_domain, shadow_directories);

	monodroid_property_set (parent_domain, shadow_copy_prop, setup, reinterpret_cast<void**> (&mono_shadow_copy), nullptr);
	if (mono_shadow_copy_dirs != nullptr)
		monodroid_property_set (parent_domain, shadow_copy_dirs_prop, setup, reinterpret_cast<void**> (&mono_shadow_copy_dirs), nullptr);

	void *args[3] = { mono_friendly_name, nullptr, setup };
	auto appdomain = reinterpret_cast<MonoAppDomain*>(monodroid_runtime_invoke (parent_domain, create_domain, nullptr, args, nullptr));
	if (appdomain == nullptr)
		return nullptr;

	return mono_domain_from_appdomain (appdomain);
}

MonoClass*
Util::monodroid_get_class_from_name (MonoDomain *domain, const char* assembly, const char *_namespace, const char *type)
{
	MonoAssembly *assm = nullptr;
	MonoImage *image = nullptr;
	MonoClass *result = nullptr;
	MonoAssemblyName *aname = mono_assembly_name_new (assembly);
	MonoDomain *current = mono_domain_get ();

	if (domain != current)
		mono_domain_set (domain, FALSE);

	assm = mono_assembly_loaded (aname);
	if (assm != nullptr) {
		image = mono_assembly_get_image (assm);
		result = mono_class_from_name (image, _namespace, type);
	}

	if (domain != current)
		mono_domain_set (current, FALSE);

	mono_assembly_name_free (aname);

	return result;
}

MonoClass*
Util::monodroid_get_class_from_image (MonoDomain *domain, MonoImage *image, const char *_namespace, const char *type)
{
	MonoClass *result = nullptr;
	MonoDomain *current = mono_domain_get ();

	if (domain != current)
		mono_domain_set (domain, FALSE);

	result = mono_class_from_name (image, _namespace, type);

	if (domain != current)
		mono_domain_set (current, FALSE);

	return result;
}

jclass
Util::get_class_from_runtime_field (JNIEnv *env, jclass runtime, const char *name, bool make_gref)
{
	static constexpr char java_lang_class_sig[] = "Ljava/lang/Class;";

	jfieldID fieldID = env->GetStaticFieldID (runtime, name, java_lang_class_sig);
	if (fieldID == nullptr)
		return nullptr;

	jobject field = env->GetStaticObjectField (runtime, fieldID);
	if (field == nullptr)
		return nullptr;

	return reinterpret_cast<jclass> (make_gref ? osBridge.lref_to_gref (env, field) : field);
}

extern "C" void
monodroid_strfreev (char **str_array)
{
	utils.monodroid_strfreev (str_array);
}

extern "C" char**
monodroid_strsplit (const char *str, const char *delimiter, size_t max_tokens)
{
	return utils.monodroid_strsplit (str, delimiter, max_tokens);
}

extern "C" char*
monodroid_strdup_printf (const char *format, ...)
{
	va_list args;

	va_start (args, format);
	char *ret = utils.monodroid_strdup_vprintf (format, args);
	va_end (args);

	return ret;
}

extern "C" void
monodroid_store_package_name (const char *name)
{
	utils.monodroid_store_package_name (name);
}

extern "C" int
monodroid_get_namespaced_system_property (const char *name, char **value)
{
	return static_cast<int>(utils.monodroid_get_namespaced_system_property (name, value));
}

extern "C" FILE*
monodroid_fopen (const char* filename, const char* mode)
{
	return utils.monodroid_fopen (filename, mode);
}

extern "C" int
send_uninterrupted (int fd, void *buf, int len)
{
	if (len < 0)
		len = 0;
	return utils.send_uninterrupted (fd, buf, static_cast<size_t>(len));
}

extern "C" int
recv_uninterrupted (int fd, void *buf, int len)
{
	if (len < 0)
		len = 0;
	return static_cast<int>(utils.recv_uninterrupted (fd, buf, static_cast<size_t>(len)));
}

extern "C" void
set_world_accessable (const char *path)
{
	utils.set_world_accessable (path);
}

extern "C" void
create_public_directory (const char *dir)
{
	utils.create_public_directory (dir);
}

extern "C" char*
path_combine (const char *path1, const char *path2)
{
	return utils.path_combine (path1, path2);
}
