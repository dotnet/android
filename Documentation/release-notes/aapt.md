#### Deprecation of aapt in favor of aapt2

```
warning XA1026: Using AAPT is deprecated in favor of AAPT2. Please enable 'Use incremental Android packaging system (aapt2)' in the Visual Studio project property pages or edit the project file in a text editor and set the 'AndroidUseAapt2' MSBuild property to 'true'.
```

[Google has deprecated][aapt] the AAPT command-line tool in favor of
AAPT2 going forward. Xamarin.Android has accordingly now deprecated
`<AndroidUseAapt2>false</AndroidUseAapt2>` as well.

Update the `AndroidUseAapt2` MSBuild property to `true` to select
AAPT2. This property corresponds to the **Use incremental Android
packaging system (aapt2)** setting in the Visual Studio project
properties pages. Alternatively, remove `<AndroidUseAapt2>` from the
_.csproj_ file to let the build select the default value `true`.

> [!IMPORTANT]
> AAPT2 will in some cases enforce stricter rules on resource files than the
> previous AAPT, so some adjustments might be needed if you see new error
> messages that come from AAPT2 itself rather than from the Xamarin.Android
> build tasks.

If needed, the `--legacy` switch can run AAPT2 in an AAPT
compatibility mode. Add the following to your _.csproj_ file:

```xml
<PropertyGroup>
  <AndroidAapt2CompileExtraArgs>--legacy</AndroidAapt2CompileExtraArgs>
</PropertyGroup>
```

[aapt]: https://developer.android.com/studio/command-line/aapt2#aapt2_changes
