using System;

internal static class StartupHook
{
    public static bool IsInitialized { get; private set; }

    public static void Initialize ()
    {
        Console.WriteLine ("StartupHook.Initialize() called");

        IsInitialized = true;
    }
}
