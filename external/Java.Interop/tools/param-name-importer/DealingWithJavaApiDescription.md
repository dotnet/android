
# Dealing with Java API description

## Java API XML description files: how it exists nowadays

Historically, Google used to ship Android Framework API information as in XML format, and to build Mono.Android.dll we parsed and interpreted these description files. Those files defined most of our API XML description format that we use nowadays.

As of Android 3.0, Google had not shipped the source code including these XML description files for long time, so we had been unable to build new API. Therefore we ended up to change our approach so that we built API description XML from android.jar by ourselves. At that time, the API extraction tool "jar2xml" was written in Java using Java reflection API (java.lang.reflect) and bytecode manipulator (asm).

And that turned to become the first core part of our Java Bindings support.

Java reflection API dependency caused several problems, especially on that we cannot extract correct java.* framework API (because with reflection API they always report java.* structure from the Java framework that the tool executes). Thus at some stage we ended up to change our strategy again and implemented bytecode parser "class-parse".

Unfortunately, class-parse was written in the way that it only reflects bytecode information, which means the outcome was totally different from the API information that Java reflection API offered. There is no type hierarchy, therefore no method overrides are resolved (which caused a lot of differences between the old and the new API information).

Therefore we needed to "adjust" the API description to match the old one, otherwise our C# binding generator ("generator.exe") and Metadata.xml don't work at all. This part is called "api-xml-adjuster" (implemented as Xamarin.Android.Tools.ApiXmlAdjuster.dll).


## Java API parsers

We have many Java parsers (unfortunately) within xamarin-android SDK and its own build system. They exist for different purposes and reasons.

(1) class-parse + api-xml-adjuster (Xamarin.Android.Tools.ApiXmlAdjuster)

class-parse is the bytecode parser tool (I wrote "the", which means that it is the only implementation) that parses class library jars into api.xml.class-parse.

api-xml-adjuster exists for the reason we described earlier. It has no other reason to exist. Since it also needs to retrieve Java API information from reference binding DLLs, it is tied to binding generator's type system.

(2) JavaDocScraper

Java bytecode does not contain method parameter names by design. We can still bind Java API without them, but the resulting API becomes awkward (a set of methods with parameter names "p0", "p1", ...).

Our solution for that is JavaDoc parsers.

JavaDoc is not great for retrieving information because it is not well structured. And it can be actually in any format, depending on "doclet"; Google indeed offers its own Android API Reference with its own "DroidDoc" doclet. But there is no other way, so we have different parser implementations for each "standard" doclet.

Doclet had changed in each Java releases: Java6, Java7 and Java8 had each doclet format that are different enough to confuse our document scraper. To make things worse, DroidDoc, the Google doclet, had also changed for each big API releases. We have three different JavaDoc scrapers up to 8 (note that it does not even include Java9 as of writing time) and two different DroidDoc scrapers (which may become three, depending on the upcoming work).

DroidDoc parsers are used for two different purposes:

* Build Mono.Android.dll: this requires from the ancient API documentation parser up to the latest parser, because we unify all the support API levels.
* Build Android support libraries: their API information is offered through developer.android.com and the docs are based on DroidDoc. For this purpose we don't need older API parsers.

JavaDoc parsers are used for generic Java Binding projects.

(We should probably support external doclet parsers so that users can provide and specify any kind of doclets, but we have heard of any requests for that. There is no plan to stabilize JavaDoc scraper API either.)

(JavaDocScraper in this context had been implemented in Java in jar2xml before, and now it is rewritten in C# in class-parse.)

(3) JavaDoc to C# Documentation converter (javadoc-to-mdoc)

Apart from JavaDocScraper for parameter names, we still have another reason to parse JavaDocs and DroidDocs. Bindings need API documentation, and they should be in our .NET API manner. It helps IDEs provide API information.

That means, we need almost entire API information including details.

It has been implemented in mono/mcs/tools/javadoc-to-mdoc. And it had been used only to generate Xamarin.Android API documentation i.e. it supported only DroidDocs. It was extended to support JavaDocs when we brought in this feature to xamarin-android to support any Java Binding projects. Yet, that is limited to the standard doclets for exactly the same reason as JavaDocScraper for method parameter names.

(Nowadays there should be almost no reason to have different JavaDoc scrapers, but as explained above, JavaDocScraper used to be Java, while javadoc-to-mdoc has been C# since its beginning.)

(4) DroidDoc parser for parameter names

JavaDocScraper for parameter names is problematic, not only because it always needs to renew the implementation whenever DroidDocs are updated, but also because it is not efficient to parse HTML docs every time we build the bindings. And that annoyed our Components team because unlike Xamarin.Android itself, they have to run JavaDoc Scraper every time (we don't run class-parse and api-xml-adjuster every time; we generate api-XY.xml.in only when new APIs get released).

Since the only information we/they need is method parameter names, they could be generated only once whenever new versions of the components get released. So the team had implemented support for "parameter names only" XML description format in class-parse.

It was part of Xamarin private repo, but now it is extracted at https://github.com/atsushieno/xamarin-android-docimporter (which was expected to be forked under xamarin, but it did not happen yet).

It is limited to DroidDoc as it was only for Android Components (support libraries and Google Play services). And it's not ready for xamarin-android that needs to build and run on Linux (this doesn't).

(5) Java stub API source parser for parameter names

DroidDoc support has been getting more and more problematic as Google stopped shipping "docs" SDK components anymore, and new API documentations are available only via the web.

Since Google ships "stub sources" in each platform (API Level) package, it is possible to parse Java sources and extract parameter names from each type. So as a replacement to DroidDoc parameter name parser, we now have a prototype of Java source parser implementation that exactly does this job.

It is part of xamarin-android-docimporter-ng in Java.Interop.

The stub sources are expected to have full type names almost everywhere so that we don't have to "resolve" type names (although we have detected that "@Deprecated" is used without "java.lang." which smells... hopefully not any more). It is safer scheme that we use it only to generate parameter names, not the entire API structure. That task can still be done by class-parse.

(6) DroidDoc parser for parameter names, reimplemented

The implementation at (4) was quite incomplete and did not try to parse all Android API, which exposes various issues. Thus it was rewritten to try to do better thing. The new DroidDoc scraper at https://github.com/atsushieno/xamarin-android-docimporter-ng generates the same parameter name description file as Java stub parser. It only targets Android API up to 23 because (5) covers the rest.

However this exposes further issues - some API documentations cannot be parsed because of Google's buggy HTML generation (e.g. android/content/ContentProvider.html significantly breaks its documentation structure. We parse the docs in extraneous way (e.g. inspecting descendants of certain elements instead of just children).

It is part of xamarin-android-docimporter-ng in Java.Interop.



*** List of parsers and their roles

| parser | info to generate | target libs | target versions | how often |
|-----------------------------|-----------------------|-----|---|---|
|class-parse+api-xml-adjuster | entire API definition | all | all ages | every build
|JavaDocScraper | parameter names | all | all ages | android.jar - every API release
|JavaDocScraper | parameter names | all | all ages | others - every build
|javadoc-to-mdoc | docs | all | latest | android.jar - every API release
|javadoc-to-mdoc | docs | all | latest | others - every release build
|xamarin-android-docimporter | parameter names | support/GPS | latest | every components release
|java-stub-parser | parameter names | android.jar | API 24 or later | every API release
|new DroidDoc parser | parameter names | android.jar | API 23 or earlier | only once, or every parser bugfixes


