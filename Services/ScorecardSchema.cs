namespace SiteReportApp.Services
{
    // ---- Scorecard schema: the single source of truth for the 20 metric sheets ----
    //
    // Generated from the standard "Monthly Site Scorecard" template. Each metric maps
    // to one sheet; each column is either an input (Number/Text/Date) or a Computed
    // column whose Formula references other columns by {key}. The frontend ships an
    // identical definition (scorecardSchema.js) so forms, template and analytics stay
    // in lockstep. Keep the two in sync when adding metrics.
    public enum ScColType { Number, Text, Date, Computed }

    public class ScColumn
    {
        public string Key { get; set; } = "";
        public string Label { get; set; } = "";
        public ScColType Type { get; set; }
        public string? Formula { get; set; }   // only for Computed; refs other cols as {key}
    }

    public class ScMetric
    {
        public string Key { get; set; } = "";
        public string Title { get; set; } = "";
        public string Category { get; set; } = "";
        public string SheetName { get; set; } = "";  // exact tab name in the template/upload
        public bool MultiRow { get; set; }
        public int Order { get; set; }
        public List<ScColumn> Columns { get; set; } = new();
    }

    public static class ScorecardSchema
    {
        public static readonly List<ScMetric> Metrics = new()
        {
            new ScMetric {
                Key = "humanError", Title = "Human Error", Category = "Quality & Compliance",
                SheetName = "Human Error", MultiRow = true, Order = 1,
                Columns = new() {
                    new ScColumn { Key = "relatedEvents", Label = "Related Events", Type = ScColType.Text },
                    new ScColumn { Key = "noOfEventClosedInTimePeriod", Label = "No of Event Closed in Time period", Type = ScColType.Number },
                    new ScColumn { Key = "rootCauseAsHumanErrror", Label = "Root cause as Human Errror", Type = ScColType.Number },
                    new ScColumn { Key = "ofHumanError", Label = "% of Human Error", Type = ScColType.Computed, Formula = "={rootCauseAsHumanErrror}/{noOfEventClosedInTimePeriod}" },
                }
            },
            new ScMetric {
                Key = "oosRate", Title = "OOS Rate (%)", Category = "Quality & Compliance",
                SheetName = "OOS Rate (%)", MultiRow = false, Order = 2,
                Columns = new() {
                    new ScColumn { Key = "noOfBatchSampleAnalyzedInQcArNo", Label = "No of Batch sample analyzed in QC (AR No.)", Type = ScColType.Number },
                    new ScColumn { Key = "noOfInvalidOosClosedInTimePeriod", Label = "No of Invalid OOS Closed in Time period", Type = ScColType.Number },
                    new ScColumn { Key = "noOfValidOosClosedInTimePeriod", Label = "No of valid OOS Closed in Time period", Type = ScColType.Number },
                    new ScColumn { Key = "totalOosClosed", Label = "Total OOS Closed", Type = ScColType.Computed, Formula = "=SUM({noOfInvalidOosClosedInTimePeriod}:{noOfValidOosClosedInTimePeriod})" },
                    new ScColumn { Key = "invalidOos", Label = "% Invalid OOS", Type = ScColType.Computed, Formula = "={noOfInvalidOosClosedInTimePeriod}/{noOfBatchSampleAnalyzedInQcArNo}" },
                    new ScColumn { Key = "validOos", Label = "% valid OOS", Type = ScColType.Computed, Formula = "={noOfValidOosClosedInTimePeriod}/{noOfBatchSampleAnalyzedInQcArNo}" },
                    new ScColumn { Key = "overallOos", Label = "%overall OOS", Type = ScColType.Computed, Formula = "={totalOosClosed}/{noOfBatchSampleAnalyzedInQcArNo}" },
                }
            },
            new ScMetric {
                Key = "deviationRate", Title = "Deviation Rate", Category = "Quality & Compliance",
                SheetName = "Deviation Rate", MultiRow = false, Order = 3,
                Columns = new() {
                    new ScColumn { Key = "noOfBatchesMgfPkg", Label = "No of Batches (Mgf & Pkg)", Type = ScColType.Number },
                    new ScColumn { Key = "totalDeviationReportedInTimePeriod", Label = "Total Deviation reported in time period", Type = ScColType.Number },
                    new ScColumn { Key = "deviation", Label = "% Deviation", Type = ScColType.Computed, Formula = "=SUM({totalDeviationReportedInTimePeriod}/{noOfBatchesMgfPkg})" },
                }
            },
            new ScMetric {
                Key = "lir", Title = "LIR", Category = "Quality & Compliance",
                SheetName = "LIR", MultiRow = false, Order = 4,
                Columns = new() {
                    new ScColumn { Key = "noOfBatchSampleAnalyzedInQcArNo", Label = "No of Batch sample analyzed in QC (AR No.)", Type = ScColType.Number },
                    new ScColumn { Key = "noOfLirReportedAfterSamples", Label = "No of LIR reported After Samples", Type = ScColType.Number },
                    new ScColumn { Key = "noOfLirReportedBeforeSamples", Label = "No of LIR reported Before samples", Type = ScColType.Number },
                    new ScColumn { Key = "totalNoOfLirReportedInTimePeriod", Label = "Total No of LIR reported in time period", Type = ScColType.Computed, Formula = "={noOfLirReportedAfterSamples}+{noOfLirReportedBeforeSamples}" },
                    new ScColumn { Key = "lir", Label = "% LIR", Type = ScColType.Computed, Formula = "=SUM({totalNoOfLirReportedInTimePeriod}/{noOfBatchSampleAnalyzedInQcArNo})" },
                }
            },
            new ScMetric {
                Key = "repetitiveEvents", Title = "Repetitive Events (%)", Category = "Quality & Compliance",
                SheetName = "Repetitive Events (%)", MultiRow = true, Order = 5,
                Columns = new() {
                    new ScColumn { Key = "events", Label = "Events", Type = ScColType.Text },
                    new ScColumn { Key = "totalNoOfEventClosed", Label = "Total no of Event Closed", Type = ScColType.Number },
                    new ScColumn { Key = "noOfRepetativeEventBasedOnProductForRepe", Label = "No of Repetative Event based on product (for repetative nature- follow SOP time period)", Type = ScColType.Number },
                    new ScColumn { Key = "repetitiveBasedOnProduct", Label = "% Repetitive Based on Product", Type = ScColType.Computed, Formula = "={noOfRepetativeEventBasedOnProductForRepe}/{totalNoOfEventClosed}" },
                    new ScColumn { Key = "noOfRepetativeEventBasedOnSimilarRootCau", Label = "No of Repetative Event based on similar Root cause (RCA) in time period (for repetative nature- follow SOP time period)", Type = ScColType.Number },
                    new ScColumn { Key = "repetitiveBasedOnRca", Label = "% Repetitive based on RCA", Type = ScColType.Computed, Formula = "={noOfRepetativeEventBasedOnSimilarRootCau}/{totalNoOfEventClosed}" },
                }
            },
            new ScMetric {
                Key = "hplcOccupancy", Title = "HPLC Occupancy (%)", Category = "Laboratory Performance",
                SheetName = "HPLC Occupancy (%)", MultiRow = false, Order = 6,
                Columns = new() {
                    new ScColumn { Key = "totalNoOfHplcA", Label = "Total no of HPLC (A)", Type = ScColType.Number },
                    new ScColumn { Key = "actualHplcRunHour", Label = "Actual HPLC Run (Hour)", Type = ScColType.Number },
                    new ScColumn { Key = "noOfWorkingDaysAtSiteByConsideringSiteWo", Label = "No of working Days at site (By considering site working for 30 days)", Type = ScColType.Number },
                    new ScColumn { Key = "standardHrsDay", Label = "Standard hrs @ day", Type = ScColType.Number },
                    new ScColumn { Key = "totalStandardTimeInHour3024A", Label = "Total standard Time in (Hour) (30*24*A)", Type = ScColType.Computed, Formula = "={standardHrsDay}*{noOfWorkingDaysAtSiteByConsideringSiteWo}*{totalNoOfHplcA}" },
                    new ScColumn { Key = "hplcOccupancy", Label = "%HPLC Occupancy", Type = ScColType.Computed, Formula = "={actualHplcRunHour}/{totalStandardTimeInHour3024A}" },
                }
            },
            new ScMetric {
                Key = "gcOccupancy", Title = "GC Occupancy (%)", Category = "Laboratory Performance",
                SheetName = "GC Occupancy (%)", MultiRow = false, Order = 7,
                Columns = new() {
                    new ScColumn { Key = "totalNoOfGcA", Label = "Total no of GC (A)", Type = ScColType.Number },
                    new ScColumn { Key = "actualGcRunHour", Label = "Actual GC Run (Hour)", Type = ScColType.Number },
                    new ScColumn { Key = "noOfWorkingDaysAtSiteByConsideringSiteWo", Label = "No of working Days at site (By considering site working for 30 days)", Type = ScColType.Number },
                    new ScColumn { Key = "standardHrsDay", Label = "Standard hrs @ day", Type = ScColType.Number },
                    new ScColumn { Key = "totalStandardTimeInHour3024A", Label = "Total standard Time in (Hour) (30*24*A)", Type = ScColType.Computed, Formula = "={standardHrsDay}*{noOfWorkingDaysAtSiteByConsideringSiteWo}*{totalNoOfGcA}" },
                    new ScColumn { Key = "gcOccupancy", Label = "%GC Occupancy", Type = ScColType.Computed, Formula = "={actualGcRunHour}/{totalStandardTimeInHour3024A}" },
                }
            },
            new ScMetric {
                Key = "analystEfficiency", Title = "Analyst Efficiency", Category = "Laboratory Performance",
                SheetName = "Analyst Efficiency", MultiRow = false, Order = 8,
                Columns = new() {
                    new ScColumn { Key = "totalNoOfSampleReceivedForTesting", Label = "Total no of sample received for testing", Type = ScColType.Number },
                    new ScColumn { Key = "totalNoOfSampleAnalysed", Label = "Total no of sample analysed", Type = ScColType.Number },
                    new ScColumn { Key = "totalNoOfAnalystInLab", Label = "Total no of analyst in LAB", Type = ScColType.Number },
                    new ScColumn { Key = "totalNoOfWorkingDays", Label = "Total no of working days", Type = ScColType.Number },
                    new ScColumn { Key = "analystEfficiency", Label = "% Analyst Efficiency", Type = ScColType.Computed, Formula = "={totalNoOfSampleAnalysed}/({totalNoOfAnalystInLab}*{totalNoOfWorkingDays})" },
                }
            },
            new ScMetric {
                Key = "eventExtensionIndex", Title = "Event Extension Index", Category = "Event & Investigation",
                SheetName = "Event Extension Index", MultiRow = true, Order = 9,
                Columns = new() {
                    new ScColumn { Key = "event", Label = "Event", Type = ScColType.Text },
                    new ScColumn { Key = "totalNoOfEventClose", Label = "Total no of Event Close", Type = ScColType.Number },
                    new ScColumn { Key = "noOfEventCloseWithInTime", Label = "No of Event Close with in time", Type = ScColType.Number },
                    new ScColumn { Key = "noOfEventCloseWithOverTimelines", Label = "No of Event Close with over timelines", Type = ScColType.Computed, Formula = "={totalNoOfEventClose}-{noOfEventCloseWithInTime}" },
                    new ScColumn { Key = "noOfExtensionInEvent", Label = "No of Extension in Event", Type = ScColType.Number },
                    new ScColumn { Key = "closureWithinSop", Label = "Closure Within SOP (%)", Type = ScColType.Computed, Formula = "=({noOfEventCloseWithInTime}/{totalNoOfEventClose})*100" },
                }
            },
            new ScMetric {
                Key = "auditPerformance", Title = "Audit Performance", Category = "Event & Investigation",
                SheetName = "Audit Performance ", MultiRow = true, Order = 10,
                Columns = new() {
                    new ScColumn { Key = "nameOfAuthorityDepartmentAuditors", Label = "Name of Authority/ Department (Auditors)", Type = ScColType.Text },
                    new ScColumn { Key = "typeOfAudit", Label = "Type of Audit", Type = ScColType.Text },
                    new ScColumn { Key = "startDateDdMmmYy", Label = "Start Date DD-MMM-YY", Type = ScColType.Date },
                    new ScColumn { Key = "endDateDdMmmYy", Label = "End date DD-MMM-YY", Type = ScColType.Date },
                    new ScColumn { Key = "reportReceived", Label = "Report Received", Type = ScColType.Text },
                    new ScColumn { Key = "capaComplianceStatus", Label = "CAPA / Compliance Status", Type = ScColType.Text },
                    new ScColumn { Key = "approvalStatus", Label = "Approval Status", Type = ScColType.Text },
                    new ScColumn { Key = "noOfCriticalObservation", Label = "No of critical observation", Type = ScColType.Number },
                    new ScColumn { Key = "noOfMajorObservation", Label = "No of major observation", Type = ScColType.Number },
                    new ScColumn { Key = "noOfMinorObservation", Label = "No of minor observation", Type = ScColType.Number },
                    new ScColumn { Key = "totalNoOfRepeatedObservationNotedByAgenc", Label = "Total no of repeated observation noted by agency/Customer/CQC of that time period", Type = ScColType.Number },
                    new ScColumn { Key = "remarkIfAny", Label = "Remark (If any)", Type = ScColType.Text },
                }
            },
            new ScMetric {
                Key = "marketDepotComplaints", Title = "Market-Depot Complaints", Category = "Market & Product Quality",
                SheetName = "Market-Depot Complaints", MultiRow = false, Order = 11,
                Columns = new() {
                    new ScColumn { Key = "noOfBatchesDispatchedOnSameTimePeriod", Label = "No. of batches dispatched on same time period", Type = ScColType.Number },
                    new ScColumn { Key = "noOfMarketCompliantReceivedForProductQua", Label = "No of Market compliant received for Product quality defects", Type = ScColType.Number },
                    new ScColumn { Key = "ofMarketCompalintLogged", Label = "% of market compalint logged", Type = ScColType.Computed, Formula = "={noOfMarketCompliantReceivedForProductQua}/{noOfBatchesDispatchedOnSameTimePeriod}" },
                }
            },
            new ScMetric {
                Key = "rightFirstTime", Title = "Right-First-Time", Category = "Market & Product Quality",
                SheetName = "Right-First-Time", MultiRow = false, Order = 12,
                Columns = new() {
                    new ScColumn { Key = "noOfLotsTestedInTheReportingTimeframeB", Label = "No. of lots tested in the reporting timeframe (B)", Type = ScColType.Number },
                    new ScColumn { Key = "noOfBatchesLotRejectedInSameTimePeriod", Label = "No. of batches/lot rejected in same time period", Type = ScColType.Number },
                    new ScColumn { Key = "lotAcceptanceRate", Label = "Lot Acceptance Rate (%)", Type = ScColType.Computed, Formula = "=({noOfLotsTestedInTheReportingTimeframeB}-{noOfBatchesLotRejectedInSameTimePeriod})/{noOfLotsTestedInTheReportingTimeframeB}" },
                    new ScColumn { Key = "noOfBatchesLotReleasedWithoutAnySingleEv", Label = "No. of batches/lot released without any single event that OOS/DEV/OOT/LIR in same time period", Type = ScColType.Number },
                    new ScColumn { Key = "ofRightFirstTime", Label = "% of Right first time", Type = ScColType.Computed, Formula = "={noOfBatchesLotReleasedWithoutAnySingleEv}/{noOfLotsTestedInTheReportingTimeframeB}" },
                }
            },
            new ScMetric {
                Key = "training", Title = "Training %", Category = "Governance & Sustainability",
                SheetName = "Training %", MultiRow = false, Order = 13,
                Columns = new() {
                    new ScColumn { Key = "completionOfSopTraining", Label = "% Completion of SOP Training", Type = ScColType.Number },
                    new ScColumn { Key = "completionOfGmpTraining", Label = "% Completion of GMP Training", Type = ScColType.Number },
                    new ScColumn { Key = "completionOfFuntionalTraining", Label = "% Completion of Funtional Training", Type = ScColType.Number },
                    new ScColumn { Key = "noOfExternalTrainingByOem", Label = "No of External training (By OEM)", Type = ScColType.Number },
                    new ScColumn { Key = "nameOfExternalTrainingByAgencySme", Label = "Name of External training (by agency/SME)", Type = ScColType.Text },
                }
            },
            new ScMetric {
                Key = "lotAcceptanceRateSop019", Title = "Lot Acceptance Rate (SOP-019)", Category = "Market & Product Quality",
                SheetName = "Lot Acceptance Rate (SOP-019)", MultiRow = false, Order = 14,
                Columns = new() {
                    new ScColumn { Key = "noOfLotsSaleableTestedInTheReportingTime", Label = "No. of lots (saleable) tested in the reporting timeframe (B)", Type = ScColType.Number },
                    new ScColumn { Key = "noOfBatchesLotRejectedInSameTimePeriod", Label = "No. of batches/lot rejected in same time period", Type = ScColType.Number },
                    new ScColumn { Key = "lotAcceptanceRate", Label = "Lot Acceptance Rate (%)", Type = ScColType.Computed, Formula = "=({noOfLotsSaleableTestedInTheReportingTime}-{noOfBatchesLotRejectedInSameTimePeriod})/{noOfLotsSaleableTestedInTheReportingTime}" },
                }
            },
            new ScMetric {
                Key = "pqcrAsPerSop019", Title = "PQCR (As per SOP-019)", Category = "Market & Product Quality",
                SheetName = "PQCR (As per SOP-019) ", MultiRow = false, Order = 15,
                Columns = new() {
                    new ScColumn { Key = "numberOfCustomerComplaintReceivedForAPro", Label = "Number of Customer Complaint Received for a Product (No. of Batches) in the reporting timeframe", Type = ScColType.Number },
                    new ScColumn { Key = "totalNumberOfBatchesDistributedInTheRepo", Label = "Total number of batches distributed in the reporting timeframe", Type = ScColType.Number },
                    new ScColumn { Key = "acceptanceRate", Label = "%Acceptance Rate", Type = ScColType.Computed, Formula = "={numberOfCustomerComplaintReceivedForAPro}/{totalNumberOfBatchesDistributedInTheRepo}" },
                }
            },
            new ScMetric {
                Key = "ioosrAsPerSop019", Title = "IOOSR (As per SOP_019)", Category = "Market & Product Quality",
                SheetName = "IOOSR (As per SOP_019)", MultiRow = false, Order = 16,
                Columns = new() {
                    new ScColumn { Key = "numberOfInvalidateOosTestResultsInCommer", Label = "Number of invalidate OOS test results (In Commercial Finished Product & Long-term Stability Analysis )", Type = ScColType.Number },
                    new ScColumn { Key = "totalNumberOfOosClosedInCommercialFinish", Label = "Total number of OOS closed (In Commercial Finished Product & Long-term Stability Analysis)", Type = ScColType.Number },
                    new ScColumn { Key = "ioosr", Label = "% IOOSR", Type = ScColType.Computed, Formula = "={numberOfInvalidateOosTestResultsInCommer}/{totalNumberOfOosClosedInCommercialFinish}" },
                }
            },
            new ScMetric {
                Key = "pqrcrAsPerSop019", Title = "PQRCR (As per SOP-019)", Category = "Market & Product Quality",
                SheetName = "PQRCR (As per SOP-019)", MultiRow = false, Order = 17,
                Columns = new() {
                    new ScColumn { Key = "numberOfApqrCompletedInTheReportingTimef", Label = "Number of APQR completed in the reporting timeframe", Type = ScColType.Number },
                    new ScColumn { Key = "numberOfApqrScheduledInTheReportingTimef", Label = "Number of APQR scheduled in the reporting timeframe", Type = ScColType.Number },
                    new ScColumn { Key = "totalCompleton", Label = "Total completon", Type = ScColType.Computed, Formula = "={numberOfApqrCompletedInTheReportingTimef}/{numberOfApqrScheduledInTheReportingTimef}" },
                }
            },
            new ScMetric {
                Key = "timeLineCompliance", Title = "Time Line compliance", Category = "Event & Investigation",
                SheetName = "Time Line compliance", MultiRow = true, Order = 18,
                Columns = new() {
                    new ScColumn { Key = "relatedEvents", Label = "Related Events", Type = ScColType.Text },
                    new ScColumn { Key = "numberOfEventClosureWithInSopDefinedTime", Label = "Number of event closure with in SOP defined timeline", Type = ScColType.Number },
                    new ScColumn { Key = "totalNumberEventClosureDone", Label = "Total number event closure done", Type = ScColType.Number },
                    new ScColumn { Key = "compliance", Label = "%Compliance", Type = ScColType.Computed, Formula = "={numberOfEventClosureWithInSopDefinedTime}/{totalNumberEventClosureDone}" },
                }
            },
            new ScMetric {
                Key = "equipmentQualification", Title = "Equipment Qualification", Category = "Qualification/Validation",
                SheetName = "Equipment Qualification ", MultiRow = true, Order = 19,
                Columns = new() {
                    new ScColumn { Key = "typeOfActivities", Label = "Type of Activities", Type = ScColType.Text },
                    new ScColumn { Key = "productSegment", Label = "Product Segment", Type = ScColType.Text },
                    new ScColumn { Key = "noSActivitiesInitiatedReportingMonth", Label = "No.'s Activities Initiated (reporting month)", Type = ScColType.Text },
                    new ScColumn { Key = "carryForwardFromPreviousMonthS", Label = "Carry Forward from Previous Month(s)", Type = ScColType.Text },
                    new ScColumn { Key = "activitiesCompleted", Label = "Activities Completed", Type = ScColType.Text },
                    new ScColumn { Key = "activitiesUProgress", Label = "Activities U/Progress", Type = ScColType.Text },
                    new ScColumn { Key = "remarks", Label = "Remarks", Type = ScColType.Text },
                }
            },
            new ScMetric {
                Key = "manPowerStatus", Title = "Man Power status", Category = "Manpower",
                SheetName = "Man Power status", MultiRow = true, Order = 20,
                Columns = new() {
                    new ScColumn { Key = "department", Label = "Department", Type = ScColType.Text },
                    new ScColumn { Key = "budgetedManpower", Label = "Budgeted manpower", Type = ScColType.Number },
                    new ScColumn { Key = "existingManpowerAsOnDate", Label = "Existing Manpower as on Date", Type = ScColType.Number },
                    new ScColumn { Key = "underResignation", Label = "Under Resignation", Type = ScColType.Number },
                    new ScColumn { Key = "leavedOrganization", Label = "Leaved organization", Type = ScColType.Number },
                    new ScColumn { Key = "newJoineeStatus", Label = "New Joinee Status", Type = ScColType.Number },
                    new ScColumn { Key = "remarks", Label = "Remarks", Type = ScColType.Text },
                }
            },
        };

        public static ScMetric? Find(string key) =>
            Metrics.FirstOrDefault(x => string.Equals(x.Key, key, System.StringComparison.OrdinalIgnoreCase));

        public static ScMetric? FindBySheet(string sheetName)
        {
            var n = sheetName.Trim();
            return Metrics.FirstOrDefault(x =>
                string.Equals(x.SheetName.Trim(), n, System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.Title.Trim(), n, System.StringComparison.OrdinalIgnoreCase));
        }
    }
}
