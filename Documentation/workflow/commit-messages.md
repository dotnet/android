# Commit Messages

> “Always code as if the guy who ends up maintaining your code will be a
> violent psychopath who knows where you live.”
> 
> [- John F. Woods](https://groups.google.com/d/msg/comp.lang.c++/rYCO5yn4lXw/oITtSkZOtoUJ)

Code maintenance is more than the structure and formatting of the code itself.
Maintenance also requires being able to access the *history* of the code.
Of equal importance as the change is *why* the change was made.


# Audience

A commit message needs to be able to answer the interrogatives: *who*, *what*,
*where*, *when*, *why*.  *Some* of these answers can be gleamed from the commit
diff: *who* made the commit, *what* files were changed and what are the changes,
*when* was the commit made.

The commit diff *cannot* answer the *why*: why does the commit exist?  Answering
"why" is what the commit message is for, and *reintroduces* the interrogatives:
*what* was the issue being addressed?  *Who* was impacted by it?
*When* did the issue occur?

The "audience" of a commit message is *you*, six months from now, after you've
forgotten everything relevant to the bug at hand but something, for some reason,
indicates that *this* commit is once again relevant.  Not only does the violent
psychopath know where you live, the violent psychopath is *you yourself*.

What are you likely to forget that will be important to know?  The commit
message is where to put that information.


# English

Commit message should be properly spelled in some English regional language; it doesn't
need to be American English vs. British English, but for whichever regional language
*is* used, words should be spelled correctly.  Proper grammar should be used.

Present tense should be used for work done within the current commit.

Past tense should be used when referring to previous commits and for the lived
experience of investigations pertinent to the current commit.

Future tense should be reserved for mentioning future work such as TODO items,
and should be held to a minimum.


# Formatting

Commit messages have two parts: the summary, and the body.

## Commit Summary

The summary is a one line summary of the commit, and should be less than 70
characters in width.  The summary should follow one of the following patterns:

[Dependency Bump](#Dependency_Bumps) should use one of:

```
Bump to org/repo/branch@commit
Bump to [Dependency Name] [Dependency Version]
```

Any other change should follow the pattern:

```
[Component] Summary
```

In which *Component* is either the directory name containing the "main purpose"
of the change, e.g.

```
[Mono.Android] Android API-R Developer Preview 2 Binding (#4468)
```

in which `src/Mono.Android` contained most of the changes, *or* Component
should be one of the following "broad" categories:

  * `build`: Changes related to *building* the xamarin-android repo,
    particularly locally.
  * `ci`: Changes related to our Continuous Integration infrastructure
  * `docs`: Changes to documentation
  * `tests`: Changes to Unit tests


## Commit Body

The commit message body should be
[GitHub Flavored Markdown-formatted plain text][markdown] between 70-72
characters in width, so that `git log` in an 80-character wide shell is usable.

The commit body may contain [sections](#Sections); see below for details.

Please use two spaces after periods for sentences.

[markdown]: https://guides.github.com/features/mastering-markdown/


### Code Formatting

Syntax highlighting blocks using triple-backticks ``` should *not* be used,
as those are ugly when reading as unformatted plain text.

Use *tabs* to indent code, not 4 spaces. 

Within a code block, use 2- or 4-space tabs to indent code:

```csharp
	// Tabs for initial indentation
	void MethodName ()
	{
	    // Use spaces to indent after initial tab
	}
```

### Ordinal and Bullet Formatting

When using numbered and bulleted lists, format them to "look pretty" and have
nicely aligned *leftmost* text.  If an item wraps onto multiple lines, each
subsequent line should be

Bullets should be two spaces, then the bullet, a space, then the bullet content.

This:

```Markdown
  * This is a bulleted item
```

*Not* this:

```Markdown
* Bullets on leftmost column are ugly.
```

Numbers should be formatted such that the width of the "number part" is
4 spaces, and *not* left aligned.

This:

```Markdown
 1. First
 2. Second.
    Second line for (2).
```

*Not* this:

```Markdown
1. First
2. Second.
    Second line for (2).
```


### Member References

When mentioning code constructs, a form of [Hungarian Notation][hung-note]
should be used.

When referring to MSBuild targets, use "the `TargetName` target."

When referring to MSBuild tasks, use `<TaskName/>`.

When referring to MSBuild properties, use `$(PropertyName)`.

When referring to MSBuild item groups, use `@(ItemGroupName)`.

When referring to a C# type, use `Full.Type.Name` on the first occurrence, and
`Name` on subsequent occurrences.

When referring to a C# method, use `Type.Name.MethodName()` on the first
occurrence, and `MethodName()` on subsequent occurrences.

When referring to any other C# member, use `Type.Name.Member`.

[hung-note]: https://en.wikipedia.org/wiki/Hungarian_notation


# Styles

Commit messages within the xamarin-android repo tend to follow a
Dependency Bump style or a Bug Fix style.


<a name="Dependency_Bumps" />

## Dependency Bumps

Dependency Bumps have one of the following two styles.

Git submodule bumps in which the target is located in GitHub should use:

```
Bump to dependency-organization/dependency-repo/dependency-branch@dependency-commit
```

This is because GitHub will auto-link `organization/repo/branch@commit` to the
specified commit.


The commit body should contain a [`Changes: `](#Changes) section which uses
GitHub's [Comparing commits][compare-commits] URL scheme:

```
Changes: https://github.com/dependency-organization/dependency-repo/compare/old-commit...dependency-commit
```

If there are less than a dozen commits in the commit range, the invdividual
commits can be converted into a bulleted list, using the org/repo@hash format,
followed by a colon `:`, followed by the commit summary.

For example, if the submodule being bumped says:

```shell
$ git log --oneline old-commit...dependency-commit
deadbeef [ExampleComponent] Example summary (#1234)
```

then this can be converted into the bulleted list:

```
  * org/repo@deadbeef: [ExampleComponent] Example summary (#1234)
```

The `git log old-commit...dependency-commit` output should also be reviewed,
and if any commits mention that any bugs are fixed, the fixed bugs should be
converted into [`Context: `](#Context) links.

If more than a dozen commits are included in the bump, it is too verbose to
mention all the commits.  Mentioned bug fixes should still be mentioned as
`Context: ` links, and the bulleted list can be skipped.

For bumps that do not involve GitHub submodule bumps, the summary should be:

```
Bump to [Dependency Name] [Dependency Version]
```

If possible, provide a `Changes: ` link to the changelog of the dependency
which includes the relevant changes.


[compare-commits]: https://help.github.com/en/github/committing-changes-to-your-project/comparing-commits



## Bug Fix

Bug fixes shouldn't mention a bug number in the commit summary, but instead
should summarize the nature of the fix:

```
[Component] Short summary of the change
```

The commit body should contain a [`Fixes: `](#Fixes) section mentioning the bug
URL, if possible.

The bug should be described: what was required to encounter the bug?  Were any
MSBuild properties required to be set to particular values?

*Were any error messages printed*?  *All* relevant error messages should be
present within the commit message body, as this makes it easier to verify if
a new bug report has already been fixed.

*Do not rely on* the `Fixes: ` URL to provide the above information.  The URL
may become inaccessible in the future, or the URL contents may be difficult to
follow, e.g. there was lots of "back and forth" between the submitter and those
responsible for fixing the bug.  *Summarize* the issue.

A description of the fix should be provided, as well as any other relevant
information.


<a name="Sections" />

# Sections

The commit message may have the following sections or labels, in this order:

  * [`Changes`](#Changes)
  * [`Fixes`](#Fixes)
  * [`Context`](#Context)


<a name="Changes" />

## Changes

When bumping a dependency such as a git submodule or a NuGet package reference,
the `Changes: ` line should be present and is a link to the fixes included
within the bump.

If the bump contains a "small" number of changes -- less than a dozen? -- then
the commit message can *also* contain a bulleted list of all the commits within
the bump.


<a name="Fixes" />

## Fixes

`Fixes: ` is used to indicate that a bug located at a specific URL is fixed by
the commit.  *Full* URLs are to be used, *not* GitHub abbreviations, as just
because GitHub is used this week doesn't mean it will continue to use GitHub
for the infinite future.  (GitHub is already .NET for Android's *third* bug
repo, after `bugzilla.novell.com` and `bugzilla.xamarin.com`...)

Links to private bugs may be used.

Even when linking to a bug report, the commit message should *re-describe* what
the bug is, how it was encountered, and the nature of the fix.  The bug URL may
become inaccessible in the future (or currently!), and even when the bug URL
exists and is public, it may be long and difficult to follow.  The commit
message is the opportunity to *summarize* the nature of the bug, so that it may
be understood.


<a name="Context" />

## Context

`Context: ` is usd to link to "something else" that is relevant to the current
commit, but doesn't make sense to appear as a link within the body text.
`Context: ` is frequently used for Mono bumps to list the bugs that the mono
bump fixes, but *without* using `Fixes: `, as `Fixes: ` will mark a bug as
closed, which may not be appropriate.

When mentioning another commit within this repo, *do not* use GitHub URLs to
the commit, just use the commit hash.  This is an indicator that the target
is within the current repo, without needing to read and understand the URL.

