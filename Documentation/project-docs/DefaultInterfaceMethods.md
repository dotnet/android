# Xamarin.Android and C# 8.0 default interface methods

We have been trying to implement support for C# 8.0 default interface methods (DIMs) in Xamarin.Android, to reflect Java 8 default interface methods (DIMs). Here we are going to write some notes about it.

## Reference materials

There are [couple](https://github.com/dotnet/csharplang/issues/52) [of](https://github.com/dotnet/csharplang/issues/288) [relevant](https://github.com/dotnet/csharplang/blob/master/proposals/default-interface-methods.md) [issues](https://github.com/dotnet/roslyn/issues/17952) (and docs), but not everything is consistent. This post is written based on the implementation (mono's `csc-dim` version of csc) I have been using and packaging.

## What are different from Java

The feature is similar, but there are actually some caucious differences.

This is how C# 8.0 DIMs work:

```
~/Desktop$ cat dim.cs 
using System;

public interface IFoo
{
    int X () { return 1; }
    int Y => 2;
}

public class Bar : IFoo
{
}

public class Baz
{
	public static void Main ()
	{
		var bar = new Bar ();
		var foo = (IFoo) bar;
		Console.WriteLine (foo.X ());
		Console.WriteLine (bar.X ());
	}
}
~/Desktop$ csc-dim dim.cs -langversion:latest
Microsoft (R) Visual C# Compiler version 42.42.42.42424 (<developer build>)
Copyright (C) Microsoft Corporation. All rights reserved.

dim.cs(20,26): error CS1061: 'Bar' does not contain a definition for 'X' and no extension method 'X' accepting a first argument of type 'Bar' could be found (are you missing a using directive or an assembly reference?)
```

(`csc-dim` is a special C# compiler distributed in the latest mono, which is anohter roslyn csc build which enabled C# 8 DIMs. We need `-langversion:latest` to actually use DIMs even with it.)

You cannot call `new Bar ().X ()` because `X()` does not exist in `Bar`. In Java 8, `Bar.X()` is publicly accessible. In current C# 8.0 language specification, `Bar.X()` is **explicitly implemented** and **has no public API**. It's like existing non-DIMs that are not implicitly declared in a class.

(It is explained at https://github.com/dotnet/csharplang/issues/288#issue-215243291 too. See "Concrete methods in interfaces".)

It is actually argurable design, but that is another story. we're explaining only the fact part.

It should be noted that we are under different circumstances than others:

- We have no control over the API. We have no position to define API. Even Googlers don't especially in java.\* API.
- Android API, or even Java 8 API, has grown up to convert non-DIMs to DIMs.
- We have existing customers who have implemented existing Xamarin.Android interfaces.

Therefore my investigation on C# 8.0 DIMs was in quite different direction from what others do/did.

Let's see what kinf of default interface methods Android API has:

- `java.lang.Iterable#spliterator()` is a DIM which was added at API Level 24 (Android N). Google had moved to OpenJDK class libraries at API Level 24 and there was a bunch of additions like this.
- Similarly, `java.lang.Iterable#remove()` is a DIM, but it had existed before API Level 24 as non-DIM. Since this method was public, there would be customers who have used this method.


Would there be any behavioral difference result in that Xamarin.Android cannot support DIMs? Not anything so far. 

What happens if, some customer has code that uses this non-default version of the method, like:

```
public class MyIterator : Java.Lang.IIterator
{
	// (there must be customer implementation of Remove() because it was not default before)
	public void Remove () { ... }
}

public void ReduceAndSkipSome (MyIterator iterator)
{
    IIterator i = iterator; // RValue can be anything. This is a PoC so just let it as is, but with explicit interface type.
	while (i.HasNext ()) {
		var current = i.Next ();
		if (MatchesSomeCondition (current)
			i.Remove (); // therefore it should still compile.
    }
}
```

This will work.

What if once we replace non-DIM IIterator.Remove() with a DIM, and customer rebuilt project without touching any of their source code, will it invoke `MyIterator.Remove()` ? The answer is **yes**.  It is not `IIterator.Remove()` and it should still work just like it used to do.

There won't be ABI breakage, but there is a problem that Java developers won't face. If a Xamarin.Android interface provides some DIMs and a third party library class `C` implements it without implicitly overriding it, the class is not as useful as one in Java, because of the reason I explained at first. There is no callable `new Bar().X()` where `X` is a DIM in `Foo` which `Bar` implements.

To avoid that uselessness, the `C` developer needs to override ALL those DIMs. That's what current specification supporters claim developers to do. The current specification is likely the most consistent with the existing interface specification indeed.

Another case Java developers would get stuck:

```
public interface IComparer
{
    int Compare (Object o1, Object o2);
}
public interface IList
{
    void Sort (IComparer comparer) { ... } // DIM
}
public class Vector : IList { ... } // implements everything in IList
public class MyVector : Vector
{
    void IList.Sort (IComparer comparer) { ...} // ERROR!!
}
```

This doesn't compile because "`MyVector` does not implement `IList`"... to make it compile, you will have to explicitly add `IList` as an implemented interface to `MyVector` declaration:

```
public class MyVector : Vector, IList { ... }
```

Then it will compile (and you don't have to implement non-DIMs in `IList` because `Vector` implements them). It is weird but that's how C# works.


### What Xamarin.Android currently does

In the stable releases as of now, we don't bind any "new" default interface methods because generating them as non-default interface methods means the interface will be messy to implement (for example, what if you are told "you are supposed to implement `forEachRemaining()` and `remove()` in all of your `Iterator` implementation classes" ?). It is safe to ignore them because they don't need implementation.

On the other hand, we generate managed methods for default interface methods IF they had existed as non-DIM in the earlier API Levels. There is a supplemental reason for that: `Mono.Android.dll` is offered per API Level, but it is not only from the corresponding API Level. Each `Mono.Android.dll` is a result of "merging" APIs from the earlier API. It is to bring API consistency across API Levels (if you look for `api-merge` in `xamarin-android/src/Mono.Android/Mono.Android.targets` you'll see how it is done). With this API model, it kind of makes sense to keep non-DIM as is, regardless of whether it is DIM now or not.

For example, `javax.security.auth.Destroyable#destroy()` is a DIM in API Level 24 or later, but we mark it as non-DIM because it had existed since API Level 1.


### What Xamarin.Android will change

We keep "already existed as non-DIMs" as is i.e. `Javax.Security.Auth.Destroyable.Destroy()` will remain DIM. I'm not sure if we will ever change that. This means Xamarin.Android application and library developers will be suffered from "extraneous work to implement some interface methods" to some extent.

On the other hand, there will be totally new interface methods as DIMs such as `Java.Util.IIterator.ForEachRemaining()`.

For non-abstract overrides such as `Java.Util.IPrimitiveIteratorOfDouble.ForEachRemaining()`, it will override the DIM as non-abstract, explicitly.

For abstract overrides such as `Java.Util.IPrimitiveIterator.ForEachRemaining()`, it will override the DIM as abstract, explicitly.


