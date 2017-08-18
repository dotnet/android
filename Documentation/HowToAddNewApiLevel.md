
# How to deal with new API Level

## This documentation is incomplete

In Xamarin ages, we used to have (more complete) API upgrade guide internally. But since then we switched to new xamarin-android repository which entirely changed the build system from Makefile to MSBuild solution, as well as the managed API for manipulating Android SDK, the old documentation almost does not make sense anymore. Even though I am writing this documentation, I don't know everything required (nor those who changed the build system didn't care about API upgrades).

Hence, this documentation is written from the ground,  exploring everything around.

And since the build system has changed between the first preview of Android O and the latest Android O preview (3), and it is quite possible that the build system changes over and over again, it might still not make much sense in the future.

## Steps

Anyhow, this commit would tell you what needs to be changed when the new API preview arrives (and that becomes stable): https://github.com/xamarin/xamarin-android/pull/642

1) Add/update new download to build-tools/android-toolchain.
The new API archive should be found on the web repository description
that Google Android SDK Manager uses (which can be now shown as part
of SDK Manager options in Android Studio).

As of Android O ages, it could be found at https://dl-ssl.google.com/android/repository/repository2-1.xml . It used to be different repository description URL, and it will be different URL in the future.

2) Create and add api-O.xml.in under src/Mono.Android/Profile directory.
It can be done from within `build-tools/api-xml-adjuster` directory. See `README.md` in that directory for details. You will have to make changes to Makefile in that directory to generate it. Also note that it often happens that Google does not ship documentation archive (!) and in such case we will have to scrape DroidDoc from the web and create our own docs archive.

3) Review new API (or review the changes in case of API updates).

Some of the changes are caused by API removal. Usually they come with
"deprecated" state in the previous API Level first, but that's not always true.

4) enumification: See `build-tools/enumification-helpers/README`.

