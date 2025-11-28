namespace Flekt.Computer.Abstractions;

/// <summary>
/// Factory for creating computer instances. Use this in DI scenarios.
/// </summary>
public interface IComputerFactory
{
    /// <summary>
    /// Creates a new computer instance with the specified options.
    /// The computer will be started and ready to use when the task completes.
    /// </summary>
    /// <param name="options">The options for the computer.</param>
    /// <param name="cancelToken">Cancellation token.</param>
    /// <returns>A started computer instance.</returns>
    Task<IComputer> Create(ComputerOptions options, CancellationToken cancelToken = default);
    
    /// <summary>
    /// Creates a new computer instance using a predefined environment.
    /// </summary>
    /// <param name="environmentId">The environment ID to use.</param>
    /// <param name="cancelToken">Cancellation token.</param>
    /// <returns>A started computer instance.</returns>
    Task<IComputer> Create(Guid environmentId, CancellationToken cancelToken = default);
}

