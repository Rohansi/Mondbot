﻿using System.ComponentModel.DataAnnotations;

namespace MondBot.Master.Models
{
    public class RunCodeRequest
    {
        [Required]
        public string Code { get; set; }
    }
}
