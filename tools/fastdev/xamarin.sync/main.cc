#include <cstdlib>
#include <cstdio>
#include <cstring>
#include <arpa/inet.h>
#include <time.h>
#include <sys/time.h>
#include <lz4.h>
#include <inttypes.h>
#include <errno.h>
#include <poll.h>
#include <sys/stat.h>
#include <unistd.h>

constexpr uint64_t BILLION = 1000000000;

void printHelp ();
bool closeOutfile (FILE * fp);

// Streams stdio to a destination for a given number of bytes
// Arguments size destination.
int main(int argc, char **argv)
{
    if (argc < 5)
    {
        printHelp ();
        return 1;
    }

    timespec start;
    timespec end;
    clock_gettime(CLOCK_MONOTONIC, &start);

    unsigned long defaultBuffersize = strtoul (argv[1], nullptr, 10);
    if (defaultBuffersize == 0) {
        fprintf(stderr, "error: invalid argument <buffersize> '%s'.", argv[1]);
        printHelp ();
        return 1;
    }
    unsigned long size = strtoul (argv[2], nullptr, 10);
    if (size == 0) {
        fprintf(stderr, "error: invalid argument <size> '%s'.", argv[2]);
        printHelp ();
        return 1;
    }
    char * outfile = argv[3];

    errno = 0;
    uint64_t modifieddatetime = strtoull (argv[4], nullptr, 10);
    if (modifieddatetime == 0 || modifieddatetime == ULLONG_MAX || errno == ERANGE) {
        fprintf(stderr, "error: invalid argument <modifieddatetime> '%s'.", argv[4]);
        printHelp ();
        return 1;
    }

    int count = 0;
    int inTotal = 0;
    unsigned int bufferSize = defaultBuffersize;
    unsigned int bufferHeader;

    char *compr = new char[defaultBuffersize];
    char *uncompr = new char[defaultBuffersize];

    size_t n = 0;
    struct pollfd fds[1];

    int timeout = 1000 * 5; // Timeout in milliseconds (5 seconds)

    //fds[0].fd = fileno (stdin); // Get file descriptor for stdin
    //fds[0].events = POLLIN | POLLPRI; // Listen for data available events.
    if (access (outfile, F_OK) != -1) {
        if (access (outfile, W_OK) == -1) {
            if (chmod (outfile, S_IWUSR | S_IRUSR) == -1) {
                if (remove (outfile) == -1) {
                    fprintf (stderr, "error: could not set write permissions on '%s': %s", outfile, strerror (errno));
                    return 1;
                }
            }
        }
    }


    FILE * fp = fopen(outfile, "w");
    if (fp == nullptr) {
        fprintf(stderr, "error: could not open '%s'.", outfile);
        return 1;
    }
    while  (count < size) {
       int pr = 1;
       fds[0].fd = fileno (stdin);
       fds[0].events = POLLIN | POLLPRI | POLLRDNORM | POLLRDBAND | POLLERR;
       fds[0].revents = 0;
       do {
           pr = poll(fds, 1, timeout);
       } while (pr == -1 && errno == EINTR);

    //    printf ("DEBUG pr= %d\n", pr);
    //    if (fds[0].revents != 0) {
    //         printf ("DEBUG Read data ok. data = %d of %d\n", count, size);
    //    }
       if (pr == 0) {
           clock_gettime(CLOCK_MONOTONIC, &end);
           uint64_t time_taken = BILLION * (end.tv_sec - start.tv_sec) + end.tv_nsec - start.tv_nsec;
           fprintf (stderr, "error: Could not read data from stdin. The operation timed out. [%" PRIu64 "] ", time_taken);
           if (fclose (fp) != 0) {
               fprintf (stderr, "error: failed to read stdin data.");
           }
           return 1;
       }
       if (pr == -1) {
           clock_gettime(CLOCK_MONOTONIC, &end);
           uint64_t time_taken = BILLION * (end.tv_sec - start.tv_sec) + end.tv_nsec - start.tv_nsec;
           fprintf (stderr, "error: Could not read data from stdin. An error '%s' occurred. [%" PRIu64 "]", strerror (errno), time_taken);
           if (fclose (fp) != 0) {
               fprintf (stderr, "error: failed to read stdin data.");
           }
           return 1;
       }
       // read length of compressed buffer this is a 4 byte (u_int) value.
       n = fread(&bufferHeader, 1, sizeof(bufferHeader), stdin);
       switch (n)
       {
            // We should only ever read 4 bytes of data for the header.
            // If we don't that is an error and we should abort.
            case 4: {
                    inTotal += 4;
                    bufferSize = ntohl (bufferHeader);
                    if (bufferSize > defaultBuffersize) {
                        delete [] compr;
                        delete [] uncompr;
                        fprintf(stderr, "error: Data length %lu CANNOT be greater than %lu. ", bufferSize, defaultBuffersize);
                        if (fclose (fp) != 0) {
                            fprintf(stderr, "error: failed to write '%s' during data length error.", outfile);
                        }
                        return 1;
                    }
                    n = fread (compr, 1, bufferSize, stdin);
                    inTotal += n;
                    int decompressed = 0;
                    if (n == defaultBuffersize) {
                        memcpy (uncompr, compr, n);
                        decompressed = n;
                    } else {
                        decompressed = LZ4_decompress_safe (compr, uncompr, n, defaultBuffersize);
                    }
                    if (decompressed > 0) {
                        n = fwrite (uncompr, 1, decompressed, fp);
                        if (n != decompressed || ferror (fp) != 0) {
                            delete [] compr;
                            delete [] uncompr;
                            fprintf (stderr, "error: Failed to write data to '%s'. %s", outfile, strerror (errno));
                            if (fclose (fp) != 0) {
                                fprintf(stderr, "error: failed to write '%s' during data write failure.", outfile);
                            }
                            return 1;
                        }
                        count += n;
                    }
                    break;
                }
            default: {
                    delete [] compr;
                    delete [] uncompr;
                    fprintf(stderr, "error: Failed to read package length.");
                    if (fclose (fp) != 0) {
                        fprintf(stderr, "error: failed to write '%s' during package length failure.", outfile);
                    }
                    return 1;
                }
       }
    }
    if (fclose (fp) != 0) {
        fprintf(stderr, "error: failed to write '%s'.", outfile);
        delete [] compr;
        delete [] uncompr;
        return 1;
    }
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
    utimes (outfile, modifiedtimes);
    if (chmod (outfile,  S_IRUSR) == -1) {
        fprintf (stderr, "error: could not set read permissions on '%s'. %s", outfile, strerror (errno));
        return 1;
    }
    delete [] compr;
    delete [] uncompr;
    clock_gettime(CLOCK_MONOTONIC, &end);
    uint64_t time_taken = BILLION * (end.tv_sec - start.tv_sec) + end.tv_nsec - start.tv_nsec;
    printf("wrote [%d] received [%d] time [%" PRIu64 "] modifieddate [%" PRId64 "]\n", count, inTotal, time_taken, modifieddatetime);
    return 0;
}

void printHelp ()
{
    printf ("xamarin.sync\n");
    printf ("This tool reads encoded data from stdin and writes that data to disk.");
    printf ("The incoming data are blocks of compressed L4Z data.");
    printf ("Each block consists of a 4 byte length followed by the compressed data itself.");
    printf ("Usage: xamarin.sync <buffersize> <size> <destination>\n");
    printf ("\t<buffersize> : Size of the Buffer to use\n");
    printf ("\t<size> : Size in Bytes of Input Stream\n");
    printf ("\t<destination> : File to write the data too.\n");
    printf ("\t<modifieddatetime> : The modified date to use for this file in unix time.\n");
}
