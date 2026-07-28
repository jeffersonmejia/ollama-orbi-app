namespace SakilaApp.Models.Delivery;

public sealed class StoreOwnerOption
{
    public string UserId { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public DateTimeOffset MemberSince { get; init; }
    public int? StoreId { get; init; }
}
