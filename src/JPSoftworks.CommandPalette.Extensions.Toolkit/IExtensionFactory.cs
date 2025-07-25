// ------------------------------------------------------------
// 
// Copyright (c) Jiří Polášek. All rights reserved.
// 
// ------------------------------------------------------------

using Microsoft.CommandPalette.Extensions;

namespace JPSoftworks.CommandPalette.Extensions.Toolkit;

/// <summary>
/// Defines a factory pattern for creating Command Palette extension instances.
/// </summary>
/// <remarks>
/// <para>
/// This interface enables the creation of <see cref="IExtension"/> instances with proper lifecycle management
/// integration through the provided disposal event mechanism. Implementations should create extensions that
/// are ready to be registered with a COM server and can signal their disposal through the event parameter.
/// </para>
/// <para>
/// The factory pattern allows for flexible extension instantiation strategies, including singleton patterns,
/// per-request creation, or delegate-based factories that wrap custom creation logic.
/// </para>
/// </remarks>
public interface IExtensionFactory
{
    /// <summary>
    /// Creates a new Command Palette extension instance.
    /// </summary>
    /// <param name="extensionDisposedEvent">
    /// A <see cref="ManualResetEvent"/> that the created extension should signal when it is disposed.
    /// This event is used by the hosting infrastructure to detect when the extension has completed
    /// its cleanup and can be safely removed from the COM server. The extension is responsible
    /// for calling <see cref="EventWaitHandle.Set"/> on this event during its disposal process.
    /// </param>
    /// <returns>
    /// A new <see cref="IExtension"/> instance that implements the Command Palette extension contract.
    /// The returned extension should be fully initialized and ready for registration with the COM server.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="extensionDisposedEvent"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    /// <para>
    /// The created extension must properly handle the disposal event to ensure graceful shutdown
    /// of the hosting process. Failure to signal the disposal event may result in the server
    /// process not terminating correctly.
    /// </para>
    /// <para>
    /// Implementations should ensure that the created extension is thread-safe if it will be
    /// accessed from multiple threads, as COM server environments may invoke extension methods
    /// from different threading contexts.
    /// </para>
    /// </remarks>
    IExtension CreateExtension(ManualResetEvent extensionDisposedEvent);
}