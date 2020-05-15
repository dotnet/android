# Fast Deployment Updates.

As part of this update the fast deplyment system used for Debugging apps has
been changed. The shared runtime is now no longer used or required.
The Platform package has also been make obsolete.

The new system also no longer uses the `external` drive for the fast deployment
files. All of the files and tooling are now stored in the applications' `internal`
directory. This has an advantage in that when the application is uninstalled all
if the files and the tooling will be removed as well.

The new system will not work on any devices older than API 21. This is because it
relys on features that only work from API 21 onwards. Also API 21 is the lowest
supported platform for the runtime, so running and debugging on older devices will
be impossible.