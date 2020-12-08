# **X**amarin **A**ndroid **T**ests

`XAT` is a utility created to make working with `Xamarin.Android`
tests easier and more approachable.  The main problem that this
utility aims to solve is the scattered nature of the traditional
(based on MSBuild) setup to configure, run and report on results of
the many test suites we have.  The MSBuild-based solution kept the
code and data related to tests in several locations:

 * `tests/RunApkTests.targets`
 * `build-tools/scripts/TestApks.targets`
 * `build-tools/scripts/RunTests.targets`
 * `external/Java.Interop/build-tools/scripts/RunNUnitTests.targets`
 * `build-tools/automation/**/*.yaml`
 * Various `*.projitems` files scattered around the source tree

The locations above together defined code that would gather test
definitions (mostly from the `*.projitems` files, but sometimes from
item groups inside the `*.targets` files), define targets to run
various kinds of tests (host system unit tests, APK tests on device,
host unit tests accessing devices, `MSBuild` tests etc) and process
their results.  The code, out of necessity, included many bits of
repeated code, filesystem locations, conditions etc - all of this
together making management and modification quite unapproachable, even
for people working day-in day-out with `Xamarin.Android`, let alone
with potential external contributors.  It was very easy to forget
where one needs to modify and what in order to add, remove or modify
tests or affect the way they are built, executed and what happens with
the results.

On top of the `MSBuild` infrastructure, we had a
separate set of `YAML` files defining how the tests are built and
executed on the CI servers.  These files indirectly used the data
defined in the locations above, but introduced their own
infrastructure to divide up the test suites into chunks we can run on
CI as effectively and in as little time as possible.  This required
another instance of code and data duplication (as the `YAML` pipeline
cannot directly access the `MSBuild` files or data stored in them).
While absolutely necessary for every day efficiency of the PR-based
workflow as well as validation of the already committed code, this
necessary design created another problem: one wasn't able to run the
tests locally in the same manner they ran on the CI servers.  This is
because of the fact that the CI pipelines cannot be executed on the
developer's system.

`XAT` puts all that information and capability in a single location,
ideally allowing for changing of a handful of source files (see in
further sections of the document) in order to change how and what
tests are ran, how their results are stored and processed. 

Another problem `XAT` aims to solve is to increase discoverability of
the tests, as well as simplification how one can run a single test
suite or a single test from any of the known test suites.

# Concepts

## Test suites

## Test suite groups

# Code layout

# Usage

## Command-line arguments

## Running test suites

## Running test groups

# Main sources to edit when adding/removing/modifying tests
