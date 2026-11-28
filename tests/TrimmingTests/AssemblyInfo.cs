using Microsoft.VisualStudio.TestTools.UnitTesting;

// Case builds are heavy (full trimming, NativeAOT), so cap how many run at once.
[assembly: Parallelize (Workers = 2, Scope = ExecutionScope.MethodLevel)]
