namespace UpdateManifestArchiver;

public static class TimeSpanExtensions {

    /// <summary>
    /// Quick and dirty, using standard periods. For more correctness, use NodaTime.
    /// </summary>
    /// <param name="t">positive for past, negative for future (now - date)</param>
    /// <returns>a string like "1 year ago" or "in 2 months", or <c>null</c> if <paramref name="t"/> is 0 milliseconds</returns>
    public static string? humanize(this TimeSpan t) {
        (double quantity, string? unit) = t.Duration() switch {
            { TotalDays: >= 365 }      => (t.TotalDays / 365, "year"),
            { TotalDays: >= 30 }       => (t.TotalDays / 30, "month"),
            { TotalDays: >= 1 }        => (t.TotalDays, "day"),
            { TotalHours: >= 1 }       => (t.TotalHours, "hour"),
            { TotalMinutes: >= 1 }     => (t.TotalMinutes, "minute"),
            { TotalSeconds: >= 1 }     => (t.TotalSeconds, "second"),
            { TotalMilliseconds: > 0 } => (t.TotalMilliseconds, "millisecond"),
            _                          => (0, null) // return null
        };

        int intQuantity = (int) Math.Abs(Math.Round(quantity, 0, MidpointRounding.ToZero));
        return unit == null ? null : $"{(quantity < 0 ? "in " : string.Empty)}{intQuantity:N0} {unit}{(intQuantity == 1 ? string.Empty : "s")}{(quantity >= 0 ? " ago" : string.Empty)}";
    }

}