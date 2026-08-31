namespace ConceptFactory.Utils
{
    // Single source of truth for the 6 production stations (index 0-5),
    // matching ProductionWorkflow's stage order: Cutting, Printing, Sewing,
    // Trimming, Quality Check, Ready for Pickup. Previously duplicated as
    // private arrays inside ProductionController — pulled out here so
    // User.StationIndex (Utils/User.cs), Notification.StationIndex, and
    // the admin sidebar (_AdminLayout.cshtml) can all resolve the same
    // names/keys without redefining them.
    public static class ProductionStations
    {
        public static readonly string[] Names =
        {
            "Cutting", "Printing", "Sewing", "Trimming", "Quality Check", "Ready for Pickup"
        };

        // URL-safe keys — used for asp-route-stage / ViewData["ActiveStation"]
        // matching (no spaces, matches pStation's route param expectations).
        public static readonly string[] Keys =
        {
            "Cutting", "Printing", "Sewing", "Trimming", "QualityCheck", "ReadyForPickup"
        };

        public const int ReadyForPickupIndex = 5;

        public static string? NameFor(int? index) =>
            index.HasValue && index.Value >= 0 && index.Value < Names.Length ? Names[index.Value] : null;

        public static string? KeyFor(int? index) =>
            index.HasValue && index.Value >= 0 && index.Value < Keys.Length ? Keys[index.Value] : null;

        // (index, name) pairs for populating a <select> — used by the
        // Users Add/Edit Staff forms when Department == "Production".
        public static IEnumerable<(int Index, string Name)> Options()
        {
            for (int i = 0; i < Names.Length; i++)
                yield return (i, Names[i]);
        }
    }
}
