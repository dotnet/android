#nullable enable
using Mono.Linker;

namespace Xamarin.Android.Tasks;

public class MSBuildLinkContext : LinkContext
{
    public new DirectoryAssemblyResolver Resolver { get; private set; }
    readonly TaskLoggingHelper logger;

    public MSBuildLinkContext (DirectoryAssemblyResolver resolver, TaskLoggingHelper logger)
        : base (resolver)
    {
        Resolver = resolver;
        this.logger = logger;
    }

    public override void LogMessage (string message) => logger.LogDebugMessage (message);

    public override void LogWarning (string code, string message) => logger.LogCodedWarning (code, message);

    public override void LogError (string code, string message) => logger.LogCodedError (code, message);
}
