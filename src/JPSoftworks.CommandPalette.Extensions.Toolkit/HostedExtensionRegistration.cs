// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

namespace JPSoftworks.CommandPalette.Extensions.Toolkit;

internal sealed record HostedExtensionRegistration(IHostedExtensionFactory? Factory, Guid? ClassId = null);