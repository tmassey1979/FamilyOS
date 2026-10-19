namespace FamilyOS.Domain.Common;

public enum FamilyRole
{
    Owner = 0,
    Adult = 1,
    Teen = 2,
    Child = 3
}

public enum FamilyTaskStatus
{
    Created = 0,
    Assigned = 1,
    Accepted = 2,
    InProgress = 3,
    Completed = 4,
    Declined = 5,
    Deferred = 6,
    Reassigned = 7,
    Cancelled = 8,
    Skipped = 9,
    Paused = 10
}

public enum TaskPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Urgent = 3
}

public enum RequestStatus
{
    Draft = 0,
    Submitted = 1,
    PendingApproval = 2,
    UnderReview = 3,
    WaitingForInformation = 4,
    Approved = 5,
    ConditionallyApproved = 6,
    Denied = 7,
    Cancelled = 8,
    Executable = 9,
    InExecution = 10,
    Completed = 11,
    Conditional = 12
}

public enum RequestTypeCode
{
    General = 0,
    Money = 1,
    Purchase = 2,
    Grocery = 3,
    Snack = 4,
    Ride = 5,
    School = 6,
    Medical = 7,
    Household = 8,
    Personal = 9
}

public enum ProcurementItemStatus
{
    Queued = 0,
    InCart = 1,
    Purchased = 2,
    Cancelled = 3,
    Held = 4,
    Deferred = 5,
    NoLongerNeeded = 6,
    Approved = 7
}

public enum CartStatus
{
    Active = 0,
    CheckedOut = 1,
    Abandoned = 2
}

public enum MealSlot
{
    Breakfast = 0,
    Lunch = 1,
    Dinner = 2,
    Snack = 3
}

public enum RecurrenceFrequency
{
    Daily = 0,
    Weekly = 1,
    BiWeekly = 2,
    Monthly = 3
}

public enum NotificationType
{
    Info = 0,
    TaskAssigned = 1,
    RequestSubmitted = 2,
    RequestApproved = 3,
    RequestDenied = 4,
    ConditionPending = 5
}

public enum CalendarEventStatus
{
    Scheduled = 0,
    Cancelled = 1,
    Completed = 2
}

public enum ConditionState
{
    Pending = 0,
    Met = 1,
    Waived = 2
}

public enum ConditionStatus
{
    Pending = 0,
    Satisfied = 1,
    Failed = 2,
    Expired = 3,
    Cancelled = 4
}

public enum ConditionType
{
    Manual = 0,
    TaskComplete = 1,
    TimeWindow = 2,
    Approval = 3,
    Custom = 4
}

public enum ConditionLogic
{
    All = 0,
    Any = 1
}

public enum PolicyDecisionKind
{
    Approve = 0,
    Deny = 1,
    DeferToAssign = 2,
    AskHouseholdAdult = 3
}
