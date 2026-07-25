using System;
using System.Collections.Generic;

namespace SakilaApp.Models;

public partial class Language
{
    public int LanguageId { get; set; }

    public string Name { get; set; } = null!;

    public DateTime LastUpdate { get; set; }

    public virtual ICollection<Film> FilmLanguages { get; set; } = new List<Film>();

    public virtual ICollection<Film> FilmOriginalLanguages { get; set; } = new List<Film>();
}
