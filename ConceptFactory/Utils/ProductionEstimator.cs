using System;

namespace ConceptFactory.Utils
{
    // Turns an order's total custom-item quantity into an estimated
    // number of working days (Mon–Fri) production will take, and from
    // that a target completion date. Used by BillingController.bApprove
    // to auto-fill Order.ProductionTargetDate the moment an order is
    // confirmed, so admins get a sensible default instead of a blank
    // date — they can still override it manually from the Production
    // Overview table (ProductionController.pSetTargetDate).
    //
    // Tiers (given): up to 20 pcs -> 3 working days, up to 50 -> 5,
    // up to 100 -> 7. Beyond 100, every extra 50 pcs (or part of) adds
    // 2 more working days, continuing the same rate the 50->100 tier
    // already implies.
    public static class ProductionEstimator
    {
        public static int EstimateWorkingDays(int quantity)
        {
            if (quantity <= 0) return 0;
            if (quantity <= 20) return 3;
            if (quantity <= 50) return 5;
            if (quantity <= 100) return 7;

            int extraTiers = (int)Math.Ceiling((quantity - 100) / 50.0);
            return 7 + (extraTiers * 2);
        }

        // Walks forward from "start", skipping Saturdays/Sundays, until
        // the required number of working days have been counted.
        public static DateTime EstimateTargetDate(DateTime start, int quantity)
        {
            int daysNeeded = EstimateWorkingDays(quantity);
            DateTime date = start.Date;
            int counted = 0;

            while (counted < daysNeeded)
            {
                date = date.AddDays(1);
                if (date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday)
                    counted++;
            }

            return date;
        }
    }
}
