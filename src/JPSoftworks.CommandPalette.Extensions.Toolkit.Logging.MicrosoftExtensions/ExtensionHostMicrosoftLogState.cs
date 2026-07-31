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
    private readonly bool _includeCategoryInMessage;

    public ExtensionHostMicrosoftLogState(
        string category,
        string message,
        bool includeCategoryInMessage)
    {
        this.Category = category;
        this.Message = message;
        this._includeCategoryInMessage = includeCategoryInMessage;
    }

    public string Category { get; }

    public string Message { get; }

    public int Count => 3;

    public KeyValuePair<string, object?> this[int index] => index switch
    {
        0 => new KeyValuePair<string, object?>("ExtensionHostCategory", this.Category),
        1 => new KeyValuePair<string, object?>("ExtensionHostMessage", this.Message),
        2 => new KeyValuePair<string, object?>(
            "{OriginalFormat}",
            this._includeCategoryInMessage
                ? "{ExtensionHostCategory}: {ExtensionHostMessage}"
                : "{ExtensionHostMessage}"),
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
        return this._includeCategoryInMessage
            ? string.Format(CultureInfo.InvariantCulture, "{0}: {1}", this.Category, this.Message)
            : this.Message;
    }
}
