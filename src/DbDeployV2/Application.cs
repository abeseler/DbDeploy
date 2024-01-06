using DbDeployV2.Common;

namespace DbDeployV2;

internal sealed class Application
{
    public async Task<Result<Success, Error>> RunAsync()
    {
        await Task.CompletedTask;

        return Success.Default;
    }
}
