using System;
using System.Collections.Generic;

namespace SakilaApp.Models;

public partial class Inventory
{
    public int InventoryId { get; set; }

    public int FilmId { get; set; }

    public int StoreId { get; set; }

    public DateTime LastUpdate { get; set; }

    public virtual Film Film { get; set; } = null!;

    public virtual ICollection<Rental> Rentals { get; set; } = new List<Rental>();

    public virtual Store Store { get; set; } = null!;
}
