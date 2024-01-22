﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HCQS.BackEnd.Common.Dto.Record
{
    public class SupplierMaterialQuotationRecord
    {
        public Guid Id { get; set; }
        public string MaterialName { get; set; }
        public string Unit { get; set; }
        public int MQO { get; set; }
        public double Price { get; set; }
    }
}