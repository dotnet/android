#include <fcntl.h>
#include <unistd.h>

#include <array>
#include <cassert>
#include <cstring>
#include <thread>

#include <probe-platform.hh>
#include <host/fastdev-assemblies.hh>

using namespace xamarin::android;

namespace {
	void write_file (const char *name, const char *contents)
	{
		int fd = open (name, O_CREAT | O_TRUNC | O_WRONLY, 0600);
		assert (fd >= 0);
		size_t size = strlen (contents);
		assert (write (fd, contents, size) == static_cast<ssize_t> (size));
		assert (close (fd) == 0);
	}

	auto probe (const char *name, const char *expected) -> void*
	{
		int64_t size = -1;
		void *data = FastDevAssemblies::open_assembly (name, size);
		assert (data != nullptr);
		assert (size == static_cast<int64_t> (strlen (expected)));
		assert (memcmp (data, expected, size) == 0);
		return data;
	}
}

int main (int argc, char **argv)
{
	assert (argc == 3);
	char directory [4096];
	assert (snprintf (directory, sizeof (directory), "%s/fastdev-XXXXXX", argv [2]) < static_cast<int> (sizeof (directory)));
	assert (mkdtemp (directory) != nullptr);
	assert (setenv ("XA_FASTDEV_TEST_DIR", directory, 1) == 0);
	assert (chdir (directory) == 0);

	write_file ("Test.dll", "original assembly data");
	if (strcmp (argv [1], "repeat") == 0) {
		void *first = probe ("Test.dll", "original assembly data");
		for (size_t i = 0; i < 10000; ++i) {
			assert (probe ("Test.dll", "original assembly data") == first);
		}
	} else if (strcmp (argv [1], "concurrent") == 0) {
		std::array<void*, 16> results {};
		std::array<std::thread, 16> threads;
		for (size_t i = 0; i < threads.size (); ++i) {
			threads [i] = std::thread ([&, i] {
				for (size_t n = 0; n < 100; ++n) {
					void *data = probe ("Test.dll", "original assembly data");
					assert (results [i] == nullptr || results [i] == data);
					results [i] = data;
				}
			});
		}
		for (auto &thread : threads) {
			thread.join ();
		}
		for (void *result : results) {
			assert (result == results [0]);
		}
	} else if (strcmp (argv [1], "snapshot") == 0) {
		void *first = probe ("Test.dll", "original assembly data");
		write_file ("Test.dll", "replacement");
		assert (probe ("Test.dll", "original assembly data") == first);
		assert (unlink ("Test.dll") == 0);
		assert (probe ("Test.dll", "original assembly data") == first);
	} else if (strcmp (argv [1], "failure-retry") == 0) {
		int64_t size = -1;
		assert (FastDevAssemblies::open_assembly ("Later.dll", size) == nullptr);
		assert (size == 0);
		write_file ("Later.dll", "later data");
		probe ("Later.dll", "later data");
		assert (unlink ("Later.dll") == 0);
		write_file ("Empty.dll", "");
		size = -1;
		assert (FastDevAssemblies::open_assembly ("Empty.dll", size) == nullptr);
		assert (size == 0);
		write_file ("Empty.dll", "no longer empty");
		probe ("Empty.dll", "no longer empty");
		assert (unlink ("Empty.dll") == 0);
	} else if (strcmp (argv [1], "names") == 0) {
		assert (mkdir ("fr", 0700) == 0);
		write_file ("fr/Test.dll", "French assembly data");
		void *first = probe ("Test.dll", "original assembly data");
		void *second = probe ("fr/Test.dll", "French assembly data");
		assert (first != second);
		assert (probe ("Test.dll", "original assembly data") == first);
		assert (probe ("fr/Test.dll", "French assembly data") == second);
		assert (unlink ("fr/Test.dll") == 0);
		assert (rmdir ("fr") == 0);
	} else if (strcmp (argv [1], "tpa") == 0) {
		write_file ("System.Private.CoreLib.dll", "CoreLib data");
		void *first = probe ("Test.dll", "original assembly data");
		char *tpa = FastDevAssemblies::build_tpa_list ();
		assert (tpa != nullptr);
		int64_t size = -1;
		assert (FastDevAssemblies::open_assembly ("Test.dll", size) == nullptr);
		assert (size == 0);
		void *corelib = probe ("System.Private.CoreLib.dll", "CoreLib data");
		assert (probe ("System.Private.CoreLib.dll", "CoreLib data") == corelib);
		FastDevAssemblies::discard_tpa_list (tpa);
		assert (probe ("Test.dll", "original assembly data") == first);
		assert (probe ("System.Private.CoreLib.dll", "CoreLib data") == corelib);
		assert (unlink ("System.Private.CoreLib.dll") == 0);
	} else {
		return 1;
	}

	if (access ("Test.dll", F_OK) == 0) {
		assert (unlink ("Test.dll") == 0);
	}
	assert (chdir (argv [2]) == 0);
	assert (rmdir (directory) == 0);
	puts ("FastDev ownership scenario passed");
	return 0;
}
