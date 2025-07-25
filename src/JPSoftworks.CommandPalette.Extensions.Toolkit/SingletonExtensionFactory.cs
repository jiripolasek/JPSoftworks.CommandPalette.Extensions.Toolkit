// ------------------------------------------------------------
// 
// Copyright (c) Jiří Polášek. All rights reserved.
// 
// ------------------------------------------------------------

using System.Runtime.Versioning;
using Microsoft.CommandPalette.Extensions;
using Shmuelie.WinRTServer;

namespace JPSoftworks.CommandPalette.Extensions.Toolkit;

/// <summary>
/// Factory that always returns the same <see cref="IExtension"/> instance.
/// </summary>
[SupportedOSPlatform("windows6.0.6000")]
internal sealed class SingletonExtensionFactory : BaseClassFactory
{
    private readonly IExtension _instance;
    private readonly Guid _clsid;

    public SingletonExtensionFactory(IExtension instance)
    {
        this._instance = instance ?? throw new ArgumentNullException(nameof(instance));
        this._clsid = instance.GetType().GUID;
    }

    /* ------------------------------------------------------------------ */
    /*  BaseClassFactory implementation                                   */
    /* ------------------------------------------------------------------ */

    protected override Guid Clsid => this._clsid;

    protected override Guid Iid => typeof(IExtension).GUID;

    protected override object CreateInstance() => this._instance;
}