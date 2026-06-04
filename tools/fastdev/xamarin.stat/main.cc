#include <cstdlib>
#include <sys/types.h>
#include <sys/stat.h>
#include <unistd.h>
#include <cstdio>
#include <cerrno>
#include <cstring>
#include <inttypes.h>

int main(int argc, char **argv)
{
    struct stat fi;
    if (argc <= 1) {
        printf("Usage: xamarin.stat PATH\n");
        return 1;
    }
    int r = stat(argv[1], &fi);
    if (r != 0) {
        fprintf (stderr, "error: File or Directory '%s' does not exist. %s.\n", argv[1], strerror (errno));
        return 1;
    }
    if (fi.st_size >= 0) {
        printf("%" PRId64 "\t%" PRId64 "\t%" PRId64 "\n", static_cast<int64_t> (fi.st_blksize), static_cast<int64_t> (fi.st_size), static_cast<int64_t> ((fi.st_mtim.tv_sec * 1000) + (fi.st_mtim.tv_nsec / 1000)));
        return 0;
    }
    fprintf (stderr, "error: File or Directory '%s' does not exist. %s.\n", argv[1], strerror (errno));
    return 1;
}