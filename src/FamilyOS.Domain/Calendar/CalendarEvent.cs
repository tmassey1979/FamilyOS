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

    /// <summary>External provider key, e.g. "google". Null for FamilyOS-native events.</summary>
    public string? ExternalProvider { get; private set; }
    /// <summary>Provider event id (Google event id).</summary>
    public string? ExternalEventId { get; private set; }
    /// <summary>Provider calendar id (Google calendar id, often "primary").</summary>
    public string? ExternalCalendarId { get; private set; }

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
        Guid? linkedRequestId = null,
        string? description = null)
    {
        return new CalendarEvent
        {
            FamilyId = familyId,
            Title = title.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            StartUtc = startUtc,
            EndUtc = endUtc,
            AllDay = allDay,
            Location = location,
            CreatedByMemberId = createdByMemberId,
            LinkedTaskId = linkedTaskId,
            LinkedRequestId = linkedRequestId
        };
    }

    /// <summary>Create from an external calendar provider (pull sync).</summary>
    public static CalendarEvent CreateFromExternal(
        Guid familyId,
        Guid ownerMemberId,
        string provider,
        string externalEventId,
        string? externalCalendarId,
        string title,
        DateTime startUtc,
        DateTime? endUtc,
        bool allDay,
        string? location,
        string? description)
    {
        if (string.IsNullOrWhiteSpace(provider))
            throw new ArgumentException("Provider required.", nameof(provider));
        if (string.IsNullOrWhiteSpace(externalEventId))
            throw new ArgumentException("External event id required.", nameof(externalEventId));

        var ev = Create(familyId, title, startUtc, ownerMemberId, endUtc, allDay, location, description: description);
        ev.ExternalProvider = provider.Trim().ToLowerInvariant();
        ev.ExternalEventId = externalEventId.Trim();
        ev.ExternalCalendarId = string.IsNullOrWhiteSpace(externalCalendarId) ? null : externalCalendarId.Trim();
        return ev;
    }

    public void Update(string title, DateTime startUtc, DateTime? endUtc, bool allDay, string? location, string? description = null)
    {
        Title = title.Trim();
        StartUtc = startUtc;
        EndUtc = endUtc;
        AllDay = allDay;
        Location = location;
        if (description is not null)
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        MarkUpdated();
    }

    public void ApplyExternalUpdate(
        string title,
        DateTime startUtc,
        DateTime? endUtc,
        bool allDay,
        string? location,
        string? description)
    {
        Update(title, startUtc, endUtc, allDay, location, description);
        if (IsCancelled)
        {
            IsCancelled = false;
            MarkUpdated();
        }
    }

    public void LinkExternal(string provider, string externalEventId, string? externalCalendarId = null)
    {
        ExternalProvider = provider.Trim().ToLowerInvariant();
        ExternalEventId = externalEventId.Trim();
        ExternalCalendarId = string.IsNullOrWhiteSpace(externalCalendarId) ? null : externalCalendarId.Trim();
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
