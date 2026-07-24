using System;
using System.Collections.Generic;

namespace SakilaApp.Models;

public partial class Actor
{
    public int ActorId { get; set; }

    public string FirstName { get; set; } = null!;

    public string LastName { get; set; } = null!;

    public DateTime LastUpdate { get; set; }

    public virtual ICollection<FilmActor> FilmActors { get; set; } = new List<FilmActor>();
}
