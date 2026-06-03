namespace HouseOfRuns.Frontend;

public static class RunTextFormatter
{
    public static string CompactNotes(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            return "No notes.";
        }

        return notes.StartsWith("Imported from ExportRunHistory", StringComparison.OrdinalIgnoreCase)
            ? "Imported from ExportRunHistory mod."
            : notes;
    }
}
