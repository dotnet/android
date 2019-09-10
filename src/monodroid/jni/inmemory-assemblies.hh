#ifndef INC_MONODROID_INMEMORY_ASSEMBLIES_H
#define INC_MONODROID_INMEMORY_ASSEMBLIES_H

#include "jni-wrappers.hh"
#include <string.h>
#include <mono/metadata/appdomain.h>
#include <mono/metadata/assembly.h>

#define DEFAULT_CAPACITY 8

namespace xamarin::android::internal {
	class InMemoryAssemblies
	{
		private:
			struct InMemoryAssemblyEntry
			{
				int domain_id;
				unsigned int assemblies_count;
				char **names;
				char **assemblies_bytes;
				unsigned int *assemblies_bytes_len;

				InMemoryAssemblyEntry (MonoDomain *domain, JNIEnv *env, jstring_array_wrapper &assemblies, jobjectArray assembliesBytes);
				~InMemoryAssemblyEntry ();
			};

		public:
			InMemoryAssemblies ()
			{
				capacity = DEFAULT_CAPACITY;
				entries = new InMemoryAssemblyEntry*[DEFAULT_CAPACITY];
			}

			bool has_assemblies () const { return length > 0; }
			MonoAssembly* load_assembly_from_memory (MonoDomain *domain, MonoAssemblyName *name);
			void add_or_update_from_java (MonoDomain *domain, JNIEnv *env, jstring_array_wrapper &assemblies, jobjectArray assembliesBytes);
			void clear_for_domain (MonoDomain *domain);

		private:
			InMemoryAssemblyEntry **entries;
			unsigned int capacity;
			unsigned int length;

			InMemoryAssemblyEntry* find_entry (int domain_id);
			void add_or_replace_entry (InMemoryAssemblyEntry *new_entry);
			void add_entry (InMemoryAssemblyEntry *entry);
			InMemoryAssemblyEntry* remove_entry (int domain_id);
	};
}

#endif