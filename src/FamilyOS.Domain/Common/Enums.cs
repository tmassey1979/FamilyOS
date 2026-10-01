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
    Skipped = 8,
    Cancelled = 9,
    Overdue = 10,
    Paused = 11
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
    WaitingForInformation = 2,
    UnderReview = 3,
    ConditionallyApproved = 4,
    Approved = 5,
    Denied = 6,
    Cancelled = 7,
    Executable = 8,
    InExecution = 9,
    Completed = 10
}

public enum RequestTypeCode
{
    Ride = 0,
    Money = 1,
    Purchase = 2,
    Grocery = 3,
    SnackFood = 4,
    School = 5,
    Medical = 6,
    Household = 7,
    Personal = 8,
    General = 9
}

public enum QuestionType
{
    Text = 0,
    Number = 1,
    Date = 2,
    Time = 3,
    DateTime = 4,
    YesNo = 5,
    SingleChoice = 6,
    MultipleChoice = 7,
    PersonSelection = 8,
    Location = 9,
    MoneyAmount = 10
}

public enum ConditionType
{
    Information = 0,
    Deadline = 1,
    Event = 2,
    Task = 3,
    Financial = 4,
    Research = 5,
    Approval = 6,
    Availability = 7,
    Custom = 8
}

public enum ConditionStatus
{
    Pending = 0,
    Satisfied = 1,
    Failed = 2,
    Expired = 3,
    Cancelled = 4
}

public enum ConditionLogic
{
    All = 0,
    Any = 1
}

public enum ApprovalMode
{
    AlwaysRequire = 0,
    NeverRequire = 1,
    PolicyDetermined = 2
}

public enum ApprovalDecision
{
    Pending = 0,
    Granted = 1,
    Denied = 2,
    Modified = 3,
    Overridden = 4
}

public enum ExecutionItemType
{
    CalendarEvent = 0,
    Task = 1,
    Notification = 2,
    ProcurementItem = 3,
    MoneyTransaction = 4,
    FollowUp = 5,
    Question = 6
}

public enum ExecutionPlanStatus
{
    Draft = 0,
    Proposed = 1,
    Committed = 2,
    InProgress = 3,
    Completed = 4,
    Cancelled = 5
}

public enum ProcurementItemStatus
{
    Approved = 0,
    Queued = 1,
    InCart = 2,
    Held = 3,
    Deferred = 4,
    Purchased = 5,
    Cancelled = 6,
    NoLongerNeeded = 7
}

public enum NotificationType
{
    TaskAssigned = 0,
    TaskAccepted = 1,
    TaskDeclined = 2,
    TaskReassigned = 3,
    RequestSubmitted = 4,
    RequestQuestionAsked = 5,
    RequestApproved = 6,
    RequestDenied = 7,
    ConditionSatisfied = 8,
    ConditionFailed = 9,
    ExecutionReady = 10,
    ProcurementChanged = 11,
    CalendarReminder = 12,
    General = 13
}

public enum RecurrenceFrequency
{
    None = 0,
    Daily = 1,
    Weekly = 2,
    BiWeekly = 3,
    Monthly = 4,
    Yearly = 5,
    Custom = 6
}

public enum DeclinePolicy
{
    NoChange = 0,
    OfferReassignment = 1,
    AutomaticallyReassign = 2,
    AskHouseholdAdult = 3
}
