using System.ComponentModel.DataAnnotations;

namespace MarkovBot.Options;

public sealed class DatabaseOptions
{
    [Required]
    public required string User { get; set; }

    [Required]
    public required string Password { get; set; }
}