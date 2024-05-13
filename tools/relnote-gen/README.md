# Release Notes Generator

Parses `git log` output to produce .NET for Android release notes fragments.

Usage:

```zsh
(cd ~/Developer/src/xamarin/xamarin-android ;
  git log -p --cherry-pick --right-only FROM...TO) \
| dotnet run \
| pbcopy
```

Use of `git log -p` allows for extraction of `Documentation/release-notes` blobs.

Given a `git log` "block" of:

```
commit COMMIT
Author: …
Date:   …

    [COMPONENT] SUMMARY (#PR)

    Fixes: URL/NUMBER

    Commit Message Body
```

Then the output of `renote-gen` will be:

```markdown
### COMPONENT

  - Summary
    ([#NUMBER](URL/NUMBER),
    [PR #PR](http://github.com/xamarin/xamarin-android/pull/PR),
    [Commit COMMIT](http://github.com/xamarin/xamarin-android/commit/COMMIT))
```

# API Diffs?

[`Microsoft.DotNet.AsmDiff`](https://github.com/dotnet/arcade/tree/main/src/Microsoft.DotNet.AsmDiff)
can be used to produce API diffs.

First, "obtain" the two assemblies to compare, e.g.

	curl -o Xamarin.Android.Sdk-11.3.0.4.vsix \
	  https://bosstoragemirror.azureedge.net/vsts-devdiv/Xamarin.Android/public/4829460/d16-10/ae14cafda4befc69867db19eee0728efec9cf71b/signed/Xamarin.Android.Sdk-11.3.0.4.vsix
	unzip Xamarin.Android.Sdk-11.3.0.4.vsix \
	  '$ReferenceAssemblies/Microsoft/Framework/MonoAndroid/v11.0/Mono.Android.dll'
	  -d v11.3.0.4

	curl -o Xamarin.Android.Sdk-12.0.0.3.vsix \
	  https://dl.internalx.com/vsts-devdiv/Xamarin.Android/public/5160375/d16-11/f0e3c2d1d269d311bc83d6875ffec3bfe5d17edb/signed/Xamarin.Android.Sdk-12.0.0.3.vsix
	unzip Xamarin.Android.Sdk-12.0.0.3.vsix \
	  '$ReferenceAssemblies/Microsoft/Framework/MonoAndroid/v12.0/Mono.Android.dll'
	  -d v12.0.0.3

Next, build `Microsoft.DotNet.AsmDiff`:

	git clone --depth 1 https://github.com/dotnet/arcade.git
	dotnet build arcade/src/Microsoft.DotNet.AsmDiff

Run `Microsoft.DotNet.AsmDiff` to produce API diffs.  We like Markdown diffs:

	dotnet arcade/artifacts/bin/Microsoft.DotNet.AsmDiff/Debug/netcoreapp3.1/Microsoft.DotNet.AsmDiff.dll \
	  --OldSet 'v11.3.0.4/$ReferenceAssemblies/Microsoft/Framework/MonoAndroid/v11.0/Mono.Android.dll' \
	  --NewSet 'v12.0.0.3/$ReferenceAssemblies/Microsoft/Framework/MonoAndroid/v12.0/Mono.Android.dll' \
	  --OldSetName 'Mono.Android.dll `$(TargetFrameworkVersion)`=v11.0' \
	  --NewSetName '`$(TargetFrameworkVersion)`=v12.0' \
	  --AlwaysDiffMembers \
	  --IncludeTableOfContents \
	  --DiffWriter Markdown \
	  --OutFile Mono.Android_30-31-all.md
