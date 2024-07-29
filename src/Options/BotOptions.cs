using System.ComponentModel.DataAnnotations;

namespace MarkovBot.Options;

public sealed class BotOptions
{
    [Required]
    public required string Token { get; set; }

    [Required]
    public required int HardWordLimit { get; set; }

    public ulong TestGuild { get; set; }

    [Required]
    public required IEnumerable<string> DefaultForbiddenWords { get; set; }
}