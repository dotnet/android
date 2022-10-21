#if !defined (ANDROID)
#include "designer-assemblies.hh"
#include "util.hh"

#include <mono/metadata/appdomain.h>
#include <mono/metadata/assembly.h>
#include <mono/metadata/reflection.h>

extern "C" {
#include "java-interop-util.h"
}

using namespace xamarin::android::internal;

void
DesignerAssemblies::add_or_update_from_java (MonoDomain *domain, JNIEnv *env, jstring_array_wrapper &assemblies, jobjectArray assembliesBytes, jstring_array_wrapper &assembliesPaths)
{
	log_info (LOG_DEFAULT, "%s ENTER", __PRETTY_FUNCTION__);
	abort_if_invalid_pointer_argument (assembliesBytes);
	log_info (LOG_DEFAULT, "Location: %s:%u", __FILE__, __LINE__);
	DesignerAssemblyEntry *new_entry = new DesignerAssemblyEntry (domain, env, assemblies, assembliesBytes, assembliesPaths);
	log_info (LOG_DEFAULT, "Location: %s:%u", __FILE__, __LINE__);
	add_or_replace_entry (new_entry);
	log_info (LOG_DEFAULT, "%s LEAVE at %s:%u", __PRETTY_FUNCTION__, __FILE__, __LINE__);
}

MonoAssembly*
DesignerAssemblies::try_load_assembly (MonoDomain *domain, MonoAssemblyName *name)
{
	log_info (LOG_DEFAULT, "%s ENTER", __PRETTY_FUNCTION__);
	int domain_id = mono_domain_get_id (domain);
	log_info (LOG_DEFAULT, "Location: %s:%u", __FILE__, __LINE__);
	DesignerAssemblyEntry *entry = find_entry (domain_id);
	log_info (LOG_DEFAULT, "Location: %s:%u", __FILE__, __LINE__);
	if (entry == nullptr) {
		log_info (LOG_DEFAULT, "Location: %s:%u", __FILE__, __LINE__);
		log_info (LOG_DEFAULT, "%s LEAVE at %s:%u", __PRETTY_FUNCTION__, __FILE__, __LINE__);
		return nullptr;
	}
	log_info (LOG_DEFAULT, "Location: %s:%u", __FILE__, __LINE__);

	const char *asm_name = mono_assembly_name_get_name (name);
	log_info (LOG_DEFAULT, "Location: %s:%u", __FILE__, __LINE__);
	unsigned int asm_count = entry->assemblies_count;
	log_info (LOG_DEFAULT, "Location: %s:%u", __FILE__, __LINE__);

	for (unsigned int i = 0; i < asm_count; i++) {
		log_info (LOG_DEFAULT, "Location: %s:%u (i == %u)", __FILE__, __LINE__, i);
		const char *entry_name = entry->names[i];
		log_info (LOG_DEFAULT, "Location: %s:%u", __FILE__, __LINE__);
		const char *entry_bytes = entry->assemblies_bytes[i];
		log_info (LOG_DEFAULT, "Location: %s:%u", __FILE__, __LINE__);
		const unsigned int entry_bytes_len = entry->assemblies_bytes_len[i];
		log_info (LOG_DEFAULT, "Location: %s:%u", __FILE__, __LINE__);
		const char *entry_path = entry->assemblies_paths[i];
		log_info (LOG_DEFAULT, "Location: %s:%u", __FILE__, __LINE__);

		if (strcmp (asm_name, entry_name) != 0) {
			log_info (LOG_DEFAULT, "Location: %s:%u", __FILE__, __LINE__);
			continue;
		}
		log_info (LOG_DEFAULT, "Location: %s:%u", __FILE__, __LINE__);

		/* We use the managed assembly loading API as there is unfortunately no public unmanaged API
		 * to select the loading context to use (it would require access to the MonoAssemblyLoadRequest API)
		 * which mean we can't properly do either loading from memory or call LoadFrom
		 */
		MonoClass *assembly_klass = Util::monodroid_get_class_from_name (domain, "mscorlib", "System.Reflection", "Assembly");

		if (entry_bytes_len > 0) {
			log_info (LOG_DEFAULT, "Location: %s:%u", __FILE__, __LINE__);
			MonoClass *byte_klass = mono_get_byte_class ();
			// Use the variant with 3 parameters so that we always get the first argument being a byte[]
			// (the two last don't matter since we pass null anyway)
			MonoMethod *assembly_load_method = mono_class_get_method_from_name (assembly_klass, "Load", 3);
			MonoArray *byteArray = mono_array_new (domain, byte_klass, entry_bytes_len);
			mono_value_copy_array (byteArray, 0, const_cast<char*> (entry_bytes), static_cast<int> (entry_bytes_len));

			void *args[3];
			args[0] = byteArray;
			args[1] = nullptr;
			args[2] = nullptr;
			MonoReflectionAssembly *res = (MonoReflectionAssembly *)Util::monodroid_runtime_invoke (domain, assembly_load_method, nullptr, args, nullptr);
			log_info (LOG_DEFAULT, "Location: %s:%u", __FILE__, __LINE__);
			log_info (LOG_DEFAULT, "%s LEAVE", __PRETTY_FUNCTION__);
			return mono_reflection_assembly_get_assembly (res);
		} else if (entry_path != nullptr && Util::file_exists (entry_path)) {
			log_info (LOG_DEFAULT, "Location: %s:%u", __FILE__, __LINE__);
			MonoMethod *assembly_load_method = mono_class_get_method_from_name (assembly_klass, "LoadFrom", 1);
			MonoString *asm_path = mono_string_new (domain, entry_path);

			void *args[1];
			args[0] = asm_path;
			MonoReflectionAssembly *res = (MonoReflectionAssembly *)Util::monodroid_runtime_invoke (domain, assembly_load_method, nullptr, args, nullptr);
			log_info (LOG_DEFAULT, "Location: %s:%u", __FILE__, __LINE__);
			log_info (LOG_DEFAULT, "%s LEAVE", __PRETTY_FUNCTION__);
			return mono_reflection_assembly_get_assembly (res);
		}
	}

	log_info (LOG_DEFAULT, "Location: %s:%u", __FILE__, __LINE__);
	log_info (LOG_DEFAULT, "%s LEAVE", __PRETTY_FUNCTION__);
	return nullptr;
}

void
DesignerAssemblies::clear_for_domain (MonoDomain *domain)
{
	log_info (LOG_DEFAULT, "%s ENTER", __PRETTY_FUNCTION__);
	int domain_id = mono_domain_get_id (domain);
	log_info (LOG_DEFAULT, "Location: %s:%u", __FILE__, __LINE__);
	DesignerAssemblyEntry *entry = remove_entry (domain_id);
	log_info (LOG_DEFAULT, "Location: %s:%u", __FILE__, __LINE__);
	delete entry;
	log_info (LOG_DEFAULT, "%s LEAVE at %s:%u", __PRETTY_FUNCTION__, __FILE__, __LINE__);
}

DesignerAssemblies::DesignerAssemblyEntry*
DesignerAssemblies::find_entry (int domain_id)
{
	log_info (LOG_DEFAULT, "%s ENTER", __PRETTY_FUNCTION__);
	for (unsigned int i = 0; i < length; i++) {
		auto entry = entries[i];
		if (entry->domain_id == domain_id) {
			log_info (LOG_DEFAULT, "Location: %s:%u", __FILE__, __LINE__);
			return entry;
		}
	}
	log_info (LOG_DEFAULT, "Location: %s:%u", __FILE__, __LINE__);
	return nullptr;
}

void
DesignerAssemblies::add_or_replace_entry (DesignerAssemblies::DesignerAssemblyEntry *new_entry)
{
	log_info (LOG_DEFAULT, "%s ENTER", __PRETTY_FUNCTION__);
	for (unsigned int i = 0; i < length; i++) {
		auto entry = entries[i];
		if (entry->domain_id == new_entry->domain_id) {
			entries[i] = new_entry;
			delete entry;
			log_info (LOG_DEFAULT, "%s LEAVE at %s:%u", __PRETTY_FUNCTION__, __FILE__, __LINE__);
			return;
		}
	}
	log_info (LOG_DEFAULT, "Location: %s:%u", __FILE__, __LINE__);
	add_entry (new_entry);
	log_info (LOG_DEFAULT, "%s LEAVE at %s:%u", __PRETTY_FUNCTION__, __FILE__, __LINE__);
}

void
DesignerAssemblies::add_entry (DesignerAssemblies::DesignerAssemblyEntry *entry)
{
	log_info (LOG_DEFAULT, "%s ENTER", __PRETTY_FUNCTION__);
	if (length >= capacity) {
		log_info (LOG_DEFAULT, "Location: %s:%u", __FILE__, __LINE__);
		capacity = MULTIPLY_WITH_OVERFLOW_CHECK(unsigned int, capacity, 2);
		DesignerAssemblyEntry **new_entries = new DesignerAssemblyEntry*[capacity];
		log_info (LOG_DEFAULT, "Location: %s:%u", __FILE__, __LINE__);
		memcpy (new_entries, entries, MULTIPLY_WITH_OVERFLOW_CHECK(size_t, sizeof(void*), length));
		log_info (LOG_DEFAULT, "Location: %s:%u", __FILE__, __LINE__);
		DesignerAssemblyEntry **old_entries = entries;
		entries = new_entries;
		log_info (LOG_DEFAULT, "Location: %s:%u", __FILE__, __LINE__);
		delete[] old_entries;
		log_info (LOG_DEFAULT, "Location: %s:%u", __FILE__, __LINE__);
	}
	log_info (LOG_DEFAULT, "Location: %s:%u", __FILE__, __LINE__);
	entries[length++] = entry;
	log_info (LOG_DEFAULT, "%s LEAVE at %s:%u", __PRETTY_FUNCTION__, __FILE__, __LINE__);
}

DesignerAssemblies::DesignerAssemblyEntry*
DesignerAssemblies::remove_entry (int domain_id)
{
	log_info (LOG_DEFAULT, "%s ENTER", __PRETTY_FUNCTION__);
	for (unsigned int i = 0; i < length; i++) {
		DesignerAssemblyEntry *entry = entries[i];
		if (entry->domain_id == domain_id) {
			log_info (LOG_DEFAULT, "Location: %s:%u", __FILE__, __LINE__);
			for (unsigned int j = i; j < length - 1; j++)
				entries[j] = entries[j + 1];
			log_info (LOG_DEFAULT, "Location: %s:%u", __FILE__, __LINE__);
			length--;
			entries[length] = nullptr;
			log_info (LOG_DEFAULT, "%s LEAVE at %s:%u", __PRETTY_FUNCTION__, __FILE__, __LINE__);
			return entry;
		}
	}
	log_info (LOG_DEFAULT, "%s LEAVE at %s:%u", __PRETTY_FUNCTION__, __FILE__, __LINE__);
	return nullptr;
}

DesignerAssemblies::DesignerAssemblyEntry::DesignerAssemblyEntry (MonoDomain *domain, JNIEnv *env, jstring_array_wrapper &assemblies, jobjectArray assembliesBytes, jstring_array_wrapper &assembliesPaths)
{
	log_info (LOG_DEFAULT, "%s ENTER", __PRETTY_FUNCTION__);
	this->domain_id = mono_domain_get_id (domain);
	this->assemblies_count = static_cast<unsigned int> (assemblies.get_length ());
	this->names = new char*[assemblies_count];
	this->assemblies_bytes = new char*[assemblies_count];
	this->assemblies_paths = new char*[assemblies_count];
	this->assemblies_bytes_len = new unsigned int[assemblies_count];

	log_info (LOG_DEFAULT, "Location: %s:%u", __FILE__, __LINE__);
	for (unsigned int index = 0; index < assemblies_count; index++) {
		jstring_wrapper &assembly = assemblies [index];
		names[index] = Util::strdup_new (assembly.get_cstr ());

		// Copy in-memory assembly bytes if any
		jboolean is_copy;
		jbyteArray assembly_byte_array = reinterpret_cast <jbyteArray> (env->GetObjectArrayElement (assembliesBytes, static_cast<jsize> (index)));
		if (assembly_byte_array != nullptr) {
			log_info (LOG_DEFAULT, "Location: %s:%u", __FILE__, __LINE__);
			unsigned int bytes_len = static_cast<unsigned int> (env->GetArrayLength (assembly_byte_array));
			jbyte *bytes = env->GetByteArrayElements (assembly_byte_array, &is_copy);
			assemblies_bytes_len[index] = bytes_len;
			assemblies_bytes[index] = new char[bytes_len];
			memcpy (assemblies_bytes[index], bytes, bytes_len);
			env->ReleaseByteArrayElements (assembly_byte_array, bytes, JNI_ABORT);
			log_info (LOG_DEFAULT, "Location: %s:%u", __FILE__, __LINE__);
		} else {
			log_info (LOG_DEFAULT, "Location: %s:%u", __FILE__, __LINE__);
			assemblies_bytes_len[index] = 0;
			assemblies_bytes[index] = nullptr;
			log_info (LOG_DEFAULT, "Location: %s:%u", __FILE__, __LINE__);
		}

		log_info (LOG_DEFAULT, "Location: %s:%u", __FILE__, __LINE__);
		// Copy path to the specific assembly if any
		jstring_wrapper &assembly_path = assembliesPaths [index];
		assemblies_paths[index] = assembly_path.hasValue () ? Util::strdup_new (assembly_path.get_cstr ()) : nullptr;
		log_info (LOG_DEFAULT, "Location: %s:%u", __FILE__, __LINE__);
	}
	log_info (LOG_DEFAULT, "%s LEAVE at %s:%u", __PRETTY_FUNCTION__, __FILE__, __LINE__);
}

DesignerAssemblies::DesignerAssemblyEntry::~DesignerAssemblyEntry ()
{
	log_info (LOG_DEFAULT, "%s ENTER", __PRETTY_FUNCTION__);
	for (unsigned int i = 0; i < assemblies_count; ++i) {
		delete[] names [i];
		delete[] assemblies_bytes [i];
		delete[] assemblies_paths [i];
	}
	log_info (LOG_DEFAULT, "Location: %s:%u", __FILE__, __LINE__);
	delete[] names;
	delete[] assemblies_bytes;
	delete[] assemblies_paths;
	delete[] assemblies_bytes_len;
	log_info (LOG_DEFAULT, "%s LEAVE at %s:%u", __PRETTY_FUNCTION__, __FILE__, __LINE__);
}
#endif // ndef ANDROID
