#include <cstdio>
#include <cstring>
#include <dirent.h>
#include <sys/types.h>
#include <sys/stat.h>
#include <unistd.h>
#include <cstdlib>
#include <cerrno>
#include <inttypes.h>
#include <time.h>
#include <sys/time.h>
#include <sys/stat.h>

void printHelp ();

int main (int argc, char **argv)
{
    if (argc <= 3) {
        printHelp ();
        return 1;
    }

    uint64_t modifieddatetime = strtoull (argv[3], nullptr, 10);
    if (modifieddatetime == 0 || modifieddatetime == ULLONG_MAX || errno == ERANGE) {
        fprintf(stderr, "error: invalid argument <modifieddatetime> '%s'.", argv[4]);
        printHelp ();
        return 1;
    }

    struct stat fi;
    decltype (fi.st_blksize) blockSize;
    int ret = stat (".", &fi);

    if (ret == 0 && fi.st_blksize >= 0) {
        blockSize = fi.st_blksize;
    } else {
        blockSize = 4096;
    }

    const char *sourcePath = argv[1];
    const char *destinationPath = argv[2];

    FILE *sourceFile = fopen (sourcePath, "rb");
    if (sourceFile == nullptr) {
        perror ("Error opening source file");
        return 1;
    }

    if (access (destinationPath, F_OK) != -1) {
        if (access (destinationPath, W_OK) == -1) {
            if (chmod (destinationPath, S_IWUSR | S_IRUSR) == -1) {
                if (remove (destinationPath) == -1) {
                    fprintf (stderr, "error: could not set write permissions on '%s': %s", destinationPath, strerror (errno));
                    return 1;
                }
            }
        }
    }

    FILE *destinationFile = fopen (destinationPath, "wb");
    if (destinationFile == nullptr) {
        perror ("Error opening destination file");
        fclose (sourceFile);
        return 1;
    }

    uint8_t buffer[blockSize];
    size_t bytesRead;
    while ((bytesRead = fread(buffer, 1, sizeof(buffer), sourceFile)) > 0) {
        if (fwrite (buffer, 1, bytesRead, destinationFile) != bytesRead) {
            perror ("Error writing to destination file");
            fclose (sourceFile);
            fclose (destinationFile);
            return 1;
        }
    }

    fclose (sourceFile);
    fclose (destinationFile);

    timeval modifiedtimes[] {
            {
                    .tv_sec = static_cast<time_t> (modifieddatetime / 1000),
                    .tv_usec = static_cast<time_t> (modifieddatetime % 1000),
            },

            {
                    .tv_sec = static_cast<time_t> (modifieddatetime / 1000),
                    .tv_usec = static_cast<time_t> (modifieddatetime % 1000),
            },
    };

    utimes (argv[2], modifiedtimes);
    if (chmod (argv[2],  S_IRUSR) == -1) {
        fprintf (stderr, "error: could not set read permissions on '%s'. %s", argv[2], strerror (errno));
        return 1;
    }

    printf ("moved [%s] to [%s] modifieddate [%" PRId64 "]\n", argv[1], argv[2], modifieddatetime);
    
    return 0;
}

void printHelp ()
{
    printf ("xamarin.cp\n");
    printf ("Usage: xamarin.cp <source> <destination> <modifieddatetime>\n");
    printf ("\t<source> : The source file to copy (required). \n");
    printf ("\t<destination> : The destination file(required). \n");
    printf ("\t<modifieddatetime> : The modified date to use for this file in unix time.\n");
}
