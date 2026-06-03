//
// This generator emits the CoreCLR `internal_pinvokes` table (symbols linked into the host itself:
// `xa-internal-api`, `java-interop`, `liblog`) and the library-name hashes. It is NOT wired into
// `build-tools/scripts/generate-pinvoke-tables.sh` (that script only regenerates the MonoVM table),
// because the CoreCLR override no longer serves the .NET BCL p/invokes from a static table. Run it by
// hand only when the internal p/invoke list changes, by compiling this file with a C++20 compiler
// (g++ 10+, clang 11+, on mac it may require XCode 12.5 or newer) against the CoreCLR host include
// dirs and running the resulting binary with the output `pinvoke-tables.include` path.
//
// Whenever a new internal p/invoke is added, try to keep the entries sorted alphabetically.  This is
// not required by the generator but easier to examine by humans.
//
// If a new library is added, please remember to generate a hash of its name and update pinvoke-override-api.cc
//
// To get the list of exported native symbols for a library, you can run the following command on Unix:
//
//   for s in $(llvm-nm -DUj [LIBRARY] | sort); do echo "\"$s\","; done
//
#include <algorithm>
#include <cerrno>
#include <cstring>
#include <filesystem>
#include <fstream>
#include <iostream>
#include <iomanip>
#include <memory>
#include <string>
#include <unordered_set>
#include <vector>

#include <shared/xxhash.hh>

namespace fs = std::filesystem;
using namespace xamarin::android;

const std::vector<std::string> internal_pinvoke_names = {
//	"create_public_directory",
//	"java_interop_free",
//	"monodroid_clear_gdb_wait",
//	"_monodroid_counters_dump",
	"_monodroid_detect_cpu_and_architecture",
//	"monodroid_dylib_mono_free",
//	"monodroid_dylib_mono_init",
//	"monodroid_dylib_mono_new",
//	"monodroid_embedded_assemblies_set_assemblies_prefix",
//	"monodroid_fopen",
	"monodroid_free",
	"_monodroid_gc_wait_for_bridge_processing",
//	"_monodroid_get_dns_servers",
//	"monodroid_get_dylib",
//	"monodroid_get_namespaced_system_property",
//	"_monodroid_get_network_interface_supports_multicast",
//	"_monodroid_get_network_interface_up_state",
//	"monodroid_get_system_property",
	"_monodroid_gref_dec",
	"_monodroid_gref_get",
	"_monodroid_gref_inc",
	"_monodroid_gref_log",
	"_monodroid_gref_log_delete",
	"_monodroid_gref_log_new",
	"monodroid_log",
//	"monodroid_log_traces",
	"_monodroid_lookup_replacement_type",
	"_monodroid_lookup_replacement_method_info",
	"_monodroid_lref_log_delete",
	"_monodroid_lref_log_new",
	"_monodroid_max_gref_get",
//	"monodroid_strdup_printf",
//	"monodroid_strfreev",
//	"monodroid_strsplit",
	"monodroid_timing_start",
	"monodroid_timing_stop",
	"monodroid_TypeManager_get_java_class_name",
	"clr_typemap_managed_to_java",
	"clr_typemap_java_to_managed",
	"clr_initialize_gc_bridge",
	"_monodroid_weak_gref_dec",
	"_monodroid_weak_gref_delete",
	"_monodroid_weak_gref_get",
	"_monodroid_weak_gref_inc",
	"_monodroid_weak_gref_new",
//	"path_combine",
//	"recv_uninterrupted",
//	"send_uninterrupted",
//	"set_world_accessable",
	"xamarin_app_init",

// We can treat liblog as "internal", since we link against it
	"__android_log_print",
};

template<typename Hash>
struct PinvokeEntry
{
	std::string name;
	Hash hash;
	bool write_func_pointer;

	template<class Os> friend
	Os& operator<< (Os& os, PinvokeEntry<Hash> const& p)
	{
		os << std::showbase << std::hex << p.hash << ", \"" << p.name << "\", ";

		if (p.write_func_pointer) {
			return os << "reinterpret_cast<void*>(&" << p.name << ")";
		}

		return os << "nullptr";
	}
};

void print (std::ostream& os, std::string comment, std::string variable_name, auto const& seq)
{
	os << "\t//" << comment << '\n';
	os << "\tstd::array<PinvokeEntry, " << std::dec << seq.size () << "> " << variable_name << " {{" << std::endl;

	for (auto const& elem : seq) {
		os << "\t\t{" << elem << "}," << std::endl;
	}

	os << "\t}};" << std::endl << std::endl;
}

template<typename Hash>
bool add_hash (std::string const& pinvoke, Hash hash, std::vector<PinvokeEntry<Hash>>& vec, std::unordered_set<Hash>& used_cache, bool write_func_pointer)
{
	vec.emplace_back (pinvoke, hash, write_func_pointer);
	if (used_cache.contains (hash)) {
		std::cerr << (sizeof(Hash) == 4 ? "32" : "64") << "-bit hash collision for key '" << pinvoke << "': " << std::hex << std::showbase << hash << std::endl;
		return true;
	}

	used_cache.insert (hash);
	return false;
}

bool generate_hashes (std::string table_name, std::vector<std::string> const& names, std::vector<PinvokeEntry<uint32_t>>& pinvokes32, std::vector<PinvokeEntry<uint64_t>>& pinvokes64, bool write_func_pointer)
{
	std::unordered_set<uint32_t> used_pinvokes32{};
	std::unordered_set<uint64_t> used_pinvokes64{};
	uint32_t hash32;
	uint64_t hash64;
	bool have_collisions = false;

	std::cout << "There are " << names.size () << " " << table_name << " p/invoke functions" << std::endl;
	for (std::string const& pinvoke : names) {
		have_collisions |= add_hash (pinvoke, xxhash32::hash (pinvoke.c_str (), pinvoke.length ()), pinvokes32, used_pinvokes32, write_func_pointer);
		have_collisions |= add_hash (pinvoke, xxhash64::hash (pinvoke.c_str (), pinvoke.length ()), pinvokes64, used_pinvokes64, write_func_pointer);
	}

	std::cout << "p/invoke hash collisions for '" << table_name << "' were " << (have_collisions ? "" : "not ") << "found" << std::endl;

	std::ranges::sort (pinvokes32, {}, &PinvokeEntry<uint32_t>::hash);
	std::ranges::sort (pinvokes64, {}, &PinvokeEntry<uint64_t>::hash);

	return have_collisions;
}

template<typename Hash>
void write_library_name_hash (Hash (*hasher)(const char*, size_t), std::ostream& os, std::string library_name, std::string variable_prefix)
{
	Hash hash = hasher (library_name.c_str (), library_name.length ());
	os << "constexpr hash_t " << variable_prefix << "_library_hash = " << std::hex << hash << ";" << std::endl;
}

template<typename Hash>
void write_library_name_hashes (Hash (*hasher)(const char*, size_t), std::ostream& output)
{
	write_library_name_hash (hasher, output, "java-interop", "java_interop");
	write_library_name_hash (hasher, output, "xa-internal-api", "xa_internal_api");
	write_library_name_hash (hasher, output, "liblog", "android_liblog");
	write_library_name_hash (hasher, output, "libSystem.Native", "system_native");
	write_library_name_hash (hasher, output, "libSystem.IO.Compression.Native", "system_io_compression_native");
	write_library_name_hash (hasher, output, "libSystem.Security.Cryptography.Native.Android", "system_security_cryptography_native_android");
	write_library_name_hash (hasher, output, "libSystem.Globalization.Native", "system_globalization_native");
}

int main (int argc, char **argv)
{
	if (argc < 2) {
		std::cerr << "Usage: generate-pinvoke-tables OUTPUT_FILE_PATH" << std::endl << std::endl;
		return 1;
	}

	fs::path output_file_path {argv[1]};

	if (fs::exists (output_file_path)) {
		if (fs::is_directory (output_file_path)) {
			std::cerr << "Output destination '" << output_file_path << "' is a directory" << std::endl;
			return 1;
		}

		fs::remove (output_file_path);
	} else {
		fs::path file_dir = output_file_path.parent_path ();
		if (fs::exists (file_dir)) {
			if (!fs::is_directory (file_dir)) {
				std::cerr << "Output destination parent path points to a file ('" << file_dir << "'" << std::endl;
				return 1;
			}
		} else if (!file_dir.empty ()) {
			if (!fs::create_directories (file_dir)) {
				std::cerr << "Failed to create output directory '" << file_dir << "'" << std::endl;
				std::cerr << strerror (errno) << std::endl;
				return 1;
			}
		}
	}

	bool have_collisions = false;
	std::vector<PinvokeEntry<uint32_t>> internal_pinvokes32{};
	std::vector<PinvokeEntry<uint64_t>> internal_pinvokes64{};
	have_collisions |= generate_hashes ("internal", internal_pinvoke_names, internal_pinvokes32, internal_pinvokes64, true);

	std::cout << "Generating tables in file: " << output_file_path << std::endl;

	std::ofstream output {output_file_path, std::ios::binary};

	output << "//" << std::endl;
	output << "// Autogenarated file. DO NOT EDIT." << std::endl;
	output << "//" << std::endl;
	output << "// To regenerate run ../../../../build-tools/scripts/generate-pinvoke-tables.sh on Linux or macOS" << std::endl;
	output << "// A compiler with support for C++20 ranges is required" << std::endl;
	output << "//" << std::endl << std::endl;

	output << "#include <array>" << std::endl;
	output << "#include <cstdint>" << std::endl << std::endl;

	output << "namespace {" << std::endl;
	output << "#if INTPTR_MAX == INT64_MAX" << std::endl;
	print (output, "64-bit internal p/invoke table", "internal_pinvokes", internal_pinvokes64);
	output << std::endl;
	write_library_name_hashes<uint64_t> (xxhash64::hash, output);

	output << "#else" << std::endl;

	print (output, "32-bit internal p/invoke table", "internal_pinvokes", internal_pinvokes32);
	output << std::endl;
	write_library_name_hashes<uint32_t> (xxhash32::hash, output);

	output << "#endif" << std::endl << std::endl;

	output << "constexpr size_t internal_pinvokes_count = " << std::dec << std::noshowbase << internal_pinvoke_names.size () << ";" << std::endl;
	output << "} // end of anonymous namespace" << std::endl;

	return have_collisions ? 1 : 0;
}

// This serves as a quick compile-time test of the algorithm's correctness.
// The tests are copied from https://github.com/ekpyron/xxhashct/test.cpp

template<uint64_t value, uint64_t expected>
struct constexpr_test {
	static_assert (value == expected, "Compile-time hash mismatch.");
};

constexpr_test<xxhash32::hash<0> ("", 0), 0x2CC5D05U> constexprTest_1;
constexpr_test<xxhash32::hash<2654435761U> ("", 0), 0x36B78AE7U> constexprTest_2;
//constexpr_test<xxhash64::hash<0> ("", 0), 0xEF46DB3751D8E999ULL> constexprTest_3;
//constexpr_test<xxhash64::hash<2654435761U> ("", 0), 0xAC75FDA2929B17EFULL> constexprTest_4;
constexpr_test<xxhash32::hash<0> ("test", 4), 0x3E2023CFU> constexprTest32_5;
constexpr_test<xxhash32::hash<2654435761U> ("test", 4), 0xA9C14438U> constexprTest32_6;
//constexpr_test<xxhash64::hash<0> ("test", 4), 0x4fdcca5ddb678139ULL> constexprTest64_7;
//constexpr_test<xxhash64::hash<2654435761U> ("test", 4), 0x5A183B8150E2F651ULL> constexprTest64_8;
