# How to update Android NDK

For the most part, update of the NDK version used to build this repository is
very straightforward. The only complication arises from the fact that we carry
a copy of some LLVM source files, for its libc++ and libc++abi libraries.
The copied files are needed only by the `NativeAOT` host (see https://github.com/dotnet/runtime/issues/121172),
the `MonoVM` and `CoreCLR` hosts link against the two libraries directly.

Our copy of LLVM sources *must* be updated *every time* we update the NDK version.

## Update NDK reference in `xaprepare`

Visit https://developer.android.com/ndk/downloads/index.html to obtain NDK revision
information then edit the `build-tools/xaprepare/xaprepare/ConfigAndData/BuildAndroidPlatforms.cs`
file and update the `BuildAndroidPlatforms.AndroidNdkVersion` and `BuildAndroidPlatforms.AndroidNdkPkgRevision`
properties with the information obtained from the NDK distribution URL.

## Update LLVM sources

The best way to do it is by using the `tools/update-llvm-sources` utility, after runing `xaprepare`.

You can run the utility directly with `dotnet tools/update-llvm-sources` or, if you are on a Unix
system, run `make update-llvm` from the top directory.

### Details (should you need to update sources manually)

Android NDK uses a fork of the upstream LLVM repository, currently
https://android.googlesource.com/toolchain/llvm-project and this is the repository updated tool
mentioned above uses to fetch the files.

Android NDK has a manifest file for the LLVM toolchain which enumerates revisions of all the
components, however that file changes name in each release, based on information it yet another
manifest file, namely `${ANDROID_NDK_ROOT}/BUILD_INFO`. This is a JSON file, which contains a
number of properties, we are however interested only in one of them, named `bid`. Its value
is a string which is part of the second manifest, found in the `${ANDROID_NDK_ROOT}/manifest_${bid}.xml`
file.

In the XML manifest, we can find an element named `project`, with its `name` attribute set to
`toolchain/llvm-project` - the `revision` attribute of that element is the Git revision we need
in order to access sources from the Google's `llvm-project` fork.

Once you have the revision, you can either clone the Android fork repository and checkout the
revision, or visit the individual files in the browser. All the LLVM sources we copied are
contained in the `src-ThirdParty/llvm/` directory, with the subdirectories reflecting exactly
the `llvm-project` layout. This way, you can take a file path relative to `src-ThirdParty/llvm` and
form the file's URL as follows:

```
https://android.googlesource.com/toolchain/llvm-project/+/${LLVM_REVISION}/${RELATIVE_FILE_PATH}
```

Visiting this url will show you the file with syntax higlighting and line numbers, however it's
not the raw source, but rather its HTML rendering, useless for our purpose. In order to fetch the
raw source, we need to append `?format=TEXT` to the URL. Once visited in the browser (or fetched
using `curl` or `wget`), the resulting file will be downloaded but not yet ready for updating of
our copy. The downloaded file is encoded in the `base64` encoding and must be decoded before use.

On Unix systems this can be done using the following command:

```shell
$ base64 -d < downloaded_file.cpp > file.cpp
```

After that, the resulting file can be copied to its destination in our source tree.
