using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;
using HB.Framework.Common;

namespace HB.Framework.Database.Entity
{
    public class DatabaseEntityDto : ValidatableObject
    {
        [Required]
        public string Guid { get; set; } = null!;
    }
}
