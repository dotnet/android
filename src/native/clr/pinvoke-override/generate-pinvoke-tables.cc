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
#include <cstdint>
#include <cstring>
#include <filesystem>
#include <fstream>
#include <iostream>
#include <iomanip>
#include <limits>
#include <memory>
#include <string>
#include <unordered_set>
#include <vector>

#include <runtime-base/crc32.hh>

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

struct PinvokeEntry
{
	std::string name;
	uint32_t hash;
	bool write_func_pointer;

	template<class Os> friend
	Os& operator<< (Os& os, PinvokeEntry const& p)
	{
		os << "crc32_hash (\"" << p.name << "\"), \"" << p.name << "\", ";

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

bool add_hash (std::string const& pinvoke, uint32_t hash, std::vector<PinvokeEntry>& vec, std::unordered_set<uint32_t>& used_cache, bool write_func_pointer)
{
	vec.emplace_back (pinvoke, hash, write_func_pointer);
	if (used_cache.contains (hash)) {
		std::cerr << "CRC32 hash collision for key '" << pinvoke << "': " << std::hex << std::showbase << hash << std::endl;
		return true;
	}

	used_cache.insert (hash);
	return false;
}

bool generate_hashes (std::string table_name, std::vector<std::string> const& names, std::vector<PinvokeEntry>& pinvokes, bool write_func_pointer)
{
	std::unordered_set<uint32_t> used_pinvokes32{};
	bool have_collisions = false;

	std::cout << "There are " << names.size () << " " << table_name << " p/invoke functions" << std::endl;
	for (std::string const& pinvoke : names) {
		have_collisions |= add_hash (pinvoke, crc32_hash (pinvoke.c_str (), pinvoke.length ()), pinvokes, used_pinvokes32, write_func_pointer);
	}

	std::cout << "p/invoke hash collisions for '" << table_name << "' were " << (have_collisions ? "" : "not ") << "found" << std::endl;

	std::ranges::sort (pinvokes, {}, &PinvokeEntry::hash);

	return have_collisions;
}

void write_library_name_hash (std::ostream& os, std::string library_name, std::string variable_prefix)
{
	os << "constexpr hash_t " << variable_prefix << "_library_hash = crc32_hash (\"" << library_name << "\");" << std::endl;
}

void write_library_name_hashes (std::ostream& output)
{
	write_library_name_hash (output, "java-interop", "java_interop");
	write_library_name_hash (output, "xa-internal-api", "xa_internal_api");
	write_library_name_hash (output, "liblog", "android_liblog");
	write_library_name_hash (output, "libSystem.Native", "system_native");
	write_library_name_hash (output, "libSystem.IO.Compression.Native", "system_io_compression_native");
	write_library_name_hash (output, "libSystem.Security.Cryptography.Native.Android", "system_security_cryptography_native_android");
	write_library_name_hash (output, "libSystem.Globalization.Native", "system_globalization_native");
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
	std::vector<PinvokeEntry> internal_pinvokes{};
	have_collisions |= generate_hashes ("internal", internal_pinvoke_names, internal_pinvokes, true);

	std::cout << "Generating tables in file: " << output_file_path << std::endl;

	std::ofstream output {output_file_path, std::ios::binary};

	output << "//" << std::endl;
	output << "// Autogenarated file. DO NOT EDIT." << std::endl;
	output << "//" << std::endl;
	output << "// To regenerate, compile and run generate-pinvoke-tables.cc with the output path as the only argument." << std::endl;
	output << "// A compiler with support for C++20 ranges is required" << std::endl;
	output << "//" << std::endl << std::endl;

	output << "#include <array>" << std::endl;
	output << "#include <cstdint>" << std::endl << std::endl;

	output << "namespace {" << std::endl;
	print (output, " CRC32 internal p/invoke table", "internal_pinvokes", internal_pinvokes);
	output << std::endl;
	write_library_name_hashes (output);
	output << std::endl;

	output << "constexpr size_t internal_pinvokes_count = " << std::dec << std::noshowbase << internal_pinvoke_names.size () << ";" << std::endl;
	output << "} // end of anonymous namespace" << std::endl;

	return have_collisions ? 1 : 0;
}

// This serves as a quick compile-time test of the algorithm's correctness.

template<uint32_t value, uint32_t expected>
struct constexpr_test {
	static_assert (value == expected, "Compile-time hash mismatch.");
};

constexpr_test<crc32_hash ("", 0), std::numeric_limits<uint32_t>::max ()> constexprTest_1;
constexpr_test<crc32_hash ("test", 4), 0xd87f7e0c> constexprTest_2;
constexpr_test<crc32_hash ("java-interop"), 0x33b98009> constexprTest_3;
