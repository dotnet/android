Android Instant Apps and Xamarin Android
========================================

Synopsis
--------

Support for Android Instant Apps in Xamarin Android is currently impossible/impractical.


Introduction
------------

[Android Instant Apps](https://developer.android.com/topic/instant-apps/index.html) is a new Android
feature that allows application developers to create applications which don't need to be fully installed
on the user's device but rather started "instantly" by clicking a link to the application.

This is essentially implemented by creating a number of individual .apk packages which contain various 
application features, most commonly accompanied by an Activity which presents the feature to the user.

To support "instant" launching of the applications as well as to ascertain security of applications
Google imposed a number of limitations on such apps described in the following sections.


Limitations
===========

https://developer.android.com/topic/instant-apps/prepare.html#restricted_features


Limitation 1: size
------------------

Each instant app consists of at least a single apk (called the **base package**) and zero or more
*feature* apk packages which are installed on demand. When the user clicks application link they
are taken to the Play Store and the base apk is downloaded, installed on the system and cached for
future use. If the application consists of more apks they are downloaded whenever the user, or an
Activity in the base or feature apk, opens a link which corresponds to the requested feature/actvity.
To ensure quick download times, Google imposed a limit of 4MB to any individual apk as well as to the
base + feature apk size. 
Instant App developers are encouraged to put as few assets in the .apk as possible and download the 
rest when the app is first started.

**Impact on .NET for Android**: .NET for Android nearly pushes the size limit with a simple "Hello World" application
where, in Release build, the Mono runtime is nearly 3MB in size - not including the BCL, SDK and
application assemblies. While it would be possible to download the Mono runtime and the assemblies on
the app startup (XA app include a small Java stub responsible for launching of the managed application),
the solution would probably be very unwieldy and impractical.


Limitation 2: native code
-------------------------

Instant Apps are not allowed to contain and run any arbitrary native code/libraries. This limitation
exists most probably for security reasons. It is also disallowed to dynamically load any code other
than the Instant Apps runtime.

**Impact on .NET for Android**: it prevents .NET for Android applications from running since both
the Mono and the .NET for Android runtimes are implemented as shared libraries (we also need, depending
on application, the Sqlite library). In order to support Instant Apps in .NET for Android,
its runtime as well as the Mono runtime would have to be included in the set of allowed native code
libraries by Google/Android.

