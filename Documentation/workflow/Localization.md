# Localization

All new Xamarin.Android MSBuild error or warning messages should be localizable,
so when adding a new message, follow these steps:

 1. Add the new message to
    `src/Xamarin.Android.Build.Tasks/Properties/Resources.resx`.  Use the error
    or warning code as the resource name.  For example, for `XA0000`, use
    `XA0000` as the name:

    ![Managed Resources Editor with XA0000 as the name for a
    resource][resources-editor]

    Be sure to use Visual Studio or Visual Studio for Mac to edit the `.resx`
    file so that the `ResXFileCodeGenerator` tool will run and update the
    corresponding `Resources.Designer.cs` file.

 2. Use the generated property from `Resources.Designer.cs` in the
    `LogCodedError()` and `LogCodedWarning()` calls:

    ```csharp
    Log.LogCodedError ("XA0000", Properties.Resources.XA0000);
    ```

 3. After adding the new message, build `Xamarin.Android.Build.Tasks.csproj`
    locally.  This will run the targets from [dotnet/xliff-tasks][xliff-tasks]
    to update the `.xlf` [XLIFF][xliff] localization files with the latest
    changes from the `.resx` file.

 4. Include the changes to the`.resx` file as well as the generated changes to
    the `Resources.Designer.cs` file and the `.xlf` files in the commit.

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
[xliff-tasks]: https://github.com/dotnet/xliff-tasks
[xliff]: http://docs.oasis-open.org/xliff/v1.2/os/xliff-core.html
