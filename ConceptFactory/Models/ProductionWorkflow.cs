namespace ConceptFactory.Models
{
    // Centralizes how advancing the production pipeline affects an
    // Order's overall Status. Used by ProductionController only — Sales/
    // Admin (OrdersController) never touches ProductionStage. Because
    // both controllers read/write the same Order row, anything changed
    // here shows up immediately in Order Management with no extra wiring.
    public static class ProductionWorkflow
    {
        // Index -1=Waiting for Production 0=Cutting, 1=Printing, 2=Sewing, 3=Trimming,
        // 4=Quality Check, 5=Ready for Pickup.
        public const int TotalStages = 6;
        public const int ReadyForPickupIndex = TotalStages - 1;

        // Sets (or advances/reverts) the production stage and keeps
        // Status in lockstep with it.   
        public static void SetStage(Order order, int stage)
        {
            stage = Math.Clamp(stage, 0, TotalStages - 1);
            order.ProductionStage = stage;
            order.ProductionStageUpdatedAt = DateTime.Now;
            // Progress typed in at the previous station doesn't carry
            // over — reset both the per-item counts (the real source of
            // truth) and the cached order-level total (kept only as a
            // cheap "any progress at all" flag for the station list's
            // stat-card queries; see ProductionController.pStation).
            order.ProductionStageQuantityDone = 0;
            foreach (var detail in order.OrderDetails)
            {
                detail.ProductionQuantityDone = 0;
            }

            // Any real production stage means the order is already being produced.
            if (stage < ReadyForPickupIndex)
            {
                order.Status = "In Production";
            }
            else
            {
                order.Status = "Ready for Pickup";
            }
        }

        // "Ready for Pickup" -> "Completed" — customer has received the order.
        public static void MarkCompleted(Order order)
        {
            order.Status = "Completed";
            order.ProductionStageUpdatedAt = DateTime.Now;
        }
    }
}
