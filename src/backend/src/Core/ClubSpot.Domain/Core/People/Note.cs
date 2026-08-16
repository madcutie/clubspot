using ClubSpot.SharedKernel.Tenancy;

namespace ClubSpot.Domain.Core.People;

public sealed class Note : ITenantOwned
{
    public Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public Guid PersonId { get; private set; }
    public string Text { get; private set; }
    public Guid AuthorUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public Note(Guid id, TenantId tenantId, Guid personId, string text, Guid authorUserId, DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Note text cannot be empty.", nameof(text));
        if (text.Trim().Length > 500) throw new ArgumentOutOfRangeException(nameof(text), "Note text cannot exceed 500 characters.");

        Id = id;
        TenantId = tenantId;
        PersonId = personId;
        Text = text.Trim();
        AuthorUserId = authorUserId;
        CreatedAt = createdAt;
    }

    private Note()
    {
        Text = null!;
    }
}
