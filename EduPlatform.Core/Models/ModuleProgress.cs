namespace EduPlatform.Core.Models;

/// <summary>
/// Avancement d'un apprenant sur un module precis.
/// </summary>
/// <remarks>
/// La progression n'etait suivie qu'au niveau du cours, via des evenements d'activite
/// Cassandra qui ne portent pas l'identifiant du module. Impossible, donc, de savoir
/// quels modules etaient termines, ni de reprendre une video la ou elle avait ete arretee.
/// Cette table relationnelle repond aux deux besoins et rend la reprise valable
/// d'un appareil a l'autre, contrairement a un stockage navigateur.
/// </remarks>
public class ModuleProgress
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }
    public Guid ModuleId { get; set; }

    /// <summary>Denormalise : permet de lister l'avancement d'un cours sans jointure.</summary>
    public Guid CourseId { get; set; }

    /// <summary>Derniere position de lecture, en secondes.</summary>
    public int LastPositionSeconds { get; set; }

    /// <summary>Fraction visionnee la plus elevee atteinte, de 0 a 1.</summary>
    public double WatchedRatio { get; set; }

    public bool IsCompleted { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public Module Module { get; set; } = null!;
}
