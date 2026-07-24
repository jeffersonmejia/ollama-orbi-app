using System;
using System.Collections.Generic;

namespace SakilaApp.Models;

public partial class StaffList
{
    public int? Id { get; set; }

    public string? Name { get; set; }

    public string? Address { get; set; }

    public string? ZipCode { get; set; }

    public string? Phone { get; set; }

    public string? City { get; set; }

    public string? Country { get; set; }

    public int? Sid { get; set; }
}
