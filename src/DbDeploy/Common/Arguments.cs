﻿namespace DbDeploy.Common;

internal static class Arguments
{
    public static readonly Dictionary<string, string> Mapping = new()
    {
        { "--command", "Deploy:Command" },
        { "--startingFile", "Deploy:StartingFile" },
        { "--maxLockWait", "Deploy:MaxLockWaitSeconds" },
        { "--contexts", "Deploy:Contexts" },
        { "--dbProvider", "Deploy:DatabaseProvider" },
        { "--connectionStr", "Deploy:ConnectionString" },
        { "--connectionAttempts", "Deploy:ConnectionAttempts" },
        { "--connectionRetryDelay", "Deploy:ConnectionRetryDelaySeconds" },
        { "--logLevel", "Serilog:MinimumLevel:Default" }
    };
}
