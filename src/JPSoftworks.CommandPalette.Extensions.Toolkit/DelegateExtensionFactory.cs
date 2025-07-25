// ------------------------------------------------------------
// 
// Copyright (c) Jiří Polášek. All rights reserved.
// 
// ------------------------------------------------------------

using Microsoft.CommandPalette.Extensions;

namespace JPSoftworks.CommandPalette.Extensions.Toolkit;

/// <summary>
/// A delegate-based implementation of <see cref="IExtensionFactory"/> that uses a provided function
/// to create Command Palette extension instances.
/// </summary>
/// <remarks>
/// <para>
/// This factory implementation provides a flexible way to create extensions by wrapping a delegate
/// function that contains the actual extension creation logic. This approach is particularly useful
/// when you need to create extensions with custom initialization parameters, dependency injection,
/// or complex construction logic without implementing a full factory class.
/// </para>
/// <para>
/// The delegate-based approach allows for easy integration with lambda expressions, method groups,
/// or any callable that matches the <see cref="Func{T, TResult}"/> signature where T is 
/// <see cref="ManualResetEvent"/> and TResult is <see cref="IExtension"/>.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Using a lambda expression
/// var factory = new DelegateExtensionFactory(disposalEvent => 
///     new MyCustomExtension(disposalEvent, someParameter));
/// 
/// // Using a method group
/// IExtension CreateMyExtension(ManualResetEvent disposalEvent) => new MyExtension(disposalEvent);
/// var factory = new DelegateExtensionFactory(CreateMyExtension);
/// </code>
/// </example>
public sealed class DelegateExtensionFactory : IExtensionFactory
{
    private readonly Func<ManualResetEvent, IExtension> _createExtension;

    /// <summary>
    /// Initializes a new instance of the <see cref="DelegateExtensionFactory"/> class with the specified creation delegate.
    /// </summary>
    /// <param name="createExtension">
    /// A function that creates extension instances. The function receives a <see cref="ManualResetEvent"/>
    /// that the created extension should signal when disposed, and returns a fully initialized
    /// <see cref="IExtension"/> instance ready for COM server registration.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="createExtension"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    /// <para>
    /// The provided delegate should create extensions that properly handle the disposal event
    /// mechanism by calling <see cref="EventWaitHandle.Set"/> on the provided event when the
    /// extension is disposed. This ensures proper lifecycle management within the hosting infrastructure.
    /// </para>
    /// <para>
    /// The delegate may be called multiple times if the factory is used to create multiple
    /// extension instances, so ensure that the creation logic is stateless or properly handles
    /// multiple invocations.
    /// </para>
    /// </remarks>
    public DelegateExtensionFactory(Func<ManualResetEvent, IExtension> createExtension)
    {
        ArgumentNullException.ThrowIfNull(createExtension, nameof(createExtension));

        this._createExtension = createExtension;
    }

    /// <inheritdoc />
    /// <remarks>
    /// This implementation delegates the extension creation to the function provided in the constructor.
    /// The disposal event is passed through to the delegate function, which is responsible for
    /// creating an extension that properly handles the lifecycle management requirements.
    /// </remarks>
    public IExtension CreateExtension(ManualResetEvent extensionDisposedEvent)
    {
        return this._createExtension(extensionDisposedEvent);
    }
}