using System;

namespace Electronic_Election_Management_System.Models
{
    /// <summary>
    /// Informații suplimentare de identitate/demografice colectate la momentul votării, într-o alegere non-anonimă.
    /// Nu este niciodată creat pentru alegeri anonime (Vote.VoteTokenId este setat în schimb, fără legătură înapoi la un utilizator).
    /// Care câmpuri sunt populate depinde de ElectionType al Alegerilor părinte <see cref="ElectionType"/>:
    /// Politic utilizează câmpurile derivate din Cnp, Comercial utilizează Gender/EmployeeId/Department.
    /// </summary>
    public class DeclaratiVotor
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid VotId { get; set; }
        public Vote? Vote { get; set; }

        // --- Alegeri politice ---

        /// <summary>
        /// CNP românesc brut, păstrat doar pentru demo/pista de audit a acestui proiect student.
        /// Într-o implementare reală, acest lucru NU ar trebui să fie stocat în text clar (sau deloc) odată ce
        /// DataNasterii/Gen/DomiciliuJudet au fost derivate din el - vezi CnpService.
        /// </summary>
        public string? Cnp { get; set; }
        public string? NumeComplet { get; set; }
        public string? DomiciliuJudet { get; set; }
        public string? DomiciliuAdresa { get; set; }
        public string? DomiciliuLocalitate { get; set; }
        public string? Cetatenie { get; set; }
        /// <summary>Derivat din CNP pe partea de server. Niciodată acceptat ca atare din client.</summary>
        public DateOnly? DataNasterii { get; set; }
        /// <summary>"M" sau "F". Derivat din CNP pentru alegeri Politice, autodeclarat pentru alegeri Comerciale.</summary>
        public string? Gen { get; set; }

        // --- Alegeri comerciale ---

        public string? IdAngajat { get; set; }
        public string? Departament { get; set; }
        public string? EmailLucru { get; set; }
        public string? PostulLucru { get; set; }
        public string? Companie { get; set; }

        public DateTime CreatLa { get; set; } = DateTime.UtcNow;
    }
}