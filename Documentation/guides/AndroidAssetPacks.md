# Android Asset Packs

Google Android began supporting splitting up the app package into multiple
packs with the introduction of the `aab` package format. This format allows
the developer to split the app up into multiple `packs`. Each `pack` can be
downloaded to the device either at install time or on demand. This allows
application developers to save space and install time by only installing
the required parts of the app initially, then installing other `packs`
as required.

There are two types of `pack`: Feature packs and Asset packs.
*Feature* pack contains *non-native* Java code and other resources.
Code in these types of `pack` can be launched via the `Context.StartActivity()`
API call. At this time due to various constraints .NET for Android cannot support
Feature packs.

*Asset* packs contain *only*
[`@(AndroidAsset)`](~/android/deploy-test/building-apps/build-items.md#androidasset) items.
It *cannot* contain any code or other resources. This type of `pack` can be
installed at install-time, fast-follow or ondemand. It is most useful for apps
which contain a lot of Assets, such as Games or Multi Media applications.
See the [Android Asset Delivery documentation](https://developer.android.com/guide/playcore/asset-delivery)
for details on how this all works.

## Asset Pack Specification

We want to provide our users the ability to use `Asset` packs without relying
on [Alternative Methods](#alternativemethods)

Add support for new `%(AndroidAsset.AssetPack)` item metadata, which
allows the build system to split up the assets into packs automatically:

```xml
<ItemGroup>
   <AndroidAsset Include="Asset/data.xml" />
   <AndroidAsset Include="Asset/movie.mp4" AssetPack="assets1" />
   <AndroidAsset Include="Asset/movie2.mp4" AssetPack="assets1" />
</ItemGroup>
```

The default value for `%(AndroidAsset.AssetPack)` is `base`, which will
cause the asset to be included in the main application package.

As auto import of items is now common, we need a way for a user to add
this additional attribute to auto included items.  This can be done by
using `Update`:

```xml
<ItemGroup>
   <AndroidAsset Update="Asset/movie.mp4"  AssetPack="assets1" />
   <AndroidAsset Update="Asset/movie2.mp4" AssetPack="assets1" />
   <AndroidAsset Update="Asset/movie3.mp4" AssetPack="assets2" />
</ItemGroup>
```

`%(AndroidAsset.DeliveryType)` item metadata can be specified to control what
*type* of asset pack is produced.  Valid values are:

  * `InstallTime`: Asset pack will be delivered when the app is installed.
    This is the default value for assets not in the base package.
  * `FastFollow`: Asset pack will be downloaded automatically as soon as the app is installed.
  * `OnDemand`: Asset pack will be downloaded while the app is running.

The `DeliveryType` for a given asset pack is based on the *first*
`@(AndroidAsset.DeliveryType)` value encountered for a `%(AssetPack)` name.

Consider the following example, in which `Asset/movie2.mp4` and `Asset/movie3.mp4`
are both in the `assets1` pack, which will have a `%(DeliveryType)` of `InstallTime`
(the first encountered value "wins").  `Asset/movie1.mp4` will be in the base package,
while `Asset/movie4.mp4` will be in the "asset2" asset pack.

```xml
<ItemGroup>
   <AndroidAsset Update="Asset/movie1.mp4" />
   <AndroidAsset Update="Asset/movie2.mp4" AssetPack="assets1" DeliveryType="InstallTime" />
   <AndroidAsset Update="Asset/movie3.mp4" AssetPack="assets1" DeliveryType="FastFollow" />
   <AndroidAsset Update="Asset/movie4.mp4" AssetPack="assets2" />
</ItemGroup>
```

See Google's [documentation](https://developer.android.com/guide/playcore/asset-delivery#asset-updates) for details on what each of the `DeliveryType` values do.

If however you have a large number of assets it might be cleaner in the
`.csproj` to make use of the `base` value for the `%(AssetPack)` attribute.
In this scenario you update *all* assets to be in a single asset pack then use
`AssetPack="base"` metadata to declare which specific assets end up in the base
aab file. With this you can use wildcards to move most assets into the asset pack:

```xml
<ItemGroup>
   <AndroidAsset Update="Assets/*" AssetPack="assets1" />
   <AndroidAsset Update="Assets/movie.mp4" AssetPack="base" />
   <AndroidAsset Update="Assets/some.png" AssetPack="base" />
</ItemGroup>
```

In this example, `movie.mp4` and `some.png` will end up in the `base` aab file,
but *all the other assets* will end up in the `assets1` asset pack.

At this time the `@(AndroidAsset)` build action does not support `%(AssetPack)`
or `%(DeliveryType)` Metadata in Library Projects.

NOTE: `AssetPacks` are only used when the
[`$(AndroidPackageFormat)`](~/android/deploy-test/building-apps/build-properties.md#debugsymbols)
property is set to `aab` (the default for Release).
When using the `apk` setting the assets will be placed inside the `apk`.

## Release Configuration

In order for the application to function correctly we need to inform the `R8`
linker which Java classes we need to keep. To do this we need to add the
following lines to a `ProGuard.cfg` file which is in the root of our project folder:

```
-keep com.google.android.play.*
```

Alternatively you can create a file called `ProGuard.cfg` and use the
[@(ProguardConfiguration)](~/android/deploy-test/building-apps/build-items.md#proguardconfiguration)
build action.  Adding these lines will ensure that all the required Java components are not linked
away during the Release build.

## Testing and Debugging

In order to test your asset packs in the `Debug` configuration, you will need to
make some changes to your `.csproj`. Firstly we need to change the
`$(AndroidPackageFormat)` to `aab`. It will be `aab` by default for `Release` builds,
but will default to `apk` for `Debug` builds. Setting the `AndroidPackageFormat` to `aab`
will disable fast deployment, so it is advised that you only do this when you need to test
your `AssetPacks`.

To test your asset packs add the following to the first `<PropertyGroup/>` in your `.csproj`. 

```xml
<AndroidPackageFormat>aab</AndroidPackageFormat>
<AndroidBundleToolExtraArgs Condition=" '$(Configuration)' == 'Debug' ">--local-testing $(AndroidBundleToolExtraArgs)</AndroidBundleToolExtraArgs>
```

The `--local-testing` argument tells the `bundletool` application to install all the asset packs
in a local cache on the device. `InstallTime` packs will be installed during the app installation process.

`FastFollow` packs behave like `OnDemand` packs. They will not automatically installed when the app
is sideloaded. You will need to request them manually when the game starts.

For more details see [https://developer.android.com/guide/playcore/asset-delivery/test](https://developer.android.com/guide/playcore/asset-delivery/test).

## Implementation Details

There are a few changes we need to make in order to support this feature.
One of the issues we will hit is the build times when dealing with large assets.
Current the assets which are to be included in the `aab` are ***copied***
into the `$(IntermediateOutputPath)assets` directory. This folder is
then passed to `aapt2` for the build process.

The new system adds a new directory `$(IntermediateOutputPath)assetpacks`.
This directory would contain a subdirectory for each `pack` that the
user wants to include.

```dotnetcli
assetpacks/
    assets1/
        assets/
            movie2.mp4
    assets2/
        assets/
             movie3.mp4
```

All the building of the `pack` zip file would take place in these subfolders.
The name of the pack will be based on the main "packagename" with the asset pack
name appended to the end. e.g `com.microsoft.assetpacksample.assets1`.

During the build process we identify all the `AndroidAsset` items which
have an `AssetPack` attribute. These files are then copied to the
new `$(IntermediateOutputPath)assetpacks` directory rather than the
existing `$(IntermediateOutputPath)assets` directory. This allows us to
continue to support the normal `AndroidAsset` behavior while adding the
new system.

Once we have collected and copied all the assets we then use the new
`<GetAssetPacks/>` Task to figure out which asset packs we need to create.
We then call the `<CreateDynamicFeatureManifest/>` task to create a required
`AndroidManifest.xml` file for the asset pack. This file will end
up in the same `$(IntermediateOutputPath)assetpacks` directory.
We call this Task `<CreateDynamicFeatureManifest/>` because it can be used
to create any feature pack if and when we get to implement full feature
packs.

```dotnetcli
assetpacks/
    assets1/
        AndroidManifest.xml
        assets/
            movie2.mp4
    assets2/
        AndroidManifest.xml
        assets/
             movie3.mp4
```

We can then call `aapt2` to build these packs into `.zip` files. A new
task `<Aapt2LinkAssetPack/>` task takes care of this. This is a special version
of `Aapt2Link` which implements linking for asset packs only.
It also takes care of a few problems which `aapt2` introduces. For some
reason the zip file that is created has the `AndroidManifest.xml` file
in the wrong place. It creates it in the root of the zip file, but the
`bundletool` expects it to be in a `manifest` directory.
`bundletool` will error out if its not in the right place.
So `<Aapt2LinkAssetPack/>` takes care of this for us. It also removes a
`resources.pb` which gets added. Again, `bundletool` will error if this
file is in the zip file.

Once the zip files have been created they are then added to the
`@(AndroidAppBundleModules)` ItemGroup. This will ensure that when the
final `.aab` file is generated they are included as asset packs.

## Alternative Methods

An alternative method is available on [github](https://github.com/infinitespace-studios/MauiAndroidAssetPackExample).
This method allows developers to place additional assets in a special
[NoTargets](https://github.com/microsoft/MSBuildSdks/blob/main/src/NoTargets/README.md) project.
This project is built just after the final `aab` is produced. It builds a zip
file which is then added to the `@(Modules)` ItemGroup in the main application.
This zip is then included into the
final app as an additional feature.

Using a separate project like in the hack is one way to go. It does have some
issues though.

 1. It is a `special` type of project. It requires a `global.json` which imports
    the `NoTargets` sdk.
 2. There is no IDE support for building this type of project.

Having the user go through a number of hoops to implement this for
.NET for Android or .NET MAUI is not ideal.
