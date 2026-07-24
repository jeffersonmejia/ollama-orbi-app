using System;
using System.Collections.Generic;

namespace SakilaApp.Models;

public partial class City
{
    public int CityId { get; set; }

    public string City1 { get; set; } = null!;

    public int CountryId { get; set; }

    public DateTime LastUpdate { get; set; }

    public virtual ICollection<Address> Addresses { get; set; } = new List<Address>();

    public virtual Country Country { get; set; } = null!;
}
