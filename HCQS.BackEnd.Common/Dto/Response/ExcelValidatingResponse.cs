﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HCQS.BackEnd.Common.Dto.Response
{
    public class ExcelValidatingResponse
    {
        public bool IsValidated {  get; set; }
        public string? HeaderError { get; set; }
        public string[]? Errors { get; set; }
    }
}