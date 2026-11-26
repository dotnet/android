#include <cstdarg>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <fstream>
#include <iterator>
#include <limits>
#include <source_location>
#include <vector>

#include <runtime-base/android-system.hh>
#include <runtime-base/binary-blob-loader.hh>
#include <runtime-base/coreclr-bootstrap.hh>
#include <shared/helpers.hh>
#include <shared/log_functions.hh>

using namespace xamarin::android;

namespace
{
	const char *system_property = nullptr;
	std::vector<uint8_t> bootstrap_body;
}

extern "C" int __system_property_get (const char*, char *value)
{
	if (system_property == nullptr || *system_property == '\0') {
		return 0;
	}
	size_t length = std::strlen (system_property);
	if (length >= PROP_VALUE_MAX) {
		Helpers::abort_application ("Test system property is too long");
	}
	std::memcpy (value, system_property, length + 1);
	return static_cast<int>(length);
}

auto AndroidSystem::lookup_system_property (const char*, size_t &value_len) noexcept -> const char*
{
	value_len = 0;
	return nullptr;
}

auto BinaryBlobLoader::load (const char*) noexcept -> BinaryBlobPayload
{
	return { bootstrap_body.data (), static_cast<uint32_t>(bootstrap_body.size ()) };
}

void xamarin::android::log_warnf (LogCategories, const char *format, ...) noexcept
{
	va_list args;
	va_start (args, format);
	std::vfprintf (stderr, format, args);
	va_end (args);
	std::fputc ('\n', stderr);
}

void Helpers::abort_application (LogCategories, const char *message, bool, std::source_location) noexcept
{
	std::fprintf (stderr, "%s\n", message);
	std::exit (EXIT_FAILURE);
}

void Helpers::abort_applicationf (LogCategories, std::source_location, const char *format, ...) noexcept
{
	va_list args;
	va_start (args, format);
	std::vfprintf (stderr, format, args);
	va_end (args);
	std::fputc ('\n', stderr);
	std::exit (EXIT_FAILURE);
}

int main (int argc, char **argv)
{
	if (argc == 4 && std::strcmp (argv [1], "gref") == 0) {
		system_property = argv [2];
		AndroidSystem::set_running_in_emulator (std::strcmp (argv [3], "emulator") == 0);
		AndroidSystem::init_max_gref_count ();
		std::printf ("%ld %ld", AndroidSystem::get_max_gref_count (), AndroidSystem::get_gref_gc_threshold ());
		return EXIT_SUCCESS;
	}
	if (argc == 3 && std::strcmp (argv [1], "bootstrap") == 0) {
		std::ifstream input (argv [2], std::ios::binary);
		if (!input) {
			Helpers::abort_application ("Unable to open bootstrap body");
		}
		bootstrap_body.assign (std::istreambuf_iterator<char> (input), std::istreambuf_iterator<char> ());
		if (input.bad () || bootstrap_body.size () > std::numeric_limits<uint32_t>::max ()) {
			Helpers::abort_application ("Unable to read bootstrap body");
		}
		CoreClrBootstrap::initialize ();
		std::printf ("%u %u %u", CoreClrBootstrap::config.number_of_dso_cache_entries,
			CoreClrBootstrap::preload_count, CoreClrBootstrap::preload_stride);
		for (uint32_t i = 0; i < CoreClrBootstrap::preload_count; i++) {
			std::printf (" %u", CoreClrBootstrap::preload_index (i));
		}
		return EXIT_SUCCESS;
	}
	Helpers::abort_application ("Expected a GREF value or bootstrap body");
}
