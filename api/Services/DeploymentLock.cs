using System.Collections.Concurrent;

namespace BDeployer.Api.Services;

public sealed class DeploymentLock
{
    private readonly ConcurrentDictionary<Guid, byte> _running = new();

    public bool TryAcquire(Guid environmentId) => _running.TryAdd(environmentId, 0);

    public void Release(Guid environmentId) => _running.TryRemove(environmentId, out _);
}

public sealed class DeploymentAlreadyRunningException(string message) : Exception(message);
