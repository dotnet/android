# Localization

All new Xamarin.Android MSBuild error or warning messages should be localizable,
so when adding a new message, follow these steps:

 1. Add the new message to
    `src/Xamarin.Android.Build.Tasks/Properties/Resources.resx`.  Use the
    message code as the name of the resource.  For example, for the message text
    associated with code `XA0000`, use `XA0000` as the name:

    ![Managed Resources Editor with XA0000 as the name for a
    resource][resources-editor]

    Be sure to use Visual Studio or Visual Studio for Mac to edit the `.resx`
    file so that the `ResXFileCodeGenerator` tool will run and update the
    corresponding `Resources.Designer.cs` file.

 2. In the call to `LogCodedError()` or `LogCodedWarning()`, reference the
    message string using the generated C# property name like
    `Properties.Resources.XA0000`.

 3. After adding the new message, build `Xamarin.Android.Build.Tasks.csproj`
    locally.  This will run the targets from [dotnet/xliff-tasks][xliff-tasks]
    to update the `.xlf` [XLIFF][xliff] localization files with the latest
    changes from the `.resx` file.

 4. Include the changes to the`.resx` file as well as the generated changes to
    the `Resources.Designer.cs` file and the `.xlf` files in the commit.

## Guidelines

  * If a message code is used in multiple calls to `LogCodedError()` or
    `LogCodedWarning()` that each logs a different message, append short
    descriptive phrases to the end of the code to create additional resource
    names as needed.  For example, you could have names like `XA0000_Files` and
    `XA0000_Directories` for two different strings.

  * To include values of variables in the message, use numbered format items
    like `{0}` and `{1}` rather than string interpolation or string
    concatenation.

    String interpolation won't work because string resources are not subject to
    interpolation.  String concatenation should also be avoided because it means
    the message text will be split across multiple string resources, which makes
    it more complicated to provide appropriate context about the message for the
    translation team.

  * Use the comments field in the `.resx` file to provide additional context to
    the translation team about the message.  For example, for a message that
    includes a format item like `{0}`, it can sometimes be helpful to add a
    comment about what will appear for that item in the final formatted string:

    ```
    {0} - The managed type name
    ```

    For a few more examples, see the dotnet/sdk repo:

    https://github.com/dotnet/sdk/blob/master/src/Tasks/Common/Resources/Strings.resx

[resources-editor]: ../images/resources-editor-xa0000.png
[xliff-tasks]: https://github.com/dotnet/xliff-tasks
[xliff]: http://docs.oasis-open.org/xliff/v1.2/os/xliff-core.html
