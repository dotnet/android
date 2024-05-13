# Localization

All new .NET for Android MSBuild error or warning messages should be localizable,
so when adding a new message, follow these steps:

 1. Add the new message to
    `src/Xamarin.Android.Build.Tasks/Properties/Resources.resx`.  Use the error
    or warning code as the resource name.  For example, for `XA0000`, use
    `XA0000` as the name:

    ![Managed Resources Editor with XA0000 as the name for a
    resource][resources-editor]

 2. If using Visual Studio or Visual Studio for Mac, skip to the next step.

    If using an editor that does not automatically run design-time builds for
    MSBuild targets specified via `%(Generator)` MSBuild item metadata,
    explicitly build the project to update the generated properties.

 3. Use the generated C# property for the resource in the `LogCodedError()` and
    `LogCodedWarning()` calls:

    ```csharp
    Log.LogCodedError ("XA0000", Properties.Resources.XA0000);
    ```

    Or, to log a message directly from an MSBuild target, pass the name of the
    resource to the `ResourceName` parameter of the `<AndroidError/>` or
    `<AndroidWarning/>` task instead:

    ```xml
    <AndroidError Code="XA0000" ResourceName="XA0000" />
    ```

 4. After adding the new message, build `Xamarin.Android.Build.Tasks.csproj`
    locally.

 5. Include the changes to the`.resx` file in the commit.

 6. The [OneLocBuild][oneloc] task will manage handoff and handback for string translations.

### Templates

All updates to `src/Microsoft.Android.Templates` should be built locally to update the
`templatestrings.*.json` used for localization.  The [OneLocBuild][oneloc] task
will manage handoff and handback for string translations after the
`templatestrings.*.json` changes are committed.

## Guidelines

  * When an error or warning code is used with more than one output string, use
    semantically meaningful suffixes to distinguish the resource names.  As a
    made-up example:

    ```xml
    <data name="XA0000_Files" xml:space="preserve">
      <value>Invalid files.</value>
    </data>
    <data name="XA0000_Directories" xml:space="preserve">
      <value>Invalid directories.</value>
    </data>
    ```

  * To include values of variables in the message, use numbered format items
    like `{0}` and `{1}` rather than string interpolation or string
    concatenation.

    The `.resx` infrastructure does not interoperate with C# 6 string
    interpolation.

    String concatenation should also be avoided because it means splitting up
    the message across multiple string resources, which makes it more
    complicated to provide appropriate context to the translators.

  * Use the comments field in the `.resx` file to provide additional context to
    the translators.  For example, if a format item like `{0}` needs additional
    explanation, add a comment:

    ```
    {0} - The managed type name
    ```

    For a few more examples, see the dotnet/sdk repo:

    https://github.com/dotnet/sdk/blob/master/src/Tasks/Common/Resources/Strings.resx

[resources-editor]: ../images/resources-editor-xa0000.png
[oneloc]: https://aka.ms/onelocbuild
