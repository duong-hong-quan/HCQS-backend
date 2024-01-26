﻿using HCQS.BackEnd.Common.Dto;
using Microsoft.AspNetCore.Http;

namespace HCQS.BackEnd.Service.Contracts
{
    public interface IPaymentService
    {
        public Task<AppActionResult> CreatePaymentUrlMomo(Guid paymentId);

        public Task<AppActionResult> CreatePaymentUrlVNPay(Guid paymentId, HttpContext context);

        public Task<AppActionResult> UpdatePaymentStatus(string paymentId, bool status, int type);
    }
}