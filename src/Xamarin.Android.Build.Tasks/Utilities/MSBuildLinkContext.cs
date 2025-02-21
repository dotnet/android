using Java.Interop.Tools.Cecil;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Mono.Cecil;
using Mono.Linker;

namespace Xamarin.Android.Tasks;

class MSBuildLinkContext : LinkContext
{
    readonly TaskLoggingHelper logger;

    public MSBuildLinkContext (DirectoryAssemblyResolver resolver, TaskLoggingHelper logger)
        : base (resolver)
    {
        this.logger = logger;
    }

    public override void LogMessage (string message) => logger.LogDebugMessage (message);

    public override void LogWarning (string code, string message) => logger.LogCodedWarning (code, message);

    public override void LogError (string code, string message) => logger.LogCodedError (code, message);
}
