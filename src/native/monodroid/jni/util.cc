#include <cerrno>
#include <cstdarg>
#include <cstdio>
#include <cstdlib>
#include <cstring>

#include <sys/socket.h>
#include <sys/stat.h>
#include <sys/types.h>

#ifdef HAVE_BSD_STRING_H
#include <bsd/string.h>
#endif

#include <mono/metadata/appdomain.h>
#include <mono/metadata/assembly.h>
#include <mono/metadata/class.h>

#include "util.hh"
#include "globals.hh"
#include "timing-internal.hh"

using namespace xamarin::android;
using namespace xamarin::android::internal;

void timing_point::mark ()
{
	FastTiming::get_time (sec, ns);
}

timing_diff::timing_diff (const timing_period &period)
{
	FastTiming::calculate_interval (period.start, period.end, *this);
}

Util::Util ()
{
	page_size = getpagesize ();
}

int
Util::send_uninterrupted (int fd, void *buf, size_t len)
{
	ssize_t res;

	do {
		res = send (fd, buf, len, 0);
	} while (res == -1 && errno == EINTR);

	return static_cast<size_t>(res) == len;
}

ssize_t
Util::recv_uninterrupted (int fd, void *buf, size_t len)
{
	using nbytes_type = size_t;

	ssize_t res;
	size_t total = 0;
	int flags = 0;
	nbytes_type nbytes;

	do {
		nbytes = static_cast<nbytes_type>(len - total);
		res = recv (fd, (char *) buf + total, nbytes, flags);

		if (res > 0)
			total += static_cast<size_t>(res);
	} while ((res > 0 && total < len) || (res == -1 && errno == EINTR));

	return static_cast<ssize_t>(total);
}

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

	// In C++14 or newer we could use std::index_sequence, but in C++11 it's a bit too much ado
	// for this simple case, so a manual sequence it is.
	//
	// And yes, I know it could be done in a simple loop or in even simpler 8 lines of code, but
	// that would be boring, wouldn't it? :)
	package_hash_to_hex (hash, 0u, 1u, 2u, 3u, 4u, 5u, 6u, 7u);
	log_debug (LOG_DEFAULT, "Generated hash 0x%s for package name %s", package_property_suffix, name);
}

MonoAssembly*
Util::monodroid_load_assembly (MonoAssemblyLoadContextGCHandle alc_handle, const char *basename)
{
	MonoImageOpenStatus  status;
	MonoAssemblyName    *aname = mono_assembly_name_new (basename);
	MonoAssembly        *assm = mono_assembly_load_full_alc (alc_handle, aname, nullptr, &status);

	mono_assembly_name_free (aname);

	if (assm == nullptr || status != MonoImageOpenStatus::MONO_IMAGE_OK) {
		log_fatal (LOG_DEFAULT, "Unable to find assembly '%s'.", basename);
		Helpers::abort_application ();
	}
	return assm;
}

MonoAssembly *
Util::monodroid_load_assembly (MonoDomain *domain, const char *basename)
{
	MonoAssembly         *assm;
	MonoAssemblyName     *aname;
	MonoImageOpenStatus   status;

	aname = mono_assembly_name_new (basename);
	MonoDomain *current = get_current_domain ();

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
		Helpers::abort_application ();
	}
	return assm;
}

MonoClass*
Util::monodroid_get_class_from_name ([[maybe_unused]] MonoDomain *domain, const char* assembly, const char *_namespace, const char *type)
{
	MonoClass *result;
	MonoAssemblyName *aname = mono_assembly_name_new (assembly);
	MonoAssembly *assm = mono_assembly_loaded (aname);
	if (assm != nullptr) {
		MonoImage *image = mono_assembly_get_image (assm);
		result = mono_class_from_name (image, _namespace, type);
	} else
		result = nullptr;

	mono_assembly_name_free (aname);
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
