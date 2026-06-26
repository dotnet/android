#include <cstdio>
#include <cstring>
#include <dirent.h>
#include <sys/types.h>
#include <sys/stat.h>
#include <unistd.h>
#include <cstdlib>
#include <cerrno>
#include <inttypes.h>

constexpr uint64_t THOUSAND = 1000;

void findAllFiles(const char *path);
void printHelp ();

bool verbose = 0;
bool debug = 0;
int main(int argc, char **argv)
{
    if (argc <= 1) {
        printHelp ();
        return 1;
    }
    int opt;
    while((opt = getopt (argc, argv, "vd")) != -1) {
        switch (opt)
        {
        case 'v':
            verbose = 1;
            break;
        case 'd':
            debug = 1;
            break;
        default:
            printHelp ();
            return 1;
        }
    }
    if (chdir (argv[optind]) == -1) {
        fprintf (stderr, "error: failed to change current directory to '%s'. %s.", argv[optind], strerror (errno));
        return 1;
    }
    findAllFiles(".");
    return 0;
}

void printHelp ()
{
    printf ("xamarin.find\n");
    printf ("Usage: xamarin.find -v -d <directory>\n");
    printf ("\t-v : Print Size and Last Modified Time of the files (optional)\n");
    printf ("\t-d : Print additional diagnostics\n");
    printf ("\t<directory> : The directory to recursively list the files for (required). \n");
}

/**
 * find all files and sub-directories recursively
 * expected output is relative path to file.
 *
 * For example a call to
 *    xamarin.find /data/data/com.foo.bar/files/.__overriide__
 *
 * would result in
 *    ./a.dll
 *    ./fr-FR/a.resources.dll
 *
 * for filesize output use -v
 *
 *    xamarin.find -v /data/data/com.foo.bar/files/.__overriide__
 *    ./a.dll 1025  3765434
 *    ./fr-FR/a.resources.dll 23 12431341
 */
void findAllFiles(const char *basePath)
{
    struct dirent *dp;
    DIR *dir = opendir(basePath);

    // Unable to open directory stream
    if (!dir)
        return;

    while ((dp = readdir(dir)) != NULL) {
        if (strcmp(dp->d_name, ".") == 0 || strcmp(dp->d_name, "..") == 0)
            continue;
        // Construct new path from our base path
        char *path = new char[strlen (basePath)+strlen(dp->d_name)+2];
        strcpy(path, basePath);
        strcat(path, "/");
        strcat(path, dp->d_name);

        if (dp->d_type == DT_REG) {
            if (verbose) {
                struct stat fi;
                stat(path, &fi);
                int64_t tv_sec = static_cast<int64_t> (fi.st_mtim.tv_sec);
                int64_t tv_nsec = static_cast<int64_t> (fi.st_mtim.tv_nsec);
                if (debug)
                    printf ("DEBUG: %s size:%" PRId64 " mtime:%" PRId64 " tv_sec:%" PRId64 " tv_nsec:%" PRId64 "\n", path,
                        static_cast<int64_t> ((tv_sec * THOUSAND) + (tv_nsec / THOUSAND)),static_cast<int64_t> (fi.st_size), static_cast<int64_t> (fi.st_mtim.tv_sec), static_cast<int64_t> (fi.st_mtim.tv_nsec));
                printf("%s\t%" PRId64 "\t%" PRId64 "\n", path, static_cast<int64_t> (fi.st_size), static_cast<int64_t> ((tv_sec * THOUSAND) + (tv_nsec / THOUSAND)));
            } else {
                printf("%s\n", path);
            }
        }

        findAllFiles(path);
        delete[] path;
    }

    closedir(dir);
}