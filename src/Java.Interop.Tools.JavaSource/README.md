# Java.Interop.Tools.JavaSource

Utilities for processing Java source code.

## SourceJavadocToXmldocGrammar & SourceJavadocToXmldocParser

`SourceJavadocToXmldocParser` parses Javadoc comments, as found in
`java-source-utils.jar` output (commit 69e1b80a), and converts it into
C# /doc XML via the Irony `SourceJavadocToXmldocGrammar` grammar.

Multiple Javadoc+HTML language constructs are not yet supported:

  * Member lookup:
    `@see #hashCode` is currently translated into
    `<seealso cref="#hashCode" />`; no translation is performed.
    This should be turned into
    `<seealso cref="M:Java.Lang.Object.GetHashCode" />`, but requires
    additional plumbing so that `SourceJavadocToXmldocGrammar` can "know"
    about all the possible types & members, and how to map them to C# names.

  * The following HTML elements need to be (better) supported:
    `ul`, `li`.
