# How to branch for .NET releases

Context: [dotnet/maui#589][0]

Let's say that it's time for a hypothetical ".NET 6 Preview 42". The
sequence of events would be:

1. [dotnet/installer][1] branches `release/6.0.1xx-preview42`

2. Builds are available on Maestro for `xamarin-android` to consume.

3. `xamarin-android` branches `release/6.0.1xx-preview42`. GitHub Web
   UI is fine for this.

4. Manually make a commit to `release/6.0.1xx-preview42` such as
  [df122518][2], so that `$(_AndroidPackLabel)` is
  `preview.42.$(PackVersionCommitCount)`.

5. Subscribe to Maestro updates for [dotnet/installer][1] `release/6.0.1xx-preview42`:

```bash
$ darc add-subscription --channel ".NET 6.0.1xx SDK Preview 42" --target-branch "release/6.0.1xx-preview42" --source-repo https://github.com/dotnet/installer --target-repo https://github.com/xamarin/xamarin-android
```

6. Publish Maestro updates for `xamarin-android/release/6.0.1xx-preview42`:

```bash
$ darc add-default-channel --channel ".NET 6.0.1xx SDK Preview 42" --branch "release/6.0.1xx-preview42" --repo https://github.com/xamarin/xamarin-android
```

See [eng/README.md][3] for details on `darc` commands.

This workflow might change slightly when previews become release candidates, such as RC1.

[0]: https://github.com/dotnet/maui/issues/598
[1]: https://github.com/dotnet/installer
[2]: https://github.com/xamarin/xamarin-android/commit/df12251856a172c7deefa9ee2a4b07a490dc9003
[3]: ../../eng/README.md
