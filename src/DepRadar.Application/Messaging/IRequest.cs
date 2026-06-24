namespace DepRadar.Application.Messaging;

/// <summary>
/// Marker for a request (command or query) that yields a <typeparamref name="TResponse"/>
/// when sent through the <see cref="ISender"/>.
/// </summary>
/// <typeparam name="TResponse">The response type produced by the handler.</typeparam>
public interface IRequest<out TResponse>;
