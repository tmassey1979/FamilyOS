using FamilyOS.Domain.Common;

namespace FamilyOS.Domain.Calendar;

public class CalendarEvent : Entity
{
    public Guid FamilyId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public DateTime StartUtc { get; private set; }
    public DateTime? EndUtc { get; private set; }
    public bool AllDay { get; private set; }
    public string? Location { get; private set; }
    public Guid? LinkedTaskId { get; private set; }
    public Guid? LinkedRequestId { get; private set; }
    public Guid CreatedByMemberId { get; private set; }
    public bool IsCancelled { get; private set; }

    private CalendarEvent() { }

    public static CalendarEvent Create(
        Guid familyId,
        string title,
        DateTime startUtc,
        Guid createdByMemberId,
        DateTime? endUtc = null,
        bool allDay = false,
        string? location = null,
        Guid? linkedTaskId = null,
        Guid? linkedRequestId = null)
    {
        return new CalendarEvent
        {
            FamilyId = familyId,
            Title = title.Trim(),
            StartUtc = startUtc,
            EndUtc = endUtc,
            AllDay = allDay,
            Location = location,
            CreatedByMemberId = createdByMemberId,
            LinkedTaskId = linkedTaskId,
            LinkedRequestId = linkedRequestId
        };
    }

    public void Update(string title, DateTime startUtc, DateTime? endUtc, bool allDay, string? location)
    {
        Title = title.Trim();
        StartUtc = startUtc;
        EndUtc = endUtc;
        AllDay = allDay;
        Location = location;
        MarkUpdated();
    }

    public void Cancel()
    {
        IsCancelled = true;
        MarkUpdated();
    }

    public void LinkTask(Guid taskId)
    {
        LinkedTaskId = taskId;
        MarkUpdated();
    }
}
