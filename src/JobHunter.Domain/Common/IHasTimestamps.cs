namespace JobHunter.Domain.Common;

/// <summary>Stamped automatically by the persistence layer on save.</summary>
public interface IHasTimestamps
{
    DateTimeOffset CreatedAt { get; set; }
    DateTimeOffset UpdatedAt { get; set; }
}
