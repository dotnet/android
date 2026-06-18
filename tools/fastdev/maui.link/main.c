#include <dirent.h>
#include <errno.h>
#include <limits.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/stat.h>
#include <unistd.h>

#ifndef PATH_MAX
#define PATH_MAX 4096
#endif

typedef struct {
	long linked;
	long unchanged;
	long removed;
	long errors;
} Counters;

static void print_help (void);
static int join_path (char *buffer, size_t size, const char *left, const char *right);
static int ensure_dir (const char *path);
static int remove_tree (const char *path);
static int mirror_tree (const char *source_root, const char *target_root, const char *relative_path, Counters *counters);
static int remove_stale_entries (const char *source_root, const char *target_root, const char *relative_path, Counters *counters);
static int link_file (const char *source, const char *target, Counters *counters);

int main (int argc, char **argv)
{
	if (argc != 3) {
		print_help ();
		return 1;
	}

	const char *source_root = argv [1];
	const char *target_root = argv [2];
	Counters counters = {0, 0, 0, 0};

	struct stat st;
	if (stat (source_root, &st) != 0 || !S_ISDIR (st.st_mode)) {
		fprintf (stderr, "error: source directory '%s' does not exist or is not a directory. %s\n", source_root, strerror (errno));
		return 1;
	}

	if (ensure_dir (target_root) != 0) {
		fprintf (stderr, "error: could not create target directory '%s'. %s\n", target_root, strerror (errno));
		return 1;
	}

	int result = 0;
	if (mirror_tree (source_root, target_root, "", &counters) != 0) {
		result = 1;
	}
	if (remove_stale_entries (source_root, target_root, "", &counters) != 0) {
		result = 1;
	}

	printf ("linked [%ld] unchanged [%ld] removed [%ld] errors [%ld]\n", counters.linked, counters.unchanged, counters.removed, counters.errors);
	return result;
}

static void print_help (void)
{
	printf ("maui.link\n");
	printf ("Usage: maui.link <source-directory> <target-directory>\n");
	printf ("\tCreates a symlink mirror of source-directory under target-directory and removes stale target entries.\n");
}

static int join_path (char *buffer, size_t size, const char *left, const char *right)
{
	if (right == NULL || right [0] == '\0') {
		int n = snprintf (buffer, size, "%s", left);
		return n < 0 || (size_t)n >= size ? -1 : 0;
	}

	int n = snprintf (buffer, size, "%s/%s", left, right);
	return n < 0 || (size_t)n >= size ? -1 : 0;
}

static int ensure_dir (const char *path)
{
	char tmp [PATH_MAX];
	size_t len;

	if (path == NULL || path [0] == '\0') {
		return -1;
	}

	if (snprintf (tmp, sizeof (tmp), "%s", path) >= (int)sizeof (tmp)) {
		errno = ENAMETOOLONG;
		return -1;
	}

	len = strlen (tmp);
	if (len == 0) {
		return -1;
	}

	if (tmp [len - 1] == '/') {
		tmp [len - 1] = '\0';
	}

	for (char *p = tmp + 1; *p != '\0'; p++) {
		if (*p != '/') {
			continue;
		}
		*p = '\0';
		if (mkdir (tmp, 0700) != 0 && errno != EEXIST) {
			return -1;
		}
		*p = '/';
	}

	if (mkdir (tmp, 0700) != 0 && errno != EEXIST) {
		return -1;
	}
	return 0;
}

static int remove_tree (const char *path)
{
	struct stat st;
	if (lstat (path, &st) != 0) {
		return errno == ENOENT ? 0 : -1;
	}

	if (S_ISDIR (st.st_mode) && !S_ISLNK (st.st_mode)) {
		DIR *dir = opendir (path);
		if (dir == NULL) {
			return -1;
		}

		struct dirent *entry;
		while ((entry = readdir (dir)) != NULL) {
			if (strcmp (entry->d_name, ".") == 0 || strcmp (entry->d_name, "..") == 0) {
				continue;
			}

			char child [PATH_MAX];
			if (join_path (child, sizeof (child), path, entry->d_name) != 0 || remove_tree (child) != 0) {
				closedir (dir);
				return -1;
			}
		}
		closedir (dir);
		return rmdir (path);
	}

	return unlink (path);
}

static int mirror_tree (const char *source_root, const char *target_root, const char *relative_path, Counters *counters)
{
	char source_dir [PATH_MAX];
	char target_dir [PATH_MAX];
	if (join_path (source_dir, sizeof (source_dir), source_root, relative_path) != 0 ||
			join_path (target_dir, sizeof (target_dir), target_root, relative_path) != 0) {
		fprintf (stderr, "error: path too long while mirroring '%s'\n", relative_path);
		counters->errors++;
		return -1;
	}

	if (ensure_dir (target_dir) != 0) {
		fprintf (stderr, "error: could not create directory '%s'. %s\n", target_dir, strerror (errno));
		counters->errors++;
		return -1;
	}

	DIR *dir = opendir (source_dir);
	if (dir == NULL) {
		fprintf (stderr, "error: could not open source directory '%s'. %s\n", source_dir, strerror (errno));
		counters->errors++;
		return -1;
	}

	int result = 0;
	struct dirent *entry;
	while ((entry = readdir (dir)) != NULL) {
		if (strcmp (entry->d_name, ".") == 0 || strcmp (entry->d_name, "..") == 0) {
			continue;
		}

		char child_relative [PATH_MAX];
		if (join_path (child_relative, sizeof (child_relative), relative_path, entry->d_name) != 0) {
			fprintf (stderr, "error: path too long for '%s'\n", entry->d_name);
			counters->errors++;
			result = -1;
			continue;
		}

		char source_path [PATH_MAX];
		char target_path [PATH_MAX];
		if (join_path (source_path, sizeof (source_path), source_root, child_relative) != 0 ||
				join_path (target_path, sizeof (target_path), target_root, child_relative) != 0) {
			fprintf (stderr, "error: path too long for '%s'\n", child_relative);
			counters->errors++;
			result = -1;
			continue;
		}

		struct stat st;
		if (stat (source_path, &st) != 0) {
			fprintf (stderr, "error: could not stat source '%s'. %s\n", source_path, strerror (errno));
			counters->errors++;
			result = -1;
			continue;
		}

		if (S_ISDIR (st.st_mode)) {
			if (mirror_tree (source_root, target_root, child_relative, counters) != 0) {
				result = -1;
			}
		} else if (S_ISREG (st.st_mode)) {
			if (link_file (source_path, target_path, counters) != 0) {
				result = -1;
			}
		}
	}

	closedir (dir);
	return result;
}

static int link_file (const char *source, const char *target, Counters *counters)
{
	struct stat st;
	if (lstat (target, &st) == 0) {
		if (S_ISLNK (st.st_mode)) {
			char link_target [PATH_MAX];
			ssize_t len = readlink (target, link_target, sizeof (link_target) - 1);
			if (len >= 0) {
				link_target [len] = '\0';
				if (strcmp (link_target, source) == 0) {
					counters->unchanged++;
					return 0;
				}
			}
		}

		if (remove_tree (target) != 0) {
			fprintf (stderr, "error: could not remove '%s'. %s\n", target, strerror (errno));
			counters->errors++;
			return -1;
		}
	} else if (errno != ENOENT) {
		fprintf (stderr, "error: could not stat target '%s'. %s\n", target, strerror (errno));
		counters->errors++;
		return -1;
	}

	if (symlink (source, target) != 0) {
		fprintf (stderr, "error: could not link '%s' -> '%s'. %s\n", target, source, strerror (errno));
		counters->errors++;
		return -1;
	}
	counters->linked++;
	return 0;
}

static int remove_stale_entries (const char *source_root, const char *target_root, const char *relative_path, Counters *counters)
{
	char target_dir [PATH_MAX];
	if (join_path (target_dir, sizeof (target_dir), target_root, relative_path) != 0) {
		fprintf (stderr, "error: path too long while cleaning '%s'\n", relative_path);
		counters->errors++;
		return -1;
	}

	DIR *dir = opendir (target_dir);
	if (dir == NULL) {
		return errno == ENOENT ? 0 : -1;
	}

	int result = 0;
	struct dirent *entry;
	while ((entry = readdir (dir)) != NULL) {
		if (strcmp (entry->d_name, ".") == 0 || strcmp (entry->d_name, "..") == 0) {
			continue;
		}

		char child_relative [PATH_MAX];
		char source_path [PATH_MAX];
		char target_path [PATH_MAX];
		if (join_path (child_relative, sizeof (child_relative), relative_path, entry->d_name) != 0 ||
				join_path (source_path, sizeof (source_path), source_root, child_relative) != 0 ||
				join_path (target_path, sizeof (target_path), target_root, child_relative) != 0) {
			fprintf (stderr, "error: path too long while cleaning '%s'\n", entry->d_name);
			counters->errors++;
			result = -1;
			continue;
		}

		struct stat source_st;
		struct stat target_st;
		if (lstat (target_path, &target_st) != 0) {
			continue;
		}

		if (stat (source_path, &source_st) != 0) {
			if (remove_tree (target_path) != 0) {
				fprintf (stderr, "error: could not remove stale '%s'. %s\n", target_path, strerror (errno));
				counters->errors++;
				result = -1;
			} else {
				counters->removed++;
			}
			continue;
		}

		if (S_ISDIR (source_st.st_mode) && S_ISDIR (target_st.st_mode) && !S_ISLNK (target_st.st_mode)) {
			if (remove_stale_entries (source_root, target_root, child_relative, counters) != 0) {
				result = -1;
			}
		}
	}

	closedir (dir);
	return result;
}
