# How to branch for .NET releases

Context: [dotnet/maui#589][0]

Let's say that it's time for a hypothetical ".NET 6 Preview 42". The
sequence of events would be:

1. [dotnet/installer][1] branches `release/6.0.1xx-preview42`

2. Builds are available on Maestro for `xamarin-android` to consume.

3. `xamarin-android` branches `release/6.0.1xx-preview42`. GitHub Web
   UI is fine for this.

4. Subscribe to Maestro updates for [dotnet/installer][1] `release/6.0.1xx-preview42`:

```bash
$ darc add-subscription --channel ".NET 6.0.1xx SDK Preview 42" --target-branch "release/6.0.1xx-preview42" --source-repo https://github.com/dotnet/installer --target-repo https://github.com/xamarin/xamarin-android
```

5. Publish Maestro updates for `xamarin-android/release/6.0.1xx-preview42`:

```bash
$ darc add-default-channel --channel ".NET 6.0.1xx SDK Preview 42" --branch "release/6.0.1xx-preview42" --repo https://github.com/xamarin/xamarin-android
```

See [eng/README.md][2] for details on `darc` commands.

6. Open a PR to `xamarin-android/main`, such that
   `$(AndroidPackVersionSuffix)` in `Directory.Build.props` is
   incremented to the *next* version: `preview.43`. You may also need
   to update `$(AndroidPackVersion)` if `main` needs to target a new
   .NET version band. In the same PR, update `azure-pipelines-nightly.yaml`
   to build the new release branch.

Note that release candidates will use values such as `rc.1`, `rc.2`, etc.

[0]: https://github.com/dotnet/maui/issues/598
[1]: https://github.com/dotnet/installer
[2]: ../../eng/README.md
