﻿using FluentValidation;
using HCQS.BackEnd.Common.Dto;
using HCQS.BackEnd.Common.Dto.Request;
using HCQS.BackEnd.Common.Validator;
using HCQS.BackEnd.DAL.Util;
using HCQS.BackEnd.Service.Contracts;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HCQS.BackEnd.API.Controllers
{
    [Route("quotation")]
    [ApiController]
    public class QuotationController : ControllerBase
    {
        private IQuotationService _service;
        private readonly IValidator<QuotationDealingDto> _validatorConfig;
        private readonly IValidator<CreateQuotationDeallingStaffRequest> _validator;

        private readonly HandleErrorValidator _handleErrorValidator;

        public QuotationController(IQuotationService service, IValidator<QuotationDealingDto> validatorConfig, IValidator<CreateQuotationDeallingStaffRequest> validator, HandleErrorValidator handleErrorValidator)
        {
            _service = service;
            _validatorConfig = validatorConfig;
            _handleErrorValidator = handleErrorValidator;
            _validator = validator;
        }

        [HttpGet("get-all-quotation-by-projectId/{projectId}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = Permission.ALL)]

        public async Task<AppActionResult> GetAllQuotationByProjectId(Guid projectId)
        {
            return await _service.GetAllQuotationByProjectId(projectId);
        }

        [HttpPost("create-quotation-dealing-request")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = Permission.CUSTOMER)]

        public async Task<AppActionResult> CreateQuotationDealingRequest(QuotationDealingDto quotationDealingDto)
        {
            var result = await _validatorConfig.ValidateAsync(quotationDealingDto);
            if (!result.IsValid)
            {
                return _handleErrorValidator.HandleError(result);
            }
            return await _service.CreateQuotationDealingRequest(quotationDealingDto);
        }

        [HttpPut("deal-quotation")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = Permission.CUSTOMER)]

        public async Task<AppActionResult> DealQuotation(Guid quotationId, bool status)
        {
            return await _service.DealQuotation(quotationId, status);
        }

        [HttpPost("create-quotation-dealing-by-staff")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = Permission.STAFF)]
        public async Task<AppActionResult> CreateQuotationDealingByStaff(CreateQuotationDeallingStaffRequest request)
        {
             var result = await _validator.ValidateAsync(request);
            if (!result.IsValid)
            {
                return _handleErrorValidator.HandleError(result);
            }
            return await _service.CreateQuotationDealingByStaff(request);
        }
    }
}