// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

using System.Collections;
using System.Globalization;

namespace JPSoftworks.CommandPalette.Extensions.Toolkit.Logging.MicrosoftExtensions;

internal sealed class ExtensionHostMicrosoftLogState : IReadOnlyList<KeyValuePair<string, object?>>
{
    private const string OriginalFormat = "{ExtensionHostCategory}: {ExtensionHostMessage}";

    public ExtensionHostMicrosoftLogState(string category, string message)
    {
        this.Category = category;
        this.Message = message;
    }

    public string Category { get; }

    public string Message { get; }

    public int Count => 3;

    public KeyValuePair<string, object?> this[int index] => index switch
    {
        0 => new KeyValuePair<string, object?>("ExtensionHostCategory", this.Category),
        1 => new KeyValuePair<string, object?>("ExtensionHostMessage", this.Message),
        2 => new KeyValuePair<string, object?>("{OriginalFormat}", OriginalFormat),
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };

    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
    {
        for (var index = 0; index < this.Count; index++)
        {
            yield return this[index];
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return this.GetEnumerator();
    }

    public override string ToString()
    {
        return string.Format(CultureInfo.InvariantCulture, "{0}: {1}", this.Category, this.Message);
    }
}