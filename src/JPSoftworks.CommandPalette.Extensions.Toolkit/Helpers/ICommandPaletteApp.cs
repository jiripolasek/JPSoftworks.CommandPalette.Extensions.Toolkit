// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

namespace JPSoftworks.CommandPalette.Extensions.Toolkit.Helpers;

internal interface ICommandPaletteApp
{
    Task<bool> LaunchAsync();
}