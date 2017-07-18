# Mono.Android API XML generator

## What is this directory for?

It is a directory for `api-xml-adjuster` build-only tool, as well as a
work directory to regenerate src/Mono.Android/Profiles/api-*.xml.in.

## Why is this not part of the build?

Generating those api-*.xml.in takes too much time to download the docs
archives (which are not always provided by Google, by the way), scrape
all those DroidDoc HTMLs and then re-parsed to resolve inheritance
hierarchy to generate correct (backward-compatible) API description XML.

It should happen only once in a while (every time Google publishes a
new API Level with docs).

(Well, I'm not very honest above: it *should* be built every time with
our latest class-parse and api-xml-adjuster so that those toolchains
don't trigger regressions that can cause Mono.Android API breakage.
But as I stated above, it takes too much time anyways...)

## Why are those docs archives not stored under obj/\* ?

Because you don't want to clean up and download them every time you run
MSBuild /t:Clean.

## Why is this based on Makefile?

It had existed as such, and it's not part of the build, no need to be
built on Windows so far.


## How do you get the docs archive for the preview APIs?

Google publishes the docs SDK component only against the stable API,
which means that preview API types are not included.
That makes our parameter names retrieval impossible for the preview API.

To workaround the issue, we create corresponding API docs zip archive
from developer.android.com using the following tool:
https://github.com/xamarin/components/tree/master/AndroidDocUtil

