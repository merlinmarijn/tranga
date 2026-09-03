using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using API.Schema.LibraryContext.LibraryConnectors;

namespace API.Controllers.Requests;

public sealed record CreateKavitaRecord
{
    
    /// <summary>
    /// The Url of the Library instance
    /// </summary>
    [Required]
    [Url]
    [Description("The Url of the Library instance")]
    public required string Url { get; init; }
    
    /// <summary>
    /// The Kavita Auth Key used to authenticate to the Library instance
    /// </summary>
    [Required]
    [Description("A Kavita Auth Key created under Settings > Auth Keys / OPDS")]
    public required string ApiKey { get; init; }
}
