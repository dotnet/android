# Introduction

We have a rudimentary call tracing system in place which can record native, Java and managed (sort of)
stack traces when necessary.  However, this system was quickly slapped up together in order to try
to find out why marshal methods break Blazor apps.  The system doesn't optimized collection and delivery,
making it heavy (e.g. it relies on logcat to log large stack traces) and hard to read the results.

This branch is an attempt to fix the above downsides and implement a generic (as far as we are concerned -
not for arbitrary use in other products) framework to store timed events in a thread-safe manner and with
as little work at runtime at possible, but with options to do more when necessary.

# Design goals and notes

## Collection

Records are stored in pre-allocated buffers, one per thread. Buffer is allocated at the time when thread
attaches to the runtime, its pointer is stored in TLS as well as in some central structure for further
processing.  At runtime, the pointer from TLS is used to store events, thus enable lockless operation.

The collection process can be started at the following points:

  * startup of the application
  * after an initial delay
  * by a p/invoke at the app discretion
  * by an external signal/intent (signal might be faster)

The collection process can be stopped at the following points:

  * exit from the application
  * after a designated delay from the start
  * by a p/invoke at the app discretion
  * by an external signal/intent

`Buffer` is used loosely here, the collected data may be called in a linked list or some other form of
container.

Each trace point may indicate that it wants to store one or more stack traces (native, Java and managed)

### Native call traces

If call stack trace is to be collected, gather:

  1. Shared library name or address, if name not available (we might be able to get away with using just
     the address, if we can gleam the address from memory map post-mortem)
  2. entry point address

### Java call traces

Those will require some JNI work as it might not be possible to map stack frame addresses to Java methods
post-mortem.

### Managed call traces

Might be heavy, may require collecting all the info at run time.

## Delivery

At the point where collection is stopped, the results are dumped to a location on device, into a single
structured file and optionally compressed (`lz4` probably)

The dumped data should contain enough information to identify native frames, most likely the process
memory map(s).

Events collected from threads are stored in separate "streams" in the output file, no ordering or processing
is done at this point.

## Time stamping

Each event is time-stamped using the highest resolution clock available.  No ordering of events is attempted
at run time.

## Multi-threaded collection

No locks should be used, pointer to buffer stored in thread's TLS and in some central structure for use when
dumping.

## Managed call tracing

While MonoVM runtime can let us know when every method is called, we don't want to employ this technique here
because it would require filtering out the methods we're not interested in at run time, and that means string
allocation, comparison - too much time wasted.  Instead we will have a system in place that makes the whole
tracing nimbler.

### Assembly and type tracing

Each trace record must identify the assembly the method is in, the class token and the method token.  Assemblies are
not identified by their MVVID, but rather by an ordinal number assigned to them at build time and stored somewhere
in `obj/` for future reference.  Each instrumented method has hard-coded class and method tokens within that
assembly.

### Filters

We'll provide a way to provide a filter to instrument only the desired methods/classes.  Filters apply to assemblies
after linking but **before** AOT since we must instrument the methods before AOT processes them.  Three kinds
of targets:

  * `Methods in types`.
    A regex might be a good idea here.
  * `Full class name`.
    By default all type methods are instrumented, should be possible to exclude some (regex)
  * `Marshal method wrappers`.
    They are **not** covered by the above two kinds, have to be enabled explicitly. By default all are instrumented,
    possible to filter with a regex.  Both inclusion and exclusion must be supported.
