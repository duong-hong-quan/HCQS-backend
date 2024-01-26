﻿using FluentValidation;
using HCQS.BackEnd.Common.Dto.Request;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HCQS.BackEnd.Common.Validator
{
    public class ProgressConstructionMaterialValidator:AbstractValidator<ProgressConstructionMaterialRequest>
    {
        public ProgressConstructionMaterialValidator() {
            RuleFor(x => x.Quantity).NotNull().NotEmpty().GreaterThan(0).WithMessage("The quantity must be higher than 0!");
            RuleFor(x => x.Date).NotEmpty().NotNull().WithMessage("The date is required!");
            RuleFor(x => x.QuotationDetailId).NotNull().NotEmpty().WithMessage("The quotation detail Id is required!");
        }
    }
}