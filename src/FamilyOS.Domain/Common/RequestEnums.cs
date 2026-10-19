namespace FamilyOS.Domain.Common;

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
