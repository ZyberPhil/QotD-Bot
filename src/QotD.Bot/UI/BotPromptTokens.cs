namespace QotD.Bot.UI;

public static class BotPromptTokens
{
    // Preferred placeholders (snake_case).
    public const string Question = "{question}";
    public const string QuestionId = "{question_id}";
    public const string Date = "{date}";

    public const string RoleName = "{role_name}";
    public const string RoleMention = "{role_mention}";
    public const string MemberCount = "{member_count}";
    public const string MembersList = "{members_list}";

    // Legacy placeholders kept for backward compatibility.
    public const string LegacyMessage = "{message}";
    public const string LegacyId = "{id}";

    public const string LegacyRoleName = "{RoleName}";
    public const string LegacyRoleMention = "{RoleMention}";
    public const string LegacyMemberCount = "{MemberCount}";
    public const string LegacyMembersList = "{MembersList}";
    public const string LegacyRank = "{rank}";
    public const string LegacyCount = "{count}";
    public const string LegacyText = "{text}";

    public static string ApplyQotdTemplate(string template, string question, string questionId, string date)
    {
        return template
            .Replace(Question, question)
            .Replace(QuestionId, questionId)
            .Replace(Date, date)
            .Replace(LegacyMessage, question)
            .Replace(LegacyId, questionId);
    }

    public static string ApplyTeamTemplate(
        string template,
        string roleName,
        string roleMention,
        string memberCount,
        string membersList)
    {
        return template
            .Replace(RoleName, roleName)
            .Replace(RoleMention, roleMention)
            .Replace(MemberCount, memberCount)
            .Replace(MembersList, membersList)
            .Replace(LegacyRoleName, roleName)
            .Replace(LegacyRoleMention, roleMention)
            .Replace(LegacyMemberCount, memberCount)
            .Replace(LegacyMembersList, membersList)
            .Replace(LegacyRank, roleMention)
            .Replace(LegacyCount, memberCount)
            .Replace(LegacyText, membersList);
    }
}
