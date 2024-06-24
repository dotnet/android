# Draft release notes

- [Overview](#overview)
- [File names](#file-names)
- [Bug and enhancement labels](#bug-and-enhancement-labels)
- [Templates](#templates)
- [Publication workflow](#publication-workflow)

## Overview

Any Pull Request that fixes a "notable" user-facing bug, implements a
user-facing feature, _intentionally_ alters semantics, etc., should ideally
_also_ add a Markdown file to this directory with a description of the change.

The "editorial audience" of these files is end-users of the product.
(Meanwhile, the "audience" of commit messages is other developers of the
xamarin-android repo.)

To ensure that release notes are cherry-picked to release branches as expected,
only add notes to this directory directly as part of the pull requests that
include the code changes.  If wording changes are needed after a pull request is
merged, send the changes to the team member who is assembling the release notes.

## File names

The name of each new file should be a "unique" string.  This could be the PR
number, if known, or the branch name used for the PR, or a GUID.  The filename
itself is largely irrelevant; the point is that it be sufficiently unique so as
to avoid potential merge conflicts with other PRs.

## Bug and enhancement labels

Add a **bug** or **enhancement** label on every user-facing issue and pull
request, even for low visibility items.  It is OK to use both labels on some
items.

Changes for One .NET should have neither label for now because they are not yet
user-facing.

PRs that bump Java.Interop, android-tools, or monodroid generally don't
need to be labeled, but Mono bumps that include changes that could affect
Android users should be labeled.

The final release notes use the labels to provide search queries for all of the
user-relevant changes in the release.

## Templates

In general, use the same grammar rules as for [commit
messages][commit-messages].  For example, for bug fixes, use the past tense to
describe what was wrong before the fix was introduced.  Use the present tense to
describe changes included in the new version.  Feel free to share some wording
between the commit messages and the release notes.

To facilitate the final publication workflow, follow the formatting style from
the [xamarin-docs contribution guidelines][docs-guidelines].  For example, use
italics for paths and quoted text instead of using backticks.  Use `_` for
italics, use `**` for bold, and use `-` as the marker for bulleted lists.  Place
the first level of list markers at the beginning of the line, with no
indentation.  When referring to MSBuild properties, use syntax like:

```
`AndroidLinkMode` MSBuild property
```

To facilitate the final publication workflow, prefer inline links or named
link labels like `[android]`.  Avoid numbered link labels like `[0]`.

### For low visibility changes

Changes that have particularly low user visibility can optionally skip adding
any custom release notes.

### For medium visibility changes with short descriptions

- For performance improvements, choose from the following headings:

  - `### Build and deployment performance`
  - `### App startup performance`
  - `### Android resource editing performance`

  For other changes, choose from the following headings:

  - `### Application and library build and deployment`
  - `### Application behavior on device and emulator`
  - `### Application Mono Framework behavior on device and emulator`
  - `### Application publishing`
  - `### Android API bindings`
  - `### Bindings projects`
  - `### Design-time build process`
  - `### .NET for Android SDK installation`
  - `### IDE compatibility`

- Write a list item to go under the heading.  Start the item with one of the
  following, in order of preference:

  - Links to all public GitHub or Developer Community items fixed by the change
  - A link to the public pull request that introduced the improvement
  - No links

Examples:

```markdown
### Build and deployment performance

- [GitHub PR 3640](https://github.com/xamarin/xamarin-android/pull/3640):
  Use System.Reflection.Metadata rather than Cecil for
  `ResolveLibraryProjectImports`.  This reduced the time for the
  `ResolveLibraryProjectImports` task from about 4.8 seconds to about 4.5
  seconds for a small test Xamarin.Forms app on an initial clean build.
```

```markdown
### Application behavior on device and emulator

- [Developer Community 743965](https://developercommunity.visualstudio.com/content/problem/743965/newtonsoftjsonjsonreaderexception-unexpected-chara.html),
  [GitHub Issue 3626](https://github.com/xamarin/xamarin-android/issues/3626):
  Starting in Xamarin.Android 10.0, _Newtonsoft.Json.JsonReaderException:
  Unexpected character encountered_ caused `JsonConvert.DeserializeObject()` to
  fail in apps built in the Release configuration.
```

```markdown
### Build and deployment performance

- [Java.Interop GitHub PR 596](https://github.com/dotnet/java-interop/pull/596):
  Use `File.Exists()` instead of `DirectoryGetFile()` in a few places.  This
  reduced the time for the `LinkAssembliesNoShrink` task from about 710
  milliseconds to about 430 milliseconds for a small test Xamarin.Forms app on
  an initial clean build.
```

### For longer notes

- Include a custom summary heading.
- If the feature represents a deprecation, removal, or default configuration
  change, include a list item for it under a `#### Deprecations, removals, and
  default configuration changes` heading at the top of the note.

Example:

````markdown
#### Deprecations, removals, and default configuration changes

- [XA1023 warning for upcoming DX DEX compiler deprecation](#xa1023-warning-for-upcoming-dx-dex-compiler-deprecation)

### XA1023 warning for upcoming DX DEX compiler deprecation

Projects that have **Dex compiler** set to **dx** in the Visual Studio project
property pages will now get a build warning:

```
warning XA1023: Using the DX DEX Compiler is deprecated. Please update `$(AndroidDexTool)` to `d8`.
```

To resolve this warning, set the **Dex compiler** in the Visual Studio project
property pages to **d8** or edit the project file [in Visual
Studio][edit-project-files] or another text editor and set the `AndroidDexTool`
MSBuild property to `d8`:

```xml
<PropertyGroup>
  <AndroidDexTool>d8</AndroidDexTool>
</PropertyGroup>
```

[edit-project-files]: https://docs.microsoft.com/visualstudio/msbuild/visual-studio-integration-msbuild?view=vs-2019#edit-project-files-in-visual-studio

#### Background information

Google [has deprecated the DX DEX compiler][dx-deprecation] in favor of the [D8
DEX compiler][d8-upstream].  After February 1, 2021, DX will no longer be a part
of the Android SDK or Android Studio.  Project authors are encouraged to migrate
their projects to D8 at their earliest convenience to prepare for this change.

[dx-deprecation]: https://android-developers.googleblog.com/2020/02/the-path-to-dx-deprecation.html
[d8-upstream]: https://developer.android.com/studio/releases/gradle-plugin#D8
````

### For submodule or .external bumps

- Use a single file for all the notes.
- Use section headings to help organize the notes.
- For items that do not have a public issue or PR on GitHub or Developer
  Community, don't worry about including a link.
- For Mono Framework version updates, the main focus for now is to include
  entries for any issues in the xamarin-android repo fixed by the bump.

Example showing multiple release notes sections for a Java.Interop bump:

```markdown
### Build and deployment performance

- [Java.Interop GitHub PR 440](https://github.com/dotnet/java-interop/pull/440),
  [Java.Interop GitHub PR 441](https://github.com/dotnet/java-interop/pull/441),
  [Java.Interop GitHub PR 442](https://github.com/dotnet/java-interop/pull/442),
  [Java.Interop GitHub PR 448](https://github.com/dotnet/java-interop/pull/448),
  [Java.Interop GitHub PR 449](https://github.com/dotnet/java-interop/pull/449),
  [Java.Interop GitHub PR 452](https://github.com/dotnet/java-interop/pull/452):
  Optimize several of the build steps for bindings projects.  For a large
  binding like _Mono.Android.dll_ itself, this reduced the total build time in a
  test environment by about 50 seconds.

#### Bindings projects

- [Java.Interop GitHub PR 458](https://github.com/dotnet/java-interop/pull/458):
  Bindings projects did not yet automatically generate event handlers for Java
  listener interfaces where the _add_ or _set_ method of the interface took two
  arguments instead of just one.
```

### Other guidelines

Feel free to include images in the notes if appropriate.  The image files can be
added to `Documentation/release-notes/images/`.

## Publication workflow

### Every week or so

1. Gather the new release notes from the master branch in reverse commit order.
   For example:

   ```sh
   git log --format="" -U v11.1.99.168..origin/master -- "Documentation/release-notes/"
   ```

2. Copy the notes into the [draft release notes wiki page][draft-notes].  Keep
   the individual headings on all of the notes, and keep the notes in reverse
   commit order.  This will make it easy to copy only the applicable notes once
   a particular commit is selected for publication.

3. Adjust the formatting to align with the style from the [xamarin-docs
   contribution guidelines][docs-guidelines].

4. For every new commit on the master branch, check for presence of the expected
   **bug** and **enhancement** labels on the corresponding issues and pull
   requests.

   The GitHub compare view is handy for this step. For example:

   ```
   https://github.com/xamarin/xamarin-android/compare/v11.1.99.168...master
   ```

   (This step will no longer be necessary once labels are being added
   consistently to issues and pull requests as part of PR authoring.)

### After the final insertion for an upcoming version

1. Determine the product version.  This can be done by viewing the history of
   the non-public [Insertion Release Definition][insertion-definition] and
   finding the corresponding xamarin-android commit.  Look in the 'vsts-devdiv artifacts'
   commit status details to find the `version`.

   ```
   {
       "branch": "master",
       "commit": "9bc00032eebc30e91a66114eff9026c6a3b0e4d7",
       "repo": "git@github.com:https://github.com/xamarin/xamarin-android",
       "sha256": "12caa3ca0e6f949183573bdc7727f7fa3143e46ef4b7202b6e42e28acc55a59b",
       "size": 411149176,
       "tag": "v11.1.99.218",
       "uploaded": true,
       "version": "11.1.99.218"
   }
   ```

2. Identify the latest change to the release notes for that version.  For
   example:

   ```sh
   git log -n 1 --format="" -U 9bc00032eebc30e91a66114eff9026c6a3b0e4d7 -- "Documentation/release-notes/"
   ```

3. If the latest change from step 2 is not yet included in the draft release
   notes wiki page, complete the [_Every week or so_](#every-week-or-so) steps
   to add any missing items.

4. Cut the applicable notes from the draft release notes wiki page and paste
   them into the appropriate final Markdown file in the non-public
   [xamarin-engineering-docs-pr repository][xe-docs].  Be sure to cut only the
   notes up to the latest change identified in step 2.

5. In the final Markdown file, rearrange the notes to remove the extra headings
   and sort the items by item number.

6. When a version is promoted from Preview to Release, remove all of the
   individual Preview versions from the release notes and consolidate the items
   under the Release version.

7. When a version is promoted from Preview to Release or when a new Preview 1
   version is published, update the [_TOC_][toc] and [_index_][index].  On days
   where both things are happening, it's convenient to make the _index_ changes
   as part of the Release PR and the _TOC_ changes as part of the Preview PR.

8. Prepare a pull request to merge the notes into the master branch of
   xamarin-engineering-docs-pr.  If you don't yet have sufficient permissions,
   request to join the non-public [xamarin-engineering-docs-write][docs-write]
   team.

9. Have at least one other team member who is a member of the
   xamarin-engineering-docs-write team review the pull request to enable the
   merge button.

10. Ensure that every Developer Community item fixed in the release has a linked
    Bug work item in Azure Boards that is marked as resolved and set to the
    correct **Milestone** and **Target**.  This allows the feedback team to bulk
    update the items as expected when the new version is published.

### As soon as a new version is published

1. Merge the xamarin-engineering-docs-pr pull request.

2. The publication process for Visual Studio for Mac should automatically create
   a Git tag in the xamarin-android repo that has a `v` prefix in front of the
   version number:

   ```
   v11.1.99.218
   ```

   If the Visual Studio release happens before the corresponding Visual Studio
   for Mac release, create the tag manually.

3. If the same build was published as a Preview version before being published
   as a Release version, then when the Release version is published, push a new
   tag that has `-pre` appended to the existing tag name:

   ```
   v11.1.0.17-pre
   ```

   After that, change the existing GitHub release for the Preview version to use
   the `pre` tag to free up the non-`pre` tag for the Release version.

4. Create a GitHub release: Go to
   <https://github.com/xamarin/xamarin-android/tags>, click the **...** menu on
   the far right of the tag name from step 2, then click **Create release**.

5. Paste the release notes into the GitHub release.

   For Release versions, paste in the additional _Versions for continuous build
   environments_ section as described in the internal [_Release
   Process_][release-process] guide.

   Recommendation: Include only the notes applicable to the specific version
   rather than the full page of release notes from docs.microsoft.com.
   Similarly, remove the _Xamarin.Android ... preview releases_ section.

6. Select the **This is a pre-release** checkbox if appropriate, and then click
   **Publish release**.

7. Navigate to <https://aka.ms> and update or create the named links for the
   release.  See the internal [_Release Process_][release-process] guide for
   additional details on how to complete this step.

   There are two styles of links to update. One style says _preview_ or
   _release_, like:

   ```
   xamarin-android-commercial-preview-windows
   ```

   The other style uses the xamarin-android release branch name instead, like:

   ```
   xamarin-android-commercial-d16-9-windows
   ```

8. When a version is promoted from Preview to Release or when a new Preview 1
   version is published, update the _Downloads_ section of the xamarin-android
   [_README_](/README.md) with the new info.

9. Add all of the pull requests and issues included in the new version to the
   appropriate GitHub milestone.  The milestone has the format:

   ```
   11.2 Release (d16-9)
   ```

   (For versions before 11.2, the milestone had a slightly different format.)

   Complete this step for xamarin-android, Java.Interop, and android-tools.

   See also the [_How to make bulk changes on pull requests and issues_][bulk-change]
   section for tips on how to complete this step.

10. (Maybe) Add comments on all of the fixed issues to notify users that fixes
    are available.

    Template for Preview versions:

    ```markdown
    _Release status update_

    A new Preview version of Xamarin.Android has now been published that includes the fix for this item. The fix is not yet included in a Release version. This item will be updated again when a Release version is available that includes the fix.

    Fix included in Xamarin.Android SDK version 11.1.99.168.

    Fix included on Windows in Visual Studio 2019 version 16.9 Preview 1. To try the Preview version that includes the fix, [check for the latest updates](https://docs.microsoft.com/visualstudio/install/update-visual-studio?view=vs-2019) in [Visual Studio Preview](https://visualstudio.microsoft.com/vs/preview/).

    Fix included on macOS in Visual Studio 2019 for Mac version 8.9 Preview 1. To try the Preview version that includes the fix, check for the latest updates on the **Preview** [updater channel](https://docs.microsoft.com/visualstudio/mac/update).
    ```

    Template for Release versions:

    ```markdown
    _Release status update_

    A new Release version of Xamarin.Android has now been published that includes the fix for this item.

    Fix included in Xamarin.Android SDK version 11.1.0.17.

    Fix included on Windows in Visual Studio 2019 version 16.8. To get the new version that includes the fix, [check for the latest updates](https://docs.microsoft.com/visualstudio/install/update-visual-studio?view=vs-2019) or install the most recent release from <https://visualstudio.microsoft.com/downloads/>.

    Fix included on macOS in Visual Studio 2019 for Mac version 8.8. To get the new version that includes the fix, check for the latest updates on the **Stable** [updater channel](https://docs.microsoft.com/visualstudio/mac/update).
    ```

### How to make bulk changes on pull requests and issues

#### Using the issue search page

1. Paste the list of space-separated item numbers into the query field on the
   issue search page for the repository. For example:

   ```
   https://github.com/xamarin/xamarin-android/issues?q=5298+5294+5250
   ```

2. Validate that the count of items returned by the search is correct.

3. Look through the list for any items that already have a milestone set.  These
   items might already have been included in an earlier release.  Edit the
   search query to remove any items that should not be updated to the new
   milestone.

4. Select the checkbox at the top left of the results to select all of the
   items.

5. Set the milestone.

6. Repeat steps 4 and 5 for any additional pages of results.

#### Using the GraphQL Explorer

1. Paste the list of PR numbers into the following program:

   ```csharp
   var numbers = new string [] { "5298", "5294", "5250" };
   Console.WriteLine ("{\n  repository(name: \"xamarin-android\", owner: \"xamarin\") {");
   foreach (var number in numbers)
   	Console.WriteLine ($"    pr{number}: pullRequest(number: {number}) {{ title, id, milestone {{ title }} }}");
   Console.WriteLine ("  }\n}");
   ```

   This will generate a query like:

   ```
   {
     repository(name: "xamarin-android", owner: "xamarin") {
       pr5298: pullRequest(number: 5298) { title, id, milestone { title } }
       pr5294: pullRequest(number: 5294) { title, id, milestone { title } }
       pr5250: pullRequest(number: 5250) { title, id, milestone { title } }
     }
   }
   ```

2. Open the [GitHub GraphQL Explorer][graphql-explorer] and sign in.

3. Paste and run the query from step 1 in the GraphQL Explorer.

4. In the GraphQL Explorer output, check for any items in the where
   `"milestone"` is not `null`.  These items might already have been included in
   an earlier release.  Remove any items that should not be updated.

5. Use another query to look up the ID of the desired milestone:

   ```
   {
     repository(name: "xamarin-android", owner: "xamarin") {
       milestones(query:  "11.3 Preview (d16-10)", first: 1) {
         nodes {title, id}
       }
     }
   }
   ```

6. Paste the results from step 4 and step 5 into the following program:

   ```csharp
   var milestone = "MDk6TWlsZXN0b25lNjA4ODE1MQ==";
   var ids = new string [] { "MDExOlB1bGxSZXF1ZXN0NTIxODQwOTIz", "MDExOlB1bGxSZXF1ZXN0NTIwNjgxMDY2", "MDExOlB1bGxSZXF1ZXN0NTEyNTgzNjE1" };
   Console.WriteLine ("{\n  mutation {");
   var i = 0;
   foreach (var id in ids)
   	Console.WriteLine ($"    update{i++}: updatePullRequest(input: {{ pullRequestId: \"{id}\", milestoneId:\"{milestone}\" }}) {{ pullRequest {{ number }} }}");
   Console.WriteLine ("  }\n}");
   ```

   This will generate a mutation like:

   ```
   {
     mutation {
       update0: updatePullRequest(input: { pullRequestId: "MDExOlB1bGxSZXF1ZXN0NTIxODQwOTIz", milestoneId:"MDk6TWlsZXN0b25lNjA4ODE1MQ==" }) { pullRequest { number } }
       update1: updatePullRequest(input: { pullRequestId: "MDExOlB1bGxSZXF1ZXN0NTIwNjgxMDY2", milestoneId:"MDk6TWlsZXN0b25lNjA4ODE1MQ==" }) { pullRequest { number } }
       update2: updatePullRequest(input: { pullRequestId: "MDExOlB1bGxSZXF1ZXN0NTEyNTgzNjE1", milestoneId:"MDk6TWlsZXN0b25lNjA4ODE1MQ==" }) { pullRequest { number } }
     }
   }
   ```

7. Paste and run the mutation in the GraphQL Explorer.

   Recommendation: Run the mutations in batches of about ten at a time to avoid
   timeouts that can cause the request to abort partway through.

[commit-messages]: /Documentation/workflow/commit-messages.md
[docs-guidelines]: https://github.com/MicrosoftDocs/xamarin-docs/blob/live/contributing-guidelines/template.md
[draft-notes]: https://github.com/xamarin/xamarin-android/wiki/Draft-release-notes
[insertion-definition]: https://dev.azure.com/devdiv/DevDiv/_release?view=mine&definitionId=1755
[xe-docs]: https://github.com/MicrosoftDocs/xamarin-engineering-docs-pr/tree/master/docs/android/release-notes.
[toc]: https://github.com/MicrosoftDocs/xamarin-engineering-docs-pr/blob/master/docs/android/release-notes/TOC.md
[index]: https://github.com/MicrosoftDocs/xamarin-engineering-docs-pr/blob/master/docs/android/release-notes/index.md
[docs-write]: https://github.com/orgs/MicrosoftDocs/teams/xamarin-engineering-docs-write
[release-process]: https://github.com/xamarin/monodroid/wiki/Release-Process#release-readiness-and-finalization
[bulk-change]: #how-to-make-bulk-changes-on-pull-requests-and-issues
[graphql-explorer]: https://developer.github.com/v4/explorer/
