﻿using FluentValidation;
using HCQS.BackEnd.Common.Dto.Request;

namespace HCQS.BackEnd.Common.Validator
{
    public class NewsValidator : AbstractValidator<NewsRequest>
    {
        public NewsValidator()
        {
            RuleFor(x => x.AccountId).NotNull().NotEmpty().WithMessage("the accountid is required!");
            RuleFor(x => x.Header).NotNull().NotEmpty().WithMessage("the header is required!");
            RuleFor(x => x.Content).NotNull().NotEmpty().WithMessage("the content is required!");
            RuleFor(x => x.ImgUrl).NotEmpty().NotEmpty().WithMessage("the file is required!");
        }
    }
}