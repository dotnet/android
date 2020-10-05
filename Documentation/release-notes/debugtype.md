#### Deprecation of DebugType full and pdbonly

```
warning XA0125: 'AndroidApp1.pdb' is using a deprecated debug
information level. Set the debugging information to Portable in the
Visual Studio project property pages or edit the project file in a
text editor and set the 'DebugType' MSBuild property to 'portable' to
use the newer, cross-platform debug information level. If this file
comes from a NuGet package, update to a newer version of the NuGet
package or notify the library author.
```

Support for _.mdb_ or _.pdb_ symbols files that were built with the
`DebugType` MSBuild property set to `full` or `pdbonly` is now
deprecated.  This applies to _.mdb_ and _.pdb_ files in application
projects as well as in referenced libraries, including NuGet packages.

Set `DebugType` to `portable` in the application project as well all
library references.  `portable` is the recommended setting for all
projects from now on.  The older `full` and `pdbonly` settings are for
older Windows-specific file formats.  .NET 6 and higher will not support
those older formats.

In Visual Studio, go to **Properties > Build > Advanced** in the project
property pages and change **Debugging information** to **Portable**.

In Visual Studio for Mac, go to **Build > Compiler > Debug information**
in the project property pages and change **Debug information** to
**Portable**.

If the problematic symbol file comes from a NuGet package, update to a
newer version of the package or notify the library author.
