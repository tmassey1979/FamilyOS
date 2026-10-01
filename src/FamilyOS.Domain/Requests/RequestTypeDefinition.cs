using FamilyOS.Domain.Common;
using System.Text.Json;

namespace FamilyOS.Domain.Requests;

/// <summary>
/// Data-driven request type with dynamic question definitions.
/// </summary>
public class RequestTypeDefinition : Entity
{
    public Guid? FamilyId { get; private set; }
    public RequestTypeCode Code { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string QuestionsJson { get; private set; } = "[]";
    public bool IsEnabled { get; private set; } = true;
    public int SortOrder { get; private set; }

    private RequestTypeDefinition() { }

    public static RequestTypeDefinition Create(
        RequestTypeCode code,
        string name,
        List<QuestionDefinition> questions,
        Guid? familyId = null,
        int sortOrder = 0)
    {
        return new RequestTypeDefinition
        {
            Code = code,
            Name = name,
            FamilyId = familyId,
            SortOrder = sortOrder,
            QuestionsJson = JsonSerializer.Serialize(questions)
        };
    }

    public List<QuestionDefinition> GetQuestions()
    {
        return JsonSerializer.Deserialize<List<QuestionDefinition>>(QuestionsJson) ?? new();
    }
}

public class QuestionDefinition
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public QuestionType Type { get; set; }
    public bool Required { get; set; }
    public List<string>? Options { get; set; }
    public string? Placeholder { get; set; }
    public int SortOrder { get; set; }
}
