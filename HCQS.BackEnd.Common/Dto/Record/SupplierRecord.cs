﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HCQS.BackEnd.Common.Dto.Record
{
    public class SupplierRecord
    {
        public Guid Id { get; set; }
        public string SupplierName { get; set; }
        public string Type { get; set; }
    }
}
