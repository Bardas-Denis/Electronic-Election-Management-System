using System.ComponentModel.DataAnnotations;

namespace ElectionSystem.Api.Dtos
{
    // Corespunde OptionDto din voting.model.ts (frontend)
    public class OptionDto
    {
        public Guid Id { get; set; } = Guid.Empty;
        public string Label { get; set; } = string.Empty;
    }

    // Corespunde ElectionDto din voting.model.ts (frontend)
    public class ElectionDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Type { get; set; } = string.Empty; // "Politic" | "Comercial"
        public bool IsAnonymous { get; set; }
        public DateTime StartsAt { get; set; }
        public DateTime EndsAt { get; set; }
        public List<OptionDto> Options { get; set; } = new();

        // Etapa 3 (votare) va completa asta pe baza userului curent.
        // In Etapa 2 se intoarce mereu false, campul exista deja pentru compatibilitate cu frontend-ul.
        public bool HasUserVoted { get; set; } = false;
    }

    // Corespunde CreateElectionRequest din voting.model.ts (frontend)
    public class CreateElectionRequest
    {
        [Required]
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Required]
        public string Type { get; set; } = string.Empty; // "Politic" | "Comercial"

        public bool IsAnonymous { get; set; } = true;

        [Required]
        public DateTime StartsAt { get; set; }

        [Required]
        public DateTime EndsAt { get; set; }

        [Required, MinLength(2)]
        public List<string> OptionLabels { get; set; } = new();
    }

    // Folosit pentru PUT (editare alegere existenta) - acelasi shape ca la creare
    public class UpdateElectionRequest : CreateElectionRequest
    {
    }
}
